# Requirements: BTC Smart DCA Bot

**Defined:** 2026-02-21
**Core Value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA — now with a stunning premium glassmorphism UI

## v5.0 Requirements

Requirements for v5.0 Stunning Mobile UI. Each maps to roadmap phases.

### Design System

- [x] **DESIGN-01**: App uses a frosted glass card component (BackdropFilter + tint + border) as the primary surface across all screens
- [x] **DESIGN-02**: App displays an ambient gradient background with static colored orbs behind glass cards
- [x] **DESIGN-03**: App uses a consistent typography scale with system fonts, proper weights, and tabular figures for all monetary values
- [x] **DESIGN-04**: All design tokens (blur sigma, glass opacity, border, glow) live in a single GlassTheme ThemeExtension
- [x] **DESIGN-05**: App honors iOS Reduce Transparency by degrading glass to opaque surfaces
- [x] **DESIGN-06**: App honors iOS Reduce Motion by skipping all animations

### Charts

- [x] **CHART-01**: Price chart displays a gradient glow fill area beneath the line with orange-to-transparent gradient
- [x] **CHART-02**: Price chart animates left-to-right draw-in on initial tab entry
- [x] **CHART-03**: Purchase marker dots display an orange radial glow effect
- [x] **CHART-04**: Chart tooltip uses a frosted glass style with rounded corners and formatted numbers

### Animations

- [x] **ANIM-01**: All loading states display shimmer skeleton placeholders matching real content shapes
- [ ] **ANIM-02**: Portfolio balance and key stat values animate with a count-up effect on first load
- [ ] **ANIM-03**: Dashboard cards cascade in with staggered entrance animation (40-60ms offset)
- [x] **ANIM-04**: All tappable cards respond with a press-scale micro-interaction and haptic feedback
- [ ] **ANIM-05**: Tab and modal routes use smooth fade+scale page transitions
- [ ] **ANIM-06**: Currency toggle animates value labels with a slot-flip effect

### Portfolio UI Redesign (Phase 35.1 — Inserted)

- [x] **PORT-UI-01**: Portfolio screen displays a glass hero header with total value, all-time P&L absolute and percentage
- [x] **PORT-UI-02**: Portfolio screen has Overview/Transactions tab switcher with sticky tab bar and horizontal filter chips that sort the asset list
- [x] **PORT-UI-03**: Portfolio asset rows render in GlassCard scrollItem variant with colored ticker badge, price + P&L%, and holding value + quantity

### DCA Bot Detail Screen (Phase 35.2 — Inserted)

- [x] **DCA-UI-01**: DCA Bot Detail screen is accessible via full-screen push from home, with back button and no bottom nav bar
- [x] **DCA-UI-02**: Overview tab shows bot identity (BTC icon, name, uptime), stats grid (invested, PnL, frequency, amount, avg price, next purchase), and action buttons
- [x] **DCA-UI-03**: Overview tab shows a PnL area chart with gradient fill colored by profit/loss direction, derived from existing price data
- [x] **DCA-UI-04**: Overview tab shows Bot info card with copyable Bot ID and creation time
- [x] **DCA-UI-05**: Overview tab shows the 5 most recent purchase events as a preview
- [x] **DCA-UI-06**: History tab shows the full paginated purchase history reusing PurchaseListItem
- [x] **DCA-UI-07**: Parameters tab shows read-only DCA config values in GlassCard sections (Schedule, Strategy, Tiers)

### Screen Redesign

- [ ] **SCRN-01**: Home screen uses dashboard overview layout with hero balance, mini allocation chart, recent activity, and quick actions
- [x] **SCRN-02**: Chart screen displays gradient glow chart with premium glass tooltip and animated draw-in
- [ ] **SCRN-03**: History screen uses glass-styled list items (non-blur) with glass filter bottom sheet
- [ ] **SCRN-04**: Config screen uses glass cards for settings groups with polished edit form
- [ ] **SCRN-05**: Portfolio screen uses glass cards with animated allocation donut and per-asset glass rows

## Future Requirements

### v5.x Polish

- **POLISH-01**: Glass bottom navigation bar with BackdropFilter (requires device performance validation)
- **POLISH-02**: Scroll-triggered opacity fade on home hero balance section
- **POLISH-03**: Pulsing glow dot animation on purchase chart markers
- **POLISH-04**: Portfolio value chart over time (deferred from v4.0)
- **POLISH-05**: Per-asset performance chart (deferred from v4.0)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Animated gradient background (moving orbs) | GPU cost — static orbs are visually identical and zero per-frame cost |
| BackdropFilter on scrollable list items | Confirmed Impeller frame drops (16ms+ raster); use non-blur glass style |
| Lottie/Rive loading animations | 200-500KB per asset; shimmer skeletons deliver 80% of quality at <5KB |
| Full-screen backdrop blur | Blur scales with screen area; causes jank in scroll content |
| Neon color palette (hot pink, cyan) | Destroys text contrast; doesn't match existing orange/green/red brand |
| Light mode | Not requested; glass tokens support future lerp if ever needed |
| New API endpoints or data model changes | v5.0 is pure UI layer; no backend changes |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| DESIGN-01 | Phase 33 | Complete |
| DESIGN-02 | Phase 33 | Complete |
| DESIGN-03 | Phase 33 | Complete |
| DESIGN-04 | Phase 33 | Complete |
| DESIGN-05 | Phase 33 | Complete |
| DESIGN-06 | Phase 33 | Complete |
| CHART-01 | Phase 35 | Complete |
| CHART-02 | Phase 35 | Complete |
| CHART-03 | Phase 35 | Complete |
| CHART-04 | Phase 35 | Complete |
| ANIM-01 | Phase 34 | Complete |
| ANIM-02 | Phase 36 | Pending |
| ANIM-03 | Phase 36 | Pending |
| ANIM-04 | Phase 34 | Complete |
| ANIM-05 | Phase 36 | Pending |
| ANIM-06 | Phase 37 | Pending |
| SCRN-01 | Phase 36 | Pending |
| SCRN-02 | Phase 35 | Complete |
| SCRN-03 | Phase 38 | Pending |
| SCRN-04 | Phase 38 | Pending |
| SCRN-05 | Phase 37 | Pending |
| PORT-UI-01 | Phase 35.1 | Complete |
| PORT-UI-02 | Phase 35.1 | Complete |
| PORT-UI-03 | Phase 35.1 | Complete |
| DCA-UI-01 | Phase 35.2 | Complete |
| DCA-UI-02 | Phase 35.2 | Complete |
| DCA-UI-03 | Phase 35.2 | Complete |
| DCA-UI-04 | Phase 35.2 | Complete |
| DCA-UI-05 | Phase 35.2 | Complete |
| DCA-UI-06 | Phase 35.2 | Complete |
| DCA-UI-07 | Phase 35.2 | Complete |

**Coverage:**
- v5.0 requirements: 31 total
- Mapped to phases: 31
- Unmapped: 0

---
*Requirements defined: 2026-02-21*
*Last updated: 2026-02-22 after Phase 35.2 requirements added*
