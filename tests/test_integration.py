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
