//go:build tools

// Package tools pins direct dependencies that are not yet imported elsewhere
// in the codebase. This ensures go mod tidy does not remove them.
package tools

import (
	_ "github.com/ethereum/go-ethereum"
	_ "github.com/redis/go-redis/v9"
	_ "github.com/shopspring/decimal"
	_ "github.com/sonirico/go-hyperliquid"
)
