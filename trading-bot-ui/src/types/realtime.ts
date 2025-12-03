import type {
  ExecuteTradeRequest,
  ExecuteTradeResponse,
  TradingSignal,
  MarketCondition,
  BacktestRequest,
  BacktestResult,
  CompareRequest,
  CompareResult,
} from "@/types";

export interface MonitoringRequest {
  symbol: string;
  interval?: string;
  strategy?: string;
}

export interface StopMonitoringRequest {
  symbol: string;
  interval?: string;
}

export interface MonitorStatus {
  symbol: string;
  interval: string;
  isMonitoring: boolean;
  isNotificationEnabled: boolean;
  strategy: string | null;
}

export interface MonitoringStatusResponse {
  totalActiveMonitors: number;
  monitors: MonitorStatus[];
}

export interface ApiResponse {
  success: boolean;
  message: string;
  symbol?: string;
  interval?: string;
  strategy?: string;
}
