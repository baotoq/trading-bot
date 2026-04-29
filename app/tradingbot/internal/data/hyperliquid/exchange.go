package hyperliquid

import (
	"context"
	"encoding/hex"
	"fmt"
	"strings"

	kratosErrors "github.com/go-kratos/kratos/v2/errors"
	"github.com/go-kratos/kratos/v2/log"
	"github.com/shopspring/decimal"
	hlsdk "github.com/sonirico/go-hyperliquid"
	"tradingbot/app/tradingbot/internal/biz"
	confpkg "tradingbot/app/tradingbot/internal/conf"
)

type hlExchange struct {
	info       *hlsdk.Info
	ex         *hlsdk.Exchange
	masterAddr string
	log        *log.Helper
}

// NewHyperliquidExchange constructs an hlExchange that implements biz.Exchange.
func NewHyperliquidExchange(
	ctx context.Context,
	conf *confpkg.Exchange_Hyperliquid,
	logger log.Logger,
) (*hlExchange, error) {
	info, ex, err := NewHyperliquidClients(ctx, conf)
	if err != nil {
		return nil, err
	}

	return &hlExchange{
		info:       info,
		ex:         ex,
		masterAddr: conf.GetMasterAddress(),
		log:        log.NewHelper(logger),
	}, nil
}

// MidPx returns the mid price for the given coin.
func (h *hlExchange) MidPx(ctx context.Context, coin biz.Coin) (decimal.Decimal, error) {
	mids, err := h.info.AllMids(ctx)
	if err != nil {
		return decimal.Zero, kratosErrors.InternalServer("EXCHANGE", err.Error())
	}

	raw, ok := mids[string(coin)]
	if !ok {
		return decimal.Zero, kratosErrors.InternalServer(
			"EXCHANGE",
			fmt.Sprintf("coin %s not found in mids", coin),
		)
	}

	px, err := decimal.NewFromString(raw)
	if err != nil {
		return decimal.Zero, kratosErrors.InternalServer("EXCHANGE", err.Error())
	}

	return px, nil
}

// SpotBalance returns the total spot balance for the given symbol.
func (h *hlExchange) SpotBalance(ctx context.Context, sym string) (decimal.Decimal, error) {
	state, err := h.info.SpotUserState(ctx, h.masterAddr)
	if err != nil {
		return decimal.Zero, kratosErrors.InternalServer("EXCHANGE", err.Error())
	}

	for _, b := range state.Balances {
		if b.Coin == sym {
			bal, parseErr := decimal.NewFromString(b.Total)
			if parseErr != nil {
				return decimal.Zero, kratosErrors.InternalServer("EXCHANGE", parseErr.Error())
			}
			return bal, nil
		}
	}

	return decimal.Zero, nil
}

// PlaceSpotBuy places a spot buy limit order with Alo (post-only) time-in-force.
func (h *hlExchange) PlaceSpotBuy(
	ctx context.Context,
	req biz.PlaceSpotBuyReq,
) (biz.OrderRef, error) {
	cloidHex := req.Cloid.Hex() // 32-char hex, no 0x prefix; SDK normalizeCloid adds 0x
	orderReq := hlsdk.CreateOrderRequest{
		Coin:  string(req.Coin),
		IsBuy: true,
		Price: req.Px.InexactFloat64(),
		Size:  req.Sz.InexactFloat64(),
		OrderType: hlsdk.OrderType{
			Limit: &hlsdk.LimitOrderType{Tif: hlsdk.TifAlo},
		},
		ClientOrderID: &cloidHex,
	}

	status, err := h.ex.Order(ctx, orderReq, nil)
	if err != nil {
		return biz.OrderRef{}, kratosErrors.InternalServer("EXCHANGE", err.Error())
	}

	ref := biz.OrderRef{
		Cloid: req.Cloid,
		Coin:  req.Coin,
		Px:    req.Px,
		Sz:    req.Sz,
	}

	if status.Resting != nil {
		ref.Oid = status.Resting.Oid
		ref.Status = status.Resting.Status
	} else if status.Filled != nil {
		ref.Oid = int64(status.Filled.Oid)
		ref.Status = "filled"
	}

	return ref, nil
}

// CancelByCloid cancels an order identified by its client order ID.
func (h *hlExchange) CancelByCloid(
	ctx context.Context,
	coin biz.Coin,
	cloid biz.Cloid,
) error {
	_, err := h.ex.CancelByCloid(ctx, string(coin), cloid.Hex())
	if err != nil {
		return kratosErrors.InternalServer("EXCHANGE", err.Error())
	}
	return nil
}

// OpenOrders returns open orders for the given coin, filtered and mapped from
// the SDK response. Orders without a cloid are skipped.
func (h *hlExchange) OpenOrders(ctx context.Context, coin biz.Coin) ([]biz.OrderRef, error) {
	orders, err := h.info.OpenOrders(ctx, h.masterAddr)
	if err != nil {
		return nil, kratosErrors.InternalServer("EXCHANGE", err.Error())
	}

	var refs []biz.OrderRef
	for _, o := range orders {
		if o.Coin != string(coin) {
			continue
		}
		if o.Cloid == nil {
			continue
		}

		cloid, parseErr := parseCloidHex(*o.Cloid)
		if parseErr != nil {
			h.log.Warnf("skipping order oid=%d: invalid cloid %q: %v", o.Oid, *o.Cloid, parseErr)
			continue
		}

		refs = append(refs, biz.OrderRef{
			Cloid: cloid,
			Oid:   o.Oid,
			Coin:  biz.Coin(o.Coin),
			Px:    decimal.NewFromFloat(o.LimitPx),
			Sz:    decimal.NewFromFloat(o.Size),
		})
	}

	return refs, nil
}

// parseCloidHex parses a 32-char hex string (with or without 0x prefix) into a biz.Cloid.
func parseCloidHex(s string) (biz.Cloid, error) {
	s = strings.TrimPrefix(s, "0x")
	if len(s) != 32 {
		return biz.Cloid{}, fmt.Errorf("cloid must be 32 hex chars (got %d)", len(s))
	}

	b, err := hex.DecodeString(s)
	if err != nil {
		return biz.Cloid{}, fmt.Errorf("invalid hex cloid: %w", err)
	}

	var c biz.Cloid
	copy(c[:], b)
	return c, nil
}
