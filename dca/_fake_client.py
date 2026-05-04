from __future__ import annotations

from dataclasses import dataclass, field
from decimal import Decimal

from dca.types import FillResult, FillStatus


@dataclass
class FakeHyperliquidClient:
    """Test double for ClientProtocol. Configurable per-test."""

    balance: Decimal = Decimal("1000")
    mid: Decimal = Decimal("60000")
    lot: Decimal = Decimal("0.00001")          # 5 dp; matches typical szDecimals
    fill_status: FillStatus = "filled"
    fill_avg_price: Decimal | None = None      # None => use the limit_price the caller passed
    fill_filled_size: Decimal | None = None    # None => fill the full submitted_size
    placed_orders: list[tuple[Decimal, Decimal]] = field(default_factory=list)

    def usdc_balance(self) -> Decimal:
        return self.balance

    def btc_mid_price(self) -> Decimal:
        return self.mid

    def btc_lot_size(self) -> Decimal:
        return self.lot

    def place_btc_buy(self, size: Decimal, limit_price: Decimal) -> FillResult:
        self.placed_orders.append((size, limit_price))
        filled = self.fill_filled_size if self.fill_filled_size is not None else size
        avg = self.fill_avg_price if self.fill_avg_price is not None else limit_price
        return FillResult(
            submitted_size=size,
            filled_size=filled,
            avg_price=avg,
            status=self.fill_status,
        )
