use crate::cli::Network;
use crate::config::Config;
use chrono::{DateTime, Utc};
use ethers::signers::LocalWallet;
use hyperliquid_rust_sdk::{
    BaseUrl, ClientLimit, ClientOrder, ClientOrderRequest, ExchangeClient, ExchangeDataStatus,
    ExchangeResponseStatus, InfoClient,
};
use std::collections::HashMap;
use std::time::Duration;
use thiserror::Error;
use tokio::time::timeout;

const SLIPPAGE: f64 = 0.005;
const HTTP_TIMEOUT: Duration = Duration::from_secs(10);
const QUOTE_TOKEN: &str = "USDC";

#[derive(Debug)]
pub struct Fill {
    pub asset: String,
    pub usd: f64,
    pub qty: f64,
    pub avg_px: f64,
    pub fee: f64,
    pub oid: u64,
    pub network: Network,
    pub ts: DateTime<Utc>,
}

#[derive(Debug, Error)]
pub enum ExchangeError {
    #[error("invalid_key")]
    InvalidKey,
    #[error("network")]
    Network(String),
    #[error("asset_not_found")]
    AssetNotFound,
    #[error("bad_market_data")]
    BadMarketData(String),
    #[error("order_rejected")]
    OrderRejected(String),
    #[error("ambiguous")]
    Ambiguous(String),
}

#[derive(Debug, Clone, PartialEq)]
struct PairInfo {
    canonical_name: String,
    pretty_name: String,
    sz_decimals: u32,
}

#[derive(Debug, Clone)]
struct PairEntry {
    tokens: [usize; 2],
    name: String,
}

#[derive(Debug, Clone)]
struct TokenEntry {
    name: String,
    index: usize,
    sz_decimals: u32,
}

pub async fn place_market_buy(cfg: &Config) -> Result<Fill, ExchangeError> {
    let base_url = match cfg.network {
        Network::Mainnet => BaseUrl::Mainnet,
        Network::Testnet => BaseUrl::Testnet,
    };

    let wallet: LocalWallet = cfg
        .private_key
        .parse()
        .map_err(|_| ExchangeError::InvalidKey)?;

    let info_client =
        run_with_timeout(InfoClient::new(None, Some(base_url)), "info_client").await?;
    let exchange_client = run_with_timeout(
        ExchangeClient::new(None, wallet, Some(base_url), None, None),
        "exchange_client",
    )
    .await?;

    let spot_meta = run_with_timeout(info_client.spot_meta(), "spot_meta").await?;

    let pairs: Vec<PairEntry> = spot_meta
        .universe
        .iter()
        .map(|p| PairEntry {
            tokens: p.tokens,
            name: p.name.clone(),
        })
        .collect();
    let tokens: Vec<TokenEntry> = spot_meta
        .tokens
        .iter()
        .map(|t| TokenEntry {
            name: t.name.clone(),
            index: t.index,
            sz_decimals: u32::from(t.sz_decimals),
        })
        .collect();

    let pair_info = find_pair(&pairs, &tokens, &cfg.asset)?;

    let all_mids = run_with_timeout(info_client.all_mids(), "all_mids").await?;
    let mid = lookup_mid(&all_mids, &pair_info)?;

    let qty = round_to_decimals(cfg.usd_amount / mid, pair_info.sz_decimals);
    let price_decimals = 8u32.saturating_sub(pair_info.sz_decimals);
    let limit_px = round_to_decimals(mid * (1.0 + SLIPPAGE), price_decimals);

    let order = ClientOrderRequest {
        asset: pair_info.pretty_name.clone(),
        is_buy: true,
        reduce_only: false,
        limit_px,
        sz: qty,
        cloid: None,
        order_type: ClientOrder::Limit(ClientLimit {
            tif: "Ioc".to_string(),
        }),
    };

    let response = run_with_timeout(exchange_client.order(order, None), "order").await?;
    parse_fill(response, cfg, pair_info.pretty_name)
}

async fn run_with_timeout<F, T, E>(fut: F, label: &str) -> Result<T, ExchangeError>
where
    F: std::future::Future<Output = Result<T, E>>,
    E: std::fmt::Display,
{
    match timeout(HTTP_TIMEOUT, fut).await {
        Err(_) => Err(ExchangeError::Ambiguous(format!("{label}: timed out"))),
        Ok(Err(e)) => Err(ExchangeError::Network(format!("{label}: {e}"))),
        Ok(Ok(v)) => Ok(v),
    }
}

fn find_pair(
    pairs: &[PairEntry],
    tokens: &[TokenEntry],
    base: &str,
) -> Result<PairInfo, ExchangeError> {
    let by_name: HashMap<&str, &TokenEntry> = tokens.iter().map(|t| (t.name.as_str(), t)).collect();
    let base_tok = by_name.get(base).ok_or(ExchangeError::AssetNotFound)?;
    let quote_tok = by_name
        .get(QUOTE_TOKEN)
        .ok_or(ExchangeError::AssetNotFound)?;
    let pair = pairs
        .iter()
        .find(|p| p.tokens == [base_tok.index, quote_tok.index])
        .ok_or(ExchangeError::AssetNotFound)?;
    Ok(PairInfo {
        canonical_name: pair.name.clone(),
        pretty_name: format!("{}/{}", base_tok.name, quote_tok.name),
        sz_decimals: base_tok.sz_decimals,
    })
}

