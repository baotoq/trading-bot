# Phase 12: Configuration Management - Research

**Researched:** 2026-02-14
**Domain:** Runtime configuration management with database persistence, .NET options pattern, and Vue form validation
**Confidence:** HIGH

## Summary

This phase adds user-editable DCA configuration to the dashboard with database persistence and server-side validation. The key technical challenge is bridging database-stored configuration with the existing IOptionsMonitor pattern while ensuring changes take effect immediately without restart.

The architecture uses a new DcaConfiguration entity in PostgreSQL (separate columns, not JSON), a custom configuration service to bridge DB and IOptionsMonitor, and Nuxt UI v4 form components with zod validation for the frontend. Manual cache invalidation via IOptionsMonitorCache is required after updates to trigger IOptionsMonitor.CurrentValue refresh.

**Primary recommendation:** Store configuration in a single-row PostgreSQL table with separate typed columns (not JSON), implement a scoped configuration service that reads from DB, and use IOptionsMonitorCache.TryRemove to invalidate cache after updates. Frontend uses UForm with zod schema for real-time validation.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Config layout & grouping:**
- Sectioned single page — all settings visible on one scrollable page, grouped into visual sections
- 3 sections: **Core DCA** (BaseDailyAmount, DailyBuyHour/Minute, HighLookbackDays, DryRun) | **Multiplier Tiers** (the tier table) | **Bear Market** (BearMarketMaPeriod, BearBoostFactor, MaxMultiplierCap)
- Inline descriptions below each field explaining what it does
- Config is a new tab on the existing dashboard page (alongside existing dashboard content), not a separate /settings route

**Editing & save flow:**
- View-then-edit pattern: show config as read-only first, click "Edit" button to enable fields, Save/Cancel to finish
- Single save button at the bottom that saves all changed settings across all sections at once
- Confirmation dialog only for critical fields (BaseDailyAmount, schedule time, DryRun toggle)
- Reset to defaults button available — restores all settings to their default values from appsettings.json

**Multiplier tiers editing:**
- Editable table with columns: Drop %, Multiplier. Each row is editable with Add Row / Remove Row buttons
- Auto-sort by DropPercentage ascending — no manual drag reordering. Backend validates sort order
- Maximum 5 tiers — keeps the strategy manageable and UI clean
- Real-time inline validation — red border and error message on invalid fields as user types

**Apply behavior & feedback:**
- Config persisted to PostgreSQL database (not appsettings.json file) — API reads from DB on each DCA cycle
- Changes take effect immediately on save — next DCA cycle uses the new config, no restart needed
- Toast notification on successful save ("Configuration saved successfully")
- No config change history or audit log — keep it simple for v1.2

### Claude's Discretion

- Exact field component choices (number input, time picker, toggle for DryRun)
- Backend API endpoint design (GET/PUT for config CRUD)
- Database schema for config storage (single row, key-value, or JSON column)
- Loading and error states for the config page
- Exact validation error message wording
- How to source "default values" for the reset button

### Specific Ideas

- Existing DcaOptionsValidator already has comprehensive validation rules — reuse the same logic server-side
- IOptionsMonitor pattern currently used for config — need to bridge DB storage with options pattern or replace reads
- Schedule time is two fields (hour + minute) — could be presented as a single time picker

</user_constraints>

## Standard Stack

### Backend (.NET 10)

| Library/Pattern | Version | Purpose | Why Standard |
|----------------|---------|---------|--------------|
| EF Core | 10.x | PostgreSQL entity for config | Already used for all DB access |
| IOptionsMonitor | Built-in | Current config consumption | Already injected in 9+ places |
| IOptionsMonitorCache | Built-in | Manual cache invalidation | Required to refresh IOptionsMonitor |
| IValidateOptions | Built-in | Existing validation rules | DcaOptionsValidator already exists |
| Minimal APIs | Built-in | RESTful endpoints | Consistent with existing endpoints |

### Frontend (Nuxt 4)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| @nuxt/ui | v4 | Form components (UInput, USelect, USwitch, UForm) | Already used in dashboard |
| zod | 3.24+ | Schema validation | Nuxt UI v4 standard-schema support |
| $fetch | Built-in | API calls | Consistent with existing composables |
| useToast | Built-in | Success/error notifications | Already used in backtest page |

