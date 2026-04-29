package biz

import (
	"context"
	"encoding/binary"
	"sync"
	"testing"

	"tradingbot/app/tradingbot/internal/conf"

	"github.com/go-kratos/kratos/v2/log"
	"github.com/shopspring/decimal"
)

// --- mock Exchange ---

type mockExchange struct {
	mid          decimal.Decimal
	placed       []PlaceSpotBuyReq
	mu           sync.Mutex
	placeErr     error
}

func (m *mockExchange) MidPx(_ context.Context, _ Coin) (decimal.Decimal, error) {
	return m.mid, nil
}

func (m *mockExchange) SpotBalance(_ context.Context, _ string) (decimal.Decimal, error) {
	return decimal.Zero, nil
}

func (m *mockExchange) PlaceSpotBuy(_ context.Context, req PlaceSpotBuyReq) (OrderRef, error) {
	m.mu.Lock()
	defer m.mu.Unlock()
	if m.placeErr != nil {
		return OrderRef{}, m.placeErr
	}
	m.placed = append(m.placed, req)
	return OrderRef{Cloid: req.Cloid, Oid: int64(len(m.placed)), Coin: req.Coin, Px: req.Px, Sz: req.Sz}, nil
}

func (m *mockExchange) CancelByCloid(_ context.Context, _ Coin, _ Cloid) error {
	return nil
}

func (m *mockExchange) OpenOrders(_ context.Context, _ Coin) ([]OrderRef, error) {
	return nil, nil
}

// --- mock StrategyStateRepo ---

type mockStateRepo struct {
	mu   sync.Mutex
	runs map[string]*DCARun // key: strategyID (most recent)
}

func newMockStateRepo() *mockStateRepo {
	return &mockStateRepo{runs: make(map[string]*DCARun)}
}

func (r *mockStateRepo) SaveRun(_ context.Context, run *DCARun) error {
	r.mu.Lock()
	defer r.mu.Unlock()
	cp := *run
	cp.Rungs = make([]Rung, len(run.Rungs))
	copy(cp.Rungs, run.Rungs)
	r.runs[run.StrategyID] = &cp
	return nil
}

func (r *mockStateRepo) LatestRun(_ context.Context, strategyID string) (*DCARun, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	return r.runs[strategyID], nil
}

func (r *mockStateRepo) GetRun(_ context.Context, strategyID, runID string) (*DCARun, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	run := r.runs[strategyID]
	if run != nil && run.ID == runID {
		return run, nil
	}
	return nil, nil
}

func (r *mockStateRepo) ListRuns(_ context.Context, strategyID string, limit int) ([]*DCARun, error) {
	r.mu.Lock()
	defer r.mu.Unlock()
	run := r.runs[strategyID]
	if run == nil {
		return nil, nil
	}
	return []*DCARun{run}, nil
}

// --- deterministic cloid helper (for tests) ---

// testDeriveCloid derives a 16-byte Cloid from strategyID, runID, and rung
// using a simple, deterministic scheme (no keccak dependency in biz tests).
func testDeriveCloid(strategyID, runID string, rung int) Cloid {
	var c Cloid
	// XOR bytes of strategyID, runID, and rung index into the 16-byte array.
	data := []byte(strategyID + ":" + runID)
	for i, b := range data {
		c[i%16] ^= b
	}
	var buf [8]byte
	binary.LittleEndian.PutUint64(buf[:], uint64(rung))
	for i, b := range buf {
		c[(i+8)%16] ^= b
	}
	return c
}

// TestLadderMath verifies ladder price and size calculations.
func TestLadderMath(t *testing.T) {
	mid := decimal.NewFromInt(100000)
	ex := &mockExchange{mid: mid}
	repo := newMockStateRepo()

	uc := NewRecursiveBuyUsecase(ex, repo, testDeriveCloid, log.DefaultLogger)

	st := &conf.Strategy{
		Id:              "strat-1",
		Coin:            "BTC",
		QuoteAmount:     "100",
		LadderSize:      4,
		PriceOffsetsBps: []string{"-50", "-100", "-150", "-200"},
	}

	if err := uc.Tick(context.Background(), st); err != nil {
		t.Fatalf("Tick returned error: %v", err)
	}

	ex.mu.Lock()
	placed := ex.placed
	ex.mu.Unlock()

	if len(placed) != 4 {
		t.Fatalf("expected 4 placed orders, got %d", len(placed))
	}

	ten000 := decimal.NewFromInt(10000)
	quote := decimal.NewFromInt(100)
	n := decimal.NewFromInt(4)
	perRung := quote.Div(n) // 25

	offsets := []string{"-50", "-100", "-150", "-200"}
	for i, req := range placed {
		bps := decimal.RequireFromString(offsets[i])
		expectedPx := mid.Mul(ten000.Add(bps)).Div(ten000)
		expectedSz := perRung.Div(expectedPx)

		if !req.Px.Equal(expectedPx) {
			t.Errorf("rung %d: expected px=%s, got %s", i, expectedPx.String(), req.Px.String())
		}
		if !req.Sz.Equal(expectedSz) {
			t.Errorf("rung %d: expected sz=%s, got %s", i, expectedSz.String(), req.Sz.String())
		}
	}

	// Spot-check explicit expected prices.
	cases := []struct {
		rung int
		wantPx string
	}{
		{0, "99500"},
		{1, "99000"},
		{2, "98500"},
		{3, "98000"},
	}
	for _, tc := range cases {
		want := decimal.RequireFromString(tc.wantPx)
		got := placed[tc.rung].Px
		if !got.Equal(want) {
			t.Errorf("rung %d: expected explicit px=%s, got %s", tc.rung, want.String(), got.String())
		}
	}
}

