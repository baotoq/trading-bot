from __future__ import annotations

import json
import logging
import signal
import sys
import time
from decimal import Decimal, InvalidOperation
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


def _parse_decimal_or_exit(raw: str, *, field: str) -> Decimal:
    try:
        return Decimal(raw)
    except (InvalidOperation, ValueError):
        typer.echo(f"invalid {field}: {raw!r} is not a number", err=True)
        raise typer.Exit(code=EX_USAGE)


@app.command()
def buy(
    amount: str = typer.Option(..., "--amount", help="USDC amount to spend on this buy."),
    dry_run: bool = typer.Option(False, "--dry-run", help="Compute but do not submit."),
    config: Optional[Path] = typer.Option(None, "--config", help="Path to TOML config."),
):
    """Place one DCA buy and exit."""
    _configure_logging()
    settings = _load_settings_or_exit(config)
    amount_dec = _parse_decimal_or_exit(amount, field="--amount")
    client = make_client(settings)
    try:
        one_buy(client, settings, amount_dec, dry_run=dry_run)
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
    amount: str = typer.Option(..., "--amount", help="USDC amount per cycle."),
    config: Optional[Path] = typer.Option(None, "--config"),
):
    """Run as a daemon, firing on the cron schedule until SIGINT/SIGTERM."""
    _configure_logging()
    settings = _load_settings_or_exit(config)
    amount_dec = _parse_decimal_or_exit(amount, field="--amount")
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
            amount_usdc=amount_dec,
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
