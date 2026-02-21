# Requirements: BTC Smart DCA Bot

**Defined:** 2026-02-21
**Core Value:** Single view of all investments (crypto, ETF, savings) with real P&L, plus automated BTC DCA — now with a stunning premium glassmorphism UI

## v5.0 Requirements

Requirements for v5.0 Stunning Mobile UI. Each maps to roadmap phases.

### Design System

- [ ] **DESIGN-01**: App uses a frosted glass card component (BackdropFilter + tint + border) as the primary surface across all screens
- [ ] **DESIGN-02**: App displays an ambient gradient background with static colored orbs behind glass cards
- [ ] **DESIGN-03**: App uses a consistent typography scale with system fonts, proper weights, and tabular figures for all monetary values
- [ ] **DESIGN-04**: All design tokens (blur sigma, glass opacity, border, glow) live in a single GlassTheme ThemeExtension
- [ ] **DESIGN-05**: App honors iOS Reduce Transparency by degrading glass to opaque surfaces
- [ ] **DESIGN-06**: App honors iOS Reduce Motion by skipping all animations

### Charts

- [ ] **CHART-01**: Price chart displays a gradient glow fill area beneath the line with orange-to-transparent gradient
- [ ] **CHART-02**: Price chart animates left-to-right draw-in on initial tab entry
- [ ] **CHART-03**: Purchase marker dots display an orange radial glow effect
- [ ] **CHART-04**: Chart tooltip uses a frosted glass style with rounded corners and formatted numbers

### Animations

- [ ] **ANIM-01**: All loading states display shimmer skeleton placeholders matching real content shapes
- [ ] **ANIM-02**: Portfolio balance and key stat values animate with a count-up effect on first load
- [ ] **ANIM-03**: Dashboard cards cascade in with staggered entrance animation (40-60ms offset)
- [ ] **ANIM-04**: All tappable cards respond with a press-scale micro-interaction and haptic feedback
- [ ] **ANIM-05**: Tab and modal routes use smooth fade+scale page transitions
- [ ] **ANIM-06**: Currency toggle animates value labels with a slot-flip effect

### Screen Redesign

- [ ] **SCRN-01**: Home screen uses dashboard overview layout with hero balance, mini allocation chart, recent activity, and quick actions
- [ ] **SCRN-02**: Chart screen displays gradient glow chart with premium glass tooltip and animated draw-in
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
| DESIGN-01 | Phase 33 | Pending |
| DESIGN-02 | Phase 33 | Pending |
| DESIGN-03 | Phase 33 | Pending |
| DESIGN-04 | Phase 33 | Pending |
| DESIGN-05 | Phase 33 | Pending |
| DESIGN-06 | Phase 33 | Pending |
| CHART-01 | Phase 35 | Pending |
| CHART-02 | Phase 35 | Pending |
| CHART-03 | Phase 35 | Pending |
| CHART-04 | Phase 35 | Pending |
| ANIM-01 | Phase 34 | Pending |
| ANIM-02 | Phase 36 | Pending |
| ANIM-03 | Phase 36 | Pending |
| ANIM-04 | Phase 34 | Pending |
| ANIM-05 | Phase 36 | Pending |
| ANIM-06 | Phase 37 | Pending |
| SCRN-01 | Phase 36 | Pending |
| SCRN-02 | Phase 35 | Pending |
| SCRN-03 | Phase 38 | Pending |
| SCRN-04 | Phase 38 | Pending |
| SCRN-05 | Phase 37 | Pending |

**Coverage:**
- v5.0 requirements: 21 total
- Mapped to phases: 21
- Unmapped: 0

---
*Requirements defined: 2026-02-21*
*Last updated: 2026-02-21 after v5.0 roadmap created (traceability complete)*
