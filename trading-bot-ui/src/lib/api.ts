import axios from "axios";
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
import type {
  MonitoringRequest,
  StopMonitoringRequest,
  MonitoringStatusResponse,
  ApiResponse,
} from "@/types/realtime";

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

const apiClient = axios.create({
  baseURL: API_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

export const tradingApi = {
  // Execute a trade
  executeTrade: async (
    request: ExecuteTradeRequest
  ): Promise<ExecuteTradeResponse> => {
    const response = await apiClient.post<ExecuteTradeResponse>(
      "/api/trade/execute",
      request
    );
    return response.data;
  },

  // Analyze a symbol
  analyzeSymbol: async (symbol: string): Promise<TradingSignal> => {
    const response = await apiClient.get<TradingSignal>(
      `/api/trade/analyze/${symbol}`
    );
    return response.data;
  },

  // Get market condition
  getMarketCondition: async (symbol: string): Promise<MarketCondition> => {
    const response = await apiClient.get<MarketCondition>(
      `/api/market/condition/${symbol}`
    );
    return response.data;
  },

  // Run backtest
  runBacktest: async (request: BacktestRequest): Promise<BacktestResult> => {
    const response = await apiClient.post<BacktestResult>(
      "/api/backtest/run",
      request
    );
    return response.data;
  },

  // Compare strategies
  compareStrategies: async (request: CompareRequest): Promise<CompareResult> => {
    const response = await apiClient.post<CompareResult>(
      "/api/backtest/compare",
      request
    );
    return response.data;
  },
};

export const realtimeApi = {
  // Start real-time monitoring
  startMonitoring: async (request: MonitoringRequest): Promise<ApiResponse> => {
    const response = await apiClient.post<ApiResponse>(
      "/api/realtime/start",
      request
    );
    return response.data;
  },

  // Stop real-time monitoring
  stopMonitoring: async (request: StopMonitoringRequest): Promise<ApiResponse> => {
    const response = await apiClient.post<ApiResponse>(
      "/api/realtime/stop",
      request
    );
    return response.data;
  },

  // Get monitoring status
  getStatus: async (): Promise<MonitoringStatusResponse> => {
    const response = await apiClient.get<MonitoringStatusResponse>(
      "/api/realtime/status"
    );
    return response.data;
  },

  // Test Telegram notification
  testTelegram: async (): Promise<ApiResponse> => {
    const response = await apiClient.post<ApiResponse>(
      "/api/realtime/test-telegram"
    );
    return response.data;
  },
};

export default apiClient;
