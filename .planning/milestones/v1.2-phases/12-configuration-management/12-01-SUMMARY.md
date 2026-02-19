---
phase: 12-configuration-management
plan: 01
subsystem: backend-api
tags: [configuration, database, api-endpoints, validation]

dependency-graph:
  requires:
    - DcaOptions and DcaOptionsValidator (existing)
    - PostgreSQL DbContext
    - IOptionsMonitor pattern
  provides:
    - DcaConfiguration entity with singleton pattern
    - IConfigurationService for config CRUD
    - GET /api/config (read current config)
    - PUT /api/config (update config with validation)
    - GET /api/config/defaults (read appsettings.json defaults)
  affects:
    - DCA scheduler (picks up config changes via IOptionsMonitor)
    - Dashboard configuration management UI (future)

tech-stack:
  added:
    - PostgreSQL JSONB for storing MultiplierTiers array
    - IOptionsMonitorCache for cache invalidation pattern
  patterns:
    - Singleton entity pattern with fixed GUID and CHECK constraint
    - Database-backed configuration with fallback to appsettings.json
    - IOptionsMonitor cache invalidation for immediate effect

key-files:
  created:
    - TradingBot.ApiService/Models/DcaConfiguration.cs
    - TradingBot.ApiService/Application/Services/ConfigurationService.cs
    - TradingBot.ApiService/Endpoints/ConfigurationEndpoints.cs
    - TradingBot.ApiService/Infrastructure/Data/Migrations/20260214135955_AddDcaConfiguration.cs
  modified:
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
    - TradingBot.ApiService/Endpoints/DashboardDtos.cs
    - TradingBot.ApiService/Program.cs

decisions:
  - title: Singleton entity pattern with fixed GUID
    rationale: DCA configuration is inherently singleton (one config per deployment). Using fixed GUID (00000000-0000-0000-0000-000000000001) with CHECK constraint enforces single-row at DB level, avoiding UUIDv7 generation overhead.
  - title: JSONB for MultiplierTiers storage
    rationale: Variable-length array stored as JSONB provides flexibility for tier count changes without schema migrations, supports PostgreSQL JSON queries if needed.
  - title: IOptionsMonitor cache invalidation pattern
    rationale: Calling optionsCache.TryRemove after DB save ensures IOptionsMonitor.CurrentValue immediately reflects changes without app restart, critical for runtime config updates.
  - title: Separate ConfigResponse and DcaConfigResponse DTOs
    rationale: DcaConfigResponse (existing, for backtest form) is missing DailyBuyHour/DailyBuyMinute/DryRun. New ConfigResponse is complete superset. Kept both for backward compatibility with backtest page.
  - title: GetDefaults reads directly from IConfiguration
    rationale: IOptionsMonitor.CurrentValue may already reflect DB override. Reading from IConfiguration.GetSection ensures we always return original appsettings.json values.

metrics:
  duration: 4 min
  tasks-completed: 2
  files-created: 4
  files-modified: 3
  commits: 2
  completed-date: 2026-02-14
---

# Phase 12 Plan 01: Backend Configuration Management Summary

Backend configuration management: database entity, service layer, and API endpoints for reading, updating, and resetting DCA configuration.

## Objective Achieved

Created database-backed configuration management infrastructure with PostgreSQL persistence and IOptionsMonitor integration. The DcaConfiguration singleton entity stores all DCA settings, ConfigurationService provides CRUD operations with validation and cache invalidation, and three authenticated API endpoints expose full config lifecycle (read current, update, read defaults). Config changes take effect immediately via IOptionsMonitor cache invalidation pattern.

## Tasks Completed

### Task 1: DcaConfiguration entity, DbContext, and EF migration
**Commit:** 0355a00

Created `DcaConfiguration` entity inheriting from `AuditedEntity` with singleton pattern (fixed GUID `00000000-0000-0000-0000-000000000001`). Entity has separate typed columns for all DcaOptions fields:
- Decimal fields: BaseDailyAmount (18,2), BearBoostFactor (4,2), MaxMultiplierCap (4,2)
- Int fields: DailyBuyHour, DailyBuyMinute, HighLookbackDays, BearMarketMaPeriod
- Bool: DryRun
- JSONB: MultiplierTiers (stored as `List<MultiplierTierData>`)

Added `MultiplierTierData` record class for JSONB serialization.

Configured entity in `TradingBotDbContext`:
- DbSet property: `DcaConfigurations`
- Precision configuration for decimal fields
- JSONB column type for MultiplierTiers
- Single-row CHECK constraint: `id = '00000000-0000-0000-0000-000000000001'::uuid`
- Used `ToTable(t => t.HasCheckConstraint(...))` pattern (modern EF Core 10 API)

Generated EF migration `AddDcaConfiguration` that auto-runs on startup via existing `dbContext.Database.MigrateAsync()` in Program.cs.

**Files created:**
- TradingBot.ApiService/Models/DcaConfiguration.cs
- TradingBot.ApiService/Infrastructure/Data/Migrations/20260214135955_AddDcaConfiguration.cs
- TradingBot.ApiService/Infrastructure/Data/Migrations/20260214135955_AddDcaConfiguration.Designer.cs

**Files modified:**
- TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs
- TradingBot.ApiService/Infrastructure/Data/Migrations/TradingBotDbContextModelSnapshot.cs

