export interface ExecuteTradeRequest {
  symbol: string;
  accountEquity: number;
  riskPercent: number;
}

export interface ExecuteTradeResponse {
  success: boolean;
  message?: string;
  trade?: TradeResult;
}

export interface TradeResult {
  symbol: string;
  side: "BUY" | "SELL";
  quantity: number;
  price: number;
  timestamp: string;
}

export interface TradingSignal {
  symbol: string;
  signal: "BUY" | "SELL" | "HOLD";
  confidence: number;
  price: number;
  indicators: {
    ema20?: number;
    ema50?: number;
    rsi?: number;
    macd?: {
      macd: number;
      signal: number;
      histogram: number;
    };
  };
}

export interface MarketCondition {
  symbol: string;
  condition: "BULLISH" | "BEARISH" | "NEUTRAL" | "VOLATILE";
  allowTrading: boolean;
  volatility: number;
  trend: string;
}

export interface BacktestRequest {
  symbol: string;
  strategy: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  riskPercent: number;
}

export interface BacktestResult {
  strategy: string;
  totalReturn: number;
  totalReturnPercent: number;
  winRate: number;
  totalTrades: number;
  profitableTrades: number;
  losingTrades: number;
  sharpeRatio: number;
  maxDrawdown: number;
  trades: Trade[];
}

export interface Trade {
  entryDate: string;
  exitDate: string;
  side: "BUY" | "SELL";
  entryPrice: number;
  exitPrice: number;
  quantity: number;
  profit: number;
  profitPercent: number;
}

export interface CompareRequest {
  symbol: string;
  strategies: string[];
  startDate: string;
  endDate: string;
  initialCapital: number;
  riskPercent: number;
}

export interface CompareResult {
  results: BacktestResult[];
}
