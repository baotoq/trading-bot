# Phase 1: Foundation & Hyperliquid Client - Context

**Gathered:** 2026-02-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Working Hyperliquid API client that can fetch prices, get balances, and place spot orders on testnet. Domain models and persistence layer ready. Configuration system in place. Distributed locking operational.

</domain>

<decisions>
## Implementation Decisions

### Persistence & data model
- PostgreSQL as the database
- .NET Aspire for local dev orchestration (Postgres container managed by Aspire) — already set up in repo
- EF Core with auto-migrate on startup (apply pending migrations when app starts)
- PostgreSQL advisory locks for distributed locking — no Redis needed
- Store full order details including raw API response data alongside parsed summary (for debugging/auditing)
- Daily price data fetched fresh from API each time (not persisted)
- Separate databases for testnet vs mainnet (different connection strings per environment)
- DbContext injected directly into services (no repository pattern)

### Configuration design
- User secrets for local dev + environment variables for production (private key handling)
- appsettings.json with IOptionsMonitor for hot reload of DCA parameters
- All schedules operate in UTC
- Config toggle for testnet/mainnet (IsTestnet flag switches API endpoints) — single binary, two modes

### Error handling & resilience
- Polly v8 via Microsoft.Extensions.Http.Resilience (HttpClientFactory integration) for retries and circuit breaker
- Built-in ILogger (Microsoft.Extensions.Logging) — works with Aspire dashboard
- When API is completely unreachable after all retries: schedule retry later same day (1-hour intervals), keep trying before giving up
- Hard balance check before placing orders — verify USDC balance >= order amount, fail gracefully if insufficient

### Project structure
- Single project with folder-based organization (Models, Services, Infrastructure, etc.)
- ASP.NET Core host with BackgroundService — gets health check endpoints for free (useful for Phase 4)
- No test project for now — add later when core logic stabilizes
- .NET 10 target framework
- Aspire AppHost and ServiceDefaults already exist in the repo

### Claude's Discretion
- Exact folder organization within the single project
- EF Core entity configuration details
- Polly retry/circuit breaker policy parameters
- Retry scheduling mechanism for same-day retries
- HTTP client configuration details

</decisions>

<specifics>
## Specific Ideas

- Aspire is already set up in the repo — build on existing AppHost/ServiceDefaults structure
- Bot should work as both API host (health checks) and background worker (DCA scheduler) in one process

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-foundation-hyperliquid-client*
*Context gathered: 2026-02-12*
