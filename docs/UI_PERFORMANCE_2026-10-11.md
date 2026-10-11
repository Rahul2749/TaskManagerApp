# UI performance review — 11 October 2026

## Confirmed findings and changes

| Finding | Cause | Change | Status |
| --- | --- | --- | --- |
| Login/logout flashes and runtime restarts | Internal authentication transitions used forced document reloads. Logout's auth notification could first render Login, followed by another document load. | Use client navigation with history replacement for login, logout, registration, invitation acceptance, onboarding and SSO completion. The external SSO handoff still loads the provider endpoint normally. | Source fixed |
| Blank startup after the first visit | A sessionStorage flag hid the startup screen on subsequent document loads; startup also waited an extra second. | Always show a dark startup screen until Blazor starts; remove the timer and show a retry message if startup fails. Set the document background before external styles load. | Source fixed |
| Page-wide flicker during updates | MainLayout observed every added DOM node and reset a GSAP animation to opacity zero. List responses, notifications and other renders could repeatedly restart it. | Remove GSAP, the whole-document observer, repeated polling and layout DOM writes. CSS now handles desktop sidebar width and mobile margins. | Source fixed |
| Unnecessary third-party startup resources | All pages loaded Razorpay Checkout and GSAP. The Phosphor loader added six icon stylesheets. | Load Razorpay only when opening checkout, coalesce concurrent script loads and allow retries after failures or a 20-second timeout. Replace the few Phosphor icons with existing MudBlazor SVG icons. | Source fixed |
| Delayed font discovery and duplicate fonts | app.css imported Jakarta through another stylesheet; App also requested Roboto. | Request the theme's Jakarta font directly in the head, with preconnect and font-display swap. Remove the CSS import and unused Roboto stylesheet. | Source fixed |
| Extra WASM dependencies for browser token parsing | The browser used the JWT SDK to read claims, plus unused direct package references. | Parse UI claims with System.Text.Json; retain role mapping and expiry checks. Remove the JWT SDK and unused references; explicitly reference Components.Authorization. API signature validation remains on the server. | Source fixed and regression checked |
| Repeated session storage interop | Concurrent page/service initialization issued separate localStorage reads for the same key. | Share only in-flight reads. Completed values are not cached, so later reads still see changes from another tab. Writes/removals invalidate pending read entries. | Source fixed and regression checked |
| Duplicate notification connection startup | Auth changes and layout initialization could simultaneously refresh the unread count and replace/start the hub. | Serialize connection/disconnection, reuse existing connections and ignore count responses from an earlier session. | Source fixed; stale-response regression checked |
| Sequential page initialization | Timeline and timesheets fetched independent resources one after another. | Fetch project/timeline and task/timesheet data concurrently. Existing task and project-member screens already parallelize their independent loads. | Source fixed |
| Repeated calendar work | Each of 42 date cells filtered the entire task collection and sorted its matches. | Build a sorted date lookup once per calendar build. | Source fixed |
| Motion accessibility | Decorations and transitions could continue when reduced motion was requested. | Add shared reduced-motion rules; startup also honors the preference before app.css loads. | Source fixed |
| WASM request queuing on the VPS | Nginx 1.24 HTTPS listeners have `ssl` without `http2`. With many framework resources, HTTP/1.1 connection limits can add startup queuing. | Prepare `ops/enable_http2.py` to enable HTTP/2 for this site, back up the file, validate Nginx and roll back if validation/reload fails. | Prepared; live setting not changed |

## Measurements

The resource sizes below are the performance-review snapshot before the subsequent login navigation fix.

The public deployment already serves the sampled client WASM file with `Content-Encoding: br`, `Cache-Control: max-age=31536000, immutable`, and `Vary: Accept-Encoding`. Its compressed size was 194,366 bytes; a single remote sample took 8.8 seconds. This is one transfer sample, not a browser startup benchmark or an average. Compression is working; disabling caching or repeatedly clearing the browser cache would increase startup traffic.

The intermediate Release build before dependency cleanup referenced **80 WASM resources totaling 3,258,451 Brotli bytes**. The final build references **74 totaling 3,208,088 bytes**: six fewer files and **50,363 bytes saved (1.55%)**. Counts and sizes come from resources referenced by the published dotnet.js configuration, excluding stale output files. They exclude JavaScript, fonts, CSS and the selected culture data. Removing unnecessary CDN resources and forced runtime restarts is separate from this WASM size reduction.

Largest final compressed WASM resources:

