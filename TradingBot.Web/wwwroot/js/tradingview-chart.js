// TradingView Advanced Charts Integration
window.TradingViewWidget = {
    widgets: {},
    
    createWidget: function (containerId, symbol, interval, theme) {
        try {
            // Remove existing widget if any
            if (this.widgets[containerId]) {
                this.destroyWidget(containerId);
            }

            // Create TradingView widget
            const widget = new TradingView.widget({
                autosize: true,
                symbol: "BINANCE:" + symbol,
                interval: interval,
                timezone: "Etc/UTC",
                theme: theme || "dark",
                style: "1", // Candlestick
                locale: "en",
                toolbar_bg: "#f1f3f6",
                enable_publishing: false,
                allow_symbol_change: true,
                container_id: containerId,
                
                // Chart settings
                studies: [
                    "STD;SMA",           // Simple Moving Average
                    "STD;EMA",           // Exponential Moving Average
                    "STD;Volume",        // Volume
                    "STD;RSI",          // RSI
                    "STD;MACD",         // MACD
                    "STD;BB"            // Bollinger Bands
                ],
                
                // Features
                hide_side_toolbar: false,
                hide_top_toolbar: false,
                hide_legend: false,
                withdateranges: true,
                details: true,
                hotlist: true,
                calendar: false,
                
                // Appearance
                overrides: {
                    "paneProperties.background": "#131722",
                    "paneProperties.backgroundType": "solid",
                    "scalesProperties.textColor": "#AAA",
                    "scalesProperties.lineColor": "#444"
                },
                
                // Disabled features
                disabled_features: [
                    "use_localstorage_for_settings",
                    "header_symbol_search",
                    "symbol_search_hot_key"
                ],
                
                // Enabled features
                enabled_features: [
                    "study_templates",
                    "create_volume_indicator_by_default"
                ],
                
                // Loading screen
                loading_screen: {
                    backgroundColor: "#131722",
                    foregroundColor: "#2962FF"
                }
            });

            this.widgets[containerId] = widget;
            return true;
        } catch (error) {
            console.error('Error creating TradingView widget:', error);
            return false;
        }
    },

    changeSymbol: function (containerId, symbol) {
        try {
            const widget = this.widgets[containerId];
            if (widget && widget.chart) {
                widget.chart().setSymbol("BINANCE:" + symbol);
                return true;
            }
            return false;
        } catch (error) {
            console.error('Error changing symbol:', error);
            return false;
        }
    },

    changeInterval: function (containerId, interval) {
        try {
            const widget = this.widgets[containerId];
            if (widget && widget.chart) {
                widget.chart().setResolution(interval);
                return true;
            }
            return false;
        } catch (error) {
            console.error('Error changing interval:', error);
            return false;
        }
    },

    addStudy: function (containerId, studyName) {
        try {
            const widget = this.widgets[containerId];
            if (widget && widget.chart) {
                widget.chart().createStudy(studyName, false, false);
                return true;
            }
            return false;
        } catch (error) {
            console.error('Error adding study:', error);
            return false;
        }
    },

    destroyWidget: function (containerId) {
        try {
            const widget = this.widgets[containerId];
            if (widget && widget.remove) {
                widget.remove();
            }
            delete this.widgets[containerId];
            
            // Clear container
            const container = document.getElementById(containerId);
            if (container) {
                container.innerHTML = '';
            }
            return true;
        } catch (error) {
            console.error('Error destroying widget:', error);
            return false;
        }
    }
};

