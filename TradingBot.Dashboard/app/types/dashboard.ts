// Portfolio overview (matches PortfolioResponse)
export interface PortfolioResponse {
  totalBtc: number
  totalCost: number
  averageCostBasis: number
  currentPrice: number
  unrealizedPnl: number
  unrealizedPnlPercent: number
  totalPurchaseCount: number
  firstPurchaseDate: string | null
  lastPurchaseDate: string | null
}

// Purchase history (matches PurchaseHistoryResponse)
export interface PurchaseHistoryResponse {
  items: PurchaseDto[]
  nextCursor: string | null
  hasMore: boolean
}

export interface PurchaseDto {
  id: string
  executedAt: string
  price: number
  cost: number
  quantity: number
  multiplierTier: string
  multiplier: number
  dropPercentage: number
}

// Live status (matches LiveStatusResponse)
export interface LiveStatusResponse {
  healthStatus: string
  healthMessage: string | null
  nextBuyTime: string | null
  lastPurchaseTime: string | null
  lastPurchasePrice: number | null
  lastPurchaseBtc: number | null
  lastPurchaseTier: string | null
}

// Price chart data (matches PriceChartResponse)
export interface PriceChartResponse {
  prices: PricePointDto[]
  purchases: PurchaseMarkerDto[]
  averageCostBasis: number
}

export interface PricePointDto {
  date: string
  price: number
}

export interface PurchaseMarkerDto {
  date: string
  price: number
  btcAmount: number
  tier: string
}

// Timeframe type for chart
export type ChartTimeframe = '7D' | '1M' | '3M' | '6M' | '1Y' | 'All'
