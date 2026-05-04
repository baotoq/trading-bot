from __future__ import annotations

import logging
from decimal import ROUND_DOWN, Decimal

from dca.config import Settings
from dca.types import (
    ClientProtocol,
    FillResult,
    InsufficientFunds,
    SlippageExceeded,
)

log = logging.getLogger("dca")

_BPS = Decimal("10000")


def _round_down_to_lot(size: Decimal, lot: Decimal) -> Decimal:
    if lot <= 0:
        raise ValueError(f"lot must be positive, got {lot}")
    # quantize to the lot grid by dividing, flooring, and multiplying back
    units = (size / lot).to_integral_value(rounding=ROUND_DOWN)
    return units * lot


def one_buy(
    client: ClientProtocol,
    settings: Settings,
    amount_usdc: Decimal,
    *,
    dry_run: bool,
) -> FillResult:
    """Place one DCA buy. Pure logic; all I/O via the injected client."""
    balance = client.usdc_balance()
    if balance < amount_usdc:
        log.warning(
            "insufficient_balance",
            extra={"balance": balance, "amount_usdc": amount_usdc},
        )
        raise InsufficientFunds(
            f"USDC balance {balance} < requested {amount_usdc}"
        )

    mid = client.btc_mid_price()
    lot = client.btc_lot_size()
    raw_size = amount_usdc / mid
    size = _round_down_to_lot(raw_size, lot)

    if size <= 0:
        raise InsufficientFunds(
            f"amount {amount_usdc} buys 0 BTC at mid {mid} with lot {lot}"
        )

    limit = mid * (Decimal(1) + Decimal(settings.slippage_bps) / _BPS)

    if dry_run:
        log.info(
            "dry_run",
            extra={"size": size, "limit": limit, "mid": mid},
        )
        return FillResult(
            submitted_size=size,
            filled_size=size,
            avg_price=mid,
            status="filled",
        )

    fill = client.place_btc_buy(size, limit)

    log.info(
        "cycle_filled" if fill.status == "filled" else "cycle_partial",
        extra={
            "mid": mid,
            "limit": limit,
            "submitted_size": fill.submitted_size,
            "filled_size": fill.filled_size,
            "avg_price": fill.avg_price,
            "status": fill.status,
        },
    )

    if fill.status != "filled" or fill.filled_size < fill.submitted_size:
        raise SlippageExceeded(
            f"order status={fill.status}, filled {fill.filled_size}/{fill.submitted_size}"
        )

    return fill
