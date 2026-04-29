package server

import (
	"context"
	"time"

	"github.com/go-kratos/kratos/v2/log"
	"tradingbot/app/tradingbot/internal/biz"
	"tradingbot/app/tradingbot/internal/conf"
)

// Scheduler is a transport.Server that ticks each strategy on its configured interval.
type Scheduler struct {
	strategies []*conf.Strategy
	uc         *biz.RecursiveBuyUsecase
	log        *log.Helper
	cancel     context.CancelFunc
}

// NewSchedulerServer constructs a Scheduler.
func NewSchedulerServer(strategies []*conf.Strategy, uc *biz.RecursiveBuyUsecase, logger log.Logger) *Scheduler {
	return &Scheduler{
		strategies: strategies,
		uc:         uc,
		log:        log.NewHelper(logger),
	}
}

// NewStrategies extracts the strategy slice from Bootstrap for Wire injection.
func NewStrategies(c *conf.Bootstrap) []*conf.Strategy {
	return c.Strategies
}

// Start implements transport.Server. It reconciles all strategies then spawns a ticker goroutine per strategy.
func (s *Scheduler) Start(ctx context.Context) error {
	rctx, cancel := context.WithCancel(context.Background())
	s.cancel = cancel
	for _, st := range s.strategies {
		st := st // capture loop variable
		if err := s.uc.Reconcile(rctx, st); err != nil {
			s.log.Warnf("reconcile %s: %v", st.Id, err)
		}
		go s.run(rctx, st)
	}
	return nil
}

func (s *Scheduler) run(ctx context.Context, st *conf.Strategy) {
	ticker := time.NewTicker(st.Interval.AsDuration())
	defer ticker.Stop()
	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if err := s.uc.Tick(ctx, st); err != nil {
				s.log.Errorf("tick %s: %v", st.Id, err)
			}
		}
	}
}

// Stop implements transport.Server. It cancels the scheduler context.
func (s *Scheduler) Stop(_ context.Context) error {
	if s.cancel != nil {
		s.cancel()
	}
	return nil
}
