"use client";

import { useState } from "react";
import { Card, Form, Input, Button, InputNumber, message, Statistic, Tag } from "antd";
import { RocketOutlined, LineChartOutlined } from "@ant-design/icons";
import { tradingApi } from "@/lib/api";
import type { TradingSignal, ExecuteTradeResponse } from "@/types";

export default function TradingPanel() {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [analyzing, setAnalyzing] = useState(false);
  const [signal, setSignal] = useState<TradingSignal | null>(null);
  const [tradeResult, setTradeResult] = useState<ExecuteTradeResponse | null>(null);

  const handleAnalyze = async (symbol: string) => {
    if (!symbol) {
      message.warning("Please enter a symbol");
      return;
    }

    setAnalyzing(true);
    try {
      const result = await tradingApi.analyzeSymbol(symbol);
      setSignal(result);
      message.success("Analysis complete");
    } catch (error) {
      message.error("Failed to analyze symbol");
      console.error(error);
    } finally {
      setAnalyzing(false);
    }
  };

  const handleExecuteTrade = async (values: any) => {
    setLoading(true);
    try {
      const result = await tradingApi.executeTrade({
        symbol: values.symbol,
        accountEquity: values.accountEquity,
        riskPercent: values.riskPercent,
      });
      setTradeResult(result);
      if (result.success) {
        message.success("Trade executed successfully!");
      } else {
        message.error(result.message || "Trade execution failed");
      }
    } catch (error) {
      message.error("Failed to execute trade");
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const getSignalColor = (signal: string) => {
    switch (signal) {
      case "BUY":
        return "green";
      case "SELL":
        return "red";
      default:
        return "default";
    }
  };

  return (
    <Card title={<span><RocketOutlined /> Trading Panel</span>} className="shadow-md">
      <Form
        form={form}
        layout="vertical"
        onFinish={handleExecuteTrade}
        initialValues={{ accountEquity: 10000, riskPercent: 1 }}
      >
        <div className="flex flex-col md:flex-row gap-4">
          <div className="flex-1">
            <Form.Item
              label="Symbol"
              name="symbol"
              rules={[{ required: true, message: "Please enter a symbol" }]}
            >
              <Input placeholder="e.g., BTCUSDT" size="large" />
            </Form.Item>
          </div>
          <div className="flex-1">
            <Form.Item label=" ">
              <Button
                icon={<LineChartOutlined />}
                onClick={() => handleAnalyze(form.getFieldValue("symbol"))}
                loading={analyzing}
                block
                size="large"
              >
                Analyze Signal
              </Button>
            </Form.Item>
          </div>
        </div>

        {signal && (
          <Card className="mb-4 bg-gray-50" size="small">
            <div className="flex flex-wrap gap-4">
              <div className="flex-1 min-w-[150px]">
                <Statistic
                  title="Signal"
                  value={signal.signal}
                  valueStyle={{ color: getSignalColor(signal.signal) === "green" ? "#3f8600" : getSignalColor(signal.signal) === "red" ? "#cf1322" : undefined }}
                />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Statistic
                  title="Confidence"
                  value={signal.confidence}
                  precision={2}
                  suffix="%"
                />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Statistic
                  title="Price"
                  value={signal.price}
                  precision={2}
                  prefix="$"
                />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Statistic
                  title="RSI"
                  value={signal.indicators.rsi || 0}
                  precision={2}
                />
              </div>
            </div>
          </Card>
        )}

        <div className="flex flex-col md:flex-row gap-4">
          <div className="flex-1">
            <Form.Item
              label="Account Equity"
              name="accountEquity"
              rules={[{ required: true, message: "Please enter account equity" }]}
            >
              <InputNumber
                prefix="$"
                min={0}
                className="w-full"
                size="large"
                formatter={(value) => `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}
              />
            </Form.Item>
          </div>
          <div className="flex-1">
            <Form.Item
              label="Risk Percent"
              name="riskPercent"
              rules={[{ required: true, message: "Please enter risk percent" }]}
            >
              <InputNumber
                min={0.1}
                max={10}
                suffix="%"
                className="w-full"
                size="large"
              />
            </Form.Item>
          </div>
        </div>

        <Form.Item>
          <Button type="primary" htmlType="submit" loading={loading} size="large" block>
            Execute Trade
          </Button>
        </Form.Item>
      </Form>

      {tradeResult && (
        <Card
          className="mt-4"
          size="small"
          title={
            <Tag color={tradeResult.success ? "success" : "error"}>
              {tradeResult.success ? "Success" : "Failed"}
            </Tag>
          }
        >
          <p>{tradeResult.message}</p>
          {tradeResult.trade && (
            <div className="flex flex-wrap gap-4 mt-3">
              <div className="flex-1 min-w-[150px]">
                <Statistic title="Side" value={tradeResult.trade.side} />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Statistic title="Quantity" value={tradeResult.trade.quantity} precision={4} />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Statistic title="Price" value={tradeResult.trade.price} precision={2} prefix="$" />
              </div>
              <div className="flex-1 min-w-[150px]">
                <Statistic title="Symbol" value={tradeResult.trade.symbol} />
              </div>
            </div>
          )}
        </Card>
      )}
    </Card>
  );
}
