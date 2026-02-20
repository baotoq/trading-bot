# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v3.0 Flutter Mobile (SHIPPED)
**Updated:** 2026-02-20 (25.1-01 complete -- notification handler split)

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-20)

**Core value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via mobile app
**Current focus:** Phase 25.1 complete -- notification handler split

## Current Position

Phase: 25.1 (Cross-Cutting Notification Handler Split)
Plan: 01 of 01 complete (Phase 25.1 complete)
Status: Complete -- notification handlers split into per-channel implementations
Last activity: 2026-02-20 -- 25.1-01 complete: Split PurchaseCompleted/PurchaseFailed handlers into Telegram and FCM, enriched PurchaseCompletedEvent with running totals

Progress: [██████████] 100% (1/1 plans complete)

## Milestones Shipped

- v1.0 Daily BTC Smart DCA (2026-02-12) -- 4 phases, 11 plans
- v1.1 Backtesting Engine (2026-02-13) -- 4 phases, 7 plans, 53 tests
- v1.2 Web Dashboard (2026-02-14) -- 5 phases, 12 plans
- v2.0 DDD Foundation (2026-02-20) -- 7 phases, 15 plans, 62 tests
- v3.0 Flutter Mobile (2026-02-20) -- 6 phases, 11 plans

## Performance Metrics

**Velocity:**
- Total plans completed: 49 (across v1.0-v3.0 so far)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)
- v3.0: 1 day (11 plans)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25 | 11 | Complete |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

**20-01 decisions:**
- Removed custom_lint from pubspec (analyzer ^8.0.0 conflicts with riverpod_generator 4.x requiring ^9.0.0); riverpod_lint 3.x uses analysis_server_plugin directly
- Dark-only theme: single ThemeData with Brightness.dark, no darkTheme/themeMode — ignores iOS system setting
- NavigationBar (Material 3) + CupertinoIcons over CupertinoTabBar — avoids GlobalKey crash with StatefulShellRoute

**20-02 decisions:**
- AsyncValue.value (T?) used instead of non-existent valueOrNull — Riverpod 3.2.1 API; extract to local variable before switch to avoid type narrowing issue
- Dart super parameter syntax ([super.message = 'default']) used for ApiException subclasses — idiomatic Dart, no behavior change

**21-01 decisions:**
- Remove home_screen.g.dart and part directive: HomeScreen no longer has @riverpod annotations after moving provider to home_providers.dart; build_runner deletes the .g.dart automatically
- Manual fromJson for DTO models: no json_serializable code generation dependency — simpler, explicit deserialization with (json['field'] as num).toDouble() for Vogen value objects that serialize as raw numbers

**21-02 decisions:**
- intl added as explicit dependency: was transitive via Flutter SDK; depend_on_referenced_packages lint flagged it; added intl: any to pubspec.yaml
- SliverAppBar with floating+snap: gives portfolio content more vertical space on scroll while keeping health badge always accessible in actions slot

**22-01 decisions:**
- purchaseSpots uses actual purchase prices (not close prices) for second LineChartBarData — ensures markers appear at correct y-position (price paid) rather than daily close price
- Two-LineChartBarData approach confirmed valid without fallback: checkToShowDot + purchaseDayIndexSet resolves the STATE.md risk about scatter markers needing fallback
- chartDataProvider takes String timeframe (not enum) to match backend query param string directly

**22-02 decisions:**
- Replaced copyWithPrevious (internal Riverpod API) with explicit isLoadingMore boolean flag on AsyncNotifier — avoids invalid_use_of_internal_member warning while keeping existing items visible during page loads

**23-01 decisions:**
- ConfigEditForm takes ConfigRepository directly and handles all error cases internally (DioException 400 parsing + non-400 snackbar) — cleaner separation of concerns
- MultiplierTierDto fields are mutable (not final) to support copyWith for in-place tier editing
- Tier validation errors separated into two maps (fieldErrors for schedule/lookback/MA errors, tierErrors for tier-specific errors passed to TierListEditor)
- No auto-refresh timer on configDataProvider — config is static, user manually refreshes or edits

### Known Risks

None.

### Pending Todos

None.

### Roadmap Evolution

- Phase 25.1 inserted and completed after Phase 25: split monolithic notification handlers into per-channel INotificationHandler implementations

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-02-20
Stopped at: Phase 25.1 complete
Next step: None -- all planned phases complete

---
*State updated: 2026-02-20 after 25.1-01 completion -- notification handler split*
