from decimal import Decimal

from dca._fake_client import FakeHyperliquidClient
from dca.types import FillResult


def test_fake_returns_configured_balance():
    fake = FakeHyperliquidClient(balance=Decimal("123.45"))
    assert fake.usdc_balance() == Decimal("123.45")


def test_fake_records_placed_order():
    fake = FakeHyperliquidClient()
    fake.place_btc_buy(Decimal("0.001"), Decimal("60_300"))
    assert fake.placed_orders == [(Decimal("0.001"), Decimal("60_300"))]


def test_fake_fill_defaults_to_full_fill_at_limit():
    fake = FakeHyperliquidClient()
    result = fake.place_btc_buy(Decimal("0.001"), Decimal("60_300"))
    assert result == FillResult(
        submitted_size=Decimal("0.001"),
        filled_size=Decimal("0.001"),
        avg_price=Decimal("60_300"),
        status="filled",
    )


def test_fake_can_simulate_partial_fill():
    fake = FakeHyperliquidClient(
        fill_status="partial",
        fill_filled_size=Decimal("0.0005"),
    )
    result = fake.place_btc_buy(Decimal("0.001"), Decimal("60_300"))
    assert result.status == "partial"
    assert result.filled_size == Decimal("0.0005")
    assert result.submitted_size == Decimal("0.001")