**Installation:** No new dependencies needed — all libraries already installed.

## Architecture Patterns

### Recommended Database Schema

```sql
-- Single row configuration table
CREATE TABLE dca_configurations (
    id UUID PRIMARY KEY,

    -- Core DCA fields
    base_daily_amount DECIMAL(18, 2) NOT NULL,
    daily_buy_hour INTEGER NOT NULL,
    daily_buy_minute INTEGER NOT NULL,
    high_lookback_days INTEGER NOT NULL,
    dry_run BOOLEAN NOT NULL DEFAULT false,

    -- Bear market fields
    bear_market_ma_period INTEGER NOT NULL,
    bear_boost_factor DECIMAL(4, 2) NOT NULL,
    max_multiplier_cap DECIMAL(4, 2) NOT NULL,

    -- Multiplier tiers (JSON array due to variable length)
    multiplier_tiers JSONB NOT NULL,

    -- Audit fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ,

    -- Ensure only one row exists
    CONSTRAINT single_row_only CHECK (id = '00000000-0000-0000-0000-000000000001')
);

-- Insert default configuration
INSERT INTO dca_configurations (id, base_daily_amount, daily_buy_hour, daily_buy_minute,
    high_lookback_days, dry_run, bear_market_ma_period, bear_boost_factor, max_multiplier_cap,
    multiplier_tiers, created_at)
VALUES (
    '00000000-0000-0000-0000-000000000001',
    10.0, 14, 0, 30, true, 200, 1.5, 4.5,
    '[{"DropPercentage": 5, "Multiplier": 1.5}, {"DropPercentage": 10, "Multiplier": 2.0}, {"DropPercentage": 20, "Multiplier": 3.0}]'::jsonb,
    now()
);
```

**Design rationale:**
- Separate columns for all config fields (not full JSON) — better type safety, queryability, and performance
- Exception: `multiplier_tiers` as JSONB because it's a variable-length array (0-5 items)
- Single-row constraint via CHECK on ID — configuration is a singleton
- Fixed UUID prevents accidental multi-row inserts
- No BaseEntity inheritance — single row doesn't need UUIDv7 generation

### Pattern 1: Database Configuration Service

**What:** Scoped service that reads configuration from database and bridges to IOptionsMonitor
**When to use:** When configuration must be persisted and reloaded without restart

```csharp
// Source: Based on IOptionsMonitor pattern (Microsoft Learn) + manual cache invalidation
// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options

public interface IConfigurationService
{
    Task<DcaOptions> GetCurrentAsync(CancellationToken ct = default);
    Task UpdateAsync(DcaOptions options, CancellationToken ct = default);
    Task<DcaOptions> GetDefaultsAsync(CancellationToken ct = default);
}

public class ConfigurationService(
    TradingBotDbContext db,
    IOptionsMonitor<DcaOptions> defaultOptions,
    IOptionsMonitorCache<DcaOptions> optionsCache,
    IValidateOptions<DcaOptions> validator) : IConfigurationService
{
    private const string CACHE_KEY = Options.DefaultName;

    public async Task<DcaOptions> GetCurrentAsync(CancellationToken ct = default)
    {
        var entity = await db.DcaConfigurations.FirstOrDefaultAsync(ct);
        if (entity == null)
        {
            // Fallback to appsettings.json if DB is empty
            return defaultOptions.CurrentValue;
        }
        return MapToOptions(entity);
    }

    public async Task UpdateAsync(DcaOptions options, CancellationToken ct = default)
    {
        // Validate before saving
        var result = validator.Validate(CACHE_KEY, options);
        if (result.Failed)
        {
            throw new ValidationException(string.Join("; ", result.Failures));
        }

        var entity = await db.DcaConfigurations.FirstOrDefaultAsync(ct);
        if (entity == null)
        {
            entity = new DcaConfiguration { Id = Guid.Parse("00000000-0000-0000-0000-000000000001") };
            db.DcaConfigurations.Add(entity);
        }

        MapFromOptions(entity, options);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        // CRITICAL: Invalidate IOptionsMonitor cache so CurrentValue reloads
        optionsCache.TryRemove(CACHE_KEY);
    }

    public Task<DcaOptions> GetDefaultsAsync(CancellationToken ct = default)
    {
        // Return defaults from appsettings.json
        return Task.FromResult(defaultOptions.CurrentValue);
    }

    private DcaOptions MapToOptions(DcaConfiguration entity) { /* ... */ }
    private void MapFromOptions(DcaConfiguration entity, DcaOptions options) { /* ... */ }
}
```

