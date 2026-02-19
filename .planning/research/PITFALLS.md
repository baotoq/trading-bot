# Pitfalls Research

**Domain:** Flutter mobile app (iOS + Web) added to existing .NET DCA trading bot
**Researched:** 2026-02-20
**Confidence:** HIGH (verified against official Flutter docs, FlutterFire docs, GitHub issues, multiple sources)

---

## Critical Pitfalls

### Pitfall 1: API Key Exposed in Flutter Mobile Client

**What goes wrong:**
The existing `.planning/research/PITFALLS.md` (web dashboard version) covers this for Nuxt, but Flutter makes it structurally worse. The current auth pattern is `x-api-key` header, which works with Nuxt because the server-side proxy hides the key. In Flutter, the key lives on the client device in compiled Dart code. Any tool like `strings`, `jadx`, or `reflutter` can extract it from the compiled binary in under 10 minutes.

Specifically: storing the key via `--dart-define`, `.env` assets (flutter_dotenv), or hardcoded in code — all produce the same result. The compiled binary contains the key value in recoverable form.

**Why it happens:**
The Nuxt pattern felt secure because the browser never saw the key (Nuxt proxied server-to-server). Developers assume the same pattern applies to mobile. It does not. Flutter runs entirely on-device.

**Consequences:**
Anyone who downloads the app (or gets the binary) can call your `.NET` API directly, bypassing the Flutter app. For a single-user personal trading bot this is lower risk than a multi-user product, but the API controls live Bitcoin trading operations. An attacker with your API key could read your purchase history and trigger status checks. More critically, if you ever expose write endpoints (place orders, modify config), the key is the only protection.

**How to avoid:**
Three-tier approach for a personal app:

1. **Storage**: Use `flutter_secure_storage` to persist the API key. It uses iOS Keychain / Android Keystore. The key is not in the binary — the user enters it once on first launch.
2. **Transport**: Keep the `x-api-key` header pattern — it already works. Do not move to query param (shows in logs).
3. **Obfuscation**: Enable Flutter code obfuscation (`flutter build --obfuscate --split-debug-info`) to make reverse engineering harder.
4. **Never use**: `--dart-define` for secrets (extracted via `strings` on the binary), `flutter_dotenv` (`.env` file is bundled as an asset, unzippable from APK/IPA), hardcoded constants.

For a single-user personal app, a manually-entered key stored in secure storage is the right threat model. It's the same security as a password manager.

**Warning signs:**
- API key appears in `flutter build` command as `--dart-define=API_KEY=xxx`
- A `.env` file is added to `pubspec.yaml` assets
- Key is initialized from a `const String apiKey = "..."` in source
- `flutter_secure_storage` is NOT in `pubspec.yaml`

**Phase to address:** API client setup phase (first phase). If the HTTP client is built without secure key storage from day one, it gets copy-pasted everywhere.

---

### Pitfall 2: Flutter Web Push Notifications Don't Work on iOS Safari

**What goes wrong:**
The milestone targets iOS + Web. Push notifications are a core feature (replacing Telegram). On Flutter Web, push notifications via Firebase only work if:
1. The user is on iOS 16.4+ AND
2. The web app is installed as a PWA (added to home screen) AND
3. The app uses the Web Push API (not FCM directly)

If the user just opens the Flutter Web app in Safari on iPhone without installing it to the home screen, push notifications are completely impossible. There is no workaround.

**Why it happens:**
Developers test push notifications on the Flutter iOS app first (where it works), then assume the Flutter Web version inherits the same capability. Web push and native push are entirely different stacks. Flutter Web push goes through browser service workers and Web Push API — not APNs directly.

**Consequences:**
If you build the notification pipeline targeting Flutter Web as the primary delivery surface, iOS Safari users (non-installed PWA) receive zero notifications. Since this is a personal app likely opened in Safari, notifications silently fail.

