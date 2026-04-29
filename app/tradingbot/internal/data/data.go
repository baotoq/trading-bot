package data

import (
	"context"
	"tradingbot/app/tradingbot/internal/conf"
	"tradingbot/app/tradingbot/internal/data/ent"

	"github.com/go-kratos/kratos/v2/log"
	"github.com/google/wire"
	_ "github.com/lib/pq"
	"github.com/redis/go-redis/v9"
)

var ProviderSet = wire.NewSet(NewData, NewGreeterRepo, NewDaprClient, NewEventRepo, NewRedisClient, NewStrategyStateRepo)

type Data struct {
	db *ent.Client
}

func NewRedisClient(c *conf.Data, logger log.Logger) (*redis.Client, func(), error) {
	rdb := redis.NewClient(&redis.Options{
		Network:      c.Redis.Network,
		Addr:         c.Redis.Addr,
		ReadTimeout:  c.Redis.ReadTimeout.AsDuration(),
		WriteTimeout: c.Redis.WriteTimeout.AsDuration(),
	})
	cleanup := func() {
		if err := rdb.Close(); err != nil {
			log.NewHelper(logger).Error(err)
		}
	}
	return rdb, cleanup, nil
}

func NewData(c *conf.Data, logger log.Logger) (*Data, func(), error) {
	client, err := ent.Open(c.Database.Driver, c.Database.Source)
	if err != nil {
		return nil, nil, err
	}
	if err := client.Schema.Create(context.Background()); err != nil {
		_ = client.Close()
		return nil, nil, err
	}
	cleanup := func() {
		if err := client.Close(); err != nil {
			log.NewHelper(logger).Error(err)
		}
	}
	return &Data{db: client}, cleanup, nil
}
