package biz

import (
	"context"
	"encoding/hex"
	"time"

	"github.com/shopspring/decimal"
)

type Coin string

type Cloid [16]byte

func (c Cloid) Hex() string {
	return hex.EncodeToString(c[:])
}

type RunStatus string

const (
	RunPending   RunStatus = "pending"
	RunPlaced    RunStatus = "placed"
	RunPartial   RunStatus = "partial"
	RunFilled    RunStatus = "filled"
	RunCancelled RunStatus = "cancelled"
	RunError     RunStatus = "error"
)

type PlaceSpotBuyReq struct {
	Coin     Coin
	Px       decimal.Decimal
	Sz       decimal.Decimal
	Cloid    Cloid
	PostOnly bool
}

type OrderRef struct {
	Cloid  Cloid
	Oid    int64
	Coin   Coin
	Px     decimal.Decimal
	Sz     decimal.Decimal
	Status string
}

type Rung struct {
	Cloid  Cloid
	Coin   Coin
	Px     decimal.Decimal
	Sz     decimal.Decimal
	Status string
	Oid    int64
}

type DCARun struct {
	ID         string
	StrategyID string
	Ts         time.Time
	Rungs      []Rung
	Status     RunStatus
}

type Exchange interface {
	MidPx(ctx context.Context, coin Coin) (decimal.Decimal, error)
	SpotBalance(ctx context.Context, sym string) (decimal.Decimal, error)
	PlaceSpotBuy(ctx context.Context, req PlaceSpotBuyReq) (OrderRef, error)
	CancelByCloid(ctx context.Context, coin Coin, cloid Cloid) error
	OpenOrders(ctx context.Context, coin Coin) ([]OrderRef, error)
}

type StrategyStateRepo interface {
	SaveRun(ctx context.Context, run *DCARun) error
	LatestRun(ctx context.Context, strategyID string) (*DCARun, error)
	GetRun(ctx context.Context, strategyID, runID string) (*DCARun, error)
	ListRuns(ctx context.Context, strategyID string, limit int) ([]*DCARun, error)
}