| Resource | Brotli bytes |
| --- | ---: |
| .NET native runtime | 977,146 |
| System.Private.CoreLib | 586,906 |
| MudBlazor | 283,012 |
| TaskManager.Client | 195,014 |
| System.Text.Json | 134,868 |
| System.Linq.Expressions | 113,082 |

The runtime remains the largest part of a cold visit. Public login still uses WASM and must initialize it; this change does not make anonymous routes render independently of the runtime. Culture support is preserved. The existing Release linker and publish compression remain enabled. AOT or invariant globalization was not enabled as a substitute for measuring runtime cost and preserving current behavior.

VPS request logs showed recent successful login processing between 289 ms and 5,351 ms, while the supplied screenshot showed a 24-second browser request. This indicates additional end-to-end delay; it does not establish the cause of every slow request. A resource snapshot showed app CPU 1.31% and memory 280.7 MiB, without current saturation. A snapshot cannot rule out earlier spikes.

Framework preloading follows [Microsoft's static asset guidance](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/static-files?view=aspnetcore-10.0). Publish compression and download size guidance are described in [Microsoft's WASM performance documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/app-download-size?view=aspnetcore-10.0).

## Validation

- Web build passed: zero warnings and errors in the final incremental build. Full earlier builds emitted the 20 existing MudBlazor analyzer warnings.
- Release publish passed with the reduced dependency graph.
- Controller/component/session/rendering regression runner, including the login, .NET 10 and dashboard follow-ups: **42 groups, 243 assertions, zero failures**.
- JavaScript checks passed: deferred checkout loading, concurrent reuse, successful callback data, failure/retry, and startup/layout source checks.
- HTTP/2 helper tests passed: IPv4/IPv6 listeners, idempotence, preserving Certbot/proxy settings, rejecting a different site and missing TLS/listeners. Tests do not edit the VPS.
- Source/test whitespace validation passed. Unrelated user guide and deployment edits were preserved.

Run from the repository root:

```powershell
dotnet run --project tests/TaskManager.RegressionTests/TaskManager.RegressionTests.csproj
node tests/ui-performance.spec.cjs
python tests/test_http2_config.py
dotnet publish src/TaskManager/TaskManager/TaskManager/TaskManager.csproj -c Release -o output/net10-ui-publish
node tests/verify_blazor_publish.cjs output/net10-ui-publish
```

Measurements and build logs are in `output/ui-performance-resources.json` and `output/ui-performance-*.log`; these generated files are not intended for Git.

## Login response without dashboard navigation — follow-up

Regression checks reproduced two failures: authentication notification was still pending when login returned, and a return URL pointing to `/login` left the user on the authentication page. The page also silently stopped when its subsequent user lookup returned null.

Login now publishes the parsed authentication state synchronously after storing the session. Reads begun before a login/logout cannot publish an older session. The client registers cascading authentication state, and the login page uses the user included in the successful response to select the role's dashboard. Authentication-page return URLs are rejected; valid application return URLs remain supported. A missing user now displays an error instead of silently stopping. Internal navigation avoids a document reload.

Checks use the actual client authentication service and tokens generated by the server TokenService, with isolated HTTP/storage fakes. They cover all dashboard roles, onboarding, valid return URLs, authentication-page return URLs, missing users and stale startup reads. The two reproduced failures failed before the fix and pass afterward. The final Release publish passed with the existing MudBlazor analyzer warnings. Logs are in `output/login-navigation-*.log`.

These are confirmed source defects; the reported browser failure has not been traced in an authenticated production browser. The fixes need deployment before checking the live login flow.

### Intermittent login with an existing browser session

Two further races were reproduced after the user clarified that the URL sometimes stays on Login:

- A user lookup started before login could finish afterward and replace the newly cached user with its old result.
- Cleanup of an expired startup token could overlap saving a successful login and delete the newly saved refresh token/user.

The user cache now rejects results from an earlier session version. Session persistence and expired-token cleanup share an asynchronous gate; reads still run outside that gate, and stale authentication reads cannot clear the new session. Login, registration, invitation acceptance, SSO, two-factor session updates, refresh and logout use the same session update path. Authentication is published after persistence completes.

The new tests failed before the fix and pass afterward. They delay a pre-login user read and expired-token removal deliberately, then assert that the new user, all three storage entries and the authenticated role survive. Release publish and the asset-contract check passed; this build has 74 WASM resources totaling 3,211,658 Brotli bytes. Logs are in `output/login-session-races-*.log`. These reproduced races support a session-timing explanation; browser HTTP caching has not been established as the live cause. The deployment still needs the updated source before live verification.

## Microsoft .NET 10 guidance — compatibility review

