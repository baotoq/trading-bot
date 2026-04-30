package hyperliquid

import (
	"testing"
)

func TestDeriveCloid_Deterministic(t *testing.T) {
	strategyID := "strat-abc"
	runID := "run-123"
	rung := 2

	c1 := DeriveCloid(strategyID, runID, rung)
	c2 := DeriveCloid(strategyID, runID, rung)

	if c1 != c2 {
		t.Errorf("DeriveCloid is not deterministic: got %x and %x", c1, c2)
	}
}

func TestDeriveCloid_Is16Bytes(t *testing.T) {
	c := DeriveCloid("s", "r", 0)
	if len(c) != 16 {
		t.Errorf("expected 16 bytes, got %d", len(c))
	}
}

func TestDeriveCloid_DifferentInputsDifferentOutputs(t *testing.T) {
	c1 := DeriveCloid("strat", "run", 0)
	c2 := DeriveCloid("strat", "run", 1)

	if c1 == c2 {
		t.Errorf("different rung should produce different cloids")
	}
}