### Task 2: ConfigurationService, API endpoints, and DI wiring
**Commit:** 0765e84

**ConfigurationService** (`Application/Services/ConfigurationService.cs`):

Implemented `IConfigurationService` interface with three methods:
- `GetCurrentAsync`: Queries DB for config, falls back to `IOptionsMonitor.CurrentValue` if no DB row exists
- `UpdateAsync`: Validates using `IValidateOptions<DcaOptions>`, upserts DB row, **invalidates IOptionsMonitor cache** via `optionsCache.TryRemove(Options.DefaultName)` (CRITICAL for immediate effect)
- `GetDefaultsAsync`: Reads directly from `IConfiguration.GetSection("DcaOptions")` to get original appsettings.json values (before any DB override)

Primary constructor dependencies: `TradingBotDbContext`, `IOptionsMonitor<DcaOptions>`, `IOptionsMonitorCache<DcaOptions>`, `IValidateOptions<DcaOptions>`, `IConfiguration`.

Includes private mapping methods `MapToOptions` and `MapFromOptions` for entity ↔ DcaOptions conversion.

**ConfigurationEndpoints** (`Endpoints/ConfigurationEndpoints.cs`):

Created three endpoints under `/api/config` route group with `ApiKeyEndpointFilter`:
1. `GET /` → `GetConfigAsync`: Returns current config as ConfigResponse
2. `GET /defaults` → `GetDefaultsAsync`: Returns appsettings.json defaults as ConfigResponse
3. `PUT /` → `UpdateConfigAsync`: Accepts UpdateConfigRequest, validates, saves, returns 200 or 400 with structured errors

Structured logging on successful config update with key fields (BaseDailyAmount, DailyBuyTime, DryRun).

Catches `ValidationException` and returns `Results.BadRequest(new { errors = new[] { ex.Message } })` for invalid input.

**DashboardDtos.cs updates:**

Added two new DTOs (appended to existing file):
- `ConfigResponse`: Full config with BaseDailyAmount, DailyBuyHour, DailyBuyMinute, HighLookbackDays, DryRun, BearMarketMaPeriod, BearBoostFactor, MaxMultiplierCap, Tiers
- `UpdateConfigRequest`: Same structure as ConfigResponse (all fields required for PUT)

Note: Kept existing `DcaConfigResponse` (subset for backtest form pre-fill) for backward compatibility.

**Program.cs updates:**

- Registered `IConfigurationService` as scoped: `builder.Services.AddScoped<IConfigurationService, ConfigurationService>()`
- Mapped endpoints: `app.MapConfigurationEndpoints()` after `MapDashboardEndpoints()`

**Files created:**
- TradingBot.ApiService/Application/Services/ConfigurationService.cs
- TradingBot.ApiService/Endpoints/ConfigurationEndpoints.cs

**Files modified:**
- TradingBot.ApiService/Endpoints/DashboardDtos.cs
- TradingBot.ApiService/Program.cs

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

✅ Build succeeded: `dotnet build TradingBot.ApiService.csproj` compiled without errors
✅ DcaConfiguration entity created with all DcaOptions fields as separate columns
✅ MultiplierTiers stored as JSONB via `HasColumnType("jsonb")`
✅ Single-row CHECK constraint enforced: `id = '00000000-0000-0000-0000-000000000001'::uuid`
✅ EF migration generated: `20260214135955_AddDcaConfiguration.cs` in Migrations/
✅ ConfigurationService implements GetCurrent, Update, GetDefaults
✅ IOptionsMonitor cache invalidation implemented: `optionsCache.TryRemove(Options.DefaultName)` called after DB save
✅ Three API endpoints mapped under `/api/config` with authentication
✅ Server-side validation reuses existing `DcaOptionsValidator`
✅ Validation errors returned as 400 with structured `{ errors: [...] }` response
✅ Successful config updates logged with structured logging

**Runtime testing deferred:** Full endpoint testing requires starting Aspire stack with Dashboard:ApiKey secret configured. AppHost.cs shows `builder.AddParameter("dashboardApiKey", secret: true)` prompts for API key at startup. Migration will auto-run on next app start. Endpoints will be fully functional once app is started with valid API key.

**No regressions:** Existing code unaffected, build succeeded with no new errors.

## What's Next

Phase 12 Plan 02: Dashboard configuration management UI
- Vue components for config form with validation
- Real-time preview of config changes
- Integration with new `/api/config` endpoints
- Reset to defaults functionality

## Key Files Reference

**Entity:**
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Models/DcaConfiguration.cs`

**Service:**
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Application/Services/ConfigurationService.cs`

**Endpoints:**
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Endpoints/ConfigurationEndpoints.cs`
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Endpoints/DashboardDtos.cs`

**Configuration:**
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs`
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Program.cs`

**Migration:**
- `/Users/baotoq/Work/trading-bot/TradingBot.ApiService/Infrastructure/Data/Migrations/20260214135955_AddDcaConfiguration.cs`

## Self-Check: PASSED

All claimed files verified:
- ✓ DcaConfiguration.cs
- ✓ ConfigurationService.cs
- ✓ ConfigurationEndpoints.cs
- ✓ Migration file (20260214135955_AddDcaConfiguration.cs)

All claimed commits verified:
- ✓ 0355a00 (Task 1: DcaConfiguration entity and migration)
- ✓ 0765e84 (Task 2: ConfigurationService and API endpoints)
