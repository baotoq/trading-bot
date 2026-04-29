package main

import "tradingbot/app/tradingbot/internal/conf"

// newExchangeConf extracts *conf.Exchange_Hyperliquid from *conf.Bootstrap for Wire.
func newExchangeConf(c *conf.Bootstrap) *conf.Exchange_Hyperliquid {
	return c.GetExchange().GetHyperliquid()
}
