package main

import (
	"context"

	"tradingbot/app/tradingbot/internal/conf"
)

// newExchangeConf extracts *conf.Exchange_Hyperliquid from *conf.Bootstrap for Wire.
func newExchangeConf(c *conf.Bootstrap) *conf.Exchange_Hyperliquid {
	return c.GetExchange().GetHyperliquid()
}

// newContext provides a background context for Wire injection.
func newContext() context.Context {
	return context.Background()
}
