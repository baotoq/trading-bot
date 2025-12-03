"use client";

import { useState } from "react";
import { Card, Input, Button, message, Statistic } from "antd";
import { FundOutlined, SearchOutlined } from "@ant-design/icons";
import { tradingApi } from "@/lib/api";
import type { MarketCondition } from "@/types";

export default function MarketAnalysis() {
  const [symbol, setSymbol] = useState("");
  const [loading, setLoading] = useState(false);
  const [condition, setCondition] = useState<MarketCondition | null>(null);

  const handleAnalyze = async () => {
    if (!symbol) {
      message.warning("Please enter a symbol");
      return;
    }

    setLoading(true);
    try {
      const result = await tradingApi.getMarketCondition(symbol);
      setCondition(result);
      message.success("Market analysis complete");
    } catch (error) {
      message.error("Failed to analyze market");
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const getConditionColor = (cond: string) => {
    switch (cond) {
      case "BULLISH":
        return "green";
      case "BEARISH":
        return "red";
      case "VOLATILE":
        return "orange";
      default:
        return "default";
    }
  };

  return (
    <Card title={<span><FundOutlined /> Market Analysis</span>} className="shadow-md">
      <div className="flex gap-2 mb-4">
        <Input
          placeholder="Enter symbol (e.g., BTCUSDT)"
          value={symbol}
          onChange={(e) => setSymbol(e.target.value.toUpperCase())}
          onPressEnter={handleAnalyze}
          size="large"
        />
        <Button
          type="primary"
          icon={<SearchOutlined />}
          onClick={handleAnalyze}
          loading={loading}
          size="large"
        >
          Analyze
        </Button>
      </div>

      {condition && (
        <div className="space-y-4">
          <div className="flex flex-wrap gap-4">
            <div className="flex-1 min-w-[200px]">
              <Card className="text-center">
                <Statistic
                  title="Market Condition"
                  value={condition.condition}
                  valueStyle={{
                    color:
                      getConditionColor(condition.condition) === "green"
                        ? "#3f8600"
                        : getConditionColor(condition.condition) === "red"
                        ? "#cf1322"
                        : "#fa8c16",
                  }}
                />
              </Card>
            </div>
            <div className="flex-1 min-w-[200px]">
              <Card className="text-center">
                <Statistic
                  title="Trading Status"
                  value={condition.allowTrading ? "ALLOWED" : "RESTRICTED"}
                  valueStyle={{ color: condition.allowTrading ? "#3f8600" : "#cf1322" }}
                />
              </Card>
            </div>
            <div className="flex-1 min-w-[200px]">
              <Card className="text-center">
                <Statistic
                  title="Volatility"
                  value={condition.volatility}
                  precision={2}
                  suffix="%"
                />
              </Card>
            </div>
          </div>

          <Card size="small" className="bg-gray-50">
            <p className="mb-2">
              <strong>Symbol:</strong> {condition.symbol}
            </p>
            <p>
              <strong>Trend:</strong> {condition.trend}
            </p>
          </Card>
        </div>
      )}
    </Card>
  );
}