**CRITICAL:** After updating database, must call `optionsCache.TryRemove(Options.DefaultName)` to invalidate cache. Without this, `IOptionsMonitor.CurrentValue` continues returning stale data.

### Pattern 2: Database-Backed Configuration Provider (ALTERNATIVE)

**What:** Custom IConfigurationProvider that loads from database, registered with IConfigurationBuilder
**When to use:** When you want IOptionsMonitor.OnChange to fire automatically on updates

```csharp
// Source: .NET Configuration Provider pattern
// NOTE: This approach is more complex and requires thread-safe reload token management

public class DatabaseConfigurationProvider : ConfigurationProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseConfigurationProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override void Load()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();

        var config = db.DcaConfigurations.FirstOrDefault();
        if (config != null)
        {
            Data["DcaOptions:BaseDailyAmount"] = config.BaseDailyAmount.ToString();
            Data["DcaOptions:DailyBuyHour"] = config.DailyBuyHour.ToString();
            // ... map all fields
        }
    }

    public void TriggerReload()
    {
        // Called after database update to reload configuration
        OnReload();
    }
}
```

**Tradeoff:** More elegant (IOptionsMonitor.OnChange fires automatically) but significantly more complex. Requires managing configuration provider lifecycle, thread safety, and reload tokens. **Not recommended for this phase.**

### Pattern 3: Vue Form with Zod Validation

**What:** Nuxt UI v4 UForm with zod schema for real-time inline validation
**When to use:** When you need client-side validation before submitting to server

```vue
<!-- Source: Nuxt UI Form component documentation -->
<!-- https://ui.nuxt.com/docs/components/form -->

<template>
  <UForm :schema="configSchema" :state="formState" @submit="onSubmit">
    <UCard>
      <template #header>
        <div class="flex justify-between items-center">
          <h3>Core DCA Settings</h3>
          <UButton v-if="!isEditing" @click="isEditing = true">Edit</UButton>
        </div>
      </template>

      <div class="space-y-4">
        <!-- Base Daily Amount -->
        <UFormField label="Base Daily Amount" name="baseDailyAmount" help="Amount to invest daily in USD">
          <UInput
            v-model.number="formState.baseDailyAmount"
            type="number"
            :disabled="!isEditing"
            step="0.01"
          />
        </UFormField>

        <!-- Schedule Time -->
        <UFormField label="Daily Buy Time (UTC)" name="scheduleTime" help="Hour and minute for daily purchase">
          <div class="grid grid-cols-2 gap-2">
            <UInput
              v-model.number="formState.dailyBuyHour"
              type="number"
              :disabled="!isEditing"
              placeholder="Hour (0-23)"
            />
            <UInput
              v-model.number="formState.dailyBuyMinute"
              type="number"
              :disabled="!isEditing"
              placeholder="Minute (0-59)"
            />
          </div>
        </UFormField>

        <!-- Dry Run Toggle -->
        <UFormField label="Dry Run" name="dryRun" help="If enabled, no real trades will be executed">
          <USwitch v-model="formState.dryRun" :disabled="!isEditing" />
        </UFormField>
      </div>

      <template #footer v-if="isEditing">
        <div class="flex justify-end gap-2">
          <UButton variant="soft" @click="onCancel">Cancel</UButton>
          <UButton variant="soft" @click="onResetDefaults">Reset to Defaults</UButton>
          <UButton type="submit">Save Configuration</UButton>
        </div>
      </template>
    </UCard>
  </UForm>
</template>

<script setup lang="ts">
import { z } from 'zod'

const configSchema = z.object({
  baseDailyAmount: z.number().positive('Must be greater than 0'),
  dailyBuyHour: z.number().int().min(0).max(23, 'Hour must be 0-23'),
  dailyBuyMinute: z.number().int().min(0).max(59, 'Minute must be 0-59'),
  highLookbackDays: z.number().int().positive(),
  dryRun: z.boolean(),
  bearMarketMaPeriod: z.number().int().positive(),
  bearBoostFactor: z.number().positive(),
  maxMultiplierCap: z.number().min(1.0, 'Must be at least 1.0'),
  tiers: z.array(z.object({
    dropPercentage: z.number().nonnegative(),
    multiplier: z.number().positive()
  })).max(5, 'Maximum 5 tiers allowed')
})

const formState = ref({
  baseDailyAmount: 10,
  dailyBuyHour: 14,
  dailyBuyMinute: 0,
  // ... other fields
  tiers: []
})

const isEditing = ref(false)

async function onSubmit() {
  // Confirmation dialog for critical fields
  const criticalFieldsChanged = /* check if BaseDailyAmount, schedule, or DryRun changed */
  if (criticalFieldsChanged) {
    const confirmed = await confirm('These changes affect live trading. Continue?')
    if (!confirmed) return
  }

  await $fetch('/api/config', {
    method: 'PUT',
    body: formState.value
  })

  toast.add({ title: 'Configuration saved successfully', color: 'green' })
  isEditing.value = false
}
</script>
```

