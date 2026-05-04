# BTC DCA on Hyperliquid CLI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Python CLI that DCAs into Hyperliquid spot BTC, supporting both one-shot (cron) and daemon (croniter) modes, with API-wallet authentication, IOC-with-slippage-cap orders, and stateless JSON logging.

**Architecture:** A small `dca/` package with one-job-each modules behind a `typer` CLI. Pure-logic core (`dca.one_buy`) takes a `ClientProtocol`, allowing TDD with a fake client. The real `HyperliquidClient` is thin glue over the official `hyperliquid-python-sdk` and is verified by an opt-in testnet integration test. `Decimal` end-to-end. No persistent state.

**Tech Stack:** Python 3.12+, `hyperliquid-python-sdk`, `typer`, `pydantic` + `pydantic-settings`, `croniter`, `pytest`. Built with `hatchling`; `uv` recommended as the user-facing tool.

**Reference spec:** `docs/superpowers/specs/2026-05-05-btc-dca-hyperliquid-cli-design.md`

**Frequent commits:** Each task ends with a commit. Don't batch.

---

## Task 1: Project bootstrap

**Files:**
- Create: `pyproject.toml`
- Create: `.env.example`
- Create: `README.md` (stub; polished in Task 9)
- Create: `dca/__init__.py`
- Create: `tests/__init__.py`
- Create: `.gitignore` *(already exists for Rust; we replace it)*
- Create: `Makefile`

- [ ] **Step 1: Replace the leftover Rust `.gitignore` with a Python one**

```gitignore
# Python
__pycache__/
*.py[cod]
*$py.class
.Python
*.egg-info/
.venv/
venv/
env/
.eggs/
build/
dist/

# pytest
.pytest_cache/
.coverage
htmlcov/

# Editor / OS
.idea/
.vscode/
.DS_Store

# Local config
.env
~/.dca/

# OMC
.omc/

# Leftover Rust artifacts (clean up)
target/
```

- [ ] **Step 2: Remove the leftover Rust `target/` directory**

```bash
rm -rf target/
```

- [ ] **Step 3: Write `pyproject.toml`**

```toml
[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[project]
name = "dca"
version = "0.1.0"
description = "Dollar-cost-averaging CLI for Hyperliquid spot BTC"
requires-python = ">=3.12"
authors = [{ name = "Bao To" }]
dependencies = [
    "hyperliquid-python-sdk>=0.10",
    "typer>=0.12",
    "pydantic>=2.6",
    "pydantic-settings>=2.2",
    "croniter>=2.0",
]

[project.optional-dependencies]
dev = [
    "pytest>=8.0",
    "pytest-mock>=3.12",
]

[project.scripts]
dca = "dca.cli:app"

[tool.hatch.build.targets.wheel]
packages = ["dca"]

[tool.pytest.ini_options]
testpaths = ["tests"]
markers = [
    "testnet: integration test that hits Hyperliquid testnet (opt-in via HL_TESTNET_KEY env var)",
]
addopts = "-ra"
```

- [ ] **Step 4: Write `.env.example`**

```dotenv
# Required
HL_API_WALLET_KEY=0x_your_api_wallet_private_key_here
HL_ACCOUNT_ADDRESS=0x_your_main_account_address_here

# Optional
HL_NETWORK=mainnet              # or "testnet"
HL_DEFAULT_AMOUNT_USDC=50
HL_DEFAULT_SCHEDULE=0 9 * * 1   # cron: Monday 09:00 host-local
HL_SLIPPAGE_BPS=50              # 50 = 0.5% maximum slippage
```

- [ ] **Step 5: Write a stub `README.md`**

```markdown
# dca — BTC DCA on Hyperliquid

Small Python CLI that dollar-cost-averages into Hyperliquid spot BTC.

## Quickstart

See `docs/superpowers/specs/2026-05-05-btc-dca-hyperliquid-cli-design.md` for the design.

## Setup (in development)

```bash
uv venv
source .venv/bin/activate
uv pip install -e ".[dev]"
pytest
```

(Full README populated in the final implementation task.)
```

