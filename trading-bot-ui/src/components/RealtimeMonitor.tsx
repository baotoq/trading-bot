"use client";

import { useState, useEffect } from "react";
import {
  Card,
  Form,
  Input,
  Button,
  Select,
  message,
  Tag,
  Empty,
  Space,
  Divider,
  Alert,
} from "antd";
import {
  PlayCircleOutlined,
  StopOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { realtimeApi } from "@/lib/api";
import type { MonitorStatus } from "@/types/realtime";

const FUTURES_COINS = [
  { symbol: "BTCUSDT", name: "BTC", color: "#F7931A" },
  { symbol: "ETHUSDT", name: "ETH", color: "#627EEA" },
  { symbol: "BNBUSDT", name: "BNB", color: "#F3BA2F" },
  { symbol: "SOLUSDT", name: "SOL", color: "#14F195" },
  { symbol: "XRPUSDT", name: "XRP", color: "#23292F" },
];

const SPOT_COINS = [
  { symbol: "BTCUSDT", name: "BTC", color: "#F7931A" },
  { symbol: "ETHUSDT", name: "ETH", color: "#627EEA" },
  { symbol: "ADAUSDT", name: "ADA", color: "#0033AD" },
  { symbol: "DOTUSDT", name: "DOT", color: "#E6007A" },
  { symbol: "LINKUSDT", name: "LINK", color: "#2A5ADA" },
];

export default function RealtimeMonitor() {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [monitors, setMonitors] = useState<MonitorStatus[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedSymbol, setSelectedSymbol] = useState<string>("");
  const [coinType, setCoinType] = useState<"futures" | "spot">("futures");

  const fetchMonitors = async () => {
    setRefreshing(true);
    try {
      const response = await realtimeApi.getStatus();
      setMonitors(response.monitors);
    } catch (error) {
      message.error("Failed to fetch monitoring status");
      console.error(error);
    } finally {
      setRefreshing(false);
    }
  };

  useEffect(() => {
    fetchMonitors();
    // Auto-refresh every 10 seconds
    const interval = setInterval(fetchMonitors, 10000);
    return () => clearInterval(interval);
  }, []);

  const handleStartMonitoring = async (values: any) => {
    setLoading(true);
    try {
      const response = await realtimeApi.startMonitoring({
        symbol: values.symbol.toUpperCase(),
        interval: "1m", // Hardcoded to 1 minute
        strategy: values.strategy,
      });
      message.success(response.message);
      form.resetFields();
      await fetchMonitors();
    } catch (error: any) {
      message.error(error.response?.data?.message || "Failed to start monitoring");
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleStopMonitoring = async (symbol: string, interval: string) => {
    try {
      const response = await realtimeApi.stopMonitoring({ symbol, interval });
      message.success(response.message);
      await fetchMonitors();
    } catch (error: any) {
      message.error(error.response?.data?.message || "Failed to stop monitoring");
      console.error(error);
    }
  };

  const handleTestTelegram = async () => {
    try {
      const response = await realtimeApi.testTelegram();
      message.success(response.message);
    } catch (error: any) {
      message.error(error.response?.data?.message || "Failed to send test notification");
      console.error(error);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header with Actions */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h2 className="text-2xl font-bold mb-1">Real-time Monitoring</h2>
          <p className="text-gray-600">
            Monitor symbols in real-time and receive Telegram signals
          </p>
        </div>
        <Space>
          <Button
            icon={<ThunderboltOutlined />}
            onClick={handleTestTelegram}
          >
            Test Telegram
          </Button>
          <Button
            icon={<ReloadOutlined />}
            onClick={fetchMonitors}
            loading={refreshing}
          >
            Refresh
          </Button>
        </Space>
      </div>

      {/* Start Monitoring Form */}
      <Card
        title={
          <span>
            <PlayCircleOutlined className="mr-2" />
            Start New Monitor
          </span>
        }
        className="shadow-md"
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={handleStartMonitoring}
          initialValues={{
            strategy: "EmaMomentumScalper",
          }}
        >
          {/* Top Coins Selector */}
          <Form.Item label="Quick Select">
            <div className="space-y-3">
              {/* Coin Type Tabs */}
              <div className="flex gap-2">
                <Button
                  type={coinType === "futures" ? "primary" : "default"}
                  onClick={() => setCoinType("futures")}
                >
                  Futures Top 5
                </Button>
                <Button
                  type={coinType === "spot" ? "primary" : "default"}
                  onClick={() => setCoinType("spot")}
                >
                  Spot Top 5
                </Button>
              </div>

              {/* Coin Buttons */}
              <div className="flex flex-wrap gap-2">
                {(coinType === "futures" ? FUTURES_COINS : SPOT_COINS).map((coin) => (
                  <Button
                    key={coin.symbol}
                    size="large"
                    type={selectedSymbol === coin.symbol ? "primary" : "default"}
                    onClick={() => {
                      setSelectedSymbol(coin.symbol);
                      form.setFieldValue("symbol", coin.symbol);
                    }}
                    className="flex items-center gap-2"
                    style={{
                      borderColor: selectedSymbol === coin.symbol ? coin.color : undefined,
                      backgroundColor: selectedSymbol === coin.symbol ? coin.color : undefined,
                    }}
                  >
                    <span className="font-semibold">{coin.name}</span>
                    <span className="text-xs opacity-75">{coin.symbol}</span>
                  </Button>
                ))}
              </div>
            </div>
          </Form.Item>

          <div className="flex flex-col md:flex-row gap-4">
            <div className="flex-1">
              <Form.Item
                label="Symbol (or type custom)"
                name="symbol"
                rules={[
                  { required: true, message: "Please select or enter a symbol" },
                  {
                    pattern: /^[A-Z]+$/,
                    message: "Symbol must be uppercase letters only",
                  },
                ]}
              >
                <Input
                  placeholder="e.g., BTCUSDT"
                  size="large"
                  onChange={(e) => {
                    const value = e.target.value.toUpperCase();
                    form.setFieldValue("symbol", value);
                    setSelectedSymbol(value);
                  }}
                />
              </Form.Item>
            </div>
            <div className="flex-1">
              <Form.Item label="Strategy" name="strategy">
                <Select size="large">
                  <Select.Option value="EmaMomentumScalper">
                    EMA Momentum Scalper
                  </Select.Option>
                  <Select.Option value="MacdStrategy">
                    MACD Strategy
                  </Select.Option>
                  <Select.Option value="RsiStrategy">
                    RSI Strategy
                  </Select.Option>
                </Select>
              </Form.Item>
            </div>
            <div className="flex-1">
              <Form.Item label=" ">
                <Button
                  type="primary"
                  htmlType="submit"
                  size="large"
                  icon={<PlayCircleOutlined />}
                  loading={loading}
                  block
                >
                  Start Monitoring (1m)
                </Button>
              </Form.Item>
            </div>
          </div>
        </Form>

        <Alert
          title="Note: Signals will be sent to your configured Telegram chat"
          type="info"
          showIcon
          className="mt-4"
        />
      </Card>

      {/* Active Monitors */}
      <Card
        title={
          <div className="flex justify-between items-center">
            <span>
              Active Monitors ({monitors.length})
            </span>
            {monitors.length > 0 && (
              <Tag color="success">
                <CheckCircleOutlined /> Connected
              </Tag>
            )}
          </div>
        }
        className="shadow-md"
      >
        {monitors.length === 0 ? (
          <Empty
            description="No active monitors. Start monitoring a symbol to receive real-time signals."
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        ) : (
          <div className="space-y-4">
            {monitors.map((monitor, index) => (
              <Card
                key={`${monitor.symbol}-${monitor.interval}`}
                size="small"
                className="bg-gray-50 border border-gray-200"
              >
                <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-2">
                      <span className="text-lg font-bold">
                        {monitor.symbol}
                      </span>
                      <Tag color="blue">{monitor.interval}</Tag>
                      {monitor.isMonitoring && (
                        <Tag color="success" icon={<CheckCircleOutlined />}>
                          Monitoring
                        </Tag>
                      )}
                      {monitor.isNotificationEnabled && (
                        <Tag color="green" icon={<ThunderboltOutlined />}>
                          Notifications ON
                        </Tag>
                      )}
                    </div>
                    {monitor.strategy && (
                      <div className="text-sm text-gray-600">
                        <span className="font-semibold">Strategy:</span>{" "}
                        {monitor.strategy}
                      </div>
                    )}
                  </div>
                  <Button
                    danger
                    icon={<StopOutlined />}
                    onClick={() =>
                      handleStopMonitoring(monitor.symbol, monitor.interval)
                    }
                  >
                    Stop
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
      </Card>

      {/* Help Section */}
      <Card title="How It Works" size="small" className="shadow-md">
        <div className="space-y-3 text-sm">
          <div>
            <span className="font-semibold">1. Start Monitoring:</span> Select a
            symbol, interval, and strategy to begin real-time candle monitoring
          </div>
          <div>
            <span className="font-semibold">2. Signal Generation:</span> When a
            candle completes, the strategy analyzes it and generates trading
            signals
          </div>
          <div>
            <span className="font-semibold">3. Telegram Notification:</span>{" "}
            Actionable signals (Buy/Sell) are sent to your Telegram with entry,
            stop loss, and take profit levels
          </div>
          <div>
            <span className="font-semibold">4. Multiple Monitors:</span> You can
            monitor multiple symbols simultaneously
          </div>
        </div>
        <Divider />
        <Alert
          title="Make sure to configure your Telegram bot token and chat ID in the API settings"
          type="warning"
          showIcon
        />
      </Card>
    </div>
  );
}