**UForm features:**
- Automatic validation on submit, input, blur, or change events
- Errors displayed inline with red border and message
- Schema-driven validation (zod, valibot, yup supported)
- Disabled state for read-only view

### Pattern 4: Multiplier Tiers Editable Table

**What:** Dynamic array of tier rows with add/remove, auto-sorted by DropPercentage
**When to use:** When editing variable-length configuration arrays

```vue
<template>
  <div class="space-y-2">
    <div class="flex justify-between items-center">
      <label class="font-medium">Multiplier Tiers</label>
      <UButton
        size="sm"
        icon="i-lucide-plus"
        :disabled="!isEditing || formState.tiers.length >= 5"
        @click="addTier"
      >
        Add Tier
      </UButton>
    </div>

    <div class="border rounded">
      <table class="w-full">
        <thead class="bg-gray-50 dark:bg-gray-800">
          <tr>
            <th class="p-2 text-left">Drop %</th>
            <th class="p-2 text-left">Multiplier</th>
            <th class="p-2 w-10"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(tier, idx) in sortedTiers" :key="idx">
            <td class="p-2">
              <UInput
                v-model.number="tier.dropPercentage"
                type="number"
                :disabled="!isEditing"
                step="0.1"
                @update:model-value="sortTiers"
              />
            </td>
            <td class="p-2">
              <UInput
                v-model.number="tier.multiplier"
                type="number"
                :disabled="!isEditing"
                step="0.1"
              />
            </td>
            <td class="p-2">
              <UButton
                icon="i-lucide-trash-2"
                variant="ghost"
                size="sm"
                :disabled="!isEditing"
                @click="removeTier(idx)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p class="text-sm text-gray-500">
      Maximum 5 tiers. Tiers are auto-sorted by drop percentage.
    </p>
  </div>
</template>

<script setup lang="ts">
const formState = defineModel()

const sortedTiers = computed(() => {
  return [...formState.value.tiers].sort((a, b) => a.dropPercentage - b.dropPercentage)
})

function addTier() {
  if (formState.value.tiers.length >= 5) return
  formState.value.tiers.push({ dropPercentage: 0, multiplier: 1.0 })
}

function removeTier(index: number) {
  formState.value.tiers.splice(index, 1)
}

function sortTiers() {
  // Auto-sort whenever a drop percentage changes
  formState.value.tiers.sort((a, b) => a.dropPercentage - b.dropPercentage)
}
</script>
```

**Key behaviors:**
- Auto-sort on any `dropPercentage` change
- Add button disabled when 5 tiers reached or not editing
- Remove button for each row
- Backend validates sort order on save

### Anti-Patterns to Avoid

