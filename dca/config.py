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
