from datetime import datetime
from decimal import Decimal
from threading import Event

from pydantic import SecretStr

from dca._fake_client import FakeHyperliquidClient
from dca.config import Settings
from dca.scheduler import run as scheduler_run


def make_settings() -> Settings:
    return Settings(
        api_wallet_key=SecretStr("0xdeadbeef"),
        account_address="0xabc",
    )


class FakeClock:
    """Deterministic clock that records sleeps instead of actually sleeping."""

    def __init__(self, start: datetime):
        self.now = start
        self.sleeps: list[float] = []

    def now_local(self) -> datetime:
        return self.now

    def sleep_until(self, target: datetime, stop: Event) -> None:
        delta = (target - self.now).total_seconds()
        self.sleeps.append(delta)
        self.now = target


def test_one_cycle_then_stops():
    fake_client = FakeHyperliquidClient(balance=Decimal("1000"), mid=Decimal("60000"))
    stop = Event()
    clock = FakeClock(datetime(2026, 5, 5, 8, 59, 59))

    # Stop after the first cycle completes by setting the event from the callback.
    def after_cycle(_outcome):
        stop.set()

    scheduler_run(
        client=fake_client,
        settings=make_settings(),
        schedule="0 9 * * *",  # daily 09:00
        amount_usdc=Decimal("50"),
        stop=stop,
        clock=clock,
        on_cycle_done=after_cycle,
    )

    assert len(fake_client.placed_orders) == 1
    # slept until 09:00 = ~1 second
    assert clock.sleeps[0] == 1.0


def test_transient_error_continues_loop(caplog):
    """A SlippageExceeded in one cycle must not kill the daemon."""
    fake_client = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("60000"),
        fill_status="rejected",
        fill_filled_size=Decimal("0"),
    )
    stop = Event()
    clock = FakeClock(datetime(2026, 5, 5, 8, 59, 59))
    cycles = {"n": 0}

    def after_each(_outcome):
        cycles["n"] += 1
        if cycles["n"] >= 2:
            stop.set()

    scheduler_run(
        client=fake_client,
        settings=make_settings(),
        schedule="0 9 * * *",
        amount_usdc=Decimal("50"),
        stop=stop,
        clock=clock,
        on_cycle_done=after_each,
    )

    # Two cycles ran, both failed, daemon kept going
    assert len(fake_client.placed_orders) == 2
    assert cycles["n"] == 2
    assert any("cycle_failed" in r.getMessage() for r in caplog.records)


def test_stop_event_set_before_loop_exits_immediately():
    fake_client = FakeHyperliquidClient()
    stop = Event()
    stop.set()
    clock = FakeClock(datetime(2026, 5, 5, 9, 0, 0))

    scheduler_run(
        client=fake_client,
        settings=make_settings(),
        schedule="0 9 * * *",
        amount_usdc=Decimal("50"),
        stop=stop,
        clock=clock,
    )

    assert fake_client.placed_orders == []
    assert clock.sleeps == []
