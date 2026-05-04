use clap::Parser;
use thiserror::Error;

#[derive(Parser, Debug)]
#[command(name = "hl-dca", about = "One-shot Hyperliquid spot DCA buy")]
pub struct Cli {
    /// USD amount to spend on this buy.
    #[arg(long, value_parser = positive_f64)]
    pub usd: f64,

    /// Hard per-run cap; the buy aborts if --usd exceeds this.
    #[arg(long, value_parser = positive_f64)]
    pub max_spend_usd: f64,

    /// Spot asset symbol. Only UBTC is supported in v1.
    #[arg(long, default_value = "UBTC")]
    pub asset: String,

    /// Target Hyperliquid mainnet (real money).
    #[arg(long)]
    pub mainnet: bool,

    /// Target Hyperliquid testnet.
    #[arg(long)]
    pub testnet: bool,

    /// Print the intended order without sending it.
    #[arg(long)]
    pub dry_run: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Network {
    Mainnet,
    Testnet,
}

#[derive(Debug, Error, PartialEq, Eq)]
pub enum CliError {
    #[error("network_required")]
    NetworkRequired,
    #[error("network_conflict")]
    NetworkConflict,
    #[error("unsupported_asset")]
    UnsupportedAsset(String),
}

fn positive_f64(s: &str) -> Result<f64, String> {
    let v: f64 = s
        .parse()
        .map_err(|e: std::num::ParseFloatError| e.to_string())?;
    if v.is_finite() && v > 0.0 {
        Ok(v)
    } else {
        Err(format!("must be a positive finite number, got {s}"))
    }
}

impl Cli {
    pub fn resolve_network(&self) -> Result<Network, CliError> {
        match (self.mainnet, self.testnet) {
            (false, false) => Err(CliError::NetworkRequired),
            (true, true) => Err(CliError::NetworkConflict),
            (true, false) => Ok(Network::Mainnet),
            (false, true) => Ok(Network::Testnet),
        }
    }

    pub fn validate_asset(&self) -> Result<(), CliError> {
        if self.asset == "UBTC" {
            Ok(())
        } else {
            Err(CliError::UnsupportedAsset(self.asset.clone()))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn parse(extra: &[&str]) -> Result<Cli, clap::Error> {
        let full: Vec<&str> = std::iter::once("hl-dca")
            .chain(extra.iter().copied())
            .collect();
        Cli::try_parse_from(full)
    }

    #[test]
    fn positive_f64_accepts_positive() {
        assert_eq!(positive_f64("1.5"), Ok(1.5));
        assert_eq!(positive_f64("100"), Ok(100.0));
    }

    #[test]
    fn positive_f64_rejects_zero() {
        assert!(positive_f64("0").is_err());
    }

    #[test]
    fn positive_f64_rejects_negative() {
        assert!(positive_f64("-1").is_err());
    }

    #[test]
    fn positive_f64_rejects_non_finite() {
        assert!(positive_f64("nan").is_err());
        assert!(positive_f64("inf").is_err());
    }

    #[test]
    fn positive_f64_rejects_garbage() {
        assert!(positive_f64("abc").is_err());
    }

    #[test]
    fn resolve_network_neither_errors() {
        let cli = parse(&["--usd", "10", "--max-spend-usd", "10"]).unwrap();
        assert_eq!(cli.resolve_network(), Err(CliError::NetworkRequired));
    }

    #[test]
    fn resolve_network_both_errors() {
        let cli = parse(&[
            "--usd",
            "10",
            "--max-spend-usd",
            "10",
            "--mainnet",
            "--testnet",
        ])
        .unwrap();
        assert_eq!(cli.resolve_network(), Err(CliError::NetworkConflict));
    }

    #[test]
    fn resolve_network_mainnet_only() {
        let cli = parse(&["--usd", "10", "--max-spend-usd", "10", "--mainnet"]).unwrap();
        assert_eq!(cli.resolve_network(), Ok(Network::Mainnet));
    }

    #[test]
    fn resolve_network_testnet_only() {
        let cli = parse(&["--usd", "10", "--max-spend-usd", "10", "--testnet"]).unwrap();
        assert_eq!(cli.resolve_network(), Ok(Network::Testnet));
    }

    #[test]
    fn validate_asset_default_is_ubtc() {
        let cli = parse(&["--usd", "10", "--max-spend-usd", "10", "--testnet"]).unwrap();
        assert_eq!(cli.asset, "UBTC");
        assert_eq!(cli.validate_asset(), Ok(()));
    }

    #[test]
    fn validate_asset_rejects_other() {
        let cli = parse(&[
            "--usd",
            "10",
            "--max-spend-usd",
            "10",
            "--testnet",
            "--asset",
            "ETH",
        ])
        .unwrap();
        assert_eq!(
            cli.validate_asset(),
            Err(CliError::UnsupportedAsset("ETH".into()))
        );
    }

    #[test]
    fn clap_rejects_zero_usd() {
        assert!(parse(&["--usd", "0", "--max-spend-usd", "10", "--testnet"]).is_err());
    }

    #[test]
    fn clap_rejects_negative_usd() {
        assert!(parse(&["--usd", "-5", "--max-spend-usd", "10", "--testnet"]).is_err());
    }

    #[test]
    fn clap_rejects_missing_usd() {
        assert!(parse(&["--max-spend-usd", "10", "--testnet"]).is_err());
    }

    #[test]
    fn clap_rejects_missing_max_spend() {
        assert!(parse(&["--usd", "10", "--testnet"]).is_err());
    }

    #[test]
    fn dry_run_flag_parses() {
        let cli = parse(&[
            "--usd",
            "10",
            "--max-spend-usd",
            "10",
            "--testnet",
            "--dry-run",
        ])
        .unwrap();
        assert!(cli.dry_run);
    }
}
