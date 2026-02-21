# Phase 33: Design System Foundation - Context

**Gathered:** 2026-02-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Create the design token layer and ambient visual foundation for the glassmorphism UI. Delivers: GlassTheme ThemeExtension with all glass/blur/border/glow tokens, ambient gradient background with static orbs, typography scale with tabular figures for monetary values, and accessibility toggles for Reduce Transparency and Reduce Motion. Every subsequent phase (34-38) builds on this foundation.

</domain>

<decisions>
## Implementation Decisions

### Background & base color
- Dark navy (#0D1117) as the app scaffold background — not pure black, slight blue warmth
- This is the base that all glass surfaces and orbs render against

### Ambient gradient orbs
- Orb opacity: very subtle at 10-15% — orbs add warmth and depth without competing with glass cards
- Orbs should be barely-visible color pools, not prominent visual elements
- The glass cards are the visual star, orbs just prevent the background from feeling flat

### Claude's Discretion
- Orb colors — choose 2-3 colors that complement dark navy base and the existing orange brand (#F7931A)
- Orb count and placement — pick arrangement that creates natural depth without distraction
- Glass surface parameters — blur sigma, tint color/opacity, border style/weight for GlassTheme tokens
- Typography hierarchy — font weights, sizes for headings/body/captions, monetary value styling
- Accessibility fallback styling — how opaque cards look under Reduce Transparency, exact behavior under Reduce Motion

</decisions>

<specifics>
## Specific Ideas

- User chose dark navy (#0D1117) specifically — similar to GitHub dark default, not Binance-style near-black
- Very subtle orbs (10-15%) — this signals a refined/restrained aesthetic, not a flashy neon crypto app
- The overall feel should be premium and understated, not vibrant or showy

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 33-design-system-foundation*
*Context gathered: 2026-02-21*
