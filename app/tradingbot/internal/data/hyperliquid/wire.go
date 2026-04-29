package hyperliquid

import "github.com/google/wire"

// ProviderSet is the Wire provider set for the hyperliquid data package.
var ProviderSet = wire.NewSet(NewHyperliquidExchange)
