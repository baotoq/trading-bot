from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal
from typing import Literal, Protocol


FillStatus = Literal["filled", "partial", "rejected"]


@dataclass(frozen=True, slots=True)
class FillResult:
    submitted_size: Decimal
    filled_size: Decimal
    avg_price: Decimal
    status: FillStatus


class ClientProtocol(Protocol):
    """Surface dca.one_buy depends on. Implemented by HyperliquidClient and FakeHyperliquidClient."""

    def usdc_balance(self) -> Decimal: ...
    def btc_mid_price(self) -> Decimal: ...
    def btc_lot_size(self) -> Decimal: ...
    def place_btc_buy(self, size: Decimal, limit_price: Decimal) -> FillResult: ...


class InsufficientFunds(Exception):
    """USDC balance below the requested amount."""


class SlippageExceeded(Exception):
    """Order did not fill (or only partially filled) within the limit price."""


class AuthError(Exception):
    """API wallet not authorized, or signature rejected."""


class TransientNetworkError(Exception):
    """Timeout or 5xx from Hyperliquid."""