fn lookup_mid(all_mids: &HashMap<String, String>, pair: &PairInfo) -> Result<f64, ExchangeError> {
    let mid_str = all_mids
        .get(&pair.canonical_name)
        .or_else(|| all_mids.get(&pair.pretty_name))
        .ok_or(ExchangeError::AssetNotFound)?;
    let mid: f64 = mid_str
        .parse()
        .map_err(|_| ExchangeError::BadMarketData(format!("non-numeric mid '{mid_str}'")))?;
    if !mid.is_finite() || mid <= 0.0 {
        return Err(ExchangeError::BadMarketData(format!(
            "non-positive mid {mid}"
        )));
    }
    Ok(mid)
}

fn round_to_decimals(v: f64, decimals: u32) -> f64 {
    let factor = 10f64.powi(decimals as i32);
    (v * factor).round() / factor
}

fn parse_fill(
    response: ExchangeResponseStatus,
    cfg: &Config,
    pair_pretty: String,
) -> Result<Fill, ExchangeError> {
    let resp = match response {
        ExchangeResponseStatus::Ok(r) => r,
        ExchangeResponseStatus::Err(s) => return Err(ExchangeError::OrderRejected(s)),
    };
    let data = resp
        .data
        .ok_or_else(|| ExchangeError::Ambiguous("response missing data".into()))?;
    let status = data
        .statuses
        .into_iter()
        .next()
        .ok_or_else(|| ExchangeError::Ambiguous("response missing statuses".into()))?;
    match status {
        ExchangeDataStatus::Filled(f) => {
            let qty: f64 = f
                .total_sz
                .parse()
                .map_err(|_| ExchangeError::BadMarketData(format!("bad qty '{}'", f.total_sz)))?;
            let avg_px: f64 = f
                .avg_px
                .parse()
                .map_err(|_| ExchangeError::BadMarketData(format!("bad avg_px '{}'", f.avg_px)))?;
            Ok(Fill {
                asset: pair_pretty,
                usd: cfg.usd_amount,
                qty,
                avg_px,
                fee: 0.0,
                oid: f.oid,
                network: cfg.network,
                ts: Utc::now(),
            })
        }
        ExchangeDataStatus::Error(s) => Err(ExchangeError::OrderRejected(s)),
        ExchangeDataStatus::Resting(_) => Err(ExchangeError::Ambiguous(
            "IOC order resting (unexpected)".into(),
        )),
        other => Err(ExchangeError::Ambiguous(format!(
            "unexpected status: {other:?}"
        ))),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use hyperliquid_rust_sdk::{ExchangeDataStatuses, ExchangeResponse, FilledOrder, RestingOrder};

    fn cfg(usd: f64) -> Config {
        Config {
            network: Network::Testnet,
            asset: "UBTC".into(),
            usd_amount: usd,
            max_spend_usd: usd,
            dry_run: false,
            private_key: "deadbeef".into(),
        }
    }

    fn token(name: &str, index: usize, sz_decimals: u32) -> TokenEntry {
        TokenEntry {
            name: name.into(),
            index,
            sz_decimals,
        }
    }

    fn pair(tokens: [usize; 2], name: &str) -> PairEntry {
        PairEntry {
            tokens,
            name: name.into(),
        }
    }

    // ---- round_to_decimals ----

    #[test]
    fn round_5_decimals() {
        assert_eq!(round_to_decimals(0.0003892345, 5), 0.00039);
    }

    #[test]
    fn round_0_decimals() {
        assert_eq!(round_to_decimals(1.6, 0), 2.0);
    }

    #[test]
    fn round_zero_input() {
        assert_eq!(round_to_decimals(0.0, 5), 0.0);
    }

    // ---- find_pair ----

    #[test]
    fn find_pair_locates_ubtc_usdc() {
        let tokens = vec![token("USDC", 0, 8), token("UBTC", 1, 5)];
        let pairs = vec![pair([1, 0], "@107")];
        let info = find_pair(&pairs, &tokens, "UBTC").unwrap();
        assert_eq!(info.canonical_name, "@107");
        assert_eq!(info.pretty_name, "UBTC/USDC");
        assert_eq!(info.sz_decimals, 5);
    }

    #[test]
    fn find_pair_unknown_base() {
        let tokens = vec![token("USDC", 0, 8)];
        let pairs: Vec<PairEntry> = vec![];
        assert!(matches!(
            find_pair(&pairs, &tokens, "UBTC"),
            Err(ExchangeError::AssetNotFound)
        ));
    }

    #[test]
    fn find_pair_missing_quote() {
        let tokens = vec![token("UBTC", 1, 5)];
        let pairs: Vec<PairEntry> = vec![];
        assert!(matches!(
            find_pair(&pairs, &tokens, "UBTC"),
            Err(ExchangeError::AssetNotFound)
        ));
    }

    #[test]
    fn find_pair_no_universe_entry() {
        let tokens = vec![token("USDC", 0, 8), token("UBTC", 1, 5)];
        let pairs = vec![pair([2, 0], "@108")];
        assert!(matches!(
            find_pair(&pairs, &tokens, "UBTC"),
            Err(ExchangeError::AssetNotFound)
        ));
    }

    // ---- lookup_mid ----

    fn pi() -> PairInfo {
        PairInfo {
            canonical_name: "@107".into(),
            pretty_name: "UBTC/USDC".into(),
            sz_decimals: 5,
        }
    }

    #[test]
    fn lookup_mid_uses_canonical_name() {
        let mut mids = HashMap::new();
        mids.insert("@107".into(), "64000.0".into());
        assert_eq!(lookup_mid(&mids, &pi()).unwrap(), 64000.0);
    }

    #[test]
    fn lookup_mid_falls_back_to_pretty_name() {
        let mut mids = HashMap::new();
        mids.insert("UBTC/USDC".into(), "64000.0".into());
        assert_eq!(lookup_mid(&mids, &pi()).unwrap(), 64000.0);
    }

    #[test]
    fn lookup_mid_missing_errors() {
        let mids = HashMap::new();
        assert!(matches!(
            lookup_mid(&mids, &pi()),
            Err(ExchangeError::AssetNotFound)
        ));
    }

    #[test]
    fn lookup_mid_non_numeric_errors() {
        let mut mids = HashMap::new();
        mids.insert("@107".into(), "not-a-number".into());
        assert!(matches!(
            lookup_mid(&mids, &pi()),
            Err(ExchangeError::BadMarketData(_))
        ));
    }

    #[test]
    fn lookup_mid_negative_errors() {
        let mut mids = HashMap::new();
        mids.insert("@107".into(), "-1.0".into());
        assert!(matches!(
            lookup_mid(&mids, &pi()),
            Err(ExchangeError::BadMarketData(_))
        ));
    }

    // ---- parse_fill ----

    fn ok_response(statuses: Vec<ExchangeDataStatus>) -> ExchangeResponseStatus {
        ExchangeResponseStatus::Ok(ExchangeResponse {
            response_type: "order".into(),
            data: Some(ExchangeDataStatuses { statuses }),
        })
    }

    #[test]
    fn parse_fill_extracts_filled() {
        let resp = ok_response(vec![ExchangeDataStatus::Filled(FilledOrder {
            total_sz: "0.000389".into(),
            avg_px: "64254.10".into(),
            oid: 12345,
        })]);
        let fill = parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()).unwrap();
        assert_eq!(fill.asset, "UBTC/USDC");
        assert_eq!(fill.usd, 25.0);
        assert_eq!(fill.qty, 0.000389);
        assert_eq!(fill.avg_px, 64254.10);
        assert_eq!(fill.oid, 12345);
        assert_eq!(fill.fee, 0.0);
        assert_eq!(fill.network, Network::Testnet);
    }

    #[test]
    fn parse_fill_outer_err_is_order_rejected() {
        let resp = ExchangeResponseStatus::Err("insufficient_balance".into());
        assert!(matches!(
            parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()),
            Err(ExchangeError::OrderRejected(_))
        ));
    }

    #[test]
    fn parse_fill_inner_error_is_order_rejected() {
        let resp = ok_response(vec![ExchangeDataStatus::Error("rate limit".into())]);
        assert!(matches!(
            parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()),
            Err(ExchangeError::OrderRejected(_))
        ));
    }

    #[test]
    fn parse_fill_resting_is_ambiguous() {
        let resp = ok_response(vec![ExchangeDataStatus::Resting(RestingOrder { oid: 999 })]);
        assert!(matches!(
            parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()),
            Err(ExchangeError::Ambiguous(_))
        ));
    }

    #[test]
    fn parse_fill_no_data_is_ambiguous() {
        let resp = ExchangeResponseStatus::Ok(ExchangeResponse {
            response_type: "order".into(),
            data: None,
        });
        assert!(matches!(
            parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()),
            Err(ExchangeError::Ambiguous(_))
        ));
    }

    #[test]
    fn parse_fill_empty_statuses_is_ambiguous() {
        let resp = ok_response(vec![]);
        assert!(matches!(
            parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()),
            Err(ExchangeError::Ambiguous(_))
        ));
    }

    #[test]
    fn parse_fill_bad_qty_is_bad_market_data() {
        let resp = ok_response(vec![ExchangeDataStatus::Filled(FilledOrder {
            total_sz: "not-a-number".into(),
            avg_px: "64254.10".into(),
            oid: 12345,
        })]);
        assert!(matches!(
            parse_fill(resp, &cfg(25.0), "UBTC/USDC".into()),
            Err(ExchangeError::BadMarketData(_))
        ));
    }
}