This solution is a Blazor Web App with an interactive WebAssembly client, both targeting `net10.0`. Standalone `index.html` configuration instructions do not apply to its server-rendered document shell.

| Recommendation | Application status |
| --- | --- |
| Framework preloading | App.razor uses the .NET 10 `ResourcePreloader` component. |
| Fingerprinting and browser caching | App.razor resolves local assets through `Assets`; `ImportMap` and server `MapStaticAssets` remain enabled. |
| Inline boot configuration | Supplied automatically by the .NET 10 SDK. |
| Release trimming and precompression | Docker publishes in Release. Brotli/gzip files and content negotiation remain supplied by static asset endpoints. |
| HTTP response streaming | Keep the .NET 10 default. JSON reads use asynchronous System.Net.Http.Json methods. No global buffering opt-out is added. |
| Limit rendering scope | Move notification subscriptions into NotificationBell. MainLayout no longer reads the session or waits for a hub connection and no longer rerenders page content when the unread count changes. |
| Bound large list rendering | Tasks, users, managers, audit entries and invoices render 20 rows per page, with 20/50/100 options and navigation to remaining records. |

These choices follow Microsoft's [.NET 10 release notes](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0), [rendering guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/rendering?view=aspnetcore-10.0), and [HTTP streaming guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0).

Independent task-detail reads (time entries, dependencies, project tasks) and integration reads (keys, webhooks, connections) now start concurrently. Writes keep their existing order. Regression checks use unresolved responses to verify all three reads start before any finishes and preserve the resulting data.

The rendering checks render the actual NotificationBell and MudBlazor table components with HtmlRenderer. Badge changes and reset do not render the sibling page content; disposal removes the subscription. A 200-record table renders the first 20 records, exposes later and final pages, and updates when its records change. Pagination bounds rendering work; it does not reduce the API result size. These checks are not browser layout or timing benchmarks.

The published-asset check passed: fingerprinted script/CSS endpoints, immutable caching, Brotli negotiation, inline boot configuration and a compressed endpoint for every referenced WASM resource. It also catches reintroduction of removed browser dependencies. This follow-up Release build references **74 WASM resources totaling 3,210,696 Brotli bytes**. Generated logs are in `output/net10-ui-*.log`.

No render mode, server authorization rules, subscription entitlements, API contract, culture support or timezone support was changed. Enabling prerendering would require compatible server services and authentication/state transfer, since this app currently reads its session from browser storage. Lazy-loading pages would require separating their shared client assembly and validating dependencies. AOT changes download size and requires runtime/build measurements. Those require a separate design and measurement pass rather than toggling project properties in this fix.

## Applying the server setting and checking the deployed result

The subsequent dashboard division-by-zero fix, API failure/retry display and project collection query changes are documented in `docs/BUG_AUDIT_2026-10-11.md` (#23–25). Their checks and Release publish passed; generated SQL contains no percentage division. These changes also require deployment.

Deploy the updated server/client Release image first. Copy `ops/enable_http2.py` to `/opt/taskmanager/enable_http2.py` on the VPS. As root, preview the change, then apply:

```bash
python3 /opt/taskmanager/enable_http2.py
python3 /opt/taskmanager/enable_http2.py --apply
```

The helper targets the observed Nginx 1.24 configuration. It adds `http2` to the existing HTTPS listen directives and preserves Certbot options. It rejects Nginx 1.25.1 and newer, which should use `http2 on;` according to [Nginx's HTTP/2 documentation](https://nginx.org/en/docs/http/ngx_http_v2_module.html). Browser-facing HTTP/2 can coexist with the existing HTTP/1.1 proxy connection to the app.

Then check with DevTools recording and cache enabled:

1. A fresh browser profile: document-to-login readiness, framework transfer sizes, request queuing, Protocol `h2`, Brotli encoding and visible dark startup UI.
2. A normal repeat visit: cached framework resources rather than complete downloads.
3. Login → dashboard → projects → logout: internal login/logout should not create another document request or reload the runtime; no white blank screen or repeated whole-page fade.
4. Sidebar collapse/mobile navigation, task refresh, calendar and notifications: content remains visible while data changes.
5. Checkout: gateway resources appear only when checkout is requested and failed loads allow retry.

No new production payment, authentication attempt, load test or server configuration mutation was performed for this review. The current browser was not connected to an authenticated session, so an end-to-end deployed trace remains necessary. The current 3.2 MB cold WASM footprint and network latency still need observation after HTTP/2 and these source fixes are deployed; no universal speedup percentage is claimed.