- **Storing full config as single JSON column:** Loses type safety, queryability, and indexing. Use separate columns for known fields.
- **File-based configuration for runtime updates:** appsettings.json changes require restart. User expects immediate effect.
- **IOptionsSnapshot for background services:** Snapshot is scoped per-request. Background services need IOptionsMonitor (singleton).
- **Not invalidating cache after DB update:** `IOptionsMonitor.CurrentValue` returns stale data without `optionsCache.TryRemove()`.
- **Duplicate validation logic:** Reuse `DcaOptionsValidator` server-side instead of writing new validation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Configuration provider | Custom DB-backed IConfigurationProvider | Scoped service + manual cache invalidation | IConfigurationProvider requires complex reload token management, thread safety, and lifecycle handling. Scoped service with cache invalidation is simpler. |
| Form validation schema | Manual field-by-field checks | Zod schema + UForm | Zod provides type-safe, composable validation. UForm handles error display automatically. |
| Confirmation dialogs | Custom modal components | Native confirm() or UModal | Built-in dialogs sufficient for simple yes/no. |
| Time picker | Custom hour/minute inputs | Two separate UInput (hour + minute) | No native time picker in Nuxt UI v4. Two inputs is standard pattern. |

**Key insight:** Don't build a custom configuration provider. The complexity of reload tokens, thread safety, and provider lifecycle far exceeds the benefit. Manual cache invalidation is simpler and sufficient.

## Common Pitfalls

### Pitfall 1: IOptionsMonitor Cache Not Invalidated After Update

**What goes wrong:** After saving configuration to database, DCA scheduler continues using old values because `IOptionsMonitor.CurrentValue` returns cached data.

**Why it happens:** IOptionsMonitor caches values and only reloads when a change token fires. Database updates don't trigger change tokens automatically — file-based providers do, but custom DB-backed scenarios don't.

**How to avoid:**
```csharp
// After SaveChangesAsync
await db.SaveChangesAsync(ct);

// CRITICAL: Invalidate cache
optionsCache.TryRemove(Options.DefaultName);
```

**Warning signs:** Configuration changes visible in database but not reflected in DCA execution. DcaSchedulerBackgroundService still using old `DailyBuyHour`.

### Pitfall 2: Frontend Validation Doesn't Match Backend Validation

**What goes wrong:** User bypasses frontend validation (e.g., via browser DevTools), submits invalid config, backend rejects it with confusing error.

**Why it happens:** Client-side validation (zod) and server-side validation (`DcaOptionsValidator`) implemented separately with different rules.

**How to avoid:**
1. Mirror ALL `DcaOptionsValidator` rules in zod schema
2. Backend ALWAYS validates with `IValidateOptions<DcaOptions>.Validate()` before saving
3. Return structured validation errors that frontend can display

```csharp
// Backend endpoint
public static async Task<IResult> UpdateConfig(
    UpdateConfigRequest request,
    IConfigurationService configService,
    CancellationToken ct)
{
    try
    {
        await configService.UpdateAsync(request.ToOptions(), ct);
        return Results.Ok();
    }
    catch (ValidationException ex)
    {
        // Return structured errors for frontend
        return Results.BadRequest(new { errors = ex.Errors });
    }
}
```

**Warning signs:** Users get generic "validation failed" errors instead of specific field messages.

### Pitfall 3: Single-Row Constraint Violation

**What goes wrong:** Multiple configuration rows accidentally created, causing non-deterministic behavior (which row gets loaded?).

**Why it happens:** Entity creation logic doesn't check for existing row, or migration doesn't include CHECK constraint.

**How to avoid:**
```sql
-- Add CHECK constraint in migration
CONSTRAINT single_row_only CHECK (id = '00000000-0000-0000-0000-000000000001')

-- Also enforce in entity
public class DcaConfiguration
{
    public Guid Id { get; init; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    // ... other properties
}
```

```csharp
// Service always updates existing row
var entity = await db.DcaConfigurations.FirstOrDefaultAsync(ct);
if (entity == null)
{
    entity = new DcaConfiguration(); // ID already set to fixed value
    db.DcaConfigurations.Add(entity);
}
// Update entity fields
await db.SaveChangesAsync(ct);
```

**Warning signs:** Query returns multiple rows. Configuration changes unpredictable.

### Pitfall 4: Default Values Out of Sync

**What goes wrong:** "Reset to Defaults" button restores values that don't match appsettings.json defaults.

**Why it happens:** Hardcoded defaults in frontend or service don't match appsettings.json.

**How to avoid:**
```csharp
public class ConfigurationService(
    IOptionsMonitor<DcaOptions> defaultOptions) // Injected from appsettings.json
{
    public Task<DcaOptions> GetDefaultsAsync()
    {
        // Source of truth: appsettings.json via IOptionsMonitor
        return Task.FromResult(defaultOptions.CurrentValue);
    }
}
```

