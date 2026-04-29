package hyperliquid

import (
	"fmt"

	"github.com/ethereum/go-ethereum/crypto"
	"tradingbot/app/tradingbot/internal/biz"
)

// DeriveCloid deterministically derives a 16-byte client order ID from
// a strategy ID, run ID, and rung index using Keccak256.
func DeriveCloid(strategyID, runID string, rung int) biz.Cloid {
	input := fmt.Sprintf("%s:%s:%d", strategyID, runID, rung)
	hash := crypto.Keccak256([]byte(input))
	var c biz.Cloid
	copy(c[:], hash[:16])
	return c
}
