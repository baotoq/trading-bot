"use client";

import { Layout, Typography, Tabs, Space } from "antd";
import { DashboardOutlined, RocketOutlined, ExperimentOutlined, FundOutlined, ApiOutlined } from "@ant-design/icons";
import TradingPanel from "@/components/TradingPanel";
import MarketAnalysis from "@/components/MarketAnalysis";
import BacktestPanel from "@/components/BacktestPanel";
import RealtimeMonitor from "@/components/RealtimeMonitor";

const { Header, Content } = Layout;
const { Title } = Typography;

export default function Home() {
  const items = [
    {
      key: "trading",
      label: (
        <span>
          <RocketOutlined />
          Trading
        </span>
      ),
      children: <TradingPanel />,
    },
    {
      key: "market",
      label: (
        <span>
          <FundOutlined />
          Market Analysis
        </span>
      ),
      children: <MarketAnalysis />,
    },
    {
      key: "backtest",
      label: (
        <span>
          <ExperimentOutlined />
          Backtest
        </span>
      ),
      children: <BacktestPanel />,
    },
    {
      key: "realtime",
      label: (
        <span>
          <ApiOutlined />
          Real-time Monitor
        </span>
      ),
      children: <RealtimeMonitor />,
    },
  ];

  return (
    <Layout className="min-h-screen">
      <Header className="bg-gradient-to-r from-blue-600 to-blue-800 flex items-center shadow-lg">
        <Space>
          <DashboardOutlined className="text-white text-2xl" />
          <Title level={3} className="!text-white !mb-0">
            Trading Bot Dashboard
          </Title>
        </Space>
      </Header>
      <Content className="p-6 bg-gray-100">
        <div className="max-w-7xl mx-auto">
          <Tabs
            defaultActiveKey="trading"
            items={items}
            size="large"
            className="bg-white p-4 rounded-lg shadow-sm"
          />
        </div>
      </Content>
    </Layout>
  );
}