```vue
// Frontend calls API to get defaults
async function onResetDefaults() {
  const defaults = await $fetch('/api/config/defaults')
  formState.value = defaults
}
```

**Warning signs:** Defaults differ between fresh install and "Reset to Defaults" action.

### Pitfall 5: Tiers Not Sorted on Save

**What goes wrong:** User edits tiers, frontend displays sorted, but backend saves unsorted. Validation fails.

**Why it happens:** Frontend sorts for display only (computed property) but doesn't sort the underlying array before submit.

**How to avoid:**
```vue
async function onSubmit() {
  // Sort tiers before submitting
  formState.value.tiers.sort((a, b) => a.dropPercentage - b.dropPercentage)

  await $fetch('/api/config', {
    method: 'PUT',
    body: formState.value
  })
}
```

Backend still validates sort order as defense-in-depth:
```csharp
// DcaOptionsValidator checks sort order
var sortedTiers = options.MultiplierTiers.OrderBy(t => t.DropPercentage).ToList();
if (!options.MultiplierTiers.SequenceEqual(sortedTiers, new MultiplierTierComparer()))
{
    errors.Add("MultiplierTiers must be sorted by DropPercentage in ascending order");
}
```

**Warning signs:** Validation error "must be sorted" even though frontend shows sorted data.

## Code Examples

Verified patterns from existing codebase and official documentation:

### Backend: Configuration Endpoints

```csharp
// Source: Existing DashboardEndpoints.cs pattern
// File: TradingBot.ApiService/Endpoints/ConfigurationEndpoints.cs

public static class ConfigurationEndpoints
{
    public static WebApplication MapConfigurationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/config")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/", GetConfigAsync);
        group.MapGet("/defaults", GetDefaultsAsync);
        group.MapPut("/", UpdateConfigAsync);

        return app;
    }

    private static async Task<IResult> GetConfigAsync(
        IConfigurationService configService,
        CancellationToken ct)
    {
        var options = await configService.GetCurrentAsync(ct);
        var response = new ConfigResponse(
            BaseDailyAmount: options.BaseDailyAmount,
            DailyBuyHour: options.DailyBuyHour,
            DailyBuyMinute: options.DailyBuyMinute,
            HighLookbackDays: options.HighLookbackDays,
            DryRun: options.DryRun,
            BearMarketMaPeriod: options.BearMarketMaPeriod,
            BearBoostFactor: options.BearBoostFactor,
            MaxMultiplierCap: options.MaxMultiplierCap,
            Tiers: options.MultiplierTiers
                .Select(t => new MultiplierTierDto(t.DropPercentage, t.Multiplier))
                .ToList()
        );
        return Results.Ok(response);
    }

    private static async Task<IResult> GetDefaultsAsync(
        IConfigurationService configService,
        CancellationToken ct)
    {
        var defaults = await configService.GetDefaultsAsync(ct);
        // Map to same ConfigResponse format
        return Results.Ok(/* ... */);
    }

    private static async Task<IResult> UpdateConfigAsync(
        UpdateConfigRequest request,
        IConfigurationService configService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            var options = new DcaOptions
            {
                BaseDailyAmount = request.BaseDailyAmount,
                DailyBuyHour = request.DailyBuyHour,
                DailyBuyMinute = request.DailyBuyMinute,
                // ... map all fields
            };

            await configService.UpdateAsync(options, ct);

            logger.LogInformation(
                "Configuration updated: BaseDailyAmount={Amount}, DryRun={DryRun}",
                options.BaseDailyAmount,
                options.DryRun
            );

            return Results.Ok();
        }
        catch (ValidationException ex)
        {
            return Results.BadRequest(new { errors = ex.Errors });
        }
    }
}

public record ConfigResponse(
    decimal BaseDailyAmount,
    int DailyBuyHour,
    int DailyBuyMinute,
    int HighLookbackDays,
    bool DryRun,
    int BearMarketMaPeriod,
    decimal BearBoostFactor,
    decimal MaxMultiplierCap,
    List<MultiplierTierDto> Tiers
);

public record UpdateConfigRequest(
    decimal BaseDailyAmount,
    int DailyBuyHour,
    int DailyBuyMinute,
    int HighLookbackDays,
    bool DryRun,
    int BearMarketMaPeriod,
    decimal BearBoostFactor,
    decimal MaxMultiplierCap,
    List<MultiplierTierDto> Tiers
);
```

