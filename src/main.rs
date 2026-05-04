mod cli;
mod config;
mod exchange;
mod risk;

use crate::cli::{CliError, Network};
use crate::config::{Config, ConfigError};
use crate::exchange::{ExchangeError, Fill};
use crate::risk::RiskError;
use clap::Parser;
use std::process::ExitCode;

#[tokio::main(flavor = "current_thread")]
async fn main() -> ExitCode {
    match run().await {
        Ok(()) => ExitCode::SUCCESS,
        Err(line) => {
            eprintln!("{line}");
            ExitCode::from(1)
        }
    }
}

async fn run() -> Result<(), String> {
    let args = cli::Cli::parse();
    let cfg = config::from_cli(args).map_err(format_config_error)?;
    risk::check(&cfg).map_err(format_risk_error)?;

    if cfg.dry_run {
        println!("{}", format_dry_run_line(&cfg));
        return Ok(());
    }

    let fill = exchange::place_market_buy(&cfg)
        .await
        .map_err(format_exchange_error)?;
    println!("{}", format_fill_line(&fill));
    Ok(())
}

fn network_str(n: Network) -> &'static str {
    match n {
        Network::Mainnet => "mainnet",
        Network::Testnet => "testnet",
    }
}

fn format_dry_run_line(cfg: &Config) -> String {
    format!(
        "dry_run=true network={} asset={} usd={}",
        network_str(cfg.network),
        cfg.asset,
        cfg.usd_amount,
    )
}

fn format_fill_line(fill: &Fill) -> String {
    format!(
        "ts={} network={} asset={} usd={} qty={} avg_px={} fee={} oid={}",
        fill.ts.to_rfc3339_opts(chrono::SecondsFormat::Secs, true),
        network_str(fill.network),
        fill.asset,
        fill.usd,
        fill.qty,
        fill.avg_px,
        fill.fee,
        fill.oid,
    )
}

fn format_kind(kind: &str, detail: &str) -> String {
    let escaped = detail.replace('"', "\\\"");
    format!("error={kind} detail=\"{escaped}\"")
}

fn format_cli_error(e: CliError) -> String {
    match e {
        CliError::NetworkRequired => format_kind(
            "network_required",
            "pass exactly one of --mainnet or --testnet",
        ),
        CliError::NetworkConflict => format_kind(
            "network_conflict",
            "--mainnet and --testnet are mutually exclusive",
        ),
        CliError::UnsupportedAsset(name) => format_kind(
            "unsupported_asset",
            &format!("v1 only supports UBTC, got {name}"),
        ),
    }
}

fn format_config_error(e: ConfigError) -> String {
    match e {
        ConfigError::Cli(c) => format_cli_error(c),
        ConfigError::MissingKey => format_kind("missing_key", "HL_PRIVATE_KEY env var not set"),
        ConfigError::InvalidKey => format_kind(
            "invalid_key",
            "HL_PRIVATE_KEY must be 64 hex chars (optional 0x prefix)",
        ),
    }
}

fn format_risk_error(e: RiskError) -> String {
    match e {
        RiskError::CapExceeded { requested, cap } => format_kind(
            "risk_cap_exceeded",
            &format!("requested {requested} exceeds cap {cap}"),
        ),
    }
}

