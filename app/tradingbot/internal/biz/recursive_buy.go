package biz

import (
	"context"
	"time"

	"tradingbot/app/tradingbot/internal/conf"

	"github.com/go-kratos/kratos/v2/log"
	"github.com/shopspring/decimal"
)

// RecursiveBuyUsecase implements a DCA ladder strategy for spot buying.
type RecursiveBuyUsecase struct {
	ex          Exchange
	state       StrategyStateRepo
	cloidDerive func(strategyID, runID string, rung int) Cloid
	log         *log.Helper
}

// NewRecursiveBuyUsecase constructs a RecursiveBuyUsecase.
func NewRecursiveBuyUsecase(
	ex Exchange,
	state StrategyStateRepo,
	cloidDerive func(strategyID, runID string, rung int) Cloid,
	logger log.Logger,
) *RecursiveBuyUsecase {
	return &RecursiveBuyUsecase{
		ex:          ex,
		state:       state,
		cloidDerive: cloidDerive,
		log:         log.NewHelper(logger),
	}
}

// Tick runs one DCA cycle: computes ladder, places orders, persists run.
func (uc *RecursiveBuyUsecase) Tick(ctx context.Context, st *conf.Strategy) error {
	mid, err := uc.ex.MidPx(ctx, Coin(st.Coin))
	if err != nil {
		return err
	}

	runID := time.Now().UTC().Format("20060102T150405Z")
	quote := decimal.RequireFromString(st.QuoteAmount)
	n := int(st.LadderSize)

	ten000 := decimal.NewFromInt(10000)
	perRungQuote := quote.Div(decimal.NewFromInt(int64(n)))

	rungs := make([]Rung, n)
	for i := 0; i < n; i++ {
		bps := decimal.RequireFromString(st.PriceOffsetsBps[i])
		px := mid.Mul(ten000.Add(bps)).Div(ten000)
		sz := perRungQuote.Div(px)
		cloid := uc.cloidDerive(st.Id, runID, i)
		rungs[i] = Rung{
			Cloid:  cloid,
			Coin:   Coin(st.Coin),
			Px:     px,
			Sz:     sz,
			Status: string(RunPending),
		}
	}

	run := &DCARun{
		ID:         runID,
		StrategyID: st.Id,
		Ts:         time.Now().UTC(),
		Rungs:      rungs,
		Status:     RunPending,
	}

	if err := uc.state.SaveRun(ctx, run); err != nil {
		return err
	}

	for i := range run.Rungs {
		rung := &run.Rungs[i]
		ref, err := uc.ex.PlaceSpotBuy(ctx, PlaceSpotBuyReq{
			Coin:     rung.Coin,
			Px:       rung.Px,
			Sz:       rung.Sz,
			Cloid:    rung.Cloid,
			PostOnly: true,
		})
		if err != nil {
			uc.log.Errorf("tick: place rung %d failed: %v", i, err)
			rung.Status = string(RunError)
			continue
		}
		rung.Oid = ref.Oid
		rung.Status = string(RunPlaced)
	}

	run.Status = RunPlaced
	if err := uc.state.SaveRun(ctx, run); err != nil {
		return err
	}

	uc.log.Infof("tick: runID=%s strategy=%s rungs=%d", runID, st.Id, n)
	return nil
}

// Reconcile loads the last run on startup and marks rungs with open cloids.
// It does NOT re-place missing orders.
func (uc *RecursiveBuyUsecase) Reconcile(ctx context.Context, st *conf.Strategy) error {
	last, err := uc.state.LatestRun(ctx, st.Id)
	if err != nil || last == nil {
		return nil
	}

	open, err := uc.ex.OpenOrders(ctx, Coin(st.Coin))
	if err != nil {
		return err
	}

	openSet := make(map[string]struct{}, len(open))
	for _, o := range open {
		openSet[o.Cloid.Hex()] = struct{}{}
	}

	for i := range last.Rungs {
		if _, ok := openSet[last.Rungs[i].Cloid.Hex()]; ok {
			last.Rungs[i].Status = "open"
		}
	}

	if err := uc.state.SaveRun(ctx, last); err != nil {
		return err
	}

	openCount := 0
	for _, r := range last.Rungs {
		if r.Status == "open" {
			openCount++
		}
	}
	uc.log.Infof("reconcile: strategy=%s lastRun=%s openRungs=%d/%d",
		st.Id, last.ID, openCount, len(last.Rungs))

	return nil
}
