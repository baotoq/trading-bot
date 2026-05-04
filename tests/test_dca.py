import logging
from decimal import Decimal

import pytest
from pydantic import SecretStr

from dca._fake_client import FakeHyperliquidClient
from dca.config import Settings
from dca.dca import one_buy
from dca.types import (
    InsufficientFunds,
    SlippageExceeded,
)


def make_settings(slippage_bps: int = 50) -> Settings:
    return Settings(
        api_wallet_key=SecretStr("0xdeadbeef"),
        account_address="0xabc",
        slippage_bps=slippage_bps,
    )


def test_happy_path_returns_filled_result():
    fake = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("60000"),
        lot=Decimal("0.00001"),
    )
    result = one_buy(fake, make_settings(), Decimal("50"), dry_run=False)
    assert result.status == "filled"
    assert result.submitted_size > 0
    # 50 / 60000 = 0.00083333..., rounded down to 5dp => 0.00083
    assert result.submitted_size == Decimal("0.00083")
    # one order placed, with limit at mid + 50bps
    assert len(fake.placed_orders) == 1
    size, limit = fake.placed_orders[0]
    assert size == Decimal("0.00083")
    assert limit == Decimal("60000") * (Decimal("1") + Decimal("50") / Decimal("10000"))


def test_size_rounds_down_to_lot():
    fake = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("63123.45"),
        lot=Decimal("0.0001"),  # 4 dp
    )
    result = one_buy(fake, make_settings(), Decimal("50"), dry_run=False)
    # 50 / 63123.45 = 0.00079211..., rounded down to 4dp => 0.0007
    assert result.submitted_size == Decimal("0.0007")


def test_insufficient_balance_raises():
    fake = FakeHyperliquidClient(balance=Decimal("10"))
    with pytest.raises(InsufficientFunds):
        one_buy(fake, make_settings(), Decimal("50"), dry_run=False)
    assert fake.placed_orders == []


def test_dry_run_does_not_place_order(caplog):
    fake = FakeHyperliquidClient(balance=Decimal("1000"), mid=Decimal("60000"))
    with caplog.at_level(logging.INFO):
        result = one_buy(fake, make_settings(), Decimal("50"), dry_run=True)
    assert fake.placed_orders == []
    assert result.status == "filled"  # dry-run synthesizes a "would-fill" result
    assert any("dry_run" in r.getMessage().lower() for r in caplog.records)


def test_partial_fill_raises_slippage_exceeded():
    fake = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("60000"),
        fill_status="partial",
        fill_filled_size=Decimal("0.0001"),  # less than submitted
    )
    with pytest.raises(SlippageExceeded):
        one_buy(fake, make_settings(), Decimal("50"), dry_run=False)


def test_rejected_fill_raises_slippage_exceeded():
    fake = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("60000"),
        fill_status="rejected",
        fill_filled_size=Decimal("0"),
    )
    with pytest.raises(SlippageExceeded):
        one_buy(fake, make_settings(), Decimal("50"), dry_run=False)


def test_amount_below_one_lot_raises_insufficient_funds():
    # mid is so high that 1 USD cannot buy even one lot
    fake = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("100_000_000"),
        lot=Decimal("0.00001"),
    )
    with pytest.raises(InsufficientFunds):
        one_buy(fake, make_settings(), Decimal("1"), dry_run=False)


def test_logs_structured_json_on_success(caplog):
    fake = FakeHyperliquidClient(balance=Decimal("1000"), mid=Decimal("60000"))
    with caplog.at_level(logging.INFO, logger="dca"):
        one_buy(fake, make_settings(), Decimal("50"), dry_run=False)
    cycle_records = [r for r in caplog.records if r.getMessage() == "cycle_filled"]
    assert len(cycle_records) == 1
    rec = cycle_records[0]
    # structured fields attached to the LogRecord
    assert getattr(rec, "filled_size") == Decimal("0.00083")
    assert getattr(rec, "status") == "filled"
