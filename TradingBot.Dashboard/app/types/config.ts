export interface ConfigResponse {
  baseDailyAmount: number
  dailyBuyHour: number
  dailyBuyMinute: number
  highLookbackDays: number
  dryRun: boolean
  bearMarketMaPeriod: number
  bearBoostFactor: number
  maxMultiplierCap: number
  tiers: MultiplierTierDto[]
}

export interface MultiplierTierDto {
  dropPercentage: number
  multiplier: number
}

// Same shape for update request
export type UpdateConfigRequest = ConfigResponse