// TestLadderSizes verifies per-rung sizes equal quote/n/px.
func TestLadderSizes(t *testing.T) {
	mid := decimal.NewFromInt(100000)
	ex := &mockExchange{mid: mid}
	repo := newMockStateRepo()
	uc := NewRecursiveBuyUsecase(ex, repo, testDeriveCloid, log.DefaultLogger)

	st := &conf.Strategy{
		Id:              "strat-2",
		Coin:            "BTC",
		QuoteAmount:     "100",
		LadderSize:      4,
		PriceOffsetsBps: []string{"-50", "-100", "-150", "-200"},
	}

	if err := uc.Tick(context.Background(), st); err != nil {
		t.Fatalf("Tick returned error: %v", err)
	}

	ex.mu.Lock()
	placed := ex.placed
	ex.mu.Unlock()

	pxExpected := []string{"99500", "99000", "98500", "98000"}
	perRung := decimal.NewFromInt(25)

	for i, req := range placed {
		px := decimal.RequireFromString(pxExpected[i])
		want := perRung.Div(px)
		if !req.Sz.Equal(want) {
			t.Errorf("rung %d: expected sz=%s, got %s", i, want.String(), req.Sz.String())
		}
	}
}

// TestDeterministicCloid verifies same inputs produce identical Cloid output.
func TestDeterministicCloid(t *testing.T) {
	strategyID := "strategy-abc"
	runID := "20240101T120000Z"

	c1 := testDeriveCloid(strategyID, runID, 0)
	c2 := testDeriveCloid(strategyID, runID, 0)

	if c1 != c2 {
		t.Errorf("cloid not deterministic: got %s and %s", c1.Hex(), c2.Hex())
	}

	// Different rung index must produce different cloid.
	c3 := testDeriveCloid(strategyID, runID, 1)
	if c1 == c3 {
		t.Errorf("rung 0 and rung 1 produced identical cloid: %s", c1.Hex())
	}

	// Different runID must produce different cloid.
	c4 := testDeriveCloid(strategyID, "20240101T130000Z", 0)
	if c1 == c4 {
		t.Errorf("different runIDs produced identical cloid: %s", c1.Hex())
	}
}

// TestTickRunPersisted verifies that SaveRun is called and the run has correct rung count.
func TestTickRunPersisted(t *testing.T) {
	ex := &mockExchange{mid: decimal.NewFromInt(50000)}
	repo := newMockStateRepo()
	uc := NewRecursiveBuyUsecase(ex, repo, testDeriveCloid, log.DefaultLogger)

	st := &conf.Strategy{
		Id:              "strat-3",
		Coin:            "ETH",
		QuoteAmount:     "200",
		LadderSize:      3,
		PriceOffsetsBps: []string{"-100", "-200", "-300"},
	}

	if err := uc.Tick(context.Background(), st); err != nil {
		t.Fatalf("Tick returned error: %v", err)
	}

	run, err := repo.LatestRun(context.Background(), st.Id)
	if err != nil || run == nil {
		t.Fatalf("expected saved run, got nil (err=%v)", err)
	}
	if len(run.Rungs) != 3 {
		t.Errorf("expected 3 rungs, got %d", len(run.Rungs))
	}
	if run.Status != RunPlaced {
		t.Errorf("expected status RunPlaced, got %s", run.Status)
	}
}

// TestReconcileMarksOpenRungs verifies Reconcile marks rungs present in OpenOrders as "open".
func TestReconcileMarksOpenRungs(t *testing.T) {
	repo := newMockStateRepo()

	// Pre-seed a run with two rungs.
	cloid0 := testDeriveCloid("strat-4", "run1", 0)
	cloid1 := testDeriveCloid("strat-4", "run1", 1)
	run := &DCARun{
		ID:         "run1",
		StrategyID: "strat-4",
		Rungs: []Rung{
			{Cloid: cloid0, Status: string(RunPlaced)},
			{Cloid: cloid1, Status: string(RunPlaced)},
		},
		Status: RunPlaced,
	}
	if err := repo.SaveRun(context.Background(), run); err != nil {
		t.Fatal(err)
	}

	// Exchange reports only cloid0 as open.
	ex := &mockExchange{mid: decimal.NewFromInt(100)}
	ex.placed = nil

	// Override OpenOrders to return cloid0.
	openEx := &openOrdersExchange{mockExchange: ex, orders: []OrderRef{{Cloid: cloid0}}}

	uc := NewRecursiveBuyUsecase(openEx, repo, testDeriveCloid, log.DefaultLogger)

	st := &conf.Strategy{Id: "strat-4", Coin: "BTC"}
	if err := uc.Reconcile(context.Background(), st); err != nil {
		t.Fatalf("Reconcile returned error: %v", err)
	}

	saved, _ := repo.LatestRun(context.Background(), "strat-4")
	if saved.Rungs[0].Status != "open" {
		t.Errorf("rung 0: expected status 'open', got %q", saved.Rungs[0].Status)
	}
	if saved.Rungs[1].Status == "open" {
		t.Errorf("rung 1: expected non-open status, got 'open'")
	}
}

// openOrdersExchange wraps mockExchange with a custom OpenOrders implementation.
type openOrdersExchange struct {
	*mockExchange
	orders []OrderRef
}

func (e *openOrdersExchange) OpenOrders(_ context.Context, _ Coin) ([]OrderRef, error) {
	return e.orders, nil
}
