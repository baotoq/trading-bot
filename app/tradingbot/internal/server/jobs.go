package server

import (
	"context"

	"tradingbot/app/tradingbot/internal/biz"
	"tradingbot/app/tradingbot/internal/conf"

	"github.com/go-kratos/kratos/v2/log"
)

// NewStrategies extracts the strategy slice from Bootstrap for Wire injection.
func NewStrategies(c *conf.Bootstrap) []*conf.Strategy {
	return c.Strategies
}

// JobBootstrapper schedules a Dapr Job per strategy and routes triggers to Tick.
// Implements transport.Server so Kratos calls Start/Stop in the app lifecycle.
type JobBootstrapper struct {
	strategies []*conf.Strategy
	uc         *biz.RecursiveBuyUsecase
	sched      biz.JobScheduler
	log        *log.Helper
}

// NewJobBootstrapper registers a /job/<name> HTTP handler for each strategy and
// returns a JobBootstrapper whose Start method upserts the corresponding Dapr jobs.
func NewJobBootstrapper(
	strategies []*conf.Strategy,
	uc *biz.RecursiveBuyUsecase,
	sched biz.JobScheduler,
	sub *DaprSubscriber,
	logger log.Logger,
) *JobBootstrapper {
	b := &JobBootstrapper{
		strategies: strategies,
		uc:         uc,
		sched:      sched,
		log:        log.NewHelper(logger),
	}
	for _, st := range strategies {
		st := st
		name := jobName(st.Id)
		RegisterJobHandler(sub, name, func(ctx context.Context, _ []byte) error {
			return uc.Tick(ctx, st)
		})
	}
	return b
}

func jobName(strategyID string) string { return "strategy-tick-" + strategyID }

// Start reconciles state and upserts a Dapr job for each strategy.
// Called by Kratos app lifecycle after all servers are constructed.
func (b *JobBootstrapper) Start(ctx context.Context) error {
	for _, st := range b.strategies {
		if err := b.uc.Reconcile(ctx, st); err != nil {
			b.log.Warnf("reconcile %s: %v", st.Id, err)
		}
		schedule := "@every " + st.Interval.AsDuration().String()
		if err := b.sched.Schedule(ctx, biz.Job{
			Name:     jobName(st.Id),
			Schedule: schedule,
		}); err != nil {
			b.log.Errorf("schedule job %s: %v", st.Id, err)
			return err
		}
		b.log.Infof("scheduled job %s schedule=%s", jobName(st.Id), schedule)
	}
	return nil
}

// Stop is a no-op: Dapr jobs persist in the Scheduler etcd across restarts by design.
func (b *JobBootstrapper) Stop(_ context.Context) error { return nil }