### Frontend: Config Composable

```typescript
// Source: Existing useBacktest.ts pattern
// File: TradingBot.Dashboard/app/composables/useConfig.ts

import type { ConfigResponse, UpdateConfigRequest } from '~/types/config'

export function useConfig() {
  const config = ref<ConfigResponse | null>(null)
  const isEditing = ref(false)
  const isSaving = ref(false)
  const error = ref<string | null>(null)

  async function loadConfig() {
    try {
      config.value = await $fetch<ConfigResponse>('/api/config')
    } catch (err: any) {
      error.value = err.message || 'Failed to load config'
      throw err
    }
  }

  async function loadDefaults() {
    try {
      return await $fetch<ConfigResponse>('/api/config/defaults')
    } catch (err: any) {
      error.value = err.message || 'Failed to load defaults'
      throw err
    }
  }

  async function saveConfig(request: UpdateConfigRequest) {
    isSaving.value = true
    error.value = null

    try {
      await $fetch('/api/config', {
        method: 'PUT',
        body: request
      })

      // Reload to get fresh data
      await loadConfig()
      isEditing.value = false
    } catch (err: any) {
      error.value = err.data?.errors?.join('; ') || err.message || 'Failed to save config'
      throw err
    } finally {
      isSaving.value = false
    }
  }

  return {
    config,
    isEditing,
    isSaving,
    error,
    loadConfig,
    loadDefaults,
    saveConfig
  }
}
```

### Frontend: Zod Schema (Mirrors DcaOptionsValidator)

