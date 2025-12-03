"use client";

import { useState } from "react";
import { Card, Form, Input, Button, InputNumber, DatePicker, message, Table, Statistic, Select } from "antd";
import { ExperimentOutlined } from "@ant-design/icons";
import { tradingApi } from "@/lib/api";
import type { BacktestResult } from "@/types";
import dayjs from "dayjs";

const { RangePicker } = DatePicker;

export default function BacktestPanel() {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<BacktestResult | null>(null);

  const handleRunBacktest = async (values: any) => {
    setLoading(true);
    try {
      const [startDate, endDate] = values.dateRange;
      const backtestResult = await tradingApi.runBacktest({
        symbol: values.symbol,
        strategy: values.strategy,
        startDate: startDate.format("YYYY-MM-DD"),
        endDate: endDate.format("YYYY-MM-DD"),
        initialCapital: values.initialCapital,
        riskPercent: values.riskPercent,
      });
      setResult(backtestResult);
      message.success("Backtest completed successfully");
    } catch (error) {
      message.error("Failed to run backtest");
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const columns = [
    {
      title: "Entry Date",
      dataIndex: "entryDate",
      key: "entryDate",
      render: (date: string) => dayjs(date).format("YYYY-MM-DD HH:mm"),
    },
    {
      title: "Exit Date",
      dataIndex: "exitDate",
      key: "exitDate",
      render: (date: string) => dayjs(date).format("YYYY-MM-DD HH:mm"),
    },
    {
      title: "Side",
      dataIndex: "side",
      key: "side",
      render: (side: string) => (
        <span style={{ color: side === "BUY" ? "#3f8600" : "#cf1322" }}>{side}</span>
      ),
    },
    {
      title: "Entry Price",
      dataIndex: "entryPrice",
      key: "entryPrice",
      render: (price: number) => `$${price.toFixed(2)}`,
    },
    {
      title: "Exit Price",
      dataIndex: "exitPrice",
      key: "exitPrice",
      render: (price: number) => `$${price.toFixed(2)}`,
    },
    {
      title: "Quantity",
      dataIndex: "quantity",
      key: "quantity",
      render: (qty: number) => qty.toFixed(4),
    },
    {
      title: "Profit",
      dataIndex: "profit",
      key: "profit",
      render: (profit: number) => (
        <span style={{ color: profit >= 0 ? "#3f8600" : "#cf1322" }}>
          ${profit.toFixed(2)}
        </span>
      ),
    },
    {
      title: "Profit %",
      dataIndex: "profitPercent",
      key: "profitPercent",
      render: (percent: number) => (
        <span style={{ color: percent >= 0 ? "#3f8600" : "#cf1322" }}>
          {percent.toFixed(2)}%
        </span>
      ),
    },
  ];

  return (
    <Card title={<span><ExperimentOutlined /> Backtest</span>} className="shadow-md">
      <Form
        form={form}
        layout="vertical"
        onFinish={handleRunBacktest}
        initialValues={{
          initialCapital: 10000,
          riskPercent: 1,
          strategy: "EmaMomentumScalper",
          dateRange: [dayjs().subtract(30, "days"), dayjs()],
        }}
      >
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Form.Item
            label="Symbol"
            name="symbol"
            rules={[{ required: true, message: "Please enter a symbol" }]}
          >
            <Input placeholder="e.g., BTCUSDT" size="large" />
          </Form.Item>
          <Form.Item
            label="Strategy"
            name="strategy"
            rules={[{ required: true, message: "Please select a strategy" }]}
          >
            <Select size="large">
              <Select.Option value="EmaMomentumScalper">EMA Momentum Scalper</Select.Option>
              <Select.Option value="MacdStrategy">MACD Strategy</Select.Option>
              <Select.Option value="RsiStrategy">RSI Strategy</Select.Option>
            </Select>
          </Form.Item>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Form.Item
            label="Date Range"
            name="dateRange"
            rules={[{ required: true, message: "Please select date range" }]}
          >
            <RangePicker className="w-full" size="large" />
          </Form.Item>
          <Form.Item
            label="Initial Capital"
            name="initialCapital"
            rules={[{ required: true, message: "Please enter initial capital" }]}
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

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
          <Form.Item label=" ">
            <Button type="primary" htmlType="submit" loading={loading} size="large" block>
              Run Backtest
            </Button>
          </Form.Item>
        </div>
      </Form>

      {result && (
        <div className="mt-6">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
            <Card className="text-center">
              <Statistic
                title="Total Return"
                value={result.totalReturn}
                precision={2}
                prefix="$"
                valueStyle={{ color: result.totalReturn >= 0 ? "#3f8600" : "#cf1322" }}
              />
            </Card>
            <Card className="text-center">
              <Statistic
                title="Return %"
                value={result.totalReturnPercent}
                precision={2}
                suffix="%"
                valueStyle={{ color: result.totalReturnPercent >= 0 ? "#3f8600" : "#cf1322" }}
              />
            </Card>
            <Card className="text-center">
              <Statistic
                title="Win Rate"
                value={result.winRate}
                precision={2}
                suffix="%"
              />
            </Card>
            <Card className="text-center">
              <Statistic title="Total Trades" value={result.totalTrades} />
            </Card>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
            <Card className="text-center">
              <Statistic
                title="Profitable Trades"
                value={result.profitableTrades}
                valueStyle={{ color: "#3f8600" }}
              />
            </Card>
            <Card className="text-center">
              <Statistic
                title="Losing Trades"
                value={result.losingTrades}
                valueStyle={{ color: "#cf1322" }}
              />
            </Card>
            <Card className="text-center">
              <Statistic
                title="Sharpe Ratio"
                value={result.sharpeRatio}
                precision={2}
              />
            </Card>
            <Card className="text-center">
              <Statistic
                title="Max Drawdown"
                value={result.maxDrawdown}
                precision={2}
                suffix="%"
                valueStyle={{ color: "#cf1322" }}
              />
            </Card>
          </div>

          <Card title="Trade History" size="small">
            <Table
              columns={columns}
              dataSource={result.trades}
              rowKey={(record, index) => `${record.entryDate}-${index}`}
              pagination={{ pageSize: 10 }}
              scroll={{ x: true }}
            />
          </Card>
        </div>
      )}
    </Card>
  );
}
