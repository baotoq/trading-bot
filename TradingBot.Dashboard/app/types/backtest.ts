// DCA config for backtest form pre-fill
export interface DcaConfigResponse {
  baseDailyAmount: number
  highLookbackDays: number
  bearMarketMaPeriod: number
  bearBoostFactor: number
  maxMultiplierCap: number
  tiers: MultiplierTierDto[]
}

export interface MultiplierTierDto {
  dropPercentage: number
  multiplier: number
}

// Backtest request
export interface BacktestRequest {
  startDate?: string
  endDate?: string
  baseDailyAmount?: number
  highLookbackDays?: number
  bearMarketMaPeriod?: number
  bearBoostFactor?: number
  maxMultiplierCap?: number
  tiers?: MultiplierTierInput[]
}

export interface MultiplierTierInput {
  dropPercentage: number
  multiplier: number
}

// Backtest response
export interface BacktestResponse {
  config: BacktestConfig
  startDate: string
  endDate: string
  totalDays: number
  result: BacktestResult
}

export interface BacktestResult {
  smartDca: DcaMetrics
  fixedDcaSameBase: DcaMetrics
  fixedDcaMatchTotal: DcaMetrics
  comparison: ComparisonMetrics
  tierBreakdown: TierBreakdownEntry[]
  purchaseLog: PurchaseLogEntry[]
}

export interface DcaMetrics {
  totalInvested: number
  totalBtc: number
  avgCostBasis: number
  portfolioValue: number
  returnPercent: number
  maxDrawdown: number
}

export interface ComparisonMetrics {
  costBasisDeltaSameBase: number
  costBasisDeltaMatchTotal: number
  extraBtcPercentSameBase: number
  extraBtcPercentMatchTotal: number
  efficiencyRatio: number
}

export interface TierBreakdownEntry {
  tierName: string
  triggerCount: number
  extraUsdSpent: number
  extraBtcAcquired: number
}

export interface PurchaseLogEntry {
  date: string
  price: number
  smartMultiplier: number
  smartTier: string
  smartAmountUsd: number
  smartBtcBought: number
  smartCumulativeUsd: number
  smartCumulativeBtc: number
  smartRunningCostBasis: number
  fixedSameBaseAmountUsd: number
  fixedSameBaseBtcBought: number
  fixedSameBaseCumulativeUsd: number
  fixedSameBaseCumulativeBtc: number
  fixedSameBaseRunningCostBasis: number
  fixedMatchTotalAmountUsd: number
  fixedMatchTotalBtcBought: number
  fixedMatchTotalCumulativeUsd: number
  fixedMatchTotalCumulativeBtc: number
  fixedMatchTotalRunningCostBasis: number
  high30Day: number
  ma200Day: number
}

export interface BacktestConfig {
  baseDailyAmount: number
  highLookbackDays: number
  bearMarketMaPeriod: number
  bearBoostFactor: number
  maxMultiplierCap: number
  tiers: MultiplierTierConfig[]
}

export interface MultiplierTierConfig {
  dropPercentage: number
  multiplier: number
}

// Parameter sweep request
export interface SweepRequest {
  startDate?: string
  endDate?: string
  preset?: string
  baseAmounts?: number[]
  highLookbackDays?: number[]
  bearMarketMaPeriods?: number[]
  bearBoosts?: number[]
  maxMultiplierCaps?: number[]
  tierSets?: TierSet[]
  rankBy?: string
  maxCombinations?: number
  validate?: boolean
}

export interface TierSet {
  tiers: MultiplierTierInput[]
}

// Parameter sweep response
export interface SweepResponse {
  totalCombinations: number
  executedCombinations: number
  rankedBy: string
  startDate: string
  endDate: string
  totalDays: number
  results: SweepResultEntry[]
  topResults: SweepResultDetailEntry[]
  walkForward?: WalkForwardSummary
}

export interface SweepResultEntry {
  rank: number
  config: BacktestConfig
  smartDca: DcaMetrics
  fixedDcaSameBase: DcaMetrics
  comparison: ComparisonMetrics
  walkForward?: WalkForwardEntry
}

export interface SweepResultDetailEntry extends SweepResultEntry {
  tierBreakdown: TierBreakdownEntry[]
  purchaseLog: PurchaseLogEntry[]
}

export interface WalkForwardEntry {
  trainReturnPercent: number
  testReturnPercent: number
  returnDegradation: number
  trainEfficiency: number
  testEfficiency: number
  efficiencyDegradation: number
  overfitWarning: boolean
}

export interface WalkForwardSummary {
  trainRatio: number
  trainEnd: string
  testStart: string
  overfitCount: number
  totalValidated: number
}