- [ ] **Step 6: Create empty `dca/__init__.py` and `tests/__init__.py`**

```python
# dca/__init__.py
__version__ = "0.1.0"
```

```python
# tests/__init__.py
```

- [ ] **Step 7: Write `Makefile` for common tasks**

```makefile
.PHONY: install test test-net lint clean

install:
	uv venv
	uv pip install -e ".[dev]"

test:
	pytest

test-net:
	pytest -m testnet

clean:
	rm -rf .pytest_cache build dist *.egg-info
```

- [ ] **Step 8: Verify the package installs and imports**

```bash
uv venv
source .venv/bin/activate
uv pip install -e ".[dev]"
python -c "import dca; print(dca.__version__)"
```

Expected output: `0.1.0`

- [ ] **Step 9: Verify pytest runs (zero tests, but the harness loads)**

```bash
pytest
```

Expected: `no tests ran in ...s` and exit code 5 (no tests collected). That's fine — we'll have tests next task.

- [ ] **Step 10: Commit**

```bash
git add pyproject.toml .env.example README.md Makefile .gitignore dca/__init__.py tests/__init__.py
git commit -m "chore: bootstrap python project for dca cli"
```

---

## Task 2: `FillResult` and `ClientProtocol`

The pure-logic layer needs a typed seam against which to test. We define both the result type and the protocol the rest of the code depends on, plus a `FakeHyperliquidClient` for tests.

**Files:**
- Create: `dca/types.py`
- Create: `dca/_fake_client.py`
- Create: `tests/test_fake_client.py`

- [ ] **Step 1: Write `dca/types.py`**

```python
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
```

- [ ] **Step 2: Write `dca/_fake_client.py`**

```python
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
```

- [ ] **Step 3: Write `tests/test_fake_client.py` to lock the fake's behavior**

```python
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
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest tests/test_fake_client.py -v
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add dca/types.py dca/_fake_client.py tests/test_fake_client.py
git commit -m "feat: add FillResult, ClientProtocol, and FakeHyperliquidClient"
```

---

## Task 3: `Settings` config

TDD the config module. Cover env-only, TOML-only, env-overrides-TOML, and required-field errors.

**Files:**
- Create: `dca/config.py`
- Create: `tests/test_config.py`

- [ ] **Step 1: Write the failing tests in `tests/test_config.py`**

```python
from decimal import Decimal
from pathlib import Path

import pytest
from pydantic import ValidationError

from dca.config import Settings


@pytest.fixture
def base_env(monkeypatch):
    monkeypatch.setenv("HL_API_WALLET_KEY", "0xdeadbeef")
    monkeypatch.setenv("HL_ACCOUNT_ADDRESS", "0xabc")
    return monkeypatch


def test_loads_required_fields_from_env(base_env):
    settings = Settings.load()
    assert settings.api_wallet_key.get_secret_value() == "0xdeadbeef"
    assert settings.account_address == "0xabc"


def test_defaults_when_optional_unset(base_env):
    settings = Settings.load()
    assert settings.network == "mainnet"
    assert settings.default_amount_usdc is None
    assert settings.default_schedule is None
    assert settings.slippage_bps == 50


def test_parses_decimal_amount_from_env(base_env):
    base_env.setenv("HL_DEFAULT_AMOUNT_USDC", "25.5")
    settings = Settings.load()
    assert settings.default_amount_usdc == Decimal("25.5")


def test_network_validates_literal(base_env):
    base_env.setenv("HL_NETWORK", "fakenet")
    with pytest.raises(ValidationError):
        Settings.load()


def test_missing_required_raises(monkeypatch):
    monkeypatch.delenv("HL_API_WALLET_KEY", raising=False)
    monkeypatch.delenv("HL_ACCOUNT_ADDRESS", raising=False)
    with pytest.raises(ValidationError) as exc:
        Settings.load()
    assert "api_wallet_key" in str(exc.value)


def test_toml_provides_defaults(base_env, tmp_path: Path):
    cfg = tmp_path / "config.toml"
    cfg.write_text(
        'default_amount_usdc = "12.34"\nslippage_bps = 100\n',
        encoding="utf-8",
    )
    settings = Settings.load(config_path=cfg)
    assert settings.default_amount_usdc == Decimal("12.34")
    assert settings.slippage_bps == 100


def test_env_wins_over_toml(base_env, tmp_path: Path):
    cfg = tmp_path / "config.toml"
    cfg.write_text('slippage_bps = 100\n', encoding="utf-8")
    base_env.setenv("HL_SLIPPAGE_BPS", "30")
    settings = Settings.load(config_path=cfg)
    assert settings.slippage_bps == 30
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
pytest tests/test_config.py -v
```

