# Project State

**Project:** BTC Smart DCA Bot
**Milestone:** v3.0 Flutter Mobile
**Updated:** 2026-02-20 (20-01 complete)

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-20)

**Core value:** Reliably execute daily BTC spot purchases with smart dip-buying, validated by backtesting, monitored via mobile app
**Current focus:** Phase 20 -- Flutter Project Setup + Core Infrastructure

## Current Position

Phase: 20 of 25 (Flutter Project Setup + Core Infrastructure)
Plan: 01 of 02 complete
Status: In progress
Last activity: 2026-02-20 -- 20-01 complete: Flutter scaffold, go_router, dark theme, placeholder screens

Progress: [░░░░░░░░░░] 9% (1/11 plans complete)

## Milestones Shipped

- v1.0 Daily BTC Smart DCA (2026-02-12) -- 4 phases, 11 plans
- v1.1 Backtesting Engine (2026-02-13) -- 4 phases, 7 plans, 53 tests
- v1.2 Web Dashboard (2026-02-14) -- 5 phases, 12 plans
- v2.0 DDD Foundation (2026-02-20) -- 7 phases, 15 plans, 62 tests

## Performance Metrics

**Velocity:**
- Total plans completed: 45 (across v1.0-v2.0)
- v1.0: 1 day (11 plans)
- v1.1: 1 day (7 plans)
- v1.2: 2 days (12 plans)
- v2.0: 2 days (15 plans)

**By Milestone:**

| Milestone | Phases | Plans | Status |
|-----------|--------|-------|--------|
| v1.0 | 1-4 | 11 | Complete |
| v1.1 | 5-8 | 7 | Complete |
| v1.2 | 9-12 | 12 | Complete |
| v2.0 | 13-19 | 15 | Complete |
| v3.0 | 20-25 | 11 est. | In progress |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

**20-01 decisions:**
- Removed custom_lint from pubspec (analyzer ^8.0.0 conflicts with riverpod_generator 4.x requiring ^9.0.0); riverpod_lint 3.x uses analysis_server_plugin directly
- Dark-only theme: single ThemeData with Brightness.dark, no darkTheme/themeMode — ignores iOS system setting
- NavigationBar (Material 3) + CupertinoIcons over CupertinoTabBar — avoids GlobalKey crash with StatefulShellRoute

### Known Risks

- Phase 24 (Push Notifications) requires a real physical iOS device -- APNs does not work on iOS Simulator
- Phase 24 requires APNs .p8 Auth Key (not .p12) -- confirmed FlutterFire bug with .p12 (issue #10920)
- Phase 24 requires active Apple Developer account and Firebase project (manual prerequisites before planning)
- Phase 22 (fl_chart): scatter markers overlaid on line chart may need fallback to vertical dashed lines -- verify before planning

### Pending Todos

None.

### Blockers/Concerns

None.

## Session Continuity

Last session: 2026-02-20
Stopped at: Completed 20-01-PLAN.md (Flutter scaffold + go_router + dark theme + placeholder screens)
Next step: Execute 20-02-PLAN.md (Dio HTTP client + API key interceptor)

---
*State updated: 2026-02-20 after 20-01 completion*