**How to avoid:**
- Keep Telegram as the primary notification channel for now (it works unconditionally)
- Implement native push on the iOS native app first — this works reliably via APNs
- Treat Flutter Web push as a bonus for PWA-installed desktop browsers (Chrome, Edge)
- Explicitly document in the app that "notifications require the iOS app" — do not promise web push on iOS Safari

**Warning signs:**
- Push notification setup targets `firebase_messaging` only on Flutter Web without PWA service worker setup
- Testing push on iOS is done in Chrome DevTools mobile simulation (not real device)
- No explicit PWA manifest and service worker configuration in Flutter Web build

**Phase to address:** Push notification phase. Establish the notification delivery matrix (iOS native, Web PWA, Telegram fallback) before writing code. Do not discover this limitation after implementing FCM.

---

### Pitfall 3: APNs Uses p12 Certificate Instead of p8 Auth Key

**What goes wrong:**
Firebase FCM for iOS can authenticate with APNs using either a `.p12` push certificate or a `.p8` auth key. The `.p12` certificate approach is the legacy method. A confirmed FlutterFire bug (issue #10920) shows that iOS notifications silently stop working when FCM is configured with a `.p12` certificate — switching to `.p8` fixes it. Additionally, Apple deprecated the certificate-based APNs authentication method, and as of 2025 enforces migration to token-based auth.

**Why it happens:**
Tutorials and StackOverflow answers from 2019-2022 use `.p12`. Developers follow the first result they find.

**Consequences:**
Push notifications appear to work during development (simulator or initial testing), then silently fail on real devices or stop working after certificate expiry (1 year). You spend hours debugging FCM payload, app code, and .NET backend before discovering the Firebase console setting.

**How to avoid:**
- Always configure Firebase with the `.p8` APNs Auth Key (not `.p12`)
- The `.p8` key is reusable across environments (dev + prod), does not expire
- Upload in Firebase Console: Project Settings → Cloud Messaging → Apple app configuration → APNs Authentication Key
- Note the Key ID and Team ID — needed for the Firebase console upload

**Warning signs:**
- Firebase console shows "APNs certificate" instead of "APNs auth key" in project settings
- Push notifications work on simulator but fail on physical device
- Certificate has an expiry date visible in Firebase Console (auth keys don't expire)

**Phase to address:** Push notification infrastructure phase, before any Flutter notification code is written.

---

### Pitfall 4: Flutter Web CanvasKit Memory Leak Crashes iOS Safari

**What goes wrong:**
Flutter Web uses CanvasKit (WebGL + WebAssembly) as its only renderer since Flutter 3.27.4 removed the HTML renderer. There is an active confirmed bug (GitHub issue #178524, reported November 2025, affects Flutter 3.27.4 through 3.38.1) where CanvasKit leaks memory when displaying network images on iOS Safari. After 5-10 screen navigations, Safari crashes and reloads the page.

For a financial dashboard with price charts and purchase history images/icons, this is not a theoretical problem — it is near-certain to trigger with normal usage on iPhone.

**Why it happens:**
The HTML renderer previously provided a fallback that avoided this WebGL memory issue. Its removal in 3.27.4 exposed the underlying WebKit bug (WebKit bug #219780: resizing WebGL canvas causes memory leak). Flutter cannot fix this unilaterally — it requires a WebKit fix.

**Consequences:**
Flutter Web is effectively broken for image-heavy apps on iOS Safari until WebKit ships a fix. The app crashes mid-session, losing user context. The crash is unpredictable (depends on navigation count and image count).

**How to avoid:**
- Minimize or eliminate network images in the Flutter Web version
- Use SVG icons and CSS-resolved assets rather than `Image.network()` where possible
- Implement aggressive image caching and disposal via `imageCache.clear()` on route transitions
- Add a session persistence mechanism (save dashboard state to localStorage) so a Safari reload restores context
- Monitor the GitHub issue for a fix; consider pinning Flutter version if a regression-fix release ships
- Treat iOS Safari Flutter Web as a known degraded experience — the iOS native app is the primary mobile target

**Warning signs:**
- The app uses `Image.network()` or `CachedNetworkImage` on any screen that is navigated to repeatedly
- Safari on iPhone shows the page going blank/white after several navigation events
- Memory profiling in Safari Web Inspector shows monotonically increasing heap before crash

**Phase to address:** Flutter Web target phase. Decide early whether iOS Safari Flutter Web is a supported target — this pitfall may shift the answer to "iOS native app is required, web is desktop-only."

---

### Pitfall 5: FCM Token Not Stored or Refreshed on .NET Backend

**What goes wrong:**
Firebase Cloud Messaging requires your .NET backend to hold each device's FCM registration token to send notifications. Tokens are not permanent: they rotate on app reinstall, OS updates, long inactivity (270+ days Android), or Firebase-initiated refresh. If the backend stores the token once (at first launch) and never updates it, notifications silently fail after any token rotation.

The current .NET backend has no concept of device tokens — Telegram is the only notification channel, which uses a static chat ID. Adding FCM requires a new persistence and refresh model.

**Why it happens:**
Tutorials show sending one test notification and stop there. Production token lifecycle (refresh events, stale token cleanup, multi-device scenarios) is omitted.

**Consequences:**
Notifications work perfectly for the first week, then degrade as the token becomes stale. User sees "0 new notifications" in the app, no error in the backend. Debugging is hard because the FCM API returns success for sends to stale tokens — you only learn they failed from delivery analytics.

**How to avoid:**
- Add a `device_tokens` table to PostgreSQL: `(id, device_token, platform, registered_at, last_seen_at)`
- Expose a `POST /api/devices/register` endpoint (protected by API key) — Flutter calls this on every app launch
- Handle `FirebaseMessagingException` with `messaging/registration-token-not-registered` error code on the .NET side by deleting the token
- Use `FirebaseMessaging.instance.onTokenRefresh` stream in Flutter to re-register when token rotates
- The `firebase-admin` NuGet package (`FirebaseAdmin`) provides the .NET server SDK

**Warning signs:**
- Token is stored once in a config file or environment variable (not in database)
- No `onTokenRefresh` handler in Flutter `main()` initialization
- .NET backend has no logic to handle FCM `messaging/registration-token-not-registered` responses
- Push notifications tested once successfully, then no re-testing after app reinstall

**Phase to address:** Push notification backend phase. Database schema and token registration endpoint must precede any FCM send logic.

---

### Pitfall 6: Flutter Web CanvasKit 1.5MB Initial Load (Cold Start)

**What goes wrong:**
Every Flutter Web cold load downloads `canvaskit.wasm` (1.5MB) before any pixels render. On a 10 Mbps connection this is ~1.2 seconds — before a single widget draws. On slow mobile connections (3G, congested wifi), it is 5-15 seconds of blank white screen. There is no loading spinner until CanvasKit loads (the spinner is rendered by Flutter/CanvasKit itself).

**Why it happens:**
Flutter Web renders everything via WebAssembly and WebGL — the rendering engine itself must load before any UI appears. This is fundamentally different from React/Vue where the HTML renders immediately and JS enhances progressively.

**Consequences:**
For a dashboard opened occasionally (morning check of overnight purchases), a 3-5 second blank screen is noticeable but acceptable. If the web version is the primary interface, this compounds with the iOS Safari memory leak (Pitfall 4) to make the web experience materially worse than the native iOS app.

**How to avoid:**
- Add a custom `index.html` loading screen (HTML/CSS, renders before Flutter loads) to fill the blank white screen:
  ```html
  <div id="loading-overlay">
    <p>Loading BTC DCA Dashboard...</p>
  </div>
  ```
  Remove it in Flutter's `main()` via JS interop after first frame.
- Use `--wasm` build mode with `skwasm` renderer when dependencies support it — skwasm is ~1.1MB and faster startup
- Enable HTTP/2 server push for `canvaskit.wasm` if self-hosting
- Use CDN for the wasm file — Flutter by default loads it from `canvaskit.chromium.org`, which is fast but adds an external dependency
- Accept this as a known limitation for a personal tool — optimize when it becomes disruptive

**Warning signs:**
- No custom `index.html` loading screen
- `flutter build web` is deployed without any CDN or gzip compression for `.wasm` files
- First Contentful Paint measured at >3 seconds on broadband

**Phase to address:** Flutter Web deployment phase. Add the loading screen in the first web build — it takes 30 minutes and prevents the most visible UX regression.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Store API key in `--dart-define` | Fast setup, no UI needed | Key extractable from binary, must recompile to rotate | Never |
| Hardcode FCM token in `.NET` config | Skip device token DB table | Tokens rotate; notifications silently die after reinstall | Never |
| Test push notifications only on simulator | No real device needed | APNs does not work on simulator; feature appears complete but isn't | Never |
| Use `fl_chart` for all charts including parameter sweep (5000+ points) | Popular library, easy API | Performance degrades past 5000 points; sweep visualizations freeze | Only if data is <1000 points |
| Skip `flutter_local_notifications` for foreground notifications | Less code | FCM does not show notification UI when app is in foreground on iOS | Only for silent/data-only notifications |
| Use Nuxt proxy to avoid changing `.NET` CORS config | Nuxt stays relevant | Nuxt dependency lives on even after Flutter replaces it | Acceptable as transitional step |
| Re-use `x-api-key` header auth | Zero backend changes | Fine for single user; not scalable if multiple devices need independent revocation | Acceptable for single-user personal app |

---

## Integration Gotchas

Common mistakes when connecting Flutter to this specific .NET backend.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| .NET CORS for Flutter Web | Leave CORS as `AllowAnyOrigin()` from dev, or forget to add Flutter Web origin | Add Flutter Web deploy URL to CORS whitelist in `Program.cs`. Flutter mobile does not use browser CORS, only Flutter Web does. |
| x-api-key header | Using lowercase `x-api-key` in Flutter but .NET header lookup is case-sensitive on some middleware | The existing `ApiKeyEndpointFilter` uses `"x-api-key"` — match exactly. Use `Dio` interceptor to add it to every request. |
| DateTimeOffset serialization | .NET returns `"2026-02-20T10:00:00+00:00"`, Flutter `DateTime.parse()` handles it correctly, but timezone display depends on device locale | Always display UTC explicitly or use `toLocal()` intentionally, not by accident. |
| Cursor-based pagination | Flutter app passes `cursor` as query param. .NET expects ISO-8601 datetime string. URL encoding of `+` in timestamps | Use `Uri.encodeQueryComponent()` in Flutter when building cursor URLs. The `+` in UTC offsets (`+00:00`) must be percent-encoded. |
| Backtest endpoint response size | Parameter sweep returns large JSON (100+ combinations). Flutter `http` client has no timeout by default | Set `Dio` connect timeout (10s) and receive timeout (60s) explicitly. Backtest sweeps can take 30-45s server-side. |
| Firebase + .NET push | `FirebaseAdmin` SDK requires a service account JSON key file. Deploying this alongside the .NET service | Use .NET User Secrets for the service account JSON in dev. In production (Aspire/Docker), mount as a file secret, not baked into image. |

---

## Performance Traps

Patterns that work in dev but degrade in production.

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| `fl_chart` with parameter sweep data (5000+ points) | UI freezes 2-5s on sweep results screen | Use `candlesticks` or Syncfusion for sweep visualization; downsample to 500 points for display | >1000 data points |
| Flutter Web loading all purchases (no pagination) | 500KB+ JSON, 3s parse + render on mobile web | Cursor pagination already exists on backend — use it; load 20 items, load more on scroll | >100 purchases |
| Polling `/api/dashboard/status` on mobile | Battery drain, unnecessary radio wake | Use FCM data messages to push status changes instead of polling. If polling, minimum 60s interval when app is backgrounded | Continuously in background |
| Flutter `StreamBuilder` without `distinct()` on price stream | Rebuilds entire widget tree on every price update (5s intervals) | Use `StreamBuilder` with `distinct()` or Riverpod `select()` to rebuild only price widget | Every price poll |
| `Image.network()` in `ListView` without `cacheWidth` | Memory grows with scroll, Safari crashes (see Pitfall 4) | Set `cacheWidth` proportional to display size; use `cached_network_image` with size constraints | On iOS Safari after 10+ scrolls |

---

## Security Mistakes

Domain-specific security issues for a personal crypto trading app.

| Mistake | Risk | Prevention |
|---------|------|------------|
| API key visible in Flutter app logs | Key extracted from device logs if someone has physical access | Never `print()` or `debugPrint()` the API key; use `flutter_secure_storage` which is opaque to logs |
| No certificate pinning on API calls | Man-in-the-middle on untrusted wifi exposes x-api-key | For personal use, standard TLS is acceptable; if desired, add cert pinning via `dio` + `http_certificate_pinning` |
| Push notification payload contains PII | Notification visible on lock screen shows purchase amount/price | Keep FCM payload minimal: `{"type": "purchase", "id": "..."}`. Fetch details in-app after tap |
| Sending trade data over unencrypted FCM data message | FCM data messages are encrypted in transit but the backend constructs them | Do not include private key, wallet address, or full order details in FCM payload |
| Leaving .NET CORS wide open after migration | Flutter Web allowed, but so is any other origin | Scope CORS to specific Flutter Web deploy domain after migration is complete |

---

## UX Pitfalls

Common mistakes specific to financial mobile apps.

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No loading skeleton for price charts | Blank chart area while data loads, feels broken | Show skeleton chart (grey rectangles for bars) during `AsyncValue.loading` state |
| Showing raw satoshi values instead of formatted BTC | "0.00023450 BTC" unreadable at small text size | Format as "234.50 sats" or "0.00023 BTC" with consistent precision rule |
| Push notification tap doesn't navigate to purchase detail | User taps notification, sees home screen, confused | Implement `onMessageOpenedApp` handler in FCM to navigate to specific purchase detail route |
| Displaying price chart in local currency without label | User in different timezone sees prices in unexpected currency | Always label chart axis: "Price (USDC)" — the backend returns USDC prices, not USD |
| No offline state handling | User opens app without internet, sees spinner forever | Detect connectivity, show "No connection — last updated X ago" with cached data if available |
| Charts don't respect dark mode | Dark mode Flutter app, chart has white background | Configure chart theme programmatically using `Theme.of(context).brightness` |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Push notifications:** Tested on simulator only — verify on real iOS device with APNs (simulator does not support APNs)
- [ ] **Push notifications:** App in foreground shows notification — FCM does not auto-show on iOS foreground; requires `flutter_local_notifications` to display it
- [ ] **Push notifications:** Notification tap navigates correctly — test when app is killed (cold start from notification) vs backgrounded
- [ ] **Flutter Web:** Push test from desktop Chrome works — verify iOS Safari PWA push separately (completely different code path)
- [ ] **API key auth:** Key "works" in dev — verify `flutter_secure_storage` persists across app restarts on real device (not just hot reload)
- [ ] **Charts:** Look correct on desktop — test touch-based zoom/pan on iPhone (touch targets must be finger-sized, not mouse-sized)
- [ ] **Nuxt deprecation:** Flutter app ships — verify Nuxt is still accessible as fallback before removing; do not remove until Flutter version is confirmed stable in production
- [ ] **CORS:** Mobile requests work — separately test Flutter Web requests from deployed domain (mobile is CORS-exempt, web is not)
- [ ] **FCM token:** Notification sends succeed in backend logs — check actual device delivery, not just FCM API 200 response (FCM returns 200 for stale tokens too)

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| API key in binary / found in git | MEDIUM | Rotate key immediately (update .NET User Secrets + Aspire param + Flutter app), add git history scrub with `git-filter-repo` |
| Wrong APNs auth method (p12 vs p8) | LOW | Firebase Console → Project Settings → Cloud Messaging → upload `.p8`, test immediately |
| FCM tokens stale / notifications not delivering | MEDIUM | Add `POST /api/devices/register` endpoint, publish app update that re-registers on launch, clean stale tokens via Firebase delivery reports |
| Flutter Web crashes iOS Safari (memory leak) | HIGH | Redirect iOS Safari users to native app download; add user-agent detection to serve "open in app" prompt |
| CanvasKit slow load on web (>5s) | LOW | Add HTML loading overlay in `index.html`, enable gzip on server, set `Cache-Control: max-age=31536000` for wasm files |
| Nuxt removed prematurely before Flutter stable | MEDIUM | Git revert Nuxt removal PR; Flutter and Nuxt can run in parallel (different ports) until Flutter is confirmed stable |

---

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| API key in binary (#1) | Flutter project setup + HTTP client phase | Run `strings` on release binary, confirm key not present; verify `flutter_secure_storage` prompts on first launch |
| iOS Safari push impossible (#2) | Notification design phase (before implementation) | Document delivery matrix: iOS native = APNs, Web = not supported on Safari |
| APNs p12 vs p8 (#3) | Firebase project configuration phase | Firebase Console shows "APNs Auth Key" with no expiry date |
| CanvasKit Safari memory leak (#4) | Flutter Web architecture phase | Test 10+ navigations on real iPhone Safari; define pass/fail threshold |
| FCM token lifecycle (#5) | Push notification backend phase | Reinstall app, confirm token refreshes in DB; simulate stale token error |
| CanvasKit slow initial load (#6) | Flutter Web deployment phase | Measure LCP < 3s on WiFi; HTML loading screen visible before Flutter loads |

---

## Sources

- [FlutterFire FCM via APNs Integration](https://firebase.flutter.dev/docs/messaging/apple-integration/) — Official docs, HIGH confidence
- [Flutter Web Renderers — Official Docs](https://docs.flutter.dev/platform-integration/web/renderers) — HIGH confidence
- [CanvasKit Memory Leak on iOS Safari — GitHub Issue #178524](https://github.com/flutter/flutter/issues/178524) — Active confirmed bug, HIGH confidence
- [HTML Renderer Removal Announcement](https://groups.google.com/g/flutter-announce/c/fngDlrI-nvY) — Official Flutter announcement, HIGH confidence
- [FCM Token Management Best Practices — Firebase Docs](https://firebase.google.com/docs/cloud-messaging/manage-tokens) — Official docs, HIGH confidence
- [iOS Notifications Broken with APNs Certificate — FlutterFire Issue #10920](https://github.com/firebase/flutterfire/issues/10920) — Confirmed bug report, HIGH confidence
- [APNs 2025 Certificate Deprecation — React Native Insights](https://reactnativeinsights.com/apns-update-apple-new-push-notification-certificates/) — MEDIUM confidence
- [Flutter API Key Security — freecodecamp.org](https://www.freecodecamp.org/news/how-to-secure-mobile-apis-in-flutter/) — MEDIUM confidence
- [Flutter Secure Storage — pub.dev](https://pub.dev/packages/flutter_secure_storage) — HIGH confidence
- [PWA Push Notification iOS Limitations](https://codewave.com/insights/progressive-web-apps-ios-limitations-status/) — MEDIUM confidence
- [CanvasKit Initial Load Speed — GitHub Issue #82757](https://github.com/flutter/flutter/issues/82757) — HIGH confidence
- [Push Notification Foreground/Background/Killed States in Flutter](https://medium.com/@yasmin2794/the-truth-about-push-notifications-in-flutter-foreground-background-killed-states-32f76f739688) — MEDIUM confidence
- [fl_chart Performance Limitations 2025](https://medium.com/@rudi-k/the-complete-flutter-charts-comparison-why-cristalyse-is-leading-the-2025-revolution-fbb0b5466e55) — MEDIUM confidence
- [Flutter CORS for Web — DhiWise](https://www.dhiwise.com/post/simplifying-cross-origin-requests-with-flutter-cors-package) — MEDIUM confidence

---
*Pitfalls research for: Flutter mobile (iOS + Web) + push notifications added to .NET DCA trading bot*
*Researched: 2026-02-20*
