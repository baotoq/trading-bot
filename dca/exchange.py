from __future__ import annotations

import logging
from decimal import Decimal
from functools import cached_property

from eth_account import Account
from hyperliquid.exchange import Exchange
from hyperliquid.info import Info
from hyperliquid.utils import constants

from dca.config import Settings
from dca.types import (
    AuthError,
    ClientProtocol,
    FillResult,
    TransientNetworkError,
)

log = logging.getLogger("dca.exchange")

_BTC_SPOT_PAIR = "UBTC/USDC"     # Hyperliquid spot ticker for BTC vs USDC
_BTC_COIN = "UBTC"               # underlying coin name in spot context


def _api_url(network: str) -> str:
    return constants.MAINNET_API_URL if network == "mainnet" else constants.TESTNET_API_URL


class HyperliquidClient(ClientProtocol):
    def __init__(self, settings: Settings):
        self._settings = settings
        api_url = _api_url(settings.network)
        self._info = Info(api_url, skip_ws=True)
        self._wallet = Account.from_key(settings.api_wallet_key.get_secret_value())
        self._exchange = Exchange(
            self._wallet,
            api_url,
            account_address=settings.account_address,
        )

    @cached_property
    def _spot_meta(self) -> dict:
        try:
            return self._info.spot_meta()
        except Exception as e:
            raise TransientNetworkError(f"spot_meta failed: {e}") from e

    @cached_property
    def _btc_sz_decimals(self) -> int:
        for token in self._spot_meta["tokens"]:
            if token["name"] == _BTC_COIN:
                return int(token["szDecimals"])
        raise RuntimeError(f"{_BTC_COIN} not found in spot meta")

    def usdc_balance(self) -> Decimal:
        try:
            state = self._info.spot_user_state(self._settings.account_address)
        except Exception as e:
            raise TransientNetworkError(f"spot_user_state failed: {e}") from e
        for bal in state.get("balances", []):
            if bal.get("coin") == "USDC":
                return Decimal(str(bal["total"]))
        return Decimal(0)

    def btc_mid_price(self) -> Decimal:
        try:
            mids = self._info.all_mids()
        except Exception as e:
            raise TransientNetworkError(f"all_mids failed: {e}") from e
        # Hyperliquid uses '@<index>' or pair string keys for spot; try both.
        for key in (_BTC_SPOT_PAIR, _BTC_COIN):
            if key in mids:
                return Decimal(str(mids[key]))
        raise RuntimeError(f"no mid price for {_BTC_SPOT_PAIR} in {list(mids)[:5]}...")

    def btc_lot_size(self) -> Decimal:
        # szDecimals=5 -> lot 0.00001
        return Decimal(1) / (Decimal(10) ** self._btc_sz_decimals)

    def place_btc_buy(self, size: Decimal, limit_price: Decimal) -> FillResult:
        try:
            resp = self._exchange.order(
                name=_BTC_SPOT_PAIR,
                is_buy=True,
                sz=float(size),
                limit_px=float(limit_price),
                order_type={"limit": {"tif": "Ioc"}},
                reduce_only=False,
            )
        except Exception as e:
            raise TransientNetworkError(f"order submit failed: {e}") from e

        return _parse_order_response(resp, submitted_size=size, fallback_price=limit_price)


def _parse_order_response(
    resp: dict,
    *,
    submitted_size: Decimal,
    fallback_price: Decimal,
) -> FillResult:
    if resp.get("status") != "ok":
        msg = str(resp)
        log.warning("order_top_level_not_ok", extra={"resp": msg})
        if "signature" in msg.lower() or "unauthorized" in msg.lower():
            raise AuthError(msg)
        raise TransientNetworkError(f"order rejected: {msg}")

    statuses = (
        resp.get("response", {})
        .get("data", {})
        .get("statuses", [])
    )
    if not statuses:
        log.warning("order_empty_statuses", extra={"resp": str(resp)})
        return FillResult(
            submitted_size=submitted_size,
            filled_size=Decimal(0),
            avg_price=fallback_price,
            status="rejected",
        )

    s = statuses[0]
    if "filled" in s:
        f = s["filled"]
        return FillResult(
            submitted_size=submitted_size,
            filled_size=Decimal(str(f.get("totalSz", "0"))),
            avg_price=Decimal(str(f.get("avgPx", str(fallback_price)))),
            status="filled",
        )
    if "resting" in s:
        log.warning("order_unexpected_resting", extra={"status": str(s)})
        # IOC should not rest; treat as rejected
        return FillResult(
            submitted_size=submitted_size,
            filled_size=Decimal(0),
            avg_price=fallback_price,
            status="rejected",
        )
    if "error" in s:
        log.warning("order_status_error", extra={"err": str(s["error"])})
        raise TransientNetworkError(s["error"])
    log.warning("order_unknown_status_shape", extra={"status": str(s)})
    return FillResult(
        submitted_size=submitted_size,
        filled_size=Decimal(0),
        avg_price=fallback_price,
        status="rejected",
    )
