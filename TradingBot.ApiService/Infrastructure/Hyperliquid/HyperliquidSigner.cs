using MessagePack;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using TradingBot.ApiService.Infrastructure.Hyperliquid.Models;

namespace TradingBot.ApiService.Infrastructure.Hyperliquid;

/// <summary>
/// Signs Hyperliquid order actions using EIP-712 typed data signatures.
/// Based on Hyperliquid Python SDK signing implementation.
/// </summary>
public class HyperliquidSigner
{
    private readonly EthECKey _key;
    private readonly string _walletAddress;
    private readonly ILogger<HyperliquidSigner> _logger;

    public HyperliquidSigner(string privateKey, ILogger<HyperliquidSigner> logger)
    {
        _logger = logger;

        // Validate private key format without logging it
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("Private key cannot be null or empty", nameof(privateKey));
        }

        // Remove 0x prefix if present
        var cleanKey = privateKey.StartsWith("0x") ? privateKey[2..] : privateKey;

        if (cleanKey.Length != 64)
        {
            throw new ArgumentException("Private key must be 64 hex characters (32 bytes)", nameof(privateKey));
        }

        try
        {
            _key = new EthECKey(cleanKey);
            _walletAddress = _key.GetPublicAddress().ToLower();
            _logger.LogInformation("HyperliquidSigner initialized for wallet: {WalletAddress}", _walletAddress);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid private key format", nameof(privateKey), ex);
        }
    }

    public string GetAddress() => _walletAddress;

    /// <summary>
    /// Signs an order action using EIP-712 L1 action signing.
    /// Follows Python SDK: sign_l1_action -> action_hash (msgpack) -> phantom agent -> EIP712
    /// </summary>
    public SignatureData SignOrderAction(OrderAction action, long nonce, bool isTestnet, string? vaultAddress = null)
    {
        // Step 1: Compute action hash (msgpack + nonce + vault flag)
        var actionHash = ComputeActionHash(action, nonce, vaultAddress);

        // Step 2: Construct phantom agent
        var phantomAgent = ConstructPhantomAgent(actionHash, isTestnet);

        // Step 3: Build EIP-712 typed data for Agent
        var typedData = BuildAgentTypedData(phantomAgent);

        // Step 4: Sign with EIP-712
        var signer = new Eip712TypedDataSigner();
        var signature = signer.SignTypedDataV4(typedData, _key);

        // Extract r, s, v from signature
        var signatureBytes = signature.HexToByteArray();
        var r = "0x" + signatureBytes.Take(32).ToArray().ToHex();
        var s = "0x" + signatureBytes.Skip(32).Take(32).ToArray().ToHex();
        var v = signatureBytes[64];

        _logger.LogDebug("Signed order action: nonce={Nonce}, agent={Agent}, signature_v={V}",
            nonce, phantomAgent.Source, v);

        return new SignatureData
        {
            R = r,
            S = s,
            V = v
        };
    }

    /// <summary>
    /// Computes action hash using MessagePack serialization + nonce + vault flag.
    /// Matches Python SDK: msgpack.packb(action) + nonce.to_bytes(8, 'big') + vault_flag
    /// </summary>
    private byte[] ComputeActionHash(OrderAction action, long nonce, string? vaultAddress)
    {
        var actionBytes = SerializeActionForHashing(action);
        var nonceBytes = BitConverter.GetBytes(nonce);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(nonceBytes); // Convert to big-endian
        }

        var vaultBytes = vaultAddress == null ? new byte[] { 0x00 } : CreateVaultBytes(vaultAddress);

        // Concatenate: action + nonce + vault
        var data = actionBytes.Concat(nonceBytes).Concat(vaultBytes).ToArray();

        // Keccak256 hash
        var sha3 = new Sha3Keccack();
        return sha3.CalculateHash(data);
    }

    /// <summary>
    /// Serializes OrderAction to MessagePack bytes for hashing.
    /// Uses MessagePack to match Python SDK msgpack.packb behavior.
    /// </summary>
    private byte[] SerializeActionForHashing(OrderAction action)
    {
        // Convert to MessagePack-compatible dictionary structure
        // Must match Python SDK structure exactly
        var actionDict = new Dictionary<string, object>
        {
            ["type"] = action.Type,
            ["orders"] = action.Orders.Select(o => new Dictionary<string, object>
            {
                ["a"] = o.Asset,
                ["b"] = o.IsBuy,
                ["p"] = o.LimitPx,
                ["s"] = o.Size,
                ["r"] = o.ReduceOnly,
                ["t"] = SerializeOrderType(o.OrderType)
            }).ToList(),
            ["grouping"] = action.Grouping
        };

        return MessagePackSerializer.Serialize(actionDict, MessagePackSerializerOptions.Standard);
    }

    private Dictionary<string, object> SerializeOrderType(OrderTypeWire orderType)
    {
        var result = new Dictionary<string, object>();

        if (orderType.Limit != null)
        {
            result["limit"] = new Dictionary<string, object>
            {
                ["tif"] = orderType.Limit.Tif
            };
        }

        if (orderType.Trigger != null)
        {
            result["trigger"] = new Dictionary<string, object>
            {
                ["triggerPx"] = orderType.Trigger.TriggerPx,
                ["isMarket"] = orderType.Trigger.IsMarket,
                ["tpsl"] = orderType.Trigger.Tpsl
            };
        }

        return result;
    }

    private byte[] CreateVaultBytes(string vaultAddress)
    {
        var cleanAddress = vaultAddress.StartsWith("0x") ? vaultAddress[2..] : vaultAddress;
        return new byte[] { 0x01 }.Concat(cleanAddress.HexToByteArray()).ToArray();
    }

    /// <summary>
    /// Constructs phantom agent object for L1 action signing.
    /// Python SDK: {"source": "a" if is_mainnet else "b", "connectionId": hash}
    /// </summary>
    private PhantomAgent ConstructPhantomAgent(byte[] actionHash, bool isTestnet)
    {
        return new PhantomAgent
        {
            Source = isTestnet ? "b" : "a",
            ConnectionId = "0x" + actionHash.ToHex()
        };
    }

    /// <summary>
    /// Builds EIP-712 TypedData for Agent signing.
    /// Domain: name="Exchange", version="1", chainId=1337, verifyingContract=0x0
    /// Primary type: Agent with [source (string), connectionId (bytes32)]
    /// </summary>
    private TypedData<Domain> BuildAgentTypedData(PhantomAgent agent)
    {
        var memberTypes = new MemberDescription[]
        {
            new() { Name = "source", Type = "string" },
            new() { Name = "connectionId", Type = "bytes32" }
        };

        var memberValues = new MemberValue[]
        {
            new() { TypeName = "string", Value = agent.Source },
            new() { TypeName = "bytes32", Value = agent.ConnectionId }
        };

        return new TypedData<Domain>
        {
            Domain = new Domain
            {
                Name = "Exchange",
                Version = "1",
                ChainId = 1337,
                VerifyingContract = "0x0000000000000000000000000000000000000000"
            },
            Types = new Dictionary<string, MemberDescription[]>
            {
                ["EIP712Domain"] = new[]
                {
                    new MemberDescription { Name = "name", Type = "string" },
                    new MemberDescription { Name = "version", Type = "string" },
                    new MemberDescription { Name = "chainId", Type = "uint256" },
                    new MemberDescription { Name = "verifyingContract", Type = "address" }
                },
                ["Agent"] = memberTypes
            },
            PrimaryType = "Agent",
            Message = memberValues
        };
    }

    private class PhantomAgent
    {
        public string Source { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }
}