```typescript
// Source: DcaOptionsValidator.cs validation rules
// File: TradingBot.Dashboard/app/schemas/configSchema.ts

import { z } from 'zod'

export const configSchema = z.object({
  baseDailyAmount: z
    .number({ required_error: 'Base daily amount is required' })
    .positive('BaseDailyAmount must be greater than 0'),

  dailyBuyHour: z
    .number({ required_error: 'Hour is required' })
    .int()
    .min(0, 'DailyBuyHour must be between 0 and 23')
    .max(23, 'DailyBuyHour must be between 0 and 23'),

  dailyBuyMinute: z
    .number({ required_error: 'Minute is required' })
    .int()
    .min(0, 'DailyBuyMinute must be between 0 and 59')
    .max(59, 'DailyBuyMinute must be between 0 and 59'),

  highLookbackDays: z
    .number({ required_error: 'Lookback days is required' })
    .int()
    .positive('HighLookbackDays must be greater than 0'),

  dryRun: z.boolean(),

  bearMarketMaPeriod: z
    .number({ required_error: 'Bear market MA period is required' })
    .int()
    .positive('BearMarketMaPeriod must be greater than 0'),

  bearBoostFactor: z
    .number({ required_error: 'Bear boost factor is required' })
    .positive('BearBoostFactor must be greater than 0'),

  maxMultiplierCap: z
    .number({ required_error: 'Max multiplier cap is required' })
    .min(1.0, 'MaxMultiplierCap must be at least 1.0'),

  tiers: z
    .array(z.object({
      dropPercentage: z
        .number()
        .nonnegative('DropPercentage must be >= 0'),
      multiplier: z
        .number()
        .positive('Multiplier must be > 0')
    }))
    .max(5, 'Maximum 5 tiers allowed')
    .refine(
      (tiers) => {
        // Check if sorted by dropPercentage ascending
        for (let i = 1; i < tiers.length; i++) {
          if (tiers[i].dropPercentage < tiers[i - 1].dropPercentage) {
            return false
          }
        }
        return true
      },
      { message: 'Tiers must be sorted by drop percentage ascending' }
    )
})

export type ConfigFormData = z.infer<typeof configSchema>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| appsettings.json only | Database-backed config | 2024-2026 trend | User-editable config without restart |
| IOptions (singleton cache) | IOptionsMonitor (reloadable) | .NET Core 2.0+ | Hot-reload support for config changes |
| VeeValidate | Zod + standard-schema | Nuxt UI v4 (2025) | Schema-based validation, type safety |
| Manual form state | UForm component | Nuxt UI v3-v4 | Automatic validation, error display |
| JSON config column | Separate typed columns | Current best practice | Type safety, queryability, performance |

**Deprecated/outdated:**
- **File-watching for IOptionsMonitor reload in DB scenarios:** Doesn't work. Manual cache invalidation required.
- **VeeValidate for Nuxt UI v4:** Nuxt UI v4 standardized on standard-schema (zod, valibot). VeeValidate still works but not the primary pattern.
- **Full JSON storage for configuration:** Modern PostgreSQL apps prefer typed columns for known schema, JSONB only for truly variable data.

## Open Questions

1. **Should schedule changes require scheduler restart?**
   - What we know: `DcaSchedulerBackgroundService` uses `IOptionsMonitor<DcaOptions>` and reads schedule time at runtime. Changing `DailyBuyHour` should take effect on next check (likely next day).
   - What's unclear: Does the scheduler need explicit notification to recalculate next execution time?
   - Recommendation: Test after implementation. If schedule changes don't take effect until process restart, add explicit notification via IOptionsMonitor.OnChange in DcaSchedulerBackgroundService.

2. **How to handle concurrent configuration updates?**
   - What we know: Single-row table with row-level locking. Two users editing simultaneously could cause race condition.
   - What's unclear: Is optimistic concurrency control needed (UpdatedAt version check)?
   - Recommendation: LOW confidence this is a real issue (single-user dashboard). If needed, add ETag-based optimistic concurrency in v1.3.

3. **Should config GET endpoint always read from DB or cache?**
   - What we know: IConfigurationService.GetCurrentAsync reads from DB. Multiple calls = multiple DB queries.
   - What's unclear: Should we cache read-only config for GET endpoint performance?
   - Recommendation: Read from DB initially (simple, always fresh). If performance issue emerges (unlikely for single row), add in-memory cache with short TTL.

## Sources

### Primary (HIGH confidence)

- [Options pattern in ASP.NET Core - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0) - IOptionsMonitor, IOptionsMonitorCache, validation patterns
- [Nuxt UI Form Component](https://ui.nuxt.com/docs/components/form) - UForm, zod validation, error handling
- [Nuxt UI Input Component](https://ui.nuxt.com/docs/components/input) - UInput, USelect examples
- Existing codebase: DcaOptionsValidator.cs, DashboardEndpoints.cs, useBacktest.ts

### Secondary (MEDIUM confidence)

- [IOptionsMonitor Demo - The Code Blogger](https://thecodeblogger.com/2021/04/22/ioptionsmonitor-demo-reload-configurations-in-net-applications/) - OnChange pattern, cache invalidation
- [Understanding IOptions, IOptionsMonitor, and IOptionsSnapshot - Felipe Gavilán](https://gavilan.blog/2025/03/25/understanding-ioptions-ioptionssnapshot-and-ioptionsmonitor/) - Lifecycle comparison (2025)
- [When To Avoid JSONB In A PostgreSQL Schema - Heap](https://www.heap.io/blog/when-to-avoid-jsonb-in-a-postgresql-schema) - Separate columns vs JSON tradeoffs
- [PostgreSQL JSON Types - Official Docs](https://www.postgresql.org/docs/current/datatype-json.html) - JSONB for variable-length arrays

### Tertiary (LOW confidence)

- [IOptionsMonitor custom ConfigurationProvider issue](https://github.com/dotnet/runtime/issues/111070) - Reports cache issue with custom providers
- [Nuxt UI v4 zod validation issues](https://github.com/nuxt/ui/issues/1988) - Backend validation integration challenges

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries already in use, no new dependencies
- Backend architecture: HIGH - IOptionsMonitor + manual cache invalidation is well-documented pattern
- Database schema: HIGH - Single-row with separate columns is standard for app config
- Frontend architecture: HIGH - UForm + zod follows Nuxt UI v4 official examples
- Nuxt UI v4 validation edge cases: MEDIUM - Some reported issues with nested schemas and backend validation integration

**Research date:** 2026-02-14
**Valid until:** ~30 days (stable patterns, unlikely to change)
