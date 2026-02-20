---
phase: 25-nuxt-deprecation
plan: 01
status: complete
completed: 2026-02-20
duration: < 1 minute
---

# Summary: Remove Nuxt Dashboard from Aspire Orchestration

## What Changed

Removed the Nuxt 4 dashboard `AddNodeApp` resource block from `TradingBot.AppHost/AppHost.cs`. Running `dotnet run` in AppHost no longer starts the Nuxt dev server.

## Files Modified

| File | Change |
|------|--------|
| `TradingBot.AppHost/AppHost.cs` | Deleted 8-line Nuxt dashboard NodeApp resource block (lines 64-71) |

## What Was Preserved

- `TradingBot.Dashboard/` directory and all its code remain intact for reference
- `dashboardApiKey` parameter declaration kept (line 52) -- still used by API service
- API service still receives `Dashboard__ApiKey` environment variable (line 62)

## Verification

- `dotnet build TradingBot.AppHost` -- 0 errors, build succeeded
- `grep "AddNodeApp" AppHost.cs` -- 0 matches (Nuxt resource fully removed)
- `grep "dashboardApiKey" AppHost.cs` -- 2 matches (parameter declaration + API service usage)
- `ls TradingBot.Dashboard/nuxt.config.ts` -- file exists (dashboard code preserved)

## Milestone Complete

This was the final plan of v3.0 Flutter Mobile milestone. All 6 phases (20-25) with 11 plans are now complete.
