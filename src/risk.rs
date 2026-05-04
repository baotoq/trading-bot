use crate::config::Config;
use thiserror::Error;

#[derive(Debug, Error, PartialEq)]
pub enum RiskError {
    #[error("risk_cap_exceeded")]
    CapExceeded { requested: f64, cap: f64 },
}

pub fn check(cfg: &Config) -> Result<(), RiskError> {
    if cfg.usd_amount > cfg.max_spend_usd {
        Err(RiskError::CapExceeded {
            requested: cfg.usd_amount,
            cap: cfg.max_spend_usd,
        })
    } else {
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::cli::Network;

    fn cfg(usd: f64, cap: f64) -> Config {
        Config {
            network: Network::Testnet,
            asset: "UBTC".into(),
            usd_amount: usd,
            max_spend_usd: cap,
            dry_run: false,
            private_key: "deadbeef".into(),
        }
    }

    #[test]
    fn under_cap_passes() {
        assert_eq!(check(&cfg(10.0, 25.0)), Ok(()));
    }

    #[test]
    fn at_cap_passes() {
        assert_eq!(check(&cfg(25.0, 25.0)), Ok(()));
    }

    #[test]
    fn over_cap_rejects() {
        assert_eq!(
            check(&cfg(30.0, 25.0)),
            Err(RiskError::CapExceeded {
                requested: 30.0,
                cap: 25.0,
            })
        );
    }
}
