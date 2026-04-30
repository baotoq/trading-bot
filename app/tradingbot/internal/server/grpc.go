package server

import (
	helloworldv1 "tradingbot/api/tradingbot/helloworld/v1"
	tradingv1 "tradingbot/api/trading/v1"
	"tradingbot/app/tradingbot/internal/conf"
	"tradingbot/app/tradingbot/internal/service"

	"github.com/go-kratos/kratos/v2/log"
	"github.com/go-kratos/kratos/v2/middleware/recovery"
	"github.com/go-kratos/kratos/v2/transport/grpc"
)

// NewGRPCServer new a gRPC server.
func NewGRPCServer(c *conf.Server, greeter *service.GreeterService, strategy *service.StrategyService, logger log.Logger) *grpc.Server {
	var opts = []grpc.ServerOption{
		grpc.Middleware(
			recovery.Recovery(),
		),
	}
	if c.Grpc.Network != "" {
		opts = append(opts, grpc.Network(c.Grpc.Network))
	}
	if c.Grpc.Addr != "" {
		opts = append(opts, grpc.Address(c.Grpc.Addr))
	}
	if c.Grpc.Timeout != nil {
		opts = append(opts, grpc.Timeout(c.Grpc.Timeout.AsDuration()))
	}
	srv := grpc.NewServer(opts...)
	helloworldv1.RegisterGreeterServer(srv, greeter)
	tradingv1.RegisterStrategyServiceServer(srv, strategy)
	return srv
}