Expected: errors on import (`dca.config` doesn't exist yet).

- [ ] **Step 3: Implement `dca/config.py`**

```python
from __future__ import annotations

import os
import tomllib
from decimal import Decimal
from pathlib import Path
from typing import Any, Literal

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_prefix="HL_",
        env_file=None,            # callers manage .env if they want it
        extra="ignore",
        case_sensitive=False,
    )

    api_wallet_key: SecretStr
    account_address: str
    network: Literal["mainnet", "testnet"] = "mainnet"
    default_amount_usdc: Decimal | None = None
    default_schedule: str | None = None
    slippage_bps: int = Field(default=50, ge=0, le=10_000)

    @classmethod
    def load(cls, *, config_path: Path | None = None) -> "Settings":
        toml_defaults: dict[str, Any] = {}
        if config_path is not None and config_path.exists():
            with config_path.open("rb") as f:
                toml_defaults = tomllib.load(f)
        # In pydantic-settings, init kwargs OUTRANK env vars. We want env to win,
        # so only forward TOML values for fields the env did not supply.
        filtered = {
            k: v for k, v in toml_defaults.items()
            if f"HL_{k.upper()}" not in os.environ
        }
        return cls(**filtered)
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest tests/test_config.py -v
```

Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add dca/config.py tests/test_config.py
git commit -m "feat: add Settings (env + TOML config with Decimal money)"
```

---

## Task 4: `dca.one_buy` core

TDD the pure-logic core against `FakeHyperliquidClient`. This is the heart of the bot.

**Files:**
- Create: `dca/dca.py`
- Create: `tests/test_dca.py`

- [ ] **Step 1: Write the failing tests in `tests/test_dca.py`**

```python
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
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
pytest tests/test_dca.py -v
```

Expected: errors on import (`dca.dca.one_buy` doesn't exist yet).

- [ ] **Step 3: Implement `dca/dca.py`**

```python
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
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest tests/test_dca.py -v
```

Expected: 8 passed.

- [ ] **Step 5: Commit**

```bash
git add dca/dca.py tests/test_dca.py
git commit -m "feat: implement dca.one_buy core with TDD coverage"
```

---

## Task 5: Scheduler

TDD the daemon loop with a fake clock and a stop event so we never `sleep` for real.

**Files:**
- Create: `dca/scheduler.py`
- Create: `tests/test_scheduler.py`

- [ ] **Step 1: Write the failing tests in `tests/test_scheduler.py`**

```python
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
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
pytest tests/test_scheduler.py -v
```

Expected: import error (`dca.scheduler` doesn't exist).

- [ ] **Step 3: Implement `dca/scheduler.py`**

```python
from __future__ import annotations

import logging
import time
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from threading import Event
from typing import Callable, Protocol

from croniter import croniter

from dca.config import Settings
from dca.dca import one_buy
from dca.types import ClientProtocol, FillResult

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
            time.sleep(min(remaining, 1.0))


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
        except Exception as exc:
            log.exception("cycle_failed")
            outcome = exc
        if on_cycle_done is not None:
            on_cycle_done(outcome)
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest tests/test_scheduler.py -v
```

Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add dca/scheduler.py tests/test_scheduler.py
git commit -m "feat: add cron-driven daemon scheduler with TDD coverage"
```

---

## Task 6: Real `HyperliquidClient`

Thin glue between the official SDK and our `ClientProtocol`. Not unit-testable in isolation; verified by the testnet integration test in Task 8.

**Files:**
- Create: `dca/exchange.py`

- [ ] **Step 1: Implement `dca/exchange.py`**

> The hyperliquid-python-sdk surface evolves. If a method below has been renamed in the version pinned by `pyproject.toml`, adjust the call site only — the public methods we expose (`usdc_balance`, `btc_mid_price`, `btc_lot_size`, `place_btc_buy`) must not change.

```python
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
        if "signature" in msg.lower() or "unauthorized" in msg.lower():
            raise AuthError(msg)
        raise TransientNetworkError(f"order rejected: {msg}")

    statuses = (
        resp.get("response", {})
        .get("data", {})
        .get("statuses", [])
    )
    if not statuses:
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
        # IOC should not rest; treat as rejected
        return FillResult(
            submitted_size=submitted_size,
            filled_size=Decimal(0),
            avg_price=fallback_price,
            status="rejected",
        )
    if "error" in s:
        raise TransientNetworkError(s["error"])
    return FillResult(
        submitted_size=submitted_size,
        filled_size=Decimal(0),
        avg_price=fallback_price,
        status="rejected",
    )
```

- [ ] **Step 2: Verify the module imports without instantiation**

```bash
python -c "from dca.exchange import HyperliquidClient; print('ok')"
```

Expected: `ok`

- [ ] **Step 3: Verify existing tests still pass (we did not touch them)**

```bash
pytest -q
```

Expected: all previous tests pass; nothing new added yet.

- [ ] **Step 4: Commit**

```bash
git add dca/exchange.py
git commit -m "feat: add HyperliquidClient (real SDK adapter)"
```

---

## Task 7: CLI commands

TDD the `typer` app using `CliRunner`, with the client factory monkey-patched to return a `FakeHyperliquidClient`. Lock exit codes per the spec.

**Files:**
- Create: `dca/cli.py`
- Create: `tests/test_cli.py`

- [ ] **Step 1: Write the failing tests in `tests/test_cli.py`**

```python
from decimal import Decimal

from typer.testing import CliRunner

from dca import cli
from dca._fake_client import FakeHyperliquidClient


runner = CliRunner(mix_stderr=False)


def _set_env(monkeypatch):
    monkeypatch.setenv("HL_API_WALLET_KEY", "0xdeadbeef")
    monkeypatch.setenv("HL_ACCOUNT_ADDRESS", "0xabc")


def test_buy_dry_run_exits_zero(monkeypatch):
    _set_env(monkeypatch)
    fake = FakeHyperliquidClient(balance=Decimal("1000"), mid=Decimal("60000"))
    monkeypatch.setattr(cli, "make_client", lambda settings: fake)

    result = runner.invoke(cli.app, ["buy", "--amount", "50", "--dry-run"])
    assert result.exit_code == 0
    assert fake.placed_orders == []


def test_buy_happy_path_exits_zero(monkeypatch):
    _set_env(monkeypatch)
    fake = FakeHyperliquidClient(balance=Decimal("1000"), mid=Decimal("60000"))
    monkeypatch.setattr(cli, "make_client", lambda settings: fake)

    result = runner.invoke(cli.app, ["buy", "--amount", "50"])
    assert result.exit_code == 0
    assert len(fake.placed_orders) == 1


def test_buy_insufficient_balance_exits_75(monkeypatch):
    _set_env(monkeypatch)
    fake = FakeHyperliquidClient(balance=Decimal("1"))
    monkeypatch.setattr(cli, "make_client", lambda settings: fake)

    result = runner.invoke(cli.app, ["buy", "--amount", "50"])
    assert result.exit_code == 75


def test_buy_slippage_exceeded_exits_75(monkeypatch):
    _set_env(monkeypatch)
    fake = FakeHyperliquidClient(
        balance=Decimal("1000"),
        mid=Decimal("60000"),
        fill_status="rejected",
        fill_filled_size=Decimal("0"),
    )
    monkeypatch.setattr(cli, "make_client", lambda settings: fake)

    result = runner.invoke(cli.app, ["buy", "--amount", "50"])
    assert result.exit_code == 75


def test_buy_missing_env_exits_64(monkeypatch):
    monkeypatch.delenv("HL_API_WALLET_KEY", raising=False)
    monkeypatch.delenv("HL_ACCOUNT_ADDRESS", raising=False)

    result = runner.invoke(cli.app, ["buy", "--amount", "50"])
    assert result.exit_code == 64


def test_balance_prints_and_exits_zero(monkeypatch):
    _set_env(monkeypatch)
    fake = FakeHyperliquidClient(balance=Decimal("123.45"))
    monkeypatch.setattr(cli, "make_client", lambda settings: fake)

    result = runner.invoke(cli.app, ["balance"])
    assert result.exit_code == 0
    assert "123.45" in result.stdout


def test_run_executes_one_cycle_then_stops(monkeypatch):
    _set_env(monkeypatch)
    fake = FakeHyperliquidClient(balance=Decimal("1000"), mid=Decimal("60000"))
    monkeypatch.setattr(cli, "make_client", lambda settings: fake)

    # Patch the scheduler to a stub that records and exits immediately.
    cycles = {"n": 0}

    def fake_run(*, client, settings, schedule, amount_usdc, stop, **kwargs):
        cycles["n"] += 1
        client.place_btc_buy(Decimal("0.001"), Decimal("60_300"))

    monkeypatch.setattr(cli, "scheduler_run", fake_run)

    result = runner.invoke(
        cli.app,
        ["run", "--schedule", "0 9 * * *", "--amount", "50"],
    )
    assert result.exit_code == 0
    assert cycles["n"] == 1
    assert len(fake.placed_orders) == 1
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
pytest tests/test_cli.py -v
```

Expected: import error (`dca.cli` doesn't exist).

- [ ] **Step 3: Implement `dca/cli.py`**

```python
from __future__ import annotations

import json
import logging
import signal
import sys
import time
from decimal import Decimal
from pathlib import Path
from threading import Event
from typing import Optional

import typer
from pydantic import ValidationError

from dca.config import Settings
from dca.dca import one_buy
from dca.scheduler import run as scheduler_run
from dca.types import (
    AuthError,
    ClientProtocol,
    InsufficientFunds,
    SlippageExceeded,
    TransientNetworkError,
)

# sysexits.h
EX_OK = 0
EX_USAGE = 64
EX_SOFTWARE = 70
EX_TEMPFAIL = 75
EX_NOPERM = 77

app = typer.Typer(add_completion=False, no_args_is_help=True)
log = logging.getLogger("dca.cli")


def _configure_logging() -> None:
    """JSON-ish line logging to stdout, one record per line."""
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(_JsonLineFormatter())
    root = logging.getLogger()
    root.handlers.clear()
    root.addHandler(handler)
    root.setLevel(logging.INFO)


class _JsonLineFormatter(logging.Formatter):
    converter = time.gmtime  # emit timestamps in UTC

    _RESERVED = {
        "name", "msg", "args", "levelname", "levelno", "pathname", "filename",
        "module", "exc_info", "exc_text", "stack_info", "lineno", "funcName",
        "created", "msecs", "relativeCreated", "thread", "threadName",
        "processName", "process", "message", "asctime",
    }

    def format(self, record: logging.LogRecord) -> str:
        payload = {
            "ts": self.formatTime(record, "%Y-%m-%dT%H:%M:%SZ"),
            "level": record.levelname,
            "logger": record.name,
            "event": record.getMessage(),
        }
        for k, v in record.__dict__.items():
            if k in self._RESERVED or k.startswith("_"):
                continue
            payload[k] = _jsonable(v)
        if record.exc_info:
            payload["exc"] = self.formatException(record.exc_info)
        return json.dumps(payload, default=str)


def _jsonable(value):
    if isinstance(value, Decimal):
        return str(value)
    return value


def make_client(settings: Settings) -> ClientProtocol:
    """Indirection point — tests monkeypatch this to inject a FakeHyperliquidClient."""
    from dca.exchange import HyperliquidClient
    return HyperliquidClient(settings)


def _load_settings_or_exit(config_path: Path | None) -> Settings:
    try:
        return Settings.load(config_path=config_path)
    except ValidationError as e:
        typer.echo(f"config error: {e}", err=True)
        raise typer.Exit(code=EX_USAGE)


@app.command()
def buy(
    amount: Decimal = typer.Option(..., "--amount", help="USDC amount to spend on this buy."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Compute but do not submit."),
    config: Optional[Path] = typer.Option(None, "--config", help="Path to TOML config."),
):
    """Place one DCA buy and exit."""
    _configure_logging()
    settings = _load_settings_or_exit(config)
    client = make_client(settings)
    try:
        one_buy(client, settings, amount, dry_run=dry_run)
        raise typer.Exit(code=EX_OK)
    except InsufficientFunds:
        raise typer.Exit(code=EX_TEMPFAIL)
    except SlippageExceeded:
        raise typer.Exit(code=EX_TEMPFAIL)
    except TransientNetworkError as e:
        log.warning("transient_error", extra={"err": str(e)})
        raise typer.Exit(code=EX_TEMPFAIL)
    except AuthError as e:
        log.error("auth_error", extra={"err": str(e)})
        raise typer.Exit(code=EX_NOPERM)
    except typer.Exit:
        raise
    except Exception:
        log.exception("unknown_error")
        raise typer.Exit(code=EX_SOFTWARE)


@app.command()
def run(
    schedule: str = typer.Option(..., "--schedule", help='Cron expression, e.g. "0 9 * * 1".'),
    amount: Decimal = typer.Option(..., "--amount", help="USDC amount per cycle."),
    config: Optional[Path] = typer.Option(None, "--config"),
):
    """Run as a daemon, firing on the cron schedule until SIGINT/SIGTERM."""
    _configure_logging()
    settings = _load_settings_or_exit(config)
    client = make_client(settings)

    stop = Event()

    def _stop(*_):
        log.info("stopping_on_signal")
        stop.set()

    signal.signal(signal.SIGINT, _stop)
    signal.signal(signal.SIGTERM, _stop)

    try:
        scheduler_run(
            client=client,
            settings=settings,
            schedule=schedule,
            amount_usdc=amount,
            stop=stop,
        )
        raise typer.Exit(code=EX_OK)
    except AuthError:
        raise typer.Exit(code=EX_NOPERM)
    except typer.Exit:
        raise
    except Exception:
        log.exception("daemon_crashed")
        raise typer.Exit(code=EX_SOFTWARE)


@app.command()
def balance(
    config: Optional[Path] = typer.Option(None, "--config"),
):
    """Print spot USDC balance and exit."""
    _configure_logging()
    settings = _load_settings_or_exit(config)
    client = make_client(settings)
    try:
        usdc = client.usdc_balance()
    except TransientNetworkError as e:
        typer.echo(f"network error: {e}", err=True)
        raise typer.Exit(code=EX_TEMPFAIL)
    typer.echo(f"USDC: {usdc}")
    raise typer.Exit(code=EX_OK)
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
pytest tests/test_cli.py -v
```

Expected: 7 passed.

- [ ] **Step 5: Run the full unit suite**

```bash
pytest -q
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add dca/cli.py tests/test_cli.py
git commit -m "feat: add typer CLI (buy, run, balance) with sysexits codes"
```

---

## Task 8: Opt-in testnet integration test

Single, manually-run test that hits Hyperliquid testnet.

**Files:**
- Create: `tests/test_integration.py`

- [ ] **Step 1: Write `tests/test_integration.py`**

```python
"""Opt-in integration test against Hyperliquid testnet.

Run with:
    HL_TESTNET_KEY=0x... HL_TESTNET_ACCOUNT=0x... \
        pytest -m testnet tests/test_integration.py -v

The account must hold at least $5 testnet USDC. The test buys $1 of UBTC.
"""
from __future__ import annotations

import os
from decimal import Decimal

import pytest
from pydantic import SecretStr

from dca.config import Settings
from dca.dca import one_buy
from dca.exchange import HyperliquidClient


pytestmark = pytest.mark.testnet


@pytest.fixture(scope="module")
def settings() -> Settings:
    key = os.environ.get("HL_TESTNET_KEY")
    addr = os.environ.get("HL_TESTNET_ACCOUNT")
    if not key or not addr:
        pytest.skip("HL_TESTNET_KEY / HL_TESTNET_ACCOUNT not set")
    return Settings(
        api_wallet_key=SecretStr(key),
        account_address=addr,
        network="testnet",
    )


def test_balance_returns_decimal(settings):
    client = HyperliquidClient(settings)
    bal = client.usdc_balance()
    assert isinstance(bal, Decimal)
    assert bal >= 0


def test_one_dollar_buy_fills(settings):
    client = HyperliquidClient(settings)
    if client.usdc_balance() < Decimal("5"):
        pytest.skip("testnet account needs at least $5 USDC")
    result = one_buy(client, settings, Decimal("1"), dry_run=False)
    assert result.status == "filled"
    assert result.filled_size > 0
```

- [ ] **Step 2: Verify the test is opt-in (skipped by default)**

```bash
pytest -q
```

Expected: previous tests pass; integration tests deselected (the `testnet` marker is not requested).

- [ ] **Step 3: Verify the test runs when explicitly selected (will skip without env vars)**

```bash
pytest -m testnet -v
```

Expected: tests collected and **skipped** with message about missing env vars (because the fixture skips). This proves the wiring works without requiring real credentials.

- [ ] **Step 4: Commit**

```bash
git add tests/test_integration.py
git commit -m "test: add opt-in testnet integration test"
```

---

## Task 9: README polish and dotenv example

Final user-facing documentation.

**Files:**
- Modify: `README.md` (full rewrite)

- [ ] **Step 1: Replace `README.md` with the full version**

````markdown
# dca — BTC DCA on Hyperliquid

Small Python CLI that dollar-cost-averages into Hyperliquid spot BTC. Two run
modes share the same core: a one-shot subcommand for cron / launchd /
systemd-timer, and a long-running daemon with an internal cron scheduler.

## Why this exists

Reliable unattended weekly/daily BTC accumulation, with a security posture
that does not put a withdrawal-capable key on the box running the bot.

## Setup

### 1. Create an API wallet on Hyperliquid

1. Open <https://app.hyperliquid.xyz/api> while signed in with the account
   that holds the funds.
2. Generate a new API wallet ("agent") and approve it for your account.
3. **Save its private key** — that's what the bot will sign with. The agent
   key cannot withdraw, so leaking it caps the blast radius at "places trades
   on the spot wallet you allocated for DCA."

### 2. Install

```bash
git clone <this repo> && cd trading-bot
make install
source .venv/bin/activate
```

### 3. Configure

Copy `.env.example` to `.env` and fill it in, or export the vars directly:

```bash
export HL_API_WALLET_KEY=0x...        # the API wallet key (NOT your main key)
export HL_ACCOUNT_ADDRESS=0x...       # the main account that holds funds
export HL_NETWORK=testnet              # start here; switch to mainnet when happy
```

### 4. Smoke-test

```bash
dca balance                            # prints USDC; confirms the wiring
dca buy --dry-run --amount 1           # full pricing path, no order sent
```

### 5. Real buy on testnet (optional but recommended)

```bash
dca buy --amount 1
```

## Usage

### One-shot (recommended)

Run from cron / launchd / a systemd timer:

```bash
# crontab: every Monday at 09:00 local time, $50 of BTC
0 9 * * 1   /path/to/.venv/bin/dca buy --amount 50 >> ~/.dca/dca.log 2>&1
```

Exit codes (sysexits.h):

| Code | Meaning |
|---|---|
| 0   | Success |
| 64  | Config error (missing/invalid env) — won't fix itself |
| 70  | Bug — investigate |
| 75  | Transient (insufficient balance, slippage, network) — try next cycle |
| 77  | Auth error (API wallet not approved) — won't fix itself |

Cron's MTA will mail you on non-zero. Distinct codes let you pipe through
something smarter.

### Daemon

```bash
dca run --schedule "0 9 * * 1" --amount 50
```

The daemon never exits on transient failure. It exits with code 77 only on
unrecoverable config/auth errors. SIGINT / SIGTERM finish the in-flight buy
and exit 0.

## Logging

Every event is one JSON line on stdout. Pipe to a file in cron, or `jq` it
during development:

```bash
dca buy --amount 1 | jq .
```

## Configuration reference

Env vars (prefix `HL_`):

| Var | Default | Description |
|---|---|---|
| `HL_API_WALLET_KEY` | required | Agent key, no withdraw scope. |
| `HL_ACCOUNT_ADDRESS` | required | Main account holding funds. |
| `HL_NETWORK` | `mainnet` | `mainnet` or `testnet`. |
| `HL_DEFAULT_AMOUNT_USDC` | (none) | Used if `--amount` omitted. |
| `HL_DEFAULT_SCHEDULE` | (none) | Cron expr, used by `run` if `--schedule` omitted. |
| `HL_SLIPPAGE_BPS` | `50` | Max slippage in basis points (50 = 0.5%). |

A TOML config at `--config <path>` provides defaults; env vars override.

## Tests

```bash
make test           # unit tests
make test-net       # integration test (needs HL_TESTNET_KEY / HL_TESTNET_ACCOUNT)
```

## Design

Full design lives at
`docs/superpowers/specs/2026-05-05-btc-dca-hyperliquid-cli-design.md`.
````

- [ ] **Step 2: Verify the project still tests clean**

```bash
pytest -q
```

Expected: all unit tests pass.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: full README with setup, usage, and exit-code reference"
```

---

## Final verification

- [ ] **Step 1: Run the full test suite**

```bash
pytest -v
```

Expected: all unit tests pass; testnet test skipped (no env).

- [ ] **Step 2: Manually exercise the CLI with credentials on testnet**

```bash
export HL_API_WALLET_KEY=0x...
export HL_ACCOUNT_ADDRESS=0x...
export HL_NETWORK=testnet
dca balance
dca buy --dry-run --amount 1
dca buy --amount 1
```

Expected: `balance` prints; dry-run prints intent; live buy returns a fill.

- [ ] **Step 3: Verify all spec sections are implemented**

Cross-reference `docs/superpowers/specs/2026-05-05-btc-dca-hyperliquid-cli-design.md`:
- Spot venue: `_BTC_SPOT_PAIR = "UBTC/USDC"` ✓
- Both run modes: `dca buy` (Task 7) and `dca run` (Task 7 + Task 5) ✓
- Fixed USDC sizing: `--amount` is `Decimal` ✓
- IOC with slippage cap: `dca.one_buy` computes `mid * (1 + bps/10_000)` ✓
- API wallet auth: `Settings.api_wallet_key` is `SecretStr` ✓
- Stateless: no DB, no on-disk ledger ✓
- Decimal end-to-end: enforced in tests ✓
- Local-tz cron + UTC log timestamps: `WallClock.now_local()` + `_JsonLineFormatter` formatTime UTC ✓

---

## Notes for the implementing engineer

- **Decimal hygiene.** Never let a money value become a `float` outside of the SDK boundary (`float(size)` in `place_btc_buy`). Tests use `==` on `Decimal`; do not change them to `pytest.approx`.
- **No retries inside a cycle.** If the API rejects an order, log and surface — the next cycle is the retry.
- **Don't broaden `ClientProtocol`.** Adding methods requires updating the fake. Keep the surface small.
- **SDK drift.** If `hyperliquid-python-sdk` renames a method, fix the call site in `dca/exchange.py` only. The protocol must not change.
- **One commit per task.** Don't squash. Each task's diff should be reviewable on its own.
