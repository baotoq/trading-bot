from __future__ import annotations

import logging
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from threading import Event
from typing import Callable, Protocol

from croniter import croniter
from pydantic import ValidationError

from dca.config import Settings
from dca.dca import one_buy
from dca.types import AuthError, ClientProtocol, FillResult

log = logging.getLogger("dca.scheduler")


class Clock(Protocol):
    def now_local(self) -> datetime: ...
    def sleep_until(self, target: datetime, stop: Event) -> None: ...


@dataclass
class WallClock:
    """Real clock. Sleeps in 1-second increments so SIGINT is responsive."""

    def now_local(self) -> datetime:
        return datetime.now().astimezone()

    def sleep_until(self, target: datetime, stop: Event) -> None:
        while not stop.is_set():
            remaining = (target - self.now_local()).total_seconds()
            if remaining <= 0:
                return
            if stop.wait(timeout=min(remaining, 1.0)):
                return  # stop was set


def run(
    *,
    client: ClientProtocol,
    settings: Settings,
    schedule: str,
    amount_usdc: Decimal,
    stop: Event,
    clock: Clock | None = None,
    on_cycle_done: Callable[[FillResult | Exception], None] | None = None,
) -> None:
    """Daemon loop. Returns when stop is set."""
    clock = clock or WallClock()
    iterator = croniter(schedule, clock.now_local())

    while not stop.is_set():
        next_t = iterator.get_next(datetime)
        log.info("cycle_scheduled", extra={"next_t": next_t.isoformat()})
        clock.sleep_until(next_t, stop)
        if stop.is_set():
            break
        try:
            outcome: FillResult | Exception = one_buy(
                client, settings, amount_usdc, dry_run=False
            )
        except (AuthError, ValidationError):
            log.exception("cycle_failed_unrecoverable")
            raise
        except Exception as exc:
            log.exception("cycle_failed")
            outcome = exc
        if on_cycle_done is not None:
            try:
                on_cycle_done(outcome)
            except Exception:
                log.exception("on_cycle_done_failed")
