package service

import (
	"context"

	v1 "tradingbot/app/tradingbot/api/trading/v1"
	"tradingbot/app/tradingbot/internal/biz"

	"github.com/go-kratos/kratos/v2/log"
)

type StrategyService struct {
	v1.UnimplementedStrategyServiceServer
	state biz.StrategyStateRepo
	log   *log.Helper
}

func NewStrategyService(state biz.StrategyStateRepo, logger log.Logger) *StrategyService {
	return &StrategyService{state: state, log: log.NewHelper(logger)}
}

func (s *StrategyService) Status(ctx context.Context, req *v1.StatusRequest) (*v1.StatusReply, error) {
	runs, err := s.state.ListRuns(ctx, req.StrategyId, 10)
	if err != nil {
		return nil, err
	}
	reply := &v1.StatusReply{StrategyId: req.StrategyId}
	for _, r := range runs {
		placed := 0
		filled := 0
		for _, rg := range r.Rungs {
			switch rg.Status {
			case "placed", "open":
				placed++
			case "filled":
				filled++
			}
		}
		reply.Runs = append(reply.Runs, &v1.RunSummary{
			RunId:  r.ID,
			Ts:     r.Ts.Format("2006-01-02T15:04:05Z"),
			Status: string(r.Status),
			Placed: int32(placed),
			Filled: int32(filled),
		})
	}
	return reply, nil
}
