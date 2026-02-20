# Phase 20: Flutter Project Setup + Core Infrastructure - Context

**Gathered:** 2026-02-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Scaffold the Flutter iOS project with build-time configuration (API URL + API key via --dart-define), Dio HTTP client with API key interceptor, go_router navigation with bottom tab bar, and a dark-first themed UI. No setup screen — credentials are injected at build time.

</domain>

<decisions>
## Implementation Decisions

### Authentication & Configuration
- No first-run setup screen — API base URL and API key are both injected at build time via --dart-define or .env
- App launches directly to the home tab on first open
- Auth failures (401/403) show a snackbar warning "Authentication failed" — user stays on current screen, no redirect
- No Keychain storage for user-entered credentials (build-time config eliminates this)

### Color Palette & Theming
- Dark-only theme — no light mode support, ignore iOS system setting
- Dark backgrounds with crypto-app aesthetic (similar to Binance/Coinbase dark mode)
- Primary accent color: Bitcoin orange (#F7931A) for buttons, highlights, and interactive elements
- P&L colors: Green (#00C087 or similar) for profit/up, Red for loss/down — standard Western convention

### Error Presentation
- Network errors / API unreachable: snackbar at bottom + keep showing last-known cached data
- Auth failures (401/403): snackbar warning, no screen redirect
- No staleness indicator — user can pull-to-refresh if concerned
- Cold start with no cached data + API failure: centered "Could not load data" message with a Retry button

### Navigation Structure
- Bottom tab bar with 4 tabs: Home | Chart | History | Config
- Icons: SF Symbols (Cupertino style) with text labels below each icon
- Standard iOS bottom tab bar pattern — always visible, quick switching

### Claude's Discretion
- Setup screen branding/layout (eliminated — no setup screen)
- Exact snackbar styling and duration
- Loading skeleton/spinner design
- Specific SF Symbol icon choices per tab
- Dark theme exact color values beyond Bitcoin orange and P&L green/red

</decisions>

<specifics>
## Specific Ideas

- "Dark-first crypto" feel like Binance or Coinbase dark mode
- Bitcoin orange (#F7931A) as the signature accent color
- Build-time config model — this is a personal single-user app, no need for runtime configuration entry

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 20-flutter-project-setup-core-infrastructure*
*Context gathered: 2026-02-20*
