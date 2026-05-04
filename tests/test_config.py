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
