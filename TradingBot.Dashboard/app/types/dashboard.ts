// Branded types for type-safe IDs (mirrors backend strongly-typed IDs)
export type PurchaseId = string & { readonly __brand: 'PurchaseId' }

// Branded types for type-safe value objects (mirrors backend domain primitives)
export type Price = number & { readonly __brand: 'Price' }
export type UsdAmount = number & { readonly __brand: 'UsdAmount' }
export type Quantity = number & { readonly __brand: 'Quantity' }
export type BtcMultiplier = number & { readonly __brand: 'Multiplier' }
export type Percentage = number & { readonly __brand: 'Percentage' }
export type TradingSymbol = string & { readonly __brand: 'Symbol' }

// Portfolio overview (matches PortfolioResponse)
export interface PortfolioResponse {
  totalBtc: Quantity
  totalCost: number                   // plain decimal (not UsdAmount) -- zero when no purchases
  averageCostBasis: Price | null      // null when no purchases
  currentPrice: Price | null          // null when Hyperliquid unreachable
  unrealizedPnl: number | null        // null when currentPrice unavailable
  unrealizedPnlPercent: number | null // null when currentPrice unavailable
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
  id: PurchaseId
  executedAt: string
  price: Price
  cost: UsdAmount
  quantity: Quantity
  multiplierTier: string
  multiplier: BtcMultiplier
  dropPercentage: Percentage
}

// Live status (matches LiveStatusResponse)
export interface LiveStatusResponse {
  healthStatus: string
  healthMessage: string | null
  nextBuyTime: string | null
  lastPurchaseTime: string | null
  lastPurchasePrice: Price | null
  lastPurchaseBtc: Quantity | null
  lastPurchaseTier: string | null
}

// Price chart data (matches PriceChartResponse)
export interface PriceChartResponse {
  prices: PricePointDto[]
  purchases: PurchaseMarkerDto[]
  averageCostBasis: Price | null  // null when no purchases
}

export interface PricePointDto {
  date: string
  price: Price
}

export interface PurchaseMarkerDto {
  date: string
  price: Price
  btcAmount: Quantity
  tier: string
}

// Timeframe type for chart
export type ChartTimeframe = '7D' | '1M' | '3M' | '6M' | '1Y' | 'All'
