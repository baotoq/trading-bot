package server

import (
	helloworldv1 "tradingbot/app/tradingbot/api/helloworld/v1"
	tradingv1 "tradingbot/app/tradingbot/api/trading/v1"
	"tradingbot/app/tradingbot/internal/conf"
	"tradingbot/app/tradingbot/internal/service"

	"github.com/go-kratos/kratos/v2/log"
	"github.com/go-kratos/kratos/v2/middleware/recovery"
	"github.com/go-kratos/kratos/v2/transport/http"
)

// NewHTTPServer new an HTTP server.
func NewHTTPServer(c *conf.Server, greeter *service.GreeterService, strategy *service.StrategyService, logger log.Logger) *http.Server {
	var opts = []http.ServerOption{
		http.Middleware(
			recovery.Recovery(),
		),
	}
	if c.Http.Network != "" {
		opts = append(opts, http.Network(c.Http.Network))
	}
	if c.Http.Addr != "" {
		opts = append(opts, http.Address(c.Http.Addr))
	}
	if c.Http.Timeout != nil {
		opts = append(opts, http.Timeout(c.Http.Timeout.AsDuration()))
	}
	srv := http.NewServer(opts...)
	helloworldv1.RegisterGreeterHTTPServer(srv, greeter)
	tradingv1.RegisterStrategyServiceHTTPServer(srv, strategy)
	return srv
}