fn format_exchange_error(e: ExchangeError) -> String {
    match e {
        ExchangeError::Ambiguous(s) => {
            let detail = s.replace('"', "\\\"");
            format!("error=ambiguous status=unknown order_send_state=ambiguous detail=\"{detail}\"")
        }
        ExchangeError::InvalidKey => format_kind("invalid_key", "private key rejected by signer"),
        ExchangeError::Network(s) => format_kind("network", &s),
        ExchangeError::AssetNotFound => {
            format_kind("asset_not_found", "spot pair not in metadata or mid feed")
        }
        ExchangeError::BadMarketData(s) => format_kind("bad_market_data", &s),
        ExchangeError::OrderRejected(s) => format_kind("order_rejected", &s),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use chrono::TimeZone;

    fn cfg() -> Config {
        Config {
            network: Network::Testnet,
            asset: "UBTC".into(),
            usd_amount: 25.0,
            max_spend_usd: 25.0,
            dry_run: true,
            private_key: "deadbeef".into(),
        }
    }

    fn fill() -> Fill {
        Fill {
            asset: "UBTC/USDC".into(),
            usd: 25.0,
            qty: 0.000389,
            avg_px: 64254.1,
            fee: 0.0,
            oid: 12345,
            network: Network::Testnet,
            ts: chrono::Utc.with_ymd_and_hms(2026, 5, 4, 12, 0, 0).unwrap(),
        }
    }

    #[test]
    fn network_str_lowercases() {
        assert_eq!(network_str(Network::Mainnet), "mainnet");
        assert_eq!(network_str(Network::Testnet), "testnet");
    }

    #[test]
    fn format_kind_basic() {
        assert_eq!(
            format_kind("missing_key", "boom"),
            r#"error=missing_key detail="boom""#
        );
    }

    #[test]
    fn format_kind_escapes_quotes() {
        assert_eq!(
            format_kind("x", r#"a "b" c"#),
            r#"error=x detail="a \"b\" c""#
        );
    }

    #[test]
    fn format_dry_run_line_shape() {
        assert_eq!(
            format_dry_run_line(&cfg()),
            "dry_run=true network=testnet asset=UBTC usd=25"
        );
    }

    #[test]
    fn format_fill_line_z_timestamp_and_all_fields() {
        let line = format_fill_line(&fill());
        assert!(
            line.starts_with("ts=2026-05-04T12:00:00Z "),
            "no Z timestamp: {line}"
        );
        for needle in [
            "network=testnet",
            "asset=UBTC/USDC",
            "usd=25",
            "qty=0.000389",
            "avg_px=64254.1",
            "fee=0",
            "oid=12345",
        ] {
            assert!(line.contains(needle), "missing {needle}: {line}");
        }
    }

    #[test]
    fn format_cli_each_variant() {
        assert!(format_cli_error(CliError::NetworkRequired).starts_with("error=network_required "));
        assert!(format_cli_error(CliError::NetworkConflict).starts_with("error=network_conflict "));
        let line = format_cli_error(CliError::UnsupportedAsset("ETH".into()));
        assert!(line.starts_with("error=unsupported_asset "));
        assert!(line.contains("ETH"));
    }

    #[test]
    fn format_config_missing_and_invalid_key() {
        assert!(format_config_error(ConfigError::MissingKey).starts_with("error=missing_key "));
        assert!(format_config_error(ConfigError::InvalidKey).starts_with("error=invalid_key "));
    }

    #[test]
    fn format_config_cli_passthrough() {
        assert!(
            format_config_error(ConfigError::Cli(CliError::NetworkRequired))
                .starts_with("error=network_required ")
        );
    }

    #[test]
    fn format_risk_cap_exceeded_includes_numbers() {
        let line = format_risk_error(RiskError::CapExceeded {
            requested: 100.0,
            cap: 25.0,
        });
        assert!(line.starts_with("error=risk_cap_exceeded "));
        assert!(line.contains("100"));
        assert!(line.contains("25"));
    }

    #[test]
    fn format_exchange_ambiguous_marks_unknown() {
        let line = format_exchange_error(ExchangeError::Ambiguous("timeout".into()));
        assert!(line.contains("error=ambiguous"));
        assert!(line.contains("status=unknown"));
        assert!(line.contains("order_send_state=ambiguous"));
        assert!(line.contains(r#"detail="timeout""#));
    }

    #[test]
    fn format_exchange_other_variants() {
        assert!(format_exchange_error(ExchangeError::InvalidKey).starts_with("error=invalid_key "));
        assert!(format_exchange_error(ExchangeError::AssetNotFound)
            .starts_with("error=asset_not_found "));
        let line = format_exchange_error(ExchangeError::OrderRejected("insufficient".into()));
        assert!(line.starts_with("error=order_rejected "));
        assert!(line.contains("insufficient"));
    }
}
