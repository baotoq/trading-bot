# Phase 28 Plan 01 — Summary

**Completed:** 2026-02-20
**Duration:** Single pass, zero errors

## What Was Built

Added SourcePurchaseId tracking for idempotent DCA import, FixedDeposit update capability, event-driven auto-import handler, and historical purchase migration service:

1. **AssetTransaction.SourcePurchaseId** — Nullable `PurchaseId?` property for tracking which Purchase a transaction originated from. Unique filtered index on non-null values ensures idempotent imports. `Create` factory method accepts optional `sourcePurchaseId` parameter.

2. **PortfolioAsset.AddTransaction overload** — Extended with optional `PurchaseId? sourcePurchaseId` parameter, passing through to `AssetTransaction.Create`. Existing callers unaffected (default `null`).

3. **FixedDeposit.Update method** — Full update of all fields (bankName, principal, rate, dates, compounding frequency) with same validation guards as `Create` (bankName not null/whitespace, maturityDate > startDate, rate in (0,1]).

4. **PortfolioPurchaseCompletedEventHandler** — MediatR `INotificationHandler<PurchaseCompletedEvent>` that auto-imports DCA purchases into portfolio:
   - Finds BTC PortfolioAsset (Crypto type); silently skips with warning if none exists
   - Checks idempotency via SourcePurchaseId before importing
   - Loads full Purchase entity for all fields (date, quantity, price)
   - Wrapped in try/catch for graceful degradation (portfolio import failure does not crash DCA bot)

5. **HistoricalPurchaseMigrator** — Scoped service that bulk-imports all filled Purchases into AssetTransactions:
   - Loads BTC asset with transactions, builds HashSet of already-imported PurchaseIds
   - Filters out already-imported purchases, creates transactions for remainder
   - Returns count of migrated records; idempotent and re-runnable
   - Registered as `AddScoped<HistoricalPurchaseMigrator>()` in DI

6. **EF Migration** — `AddSourcePurchaseIdToAssetTransaction` adds nullable uuid column with unique filtered index (`"SourcePurchaseId" IS NOT NULL`).

## Files Created

| File | Purpose |
|------|---------|
| `Application/Handlers/PortfolioImportHandler.cs` | Event handler for auto-importing DCA purchases into portfolio |
| `Application/Services/HistoricalPurchaseMigrator.cs` | Bulk migration of existing purchases into portfolio transactions |
| `Infrastructure/Data/Migrations/*_AddSourcePurchaseIdToAssetTransaction.cs` | EF migration for SourcePurchaseId column + index |

## Files Modified

| File | Change |
|------|--------|
| `Models/AssetTransaction.cs` | Added `SourcePurchaseId` property and `sourcePurchaseId` parameter to `Create` |
| `Models/PortfolioAsset.cs` | Added `sourcePurchaseId` parameter to `AddTransaction` |
| `Models/FixedDeposit.cs` | Added `Update` method with validation |
| `Infrastructure/Data/TradingBotDbContext.cs` | Added SourcePurchaseId config with unique filtered index |
| `Program.cs` | Registered `HistoricalPurchaseMigrator` as scoped service |

## Verification

- `dotnet build TradingBot.slnx` — 0 errors
- `dotnet test` — all 76 existing tests pass
- Migration file generates correct nullable uuid column + unique filtered index
- PortfolioPurchaseCompletedEventHandler auto-discovered by MediatR assembly scanning

## Decisions Made

- SourcePurchaseId uses optional parameter (default null) rather than separate overload to keep API surface minimal
- HistoricalPurchaseMigrator loads all transactions in memory for HashSet lookup — acceptable for expected data volume
- Auto-import handler uses try/catch at top level — portfolio import is non-critical and must not disrupt DCA execution
