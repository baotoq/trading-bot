package service

import (
	"context"

	"github.com/go-kratos/kratos/v2/log"
)

// TradingEvent is the typed payload for trading pubsub events.
type TradingEvent struct{}

type EventService struct {
	log *log.Helper
}

func NewEventService(logger log.Logger) *EventService {
	return &EventService{log: log.NewHelper(logger)}
}

func (s *EventService) HandleTradingEvent(ctx context.Context, e TradingEvent) error {
	s.log.WithContext(ctx).Infof("trading event: %+v", e)
	return nil
}
