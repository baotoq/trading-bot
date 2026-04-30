package hyperliquid

import (
	"github.com/google/wire"
	"tradingbot/app/tradingbot/internal/biz"
)

// NewCloidDeriver returns a function that deterministically derives a Cloid
// from a strategyID, runID and rung index.
func NewCloidDeriver() func(string, string, int) biz.Cloid {
	return func(strategyID, runID string, rung int) biz.Cloid {
		return DeriveCloid(strategyID, runID, rung)
	}
}

// ProviderSet is the Wire provider set for the hyperliquid data package.
var ProviderSet = wire.NewSet(NewHyperliquidExchange, NewCloidDeriver)
