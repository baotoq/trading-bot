---
phase: 09-infrastructure-aspire-integration
plan: 02
subsystem: infrastructure
tags: [aspire, nuxt, api-proxy, authentication, security]
completed: 2026-02-13
duration_minutes: 2

dependency_graph:
  requires:
    - 09-01 (Nuxt 4 frontend & Aspire integration)
    - TradingBot.ApiService/Endpoints pattern
  provides:
    - API key authentication for dashboard endpoints
    - CORS-free API proxy via Nuxt routeRules
    - Server-to-server authentication pattern
  affects:
    - TradingBot.AppHost/AppHost.cs (dashboardApiKey parameter)
    - TradingBot.ApiService/Program.cs (DashboardEndpoints registration)
    - dashboard/nuxt.config.ts (runtimeConfig and routeRules)

tech_stack:
  added:
    - Aspire parameter secrets (dashboardApiKey)
    - Nuxt runtimeConfig for environment injection
    - Nuxt routeRules for API proxying
    - Nuxt server utils for reusable auth
  patterns:
    - Endpoint filter pattern for API key validation
    - Environment variable injection from Aspire to services
    - Server-to-server authentication (Nuxt → .NET)
    - API proxy pattern for CORS-free development

key_files:
  created:
    - TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
    - dashboard/server/utils/auth.ts
    - dashboard/server/api/portfolio.get.ts
  modified:
    - TradingBot.ApiService/Program.cs
    - TradingBot.AppHost/AppHost.cs
    - dashboard/nuxt.config.ts

decisions:
  - name: "Use /proxy/api/** prefix instead of /api/**"
    rationale: "Nuxt's own server API routes are served at /api/, so using /proxy/api/** prefix avoids routing conflicts"
    impact: "Frontend calls /proxy/api/dashboard/portfolio which proxies to backend's /api/dashboard/portfolio"
    alternatives: "Could use different prefix like /backend/** but /proxy/** is more semantically clear"

  - name: "Portfolio server route does NOT use requireApiKey for client requests"
    rationale: "Server route is server-to-server call to .NET backend. API key is used when Nuxt server calls .NET API. Browser clients call Nuxt server without API key since Nuxt server is trusted."
    impact: "Auth happens at the server-to-server boundary, not at the browser-to-Nuxt boundary"
    alternatives: "Could add auth for browser clients but that would require client-side credential management"

  - name: "Return 401 for missing key, 403 for invalid key"
    rationale: "HTTP standards: 401 = authentication required, 403 = authenticated but forbidden"
    impact: "Clearer error responses for debugging and client error handling"
    alternatives: "Could return 403 for both but 401/403 distinction is more accurate"

metrics:
  tasks_completed: 2
  files_created: 3
  files_modified: 3
  commits: 2
  build_verification: passed
---

# Phase 09 Plan 02: API Proxy & Authentication Summary

**One-liner:** Implemented API key authentication with endpoint filter on .NET backend, configured Nuxt API proxy for CORS-free development, and established server-to-server auth pattern.

## What Was Built

Added secure authentication layer between the Nuxt dashboard and .NET API service using API key validation, and configured API proxy routing to eliminate CORS issues during development.

**Backend Authentication:**
- Created `DashboardEndpoints.cs` with `ApiKeyEndpointFilter` for API key validation
- Filter checks `x-api-key` header against `Dashboard:ApiKey` configuration
- Returns 401 if key missing, 403 if invalid, allows request if valid
- Placeholder `/api/dashboard/portfolio` endpoint returns purchase count
- Aspire passes `dashboardApiKey` secret parameter to both services

**Frontend Proxy & Auth:**
- Configured Nuxt `runtimeConfig` with server-only `apiKey` and public `apiEndpoint`
- Added `routeRules` to proxy `/proxy/api/**` → backend `/api/**` (CORS-free)
- Created `server/utils/auth.ts` utility for reusable API key validation
- Created `server/api/portfolio.get.ts` demonstrating server-to-server auth pattern
- Nuxt server calls .NET backend with API key, proxies data to browser client

## Task Breakdown

### Task 1: Add API key auth to .NET backend and create dashboard endpoints
**Commit:** `ba39ae1` - feat(09-02): add API key auth to .NET backend and create dashboard endpoints

Created secure dashboard endpoints on the .NET backend:
- **DashboardEndpoints.cs**: New endpoint class with `ApiKeyEndpointFilter` implementing `IEndpointFilter`
- **ApiKeyEndpointFilter**: Validates `x-api-key` header against `Dashboard:ApiKey` config value
- **Error responses**: 500 if API key not configured, 401 if missing, 403 if invalid
- **Portfolio endpoint**: Placeholder GET `/api/dashboard/portfolio` returning purchase count
- **Program.cs**: Registered `MapDashboardEndpoints()` after existing endpoint mappings
- **AppHost.cs**: Added `dashboardApiKey` parameter marked as secret
- **Environment injection**: `Dashboard__ApiKey` → API service, `NUXT_API_KEY` → dashboard
- **NUXT_PUBLIC_API_ENDPOINT**: Passes API service endpoint to Nuxt via environment
- **User secrets**: Set dev API key "dev-dashboard-key-change-in-production" in AppHost secrets

**Files created:** 1 (DashboardEndpoints.cs)
**Files modified:** 2 (Program.cs, AppHost.cs)

