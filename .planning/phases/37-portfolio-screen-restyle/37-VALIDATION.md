---
phase: 37
slug: portfolio-screen-restyle
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-04
---

# Phase 37 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Flutter widget tests (flutter_test SDK) |
| **Config file** | none — standard `flutter test` discovery |
| **Quick run command** | `cd TradingBot.Mobile && flutter test` |
| **Full suite command** | `cd TradingBot.Mobile && flutter test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Manual visual check on iOS Simulator + `flutter analyze`
- **After every plan wave:** Run `flutter analyze` (no errors) + visual smoke test on Simulator
- **Before `/gsd:verify-work`:** `flutter analyze` clean + visual verification of donut glow and slot-flip on device/simulator
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 37-01-01 | 01 | 1 | SCRN-05 | visual | Manual: verify GlassCard wrapping on donut + asset rows | N/A | ⬜ pending |
| 37-01-02 | 01 | 1 | SCRN-05 | analyze | `flutter analyze` | ✅ | ⬜ pending |
| 37-02-01 | 02 | 1 | ANIM-06 | visual | Manual: toggle currency, verify slot-flip animation | N/A | ⬜ pending |
| 37-02-02 | 02 | 1 | SCRN-05 | visual | Manual: verify donut ambient glow renders | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*No existing Flutter test infrastructure in TradingBot.Mobile. All phase behaviors are verified through manual visual inspection + `flutter analyze` for static analysis. Creating widget test stubs is optional given no prior mobile test baseline.*

*Existing infrastructure (flutter analyze) covers static analysis requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Donut ambient glow renders with dominant asset color | SCRN-05 | Visual effect requires human eye to verify color accuracy and glow intensity | 1. Open Portfolio tab 2. Verify colored glow halo around donut chart 3. Confirm color matches dominant asset |
| Slot-flip animation plays on currency toggle | ANIM-06 | Animation timing and visual smoothness require human perception | 1. Open Portfolio tab 2. Tap VND/USD toggle 3. Verify value labels flip vertically (old exits up, new enters from below) |
| Asset rows use non-blur glass cards | SCRN-05 | Visual style verification (GlassVariant.scrollItem) | 1. Scroll asset list 2. Verify glass card styling without blur on each row |
| Reduce-motion guard disables animations | ANIM-06 | Accessibility setting requires device-level toggle | 1. Enable reduce-motion in iOS Settings 2. Toggle currency 3. Verify instant switch (no animation) |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: manual visual checks after every task
- [ ] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
