from decimal import Decimal

from typer.testing import CliRunner

from dca import cli
from dca._fake_client import FakeHyperliquidClient


runner = CliRunner()


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