**Verification:**
- Built TradingBot.ApiService successfully
- Built TradingBot.AppHost successfully
- Confirmed `x-api-key` validation in DashboardEndpoints.cs
- Confirmed `MapDashboardEndpoints` registration in Program.cs
- Confirmed `dashboardApiKey` parameter and environment injection in AppHost.cs

### Task 2: Configure Nuxt API proxy and server-side API key validation
**Commit:** `8e23ff2` - feat(09-02): configure Nuxt API proxy and server-side API key validation

Configured Nuxt for CORS-free API access and server-side authentication:
- **nuxt.config.ts runtimeConfig**: Added `apiKey` (server-only) and `public.apiEndpoint`
- **nuxt.config.ts routeRules**: Proxy `/proxy/api/**` to `${NUXT_PUBLIC_API_ENDPOINT}/api/**`
- **server/utils/auth.ts**: Created `requireApiKey(event)` utility function
- **Auth utility behavior**: Throws 401 if header missing, 403 if key doesn't match config
- **server/api/portfolio.get.ts**: Example server route fetching from .NET backend
- **Server-to-server pattern**: Nuxt server calls backend with `x-api-key` header from `config.apiKey`
- **Error handling**: Catches fetch errors, returns appropriate status codes (502 for backend down)
- **TypeScript verification**: Ran `npx nuxi prepare` successfully

**Files created:** 2 (server/utils/auth.ts, server/api/portfolio.get.ts)
**Files modified:** 1 (nuxt.config.ts)

**Verification:**
- `npx nuxi prepare` completed without type errors
- Confirmed `runtimeConfig` with `apiKey` and `public.apiEndpoint` in nuxt.config.ts
- Confirmed `routeRules` proxy configuration in nuxt.config.ts
- Confirmed `requireApiKey` export in server/utils/auth.ts
- Confirmed `x-api-key` header in portfolio server route

## Deviations from Plan

None - plan executed exactly as written.

## Verification Results

All verification checks passed:

1. ✅ `dotnet build TradingBot.AppHost` compiles successfully
2. ✅ `dotnet build TradingBot.ApiService` compiles with DashboardEndpoints
3. ✅ `dashboard/nuxt.config.ts` has runtimeConfig with apiKey (server-only) and public.apiEndpoint
4. ✅ `dashboard/nuxt.config.ts` has routeRules proxy for `/proxy/api/**`
5. ✅ `dashboard/server/utils/auth.ts` exports requireApiKey that throws 401/403
6. ✅ `dashboard/server/api/portfolio.get.ts` calls .NET backend with x-api-key header
7. ✅ `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` validates x-api-key via endpoint filter
8. ✅ `TradingBot.AppHost/AppHost.cs` passes NUXT_API_KEY and NUXT_PUBLIC_API_ENDPOINT env vars
9. ✅ `TradingBot.AppHost/AppHost.cs` passes Dashboard__ApiKey to API service

## Authentication Flow

**Successful Request Flow:**
1. Browser → `GET /api/portfolio` → Nuxt server
2. Nuxt server → `GET ${NUXT_PUBLIC_API_ENDPOINT}/api/dashboard/portfolio` with `x-api-key: ${NUXT_API_KEY}` → .NET API
3. .NET ApiKeyEndpointFilter → validates header → allows request
4. .NET endpoint → returns portfolio data
5. Nuxt server → proxies response → Browser

**Failed Auth Flow (Missing Key):**
1. Direct request to .NET API without header → ApiKeyEndpointFilter → 401 Unauthorized

**Failed Auth Flow (Invalid Key):**
1. Direct request to .NET API with wrong key → ApiKeyEndpointFilter → 403 Forbidden

**Development Configuration:**
- Aspire sets `NUXT_API_KEY=dev-dashboard-key-change-in-production`
- Aspire sets `Dashboard:ApiKey=dev-dashboard-key-change-in-production`
- Both services receive the same secret, enabling server-to-server auth

## What's Next

**Immediate next steps (Plan 09-03 if exists, or Phase 10):**
- Build actual dashboard UI using Nuxt UI components
- Implement real portfolio data aggregation in backend
- Add more dashboard endpoints (purchases, stats, charts)
- Test full Aspire orchestration with running services

**Downstream dependencies:**
- Phase 10: Dashboard UI implementation (can now securely fetch data)
- Phase 11: API integration (auth pattern established and reusable)

## Self-Check: PASSED

Verified all claims in this summary:

**Files created:**
```bash
✅ TradingBot.ApiService/Endpoints/DashboardEndpoints.cs exists
✅ dashboard/server/utils/auth.ts exists
✅ dashboard/server/api/portfolio.get.ts exists
```

**Files modified:**
```bash
✅ TradingBot.ApiService/Program.cs contains MapDashboardEndpoints
✅ TradingBot.AppHost/AppHost.cs contains dashboardApiKey
✅ dashboard/nuxt.config.ts contains runtimeConfig and routeRules
```

**Commits exist:**
```bash
✅ ba39ae1 - feat(09-02): add API key auth to .NET backend and create dashboard endpoints
✅ 8e23ff2 - feat(09-02): configure Nuxt API proxy and server-side API key validation
```

**Build verification:**
```bash
✅ dotnet build TradingBot.AppHost - Build succeeded
✅ dotnet build TradingBot.ApiService - Build succeeded
✅ npx nuxi prepare - Types generated successfully
```

All verifications passed. Summary claims are accurate.
