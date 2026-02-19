# Phase 12: Configuration Management - Context

**Gathered:** 2026-02-14
**Status:** Ready for planning

<domain>
## Phase Boundary

User can view and edit DCA configuration from the dashboard with server-side validation. Covers all DcaOptions fields: base amount, schedule, lookback, multiplier tiers, bear market settings, dry run toggle. Config is persisted to PostgreSQL and takes effect on the next DCA cycle.

</domain>

<decisions>
## Implementation Decisions

### Config layout & grouping
- Sectioned single page — all settings visible on one scrollable page, grouped into visual sections
- 3 sections: **Core DCA** (BaseDailyAmount, DailyBuyHour/Minute, HighLookbackDays, DryRun) | **Multiplier Tiers** (the tier table) | **Bear Market** (BearMarketMaPeriod, BearBoostFactor, MaxMultiplierCap)
- Inline descriptions below each field explaining what it does (e.g., "How many days back to check for ATH" under HighLookbackDays)
- Config is a new tab on the existing dashboard page (alongside existing dashboard content), not a separate /settings route

### Editing & save flow
- View-then-edit pattern: show config as read-only first, click "Edit" button to enable fields, Save/Cancel to finish
- Single save button at the bottom that saves all changed settings across all sections at once
- Confirmation dialog only for critical fields (BaseDailyAmount, schedule time, DryRun toggle) — skip for other fields
- Reset to defaults button available — restores all settings to their default values from appsettings.json

### Multiplier tiers editing
- Editable table with columns: Drop %, Multiplier. Each row is editable with Add Row / Remove Row buttons
- Auto-sort by DropPercentage ascending — no manual drag reordering. Backend validates sort order
- Maximum 5 tiers — keeps the strategy manageable and UI clean
- Real-time inline validation — red border and error message on invalid fields as user types (duplicate drop %, negative values, etc.)

### Apply behavior & feedback
- Config persisted to PostgreSQL database (not appsettings.json file) — API reads from DB on each DCA cycle
- Changes take effect immediately on save — next DCA cycle uses the new config, no restart needed
- Toast notification on successful save ("Configuration saved successfully") — auto-dismisses
- No config change history or audit log — keep it simple for v1.2

### Claude's Discretion
- Exact field component choices (number input, time picker, toggle for DryRun)
- Backend API endpoint design (GET/PUT for config CRUD)
- Database schema for config storage (single row, key-value, or JSON column)
- Loading and error states for the config page
- Exact validation error message wording
- How to source "default values" for the reset button

</decisions>

<specifics>
## Specific Ideas

- Existing DcaOptionsValidator already has comprehensive validation rules — reuse the same logic server-side
- IOptionsMonitor pattern currently used for config — need to bridge DB storage with options pattern or replace reads
- Schedule time is two fields (hour + minute) — could be presented as a single time picker

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 12-configuration-management*
*Context gathered: 2026-02-14*
