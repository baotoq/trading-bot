use crate::cli::{Cli, CliError, Network};
use std::env;
use thiserror::Error;

const HL_PRIVATE_KEY_ENV: &str = "HL_PRIVATE_KEY";
const KEY_HEX_LEN: usize = 64;

pub struct Config {
    pub network: Network,
    pub asset: String,
    pub usd_amount: f64,
    pub max_spend_usd: f64,
    pub dry_run: bool,
    pub private_key: String,
}

impl std::fmt::Debug for Config {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Config")
            .field("network", &self.network)
            .field("asset", &self.asset)
            .field("usd_amount", &self.usd_amount)
            .field("max_spend_usd", &self.max_spend_usd)
            .field("dry_run", &self.dry_run)
            .field("private_key", &"<redacted>")
            .finish()
    }
}

#[derive(Debug, Error, PartialEq, Eq)]
pub enum ConfigError {
    #[error("{0}")]
    Cli(#[from] CliError),
    #[error("missing_key")]
    MissingKey,
    #[error("invalid_key")]
    InvalidKey,
}

pub fn from_cli(cli: Cli) -> Result<Config, ConfigError> {
    let raw_key = env::var(HL_PRIVATE_KEY_ENV).map_err(|_| ConfigError::MissingKey)?;
    assemble(cli, raw_key)
}

fn assemble(cli: Cli, raw_key: String) -> Result<Config, ConfigError> {
    let network = cli.resolve_network()?;
    cli.validate_asset()?;
    validate_key_format(&raw_key)?;
    Ok(Config {
        network,
        asset: cli.asset,
        usd_amount: cli.usd,
        max_spend_usd: cli.max_spend_usd,
        dry_run: cli.dry_run,
        private_key: raw_key,
    })
}

fn validate_key_format(s: &str) -> Result<(), ConfigError> {
    let body = s.strip_prefix("0x").unwrap_or(s);
    if body.len() != KEY_HEX_LEN {
        return Err(ConfigError::InvalidKey);
    }
    if !body.chars().all(|c| c.is_ascii_hexdigit()) {
        return Err(ConfigError::InvalidKey);
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use clap::Parser;

    const VALID_KEY_HEX: &str = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    fn cli_from(args: &[&str]) -> Cli {
        let full: Vec<&str> = std::iter::once("hl-dca")
            .chain(args.iter().copied())
            .collect();
        Cli::try_parse_from(full).expect("test args should parse")
    }

    #[test]
    fn validate_key_accepts_lowercase_hex() {
        assert_eq!(validate_key_format(VALID_KEY_HEX), Ok(()));
    }

    #[test]
    fn validate_key_accepts_uppercase_hex() {
        assert_eq!(validate_key_format(&VALID_KEY_HEX.to_uppercase()), Ok(()));
    }

    #[test]
    fn validate_key_accepts_0x_prefix() {
        let with_prefix = format!("0x{VALID_KEY_HEX}");
        assert_eq!(validate_key_format(&with_prefix), Ok(()));
    }

    #[test]
    fn validate_key_rejects_too_short() {
        assert_eq!(validate_key_format("abc"), Err(ConfigError::InvalidKey));
    }

    #[test]
    fn validate_key_rejects_too_long() {
        let too_long = format!("{VALID_KEY_HEX}aa");
        assert_eq!(validate_key_format(&too_long), Err(ConfigError::InvalidKey));
    }

    #[test]
    fn validate_key_rejects_non_hex_chars() {
        let nonhex = "z".repeat(64);
        assert_eq!(validate_key_format(&nonhex), Err(ConfigError::InvalidKey));
    }

    #[test]
    fn validate_key_rejects_empty() {
        assert_eq!(validate_key_format(""), Err(ConfigError::InvalidKey));
    }

    #[test]
    fn debug_redacts_private_key() {
        let cfg = Config {
            network: Network::Testnet,
            asset: "UBTC".into(),
            usd_amount: 10.0,
            max_spend_usd: 25.0,
            dry_run: false,
            private_key: "supersecretdeadbeef".into(),
        };
        let printed = format!("{cfg:?}");
        assert!(
            !printed.contains("supersecretdeadbeef"),
            "leaked: {printed}"
        );
        assert!(printed.contains("<redacted>"), "no marker: {printed}");
    }

    #[test]
    fn assemble_happy_path_testnet() {
        let cli = cli_from(&["--usd", "10", "--max-spend-usd", "25", "--testnet"]);
        let cfg = assemble(cli, VALID_KEY_HEX.to_string()).unwrap();
        assert_eq!(cfg.network, Network::Testnet);
        assert_eq!(cfg.asset, "UBTC");
        assert_eq!(cfg.usd_amount, 10.0);
        assert_eq!(cfg.max_spend_usd, 25.0);
        assert!(!cfg.dry_run);
        assert_eq!(cfg.private_key, VALID_KEY_HEX);
    }

    #[test]
    fn assemble_resolves_mainnet() {
        let cli = cli_from(&["--usd", "5", "--max-spend-usd", "5", "--mainnet"]);
        let cfg = assemble(cli, VALID_KEY_HEX.to_string()).unwrap();
        assert_eq!(cfg.network, Network::Mainnet);
    }

    #[test]
    fn assemble_dry_run_flag_propagates() {
        let cli = cli_from(&[
            "--usd",
            "10",
            "--max-spend-usd",
            "25",
            "--testnet",
            "--dry-run",
        ]);
        let cfg = assemble(cli, VALID_KEY_HEX.to_string()).unwrap();
        assert!(cfg.dry_run);
    }

    #[test]
    fn assemble_propagates_network_required() {
        let cli = cli_from(&["--usd", "10", "--max-spend-usd", "10"]);
        assert_eq!(
            assemble(cli, VALID_KEY_HEX.to_string()).unwrap_err(),
            ConfigError::Cli(CliError::NetworkRequired)
        );
    }

    #[test]
    fn assemble_propagates_network_conflict() {
        let cli = cli_from(&[
            "--usd",
            "10",
            "--max-spend-usd",
            "10",
            "--mainnet",
            "--testnet",
        ]);
        assert_eq!(
            assemble(cli, VALID_KEY_HEX.to_string()).unwrap_err(),
            ConfigError::Cli(CliError::NetworkConflict)
        );
    }

    #[test]
    fn assemble_propagates_unsupported_asset() {
        let cli = cli_from(&[
            "--usd",
            "10",
            "--max-spend-usd",
            "10",
            "--testnet",
            "--asset",
            "ETH",
        ]);
        assert_eq!(
            assemble(cli, VALID_KEY_HEX.to_string()).unwrap_err(),
            ConfigError::Cli(CliError::UnsupportedAsset("ETH".into()))
        );
    }

    #[test]
    fn assemble_rejects_invalid_key() {
        let cli = cli_from(&["--usd", "10", "--max-spend-usd", "10", "--testnet"]);
        assert_eq!(
            assemble(cli, "not-a-key".to_string()).unwrap_err(),
            ConfigError::InvalidKey
        );
    }
}
