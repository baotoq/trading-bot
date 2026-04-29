package data

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"time"
	"tradingbot/app/tradingbot/internal/biz"

	"github.com/go-kratos/kratos/v2/log"
	"github.com/redis/go-redis/v9"
)

const runTTL = 720 * time.Hour // 30 days

type strategyStateRepo struct {
	rdb *redis.Client
	log *log.Helper
}

func NewStrategyStateRepo(rdb *redis.Client, logger log.Logger) biz.StrategyStateRepo {
	return &strategyStateRepo{
		rdb: rdb,
		log: log.NewHelper(logger),
	}
}

func runKey(strategyID, runID string) string {
	return fmt.Sprintf("strategy:%s:run:%s", strategyID, runID)
}

func latestKey(strategyID string) string {
	return fmt.Sprintf("strategy:%s:latest", strategyID)
}

func runsKey(strategyID string) string {
	return fmt.Sprintf("strategy:%s:runs", strategyID)
}

func (r *strategyStateRepo) SaveRun(ctx context.Context, run *biz.DCARun) error {
	b, err := json.Marshal(run)
	if err != nil {
		return fmt.Errorf("strategy_state: marshal run: %w", err)
	}

	pipe := r.rdb.Pipeline()
	pipe.Set(ctx, runKey(run.StrategyID, run.ID), b, runTTL)
	pipe.Set(ctx, latestKey(run.StrategyID), run.ID, 0)
	pipe.ZAdd(ctx, runsKey(run.StrategyID), redis.Z{
		Score:  float64(run.Ts.Unix()),
		Member: run.ID,
	})

	_, err = pipe.Exec(ctx)
	if err != nil {
		return fmt.Errorf("strategy_state: save run pipeline: %w", err)
	}
	return nil
}

func (r *strategyStateRepo) LatestRun(ctx context.Context, strategyID string) (*biz.DCARun, error) {
	runID, err := r.rdb.Get(ctx, latestKey(strategyID)).Result()
	if err != nil {
		if errors.Is(err, redis.Nil) {
			return nil, nil
		}
		return nil, fmt.Errorf("strategy_state: get latest run id: %w", err)
	}
	return r.GetRun(ctx, strategyID, runID)
}

func (r *strategyStateRepo) GetRun(ctx context.Context, strategyID, runID string) (*biz.DCARun, error) {
	b, err := r.rdb.Get(ctx, runKey(strategyID, runID)).Bytes()
	if err != nil {
		if errors.Is(err, redis.Nil) {
			return nil, nil
		}
		return nil, fmt.Errorf("strategy_state: get run %s/%s: %w", strategyID, runID, err)
	}

	var run biz.DCARun
	if err := json.Unmarshal(b, &run); err != nil {
		return nil, fmt.Errorf("strategy_state: unmarshal run %s/%s: %w", strategyID, runID, err)
	}
	return &run, nil
}

func (r *strategyStateRepo) ListRuns(ctx context.Context, strategyID string, limit int) ([]*biz.DCARun, error) {
	runIDs, err := r.rdb.ZRevRange(ctx, runsKey(strategyID), 0, int64(limit-1)).Result()
	if err != nil {
		return nil, fmt.Errorf("strategy_state: list runs for %s: %w", strategyID, err)
	}

	runs := make([]*biz.DCARun, 0, len(runIDs))
	for _, id := range runIDs {
		run, err := r.GetRun(ctx, strategyID, id)
		if err != nil {
			r.log.Warnf("strategy_state: skipping run %s/%s: %v", strategyID, id, err)
			continue
		}
		if run == nil {
			continue
		}
		runs = append(runs, run)
	}
	return runs, nil
}
