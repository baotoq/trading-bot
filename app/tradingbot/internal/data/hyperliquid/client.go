package hyperliquid

import (
	"context"
	"fmt"
	"os"
	"strings"

	"github.com/ethereum/go-ethereum/crypto"
	hlsdk "github.com/sonirico/go-hyperliquid"
	confpkg "tradingbot/app/tradingbot/internal/conf"
)

const defaultAgentKeyEnv = "HL_AGENT_KEY"

// NewHyperliquidClients constructs SDK Info and Exchange clients from config.
// It loads the agent private key from the env var named by conf.AgentKeyEnv
// (defaulting to HL_AGENT_KEY). Pass nil for meta/spotMeta/perpDexs to let
// the SDK auto-fetch them.
func NewHyperliquidClients(
	ctx context.Context,
	conf *confpkg.Exchange_Hyperliquid,
) (*hlsdk.Info, *hlsdk.Exchange, error) {
	// Determine base URL
	baseURL := conf.GetApiUrl()
	if conf.GetTestnet() {
		baseURL = hlsdk.TestnetAPIURL
	} else if baseURL == "" {
		baseURL = hlsdk.MainnetAPIURL
	}

	// Load agent key env var name
	envName := conf.GetAgentKeyEnv()
	if envName == "" {
		envName = defaultAgentKeyEnv
	}

	rawKey := os.Getenv(envName)
	if rawKey == "" {
		return nil, nil, fmt.Errorf("hyperliquid: env var %s is not set", envName)
	}

	// Strip optional 0x prefix
	rawKey = strings.TrimPrefix(rawKey, "0x")

	privateKey, err := crypto.HexToECDSA(rawKey)
	if err != nil {
		return nil, nil, fmt.Errorf("hyperliquid: failed to parse private key from %s: %w", envName, err)
	}

	// Build Info client — pass nil for meta/spotMeta/perpDexs so the SDK fetches them.
	info := hlsdk.NewInfo(ctx, baseURL, true, nil, nil, nil)

	// Build Exchange client — vault address is empty; master address is passed as accountAddr.
	ex := hlsdk.NewExchange(
		ctx,
		privateKey,
		baseURL,
		nil, // meta: auto-fetch
		"",  // vaultAddr: none
		conf.GetMasterAddress(),
		nil, // spotMeta: auto-fetch
		nil, // perpDexs: auto-fetch
	)

	return info, ex, nil
}
