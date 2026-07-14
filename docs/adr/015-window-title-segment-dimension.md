# ADR-015: Window Title as a Segment-Level Dimension (Strategy A)

## Status: Accepted

## Date: 2026-07-01

[`407bfb4`](https://github.com/awaqwq233/heartbeat/commit/407bfb4) — feat: capture window title as a segment-level dimension
[`f70ba58`](https://github.com/awaqwq233/heartbeat/commit/f70ba58) — fix: capture same-window title changes via EVENT_OBJECT_NAMECHANGE

## Context

App usage was identified solely by process executable name (see the App glossary entry in `shared/CONTEXT.md`). This collapses granularity badly:

- All UWP apps share the foreground process `ApplicationFrameHost`.
- All browser tabs share `msedge` / `chrome`.
- Electron apps each have their own exe but no per-window distinction.

The richest, cheapest signal for telling these apart — the **window title** (`GetWindowText`) — was being discarded. It is the key signal for the planned "daily Replay": within one app, knowing *what* was on screen (which page, which file) and *when it switched* is exactly the段内细节 a replay needs.

### Scope decision: title is a segment dimension, NOT a new App identity

We explicitly do **not** redefine App. `msedge:youtube` and `msedge:github` remain **one App (msedge)** in the App table, in dedup, in report aggregation, and in the dashboard ranking. This keeps the App domain concept, the schema's App table, and the two data paths (stats vs replay) untouched.

Instead, the window title rides along as a **field on each AppUsage segment**. Same App, different title → distinguishable at the segment level. Result:

- **Stats path** (report aggregation) — groups by App only; title does not participate. Zero change.
- **Replay path** (raw usageData) — carries title; segment-level "when did I switch from YouTube to GitHub" becomes visible.

### Strategy A: split on any title change, no normalization ("先脏后治")

When App *or* title changes, we cut a new segment. We deliberately do **no** title normalization or debouncing in this version. Browser titles are noisy (unread counts, playback progress, ` - Google Chrome` suffixes), so this will produce many segments and some noise. That is accepted: we cannot design good normalization rules without first seeing what real title data looks like on the owner's machine. Normalization/debounce is deferred until real data justifies specific rules — which is also where the future "extraction rule layer / per-app specialization" will live.

### Merge must become title-aware

The server merges adjacent **same-app, head-to-tail (≤1s)** segments. Without change, a YouTube segment and a GitHub segment (both `msedge`, adjacent in time) would be **re-merged**, erasing the title split. So the merge predicate must require **same App AND same Title**.

Merge logic exists in two places (see the deferred half of ADR review candidate 5): `UsageMerger.Merge` (client, intra-batch) and `UsageService.SaveUsageAsync`'s inline first-record merge (server, cross-batch seam). To avoid the two predicates drifting apart — a divergence would silently glue wrong segments — the App+Title predicate is extracted into a single public `UsageMerger.CanMerge(...)` used by both. (The full collapse of the server's inline merge into `UsageMerger` remains deferred; only the predicate is unified here.)

## Decision

1. **Collection** — introduce `ForegroundWindow { ProcessName, Title }` (a multi-signal container). `ActiveWindowHelper` reads `GetWindowText`; `IWindowEventMonitor` carries `ForegroundWindow`. `AppMonitorService` tracks `_currentTitle` and splits a segment when App **or** Title changes. Away segments carry `Title = null`.

   **Title-change events are load-bearing.** `EVENT_SYSTEM_FOREGROUND` only fires when the foreground *window* changes — switching tabs *within* the same window (e.g. Edge YouTube → GitHub) does not change the foreground window, only its title, so it fires nothing. Without also subscribing `EVENT_OBJECT_NAMECHANGE`, the "Title changed" split can never trigger for same-window switches and the whole title dimension is half-dead (only updates on app switches). We therefore subscribe `EVENT_OBJECT_NAMECHANGE` as well, with strict callback filtering — only `OBJID_WINDOW` + `CHILDID_SELF` (the window itself, not child controls) **and** only when `hwnd == GetForegroundWindow()` (ignore the flood of background-window name changes). This event is high-frequency; the filter is what keeps it from swamping the pipeline.
2. **Shared kernel** — `AppUsageItem` gains `Title` (nullable). `UsageMerger` exposes `CanMerge(app, title, end, app, title, start)` as the single merge predicate (same AppName case-insensitive + same Title ordinal + within tolerance); `Merge` uses it and preserves title.
3. **Server** — `AppUsage` entity + `AppUsageResponse` gain `Title`; EF migration adds a nullable `Title text` column. `SaveUsageAsync` uses `UsageMerger.CanMerge` for the first-record seam and persists title; `GetUsageAsync` projects it.
4. **Frontend** — `useReports.titleBreakdown(appId)` aggregates the current day's segments by title (duration + count). `TodayRanking` wraps each row in a popover showing that app's per-title breakdown — App stays the aggregate unit in the main view, title detail is one click away. Client regenerated from live OpenAPI via NSwag.
5. **Privacy** — titles are uploaded as-is (no filtering). Accepted for single-user self-hosted deployment, same posture as raw input events (ADR-012).

## Consequences

- ✅ Window title captured; UWP/browser/Electron become distinguishable at the segment level without redefining App.
- ✅ Stats (rankings, totals, reports) unaffected — App remains the aggregation unit.
- ✅ Replay gains its first real intra-app signal; the ranking popover surfaces it immediately.
- ✅ Single merge predicate (`CanMerge`) shared by client and server — no risk of the two merge sites drifting on the App+Title rule.
- ⚠️ **Segment count will grow, possibly a lot** — browser title churn produces many/noisy segments. Accepted under Strategy A; normalization deferred until real data guides it.
- ⚠️ Titles on the server are equivalent to a browsing/activity log (filenames, page titles). Accepted for single-user self-hosted use; no blacklist yet.
- ⚠️ `CONTEXT.md` App glossary still says "由进程可执行文件名唯一标识" — that remains true for App identity; title is a segment dimension, not part of App identity. No glossary change needed, but noted to avoid confusion.
- ⚠️ Deferred: title normalization/debounce, an extraction-rule layer for per-app specialization, and browser URL/domain (unreliable to capture — rejected for this version).

## References

- `desktop/Heartbeat.Agent/Utils/ForegroundWindow.cs` — multi-signal container
- `desktop/Heartbeat.Agent/Utils/ActiveWindowHelper.cs` — `GetWindowText` capture + `EVENT_OBJECT_NAMECHANGE` subscription with foreground/window filtering
- `desktop/Heartbeat.Agent/Services/AppMonitorService.cs` — App-or-Title split, title into segment
- `shared/Heartbeat.Core/UsageMerger.cs` — `CanMerge` shared predicate
- `server/Heartbeat.Server/Migrations/20260701075246_AddAppUsageTitle.cs` — Title column
- `frontend/src/composables/useReports.ts` — `titleBreakdown`
- `frontend/src/components/TodayRanking.vue` — per-title popover
- [ADR-014](./014-away-detection-display-sleep.md) — away segments (Title = null)
