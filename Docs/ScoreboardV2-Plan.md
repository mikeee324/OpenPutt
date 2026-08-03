# Scoreboard V2 — Plan

A from-scratch rebuild of the OpenPutt scoreboard system. No migration path from V1 *in the code* — V1 gets deleted once V2 works, and nothing is written twice to keep both alive. That is not the same as no migration path for **worlds**: OpenPutt ships as a package, so authors who update get their placed boards replaced, and Part 4 covers what they're owed.

Primary design driver: **minimise compute.** Every architectural choice below is justified against the cost model in Part 2.

---

## Part 1 — How the current system works

### 1.1 The pieces

| Script | Lines | Role |
|---|---|---|
`ScoreboardManager.cs` | 514 | Owns everything. Pool of boards, list of positions, all colours, refresh scheduling, player sorting/slicing, visibility pass, global tab state, dev-mode gating, speed-golf toggle.
`Scoreboard.cs` | 879 | One board instance. ~60 hand-wired inspector references, 5 tabs switched by a big `switch`, tab colouring, scroll/viewport math, reset-score flow, HSV cache for ball colour sliders. Plus `ScoreboardExtensions.CreateRow` used by the editor build.
`ScoreboardPlayerRow.cs` | 112 | One row. `rowType` (Normal/Par/Header), `columns[]`, canvas toggling, Y position math.
`ScoreboardPlayerColumn.cs` | 328 | One cell. Text + background colour. Knows about course types, speed golf, par colouring. Note: it's in the global namespace, not `dev.mikeee324.OpenPutt`.
`ScoreboardControl.cs` | 503 | Generic settings widget. `ControlKind` (Slider/Toggle/Dropdown) + `SettingId` enum bound to game values through hand-written switch statements.
`ScoreboardPositioner.cs` | 197 | Marks a spot in the world. Visibility mode, radii, closest-point-on-rect maths, FOV cone that widens at close range, gizmos.
`ScoreboardDevModeUpdater.cs` | 48 | Unconditional `Update()` pushing live club/ball debug values into one board's dev text fields.
`Editor/ScoreboardBuildProcessor.cs` | 184 | `IProcessSceneWithReport`. Destroys and regenerates every row and cell by instantiating `rowPrefab`/`colPrefab`. `ShouldBuildScoreboards()` deep-walks children to decide if a rebuild is needed.
`Editor/ScoreboardManagerEditor.cs` | 79 | Two buttons: "Clear Scoreboard Rows" and "Setup Scoreboards".

Prefabs:
- `Runtime/Prefabs/UI/InternalUI/Scoreboard.prefab` — the whole board UI, ~20,600 lines of YAML, every tab baked into one prefab.
- `Runtime/Prefabs/UI/OpenPuttScoreboard.prefab` — the physical board model (legs, backing mesh) with a `ScoreboardPositioner` child. What a world author drops in the scene.
- `Runtime/Prefabs/UI/ScoreboardPositioner.prefab` — bare positioner.
- `PlayerListRow.prefab`, `PlayerListColumn.prefab` — grid source prefabs.
- `ScoreboardCheckbox.prefab`, `ScoreboardSlider.prefab` — settings widgets.

Scene layout: `OpenPutt.prefab` contains a `ScoreboardPool` object holding 3 pooled `Scoreboard` instances plus 1 static one, and a `ScoreboardPositions` object.

### 1.2 The pooling flow

Every `scoreboardPositionUpdateInterval` (1s default), `ScoreboardManager.UpdateScoreboardVisibility()`:

1. Parks all pooled boards at `(0,-100,0)` with `localScale = 0`.
2. Forces every board to `requestedScoreboardView` (the manager's single global tab).
3. Reads the local player's head pose, plus the screen/photo camera if active.
4. Asks each positioner `ShouldBeVisible(pos, forward, fov, out distance)` — once for the head, once for the camera.
5. If more positioners qualify than there are boards in the pool, drops the farthest until it fits.
6. Walks qualifying positioners in index order, assigning `scoreboards[i++]` to each — moving it to the positioner's transform, converting world scale to local scale, rotating 180°, toggling laser colliders per `interactionMaxRadius`.
7. Positioners without a board re-enable their own `backgroundCanvas`.

Data refresh is separate: `RequestRefresh()` enables the `OpenPuttTimeSlicer` `Update` loop, which walks `numberOfPlayersToDisplay` row indices at `timeAllowedPerFrame` ms per frame, refreshing that row index on *every* board.

### 1.3 Options available today

**`ScoreboardManager`:** `maxRefreshInterval`, `scoreboardPositionUpdateInterval`, `numberOfPlayersToDisplay` (1–82), `hideInactivePlayers`, `extraCreditsText`, 6 alternating row background colours, `currentCourseBackground`, under/on/over-par backgrounds, 5 text colours, `rowPrefab`, `colPrefab`, `timeAllowedPerFrame`.

**`Scoreboard`:** `nameColumnWidth`, `totalColumnWidth`, `columnPadding`, `rowPadding`, `rowHeight`, plus ~60 required internal references.

**`ScoreboardPositioner`:** `scoreboardVisiblility` (AlwaysVisible / NearbyAndCourseFinished / NearbyOnly / Hidden), `nearbyMaxRadius`, `interactionMaxRadius`, `attachedToCourse`, `closeRangeFullVisibilityRadius`, `backgroundCanvas`, `nearbyCenterTransform`.

**`ScoreboardControl`:** `kind`, widget refs, `valueLabel` + `labelFormat`, `id` (`SettingId`), `integerValue`, `refreshMenuOnChange`, `controlsToRefreshOnChange`, `saveOnChange`.

**Pages that exist:** Scoreboard (scores), Scoreboard in speed-golf mode (same page, manager-wide bool), Info (VR / Desktop / Mobile sub-tabs), Settings, Dev Mode, OpenPutt/About.

### 1.4 Verified findings

Checked against the source, not assumed:

- **`ScoreboardManager.maxRefreshInterval` is dead.** Nothing reads it. Refresh requests are never coalesced — `RequestRefresh()` increments `updatesRequested`, so N requests cause N full passes. `PlayerListManager.LateUpdate` calls it on every dirty-player batch, so during active play it fires continuously.
- **Grid cells have TMP auto-sizing ON** (`m_enableAutoSizing: 1`, min 0.05 / max 0.07 in `PlayerListColumn.prefab`) plus `m_isRichText: 1` and `m_parseCtrlCharacters: 1`. Auto-size runs a font-size fitting search with layout passes on every text change.
- **`raycastTarget` is already 0** on both the cell Image and the cell TMP. That win is already taken — don't claim it again.
- **The scroll system is dead code.** `UpdateViewportHeight`, `ScrollToLocalPlayer`, `UpdateScrollableState` and `SnapTo` are never called from anywhere. So the `Canvas.ForceUpdateCanvases()` inside `SnapTo` never actually runs, but the ScrollRect and its raycast-target panel still exist in the prefab.
- **`PlayerListManager` sorting is good** — incremental binary-insert per dirty player, time-sliced with its own frame budget. Don't rewrite it. It does allocate a new array per operation via ArrayExtensions (`Insert`/`RemoveAt`/`Remove`), which is GC churn but out of scope.
- **`_OnUpdateItem`'s `-1` / `-2` branches are unreachable.** The time slicer only produces `0..numberOfObjects-1`. Header and par rows only refresh via `InitUI()` and the speed-golf toggle.
- **`scoreboardRowNeedsUpdating` is set and cleared but never gates any work.** It's set per player by `PlayerListManager` and cleared inside `ScoreboardPlayerRow.Refresh` — i.e. by whichever board refreshes that player first, which is only safe because nothing reads it (see 3.4).
- **`GetPlayerList()` allocates a `Stopwatch` unconditionally**, even with `debugMode` off, plus a `GetRange` array when slicing.
- **Parked boards are still fully refreshed.** `allScoreboards` covers all pooled boards plus static ones regardless of whether they're leased or visible.
- **Empty rows still cost.** `_OnUpdateItem` calls `Refresh(null)` for row indices past the player count. `Column.Refresh` no-ops on a null player for Normal rows, so no text is set, but it still costs ~45 extern calls per empty row (row canvas toggle + 20 column dispatches, each reading `scoreboardRow.rowType`).
- **`courses.Length + 2` is computed in three places** (`ScoreboardManager.NumberOfColumns`, `Scoreboard.NumberOfColumns`, `CreateRow`).
- **`Scoreboard.Start()` re-derives `scoreboardRows` from child count**, with the comment *"This is here because i haven't figured out how to make editor scripts properly yet"*. `ScoreboardPlayerRow.Start()` overwrites its editor-built `columns[]` from children. Both should be baked.
- **Lease churn.** Board→positioner assignment is `currentVisibleScoreboardID++` in positioner index order, so leases reshuffle whenever the qualifying set changes. In V2 that costs a canvas toggle and a full refresh, which is why leases are sticky (3.5).
- **`ScoreboardPlayerColumn` is in the global namespace**, not `dev.mikeee324.OpenPutt`.
- **`hideInactivePlayers` decides sorted-list membership, not just display.** `PlayerListManager.LateUpdate` reads `openPutt.scoreboardManager.hideInactivePlayers` once per dirty player — a cross-behaviour read inside a per-player loop — and `ScoreboardPlayerRow` reads it again per row. V2 deletes the option entirely; inactive players are always hidden (3.1).
- **`StampPositions()` re-stamps both entire sorted lists after every time-sliced batch**, and each player carries exactly one `ScoreboardPositionByScore` and one `ScoreboardPositionByTime`. Both assumptions break if per-view ranking is ever turned on (2.7).
- **The current view is set from outside the scoreboard folder.** `OpenPutt.cs:440` forces `ScoreboardView.Scoreboard` after a persistence restore; `PlayerManager.cs:696` does the same on course completion. Whatever page identity V2 uses has to keep a stable entry point for those two callers.
- **Access levels change during a session.** `enableDevModeForAll` is a live toggle (`SettingId.DevForAll`), `SettingId.PracticeMode` is gated on `OpenPuttUtils.LocalPlayerIsInstanceMaster()`, and the instance master changes when the master leaves. V1 only re-checks when something happens to refresh the tab strip.
- **Persisted scores arrive after `Start`.** `OpenPutt` restores `courseStates` / `courseScores` / `courseTimes` from `PlayerData` once the local player is restored, which can land after any startup pass has finished — which is exactly why line 440 pokes the scoreboard.
- **The authoring UI is not the manager inspector.** `Editor/OpenPuttMainMenu.cs` has a Scoreboards tab (rebuild button, needed-column readout, positions list, "add position") and is where `ShouldBuildScoreboards` is called from. `ScoreboardManagerEditor`'s two buttons are the secondary path.
- **Rich text is on for name cells too**, and display names are user-supplied.

---

## Part 2 — Cost model

Four separate budgets, with different fixes. Confusing them is how you optimise the wrong thing.

**A. Udon VM cost.** Udon is interpreted and slow at everything. Three separate things cost:
- **Externs** — any touch of a Unity object or another UdonBehaviour's field. By far the most expensive per call.
- **Cross-behaviour access** — reading `other.field` or calling `other.Method()` is an extern, so property chains like `row.scoreboard.manager.openPutt` cost one extern per hop, every time.
- **Raw iteration** — loop condition, increment and jump are each VM instructions with dispatch overhead. A loop that does *nothing* still costs. So **iteration count is a budget in its own right**, not just a multiplier on what's inside.

Cost tracks extern count, cross-behaviour hops, and total iterations — not lines of code.

**B. Unity UI cost.** Canvas batch rebuilds and TMP mesh regeneration. Tracks *graphic count* and *how often graphics change*. Independent of Udon.

**C. Per-frame overhead.** Every enabled UdonBehaviour with `Update()` gets called every frame. Tracks *count of enabled behaviours with an update loop*.

**D. Load time and world size.** Tracks *total UdonBehaviour and GameObject count*. Not per-frame, but it's what makes the world slow to load and slow to build.

### 2.1 Where V1 spends it

Take 12 players, 18 courses, 3 pooled world boards + the portable menu's board. Grid is 20 columns × 14 rows (12 players + header + par) = 280 cells per board.

**Budget D:** 280 column behaviours + 14 row behaviours + 1 board + ~26 settings controls + 1 dev updater ≈ **322 UdonBehaviours per board, ~1,290 total.** Plus 1,120 cell GameObjects with 2,240 graphics.

**Budget A, one full refresh:** 12 row indices × 4 boards × 20 cells = 960 cell refreshes. Each cell walks a property chain (`scoreboardRow.scoreboard` → `.manager` → `.openPutt`), calls `transform.GetSiblingIndex()` and `transform.childCount`, indexes `player.courseStates[]` / `player.courseScores[]` / `openPutt.courses[]` across behaviour boundaries, reads `course.courseType` and `course.parScore`, then does a get-compare-maybe-set on `colText.text`, `colText.color` and `colBackground.color`. Conservatively 25–40 externs per cell → **roughly 30,000 externs per full refresh.** That is why the time slicer is needed at all.

**Budget B:** the change-guards (`if (colText.text != newText)`) are correct and already avoid most canvas dirtying. But when text *does* change, auto-sizing makes each regeneration far more expensive than it needs to be.

**Budget C:** `ScoreboardDevModeUpdater.Update()` runs every frame for every player forever, doing ~10 externs and 4 interpolated-string TMP writes, whether or not anyone is on the dev tab. The time slicer correctly sets `enabled = false` when idle — keep that pattern.

### 2.2 Hard rules for Udon code

Non-negotiable, because every one of these is a mistake V1 makes:

1. **Never put a cross-behaviour access inside a loop.** Hoist it. `player.courseScores` read once before the column loop, then indexed locally, is 1 extern instead of 20.
2. **Push loops across the behaviour boundary, don't call across the boundary inside a loop.** Prefer `page._RefreshRows(start, count)` — where the loop runs inside the page — over calling `page._RefreshRow(i)` from a loop outside it. This directly contradicts V1's time slicer shape, which calls per item; see 3.4.
3. **No properties that wrap externs.** V1 has `private int columnIndex => transform.GetSiblingIndex();` and `NumberOfColumns => transform.childCount` — externs that look free at the call site and get hit several times per refresh. Assign to a local once.
4. **`Utilities.IsValid()` is an extern.** Validate once at the top of a method, then trust it. V1 calls it per cell.
5. **Compare cheap values, not expensive ones.** Never guard on a string or `Color` comparison — those are externs. Guard on an `int` (see 2.3).
6. **Reduce iteration count before optimising the loop body.** Skipping a row entirely beats making its 20 cells cheaper.
7. **No virtual/abstract calls in inner loops.** The page hierarchy in 3.2 is fine because page methods are called a handful of times per refresh, never per cell.
8. **Hoist `.Length` into a local** for any loop bound.
9. **Cache anything derived from a transform that doesn't move.** Most anchors never move; their geometry should be computed once, ever.
10. **Budget work by counting, not by timing.** Reading a clock is an extern, so a time-sliced loop pays an extern per check to decide whether it can afford the next one. Count items against a fixed quantum instead; keep stopwatches for `debugMode` measurement only (3.4).

### 2.3 Ranked wins

Ordered by (saving × confidence) ÷ risk.

| # | Change | Budget | Effect |
|---|---|---|---|
1 | **Incremental cell updates** — during play, write only the current-course cell and the total cell | A | ~10x on the dominant steady-state path: 2 cell writes per changed player instead of 20 (see 2.4)
2 | Don't refresh unleased/invisible boards or hidden pages | A, B | 2–4x; parked boards currently get the full treatment, and dirtying their canvases costs Unity-side rebuilds too. Pairs with 2.8 — the gate is what makes a hidden board actually free
3 | Flatten the grid — delete per-row and per-cell behaviours | A, D | 322 → ~32 per board (1 board + 5 pages + ~26 settings controls, which stay); the 294 that go are the grid, and with them the property chain and the cross-behaviour hop per cell
4 | Only refresh `min(playerCount, rowCount)` rows | A | ~6x in a 2-player instance; disable unused row canvases and skip their column loops entirely
5 | Coalesce refresh requests (make `maxRefreshInterval` real) | A, B | Collapses the continuous `RequestRefresh()` storm during play into one pass per interval
6 | Guard on shadowed `int` state, not strings/Colors | A | Turns each guard from an extern into a VM op, and skips the string formatting too (2.5)
7 | Read player arrays once per row, not once per cell | A | 20 externs → 1 per row per array
8 | Cache course metadata into flat local arrays at Start | A | Removes `openPutt.courses[i].parScore` chains from the inner loop
9 | Slice the refresh by (board, row range), not per row | A | Cross-behaviour calls per refresh drop from ~960 to a handful
10 | Gate per-frame page updates on visibility | C | Dev readouts stop costing anything when nobody's looking
11 | Turn off TMP auto-sizing and rich text on grid cells | B | Removes a fitting search + tag parse per text change
12 | Precompute number/time strings | A, B | Kills `$"{x}"` boxing and `TimeSpan.ToString` per cell
13 | Cache anchor geometry; single-pass anchor selection | A | The 1Hz pass currently does `GetComponent<RectTransform>()` + `GetWorldCorners()` twice per anchor per second, then rescans all anchors per dropped anchor
14 | Keep pool size minimal | all | Everything scales linearly with it. Floor is set by concurrency, not by anchor count (2.6)

### 2.4 Incremental cell updates (win #1)

The most important structural insight, and V1 has no equivalent.

While a player is actually playing, **almost nothing on their row changes.** Completed courses are frozen. The only cells that move are:
- the cell for the course they're currently on, and
- their total cell.

Everything else changes only on discrete events: completing or skipping a course, a score reset, a rank change, or a speed-golf toggle.

So there are two refresh paths, not one:

- **Full row refresh** — 20 cells. Runs on lease, first show, reset, rank change, course completion. Rare.
- **Incremental refresh** — 2 cells. Runs while a player is putting. This is the steady-state path, and it's the one that currently costs 20 cells × every board × several times a second.

`PlayerManager` already knows the player's current course, so the manager can dispatch "player at row R, course C changed" instead of "refresh everything". Header and par rows are static except on a speed-golf toggle — they should never appear in a routine refresh at all.

This is worth more than every micro-optimisation below it combined, and it should shape the `ScorePage` API from Phase 3 rather than being retrofitted.

### 2.5 Shadowed state, done right (win #6)

V1's guard costs an extern to read current state before deciding:

```csharp
if (colText.text != newText) colText.text = newText;   // extern get, then maybe extern set
```

The obvious fix — shadowing the applied string in a local `string[]` — is **wrong**, because string comparison in Udon is itself an extern. Same for `Color` equality. Shadow the *source value* instead, as an `int`:

```csharp
// score cells: compare the number, not the rendered text
if (lastValue[i] != score)
{
    lastValue[i] = score;
    cellTexts[i].text = numberStrings[score];   // precomputed, no formatting
}

// colours: compare a palette index, not a Color
if (lastPaletteId[i] != paletteId)
{
    lastPaletteId[i] = paletteId;
    cellBackgrounds[i].color = palette[paletteId];
}
```

Both guards are now pure VM integer compares. Two extra benefits: the string formatting is skipped entirely on a no-op, and colours come from a small fixed palette (`nameBackground1/2`, `scoreBackground1/2`, `totalBackground1/2`, `currentCourse`, `under/on/overPar`) so an index is all that's needed — foreground colours get their own shadow and the same treatment. Canvas-dirty avoidance is preserved.

The soundness condition is that the shadowed int captures *everything* that decides the text, sentinels included, and that it's invalidated when the meaning of the number changes rather than when the player in the row changes. 3.3 works that through — it's the difference between this being a 10× win and being a source of wrong scores on screen.

### 2.6 Pooling

**Udon cannot create UdonSharpBehaviours at runtime** — `VRCInstantiate` is networked and unusable for per-player UI — so every board, row and cell must exist in the scene at build time. One board per position scales linearly on every budget and stops being viable past about three boards, so **N pooled boards serve M anchors**, N ≪ M.

**The pool is authored by hand, not generated.** Three instances ship in the pool; an author who needs more duplicates one. No editor script adds or removes pool instances — the tooling in 3.7 only manages the *pages inside* a board.

**Pool size is still the primary cost dial** — it multiplies every other budget, so the inspector surfaces the resulting cell count (3.7) rather than letting authors add boards blind. Win #14 says keep it minimal; what "minimal" means is worth being precise about, because two lower bounds fight each other:

- **How many boards can be visible at once**, which is what the pool must cover. Fewer boards than that and anchors flicker in and out as the pool thrashes.
Views don't enter into it: because every board carries every view (2.7), pool size is purely about concurrency and not about how many course sets a world has. Three is a sane ship default and the number to reason from; the validation warns when the pool can't cover the `AlwaysVisible` anchors (3.7), which is the case that's unambiguously wrong.

**Not every board is anchored.** V1's single `staticScoreboards` entry is a `Scoreboard.prefab` nested inside `PortableMenu.prefab` — the hand-held menu the player picks up and throws. Its transform is driven by `OpenPuttPortableMenu` (pickup, hand tracking, `PostLateUpdate`), so it can never be leased to an anchor. So boards come in two modes:

- **Anchored** — pooled, leased to anchors by the visibility pass. The three world boards.
- **Owned** — transform driven by something else entirely; never pooled, never leased, always present. The portable menu board today, and the model any future hand-attached board follows.

Owned boards use the same page, refresh and filter code, but **not** the pool's shared page index — their page is their own (3.1). What they need instead of a lease is a **visibility flag their owner sets**, because V1 refreshes the portable board unconditionally, forever, even while it's stowed: a full board's refresh cost for something the player usually isn't looking at. `OpenPuttPortableMenu` already fires `eventToSendOnMenuOpen` / `eventToSendOnMenuClose` at an `eventReceiver`, so the hook exists — point it at the board and win #2 applies to owned boards for free.

One wiring detail, since it decides the method signature: `eventReceiver` is a plain `UdonBehaviour` and the menu calls `SendCustomEvent(eventToSendOnMenuOpen)`, which **takes no parameters**. So the board exposes two parameterless entry points — `_OnOwnerShown` and `_OnOwnerHidden` — rather than a `SetVisible(bool)` the events can't reach. Same for any future hand-attached board.

### 2.7 Course filtering

A board at anchor X shows courses 1–18, anchor Y shows 19–36.

**Do it at bake time, not at lease time: one score page per view.** The earlier design built every board for the maximum column count and re-filtered on lease — hiding columns, recomputing widths, re-running X offsets. Instead, each *view* an author wants is generated as its own score page with its grid baked to exactly its own courses, and a board simply shows the page its anchor asks for. Nothing is filtered at runtime, ever.

That deletes most of a phase:

- **No `_ApplyCourseFilter`.** `courseForColumn`, `columnForCourse` and `columnDisplayMode` become bake-time constants that are never rewritten, and `visibleColumnCount` disappears entirely — every column in a page is shown, so it's just `columnCount`.
- **No lease-time relayout.** V1's column width is `(boardWidth - nameWidth - totalWidth - padding × (columns - 1)) / (columns - 2)` — a function of the column *count*. Re-filtering to a different count meant a new `sizeDelta`, `anchoredPosition` and text-child `sizeDelta` for every visible cell on every row: on the order of a thousand externs, needing its own time-slicing. All of it goes.
- **No group affinity in leasing.** Any board can serve any anchor, because every board carries every view. Leasing goes back to being about geometry and nothing else (3.5).
- **No shadow invalidation on filter change** (3.3) — only the speed-golf toggle remains.
- **No `ScoreboardCourseGroup` behaviour at all.** Views are editor-side configuration that produces prefabs; there's nothing left to instantiate at runtime.

**The cost is duplicated grids, and it's smaller than it sounds.** The old design already paid for a max-width grid on every board — 38 columns × 14 rows = 532 cells in a 36-hole world — and hid half of it. Two 20-column views cost 560. For disjoint views it's a wash, and for the overwhelmingly common single-view world it's identical. The genuinely worse case is *overlapping* views — "All 36" plus "Front 18" plus "Back 18" is 78 columns of grid against 38 — so the validation surfaces total cell count across views (3.7) and authors pay for overlap knowingly. A Front-18-only world is still half the cells of a 36-hole one, so filtering remains a performance feature.

Bonus the old design couldn't offer: because views are real pages, an author can expose one as its own tab and let players switch between Front and Back on the same board, instead of the view being fixed by whichever anchor the board landed on.

Columns are the easy half. Also required, per view:

- **Par totals** are now a single baked int per view — `TotalParScore` / `TotalParTime` are properties that loop every course on every read, and par never changes at runtime, so the generator computes each view's total once and the page reads an int (win #8, rules 1 and 3).
- **Player totals** sum over the view's courses rather than reading `PlayerTotalScore` — but *by the same rules `_UpdateTotals` uses*, not naively (below).
- **Sort order** is the one thing that can't be baked, since it depends on live scores. It's also the expensive part, so it becomes opt-in rather than implied (below).

**`_UpdateTotals` is authoritative, and a naive group sum will disagree with it.** Three things in it that a "just add up the visible courses" implementation gets wrong:

- **Driving-range courses are excluded from totals entirely.** Both types are `continue`d before scoring, so they contribute nothing to `PlayerTotalScore` or `PlayerTotalTime`. A view total must skip them too, or a view containing a driving range ranks differently from the global list for no visible reason.
- **It mutates as it goes.** For `Skipped` and `PlayedAndSkipped` it *writes* `courseScores[i] = maxScore` and `courseTimes[i] = maxTime`, and it calls `UpdateCourseState` per course. So it normalises state as a side effect, and any group sum must run **after** it — never instead of it, and never duplicating those writes. Group totals are derived values; `_UpdateTotals` owns the arrays.
- **In-progress courses contribute no time.** `Playing` is deliberately skipped for `totalTime` (same reason the cell shows `-`, 3.3), while `NotStarted` and `Completed` both add `courseTimes[i]`.

Which implies a validation case: **a view ranking by its own courses can't contain only driving ranges**, because every player's total would be zero. Warn at author time rather than shipping a board where everyone ties.

**Ranking is a per-view setting, and its default costs nothing.** Separating it from column filtering is the other half of the simplification, because the two were conflated into one feature with one price. Each view picks:

- **Rank by all courses** (default) — uses `PlayersSortedByScore` / `ByTime` exactly as they exist today. Zero new machinery. This is also the *correct* answer for the common case: in a 36-hole world where players play all 36, a Front-18 board is a column view of one competition, and showing global rank is right rather than a compromise.
- **Rank by this view's courses** — for genuinely independent competitions, e.g. two 18s played separately. Only this option costs anything, and only worlds that turn it on pay.

**The existing global lists are the default, not legacy.** `PlayersSortedByScore` / `ByTime` stay exactly as they are, and they're public API besides: `OpenPutt` exposes both as passthroughs and derives `CurrentPlayerCount` from the score list's length, a property with no internal callers that exists solely for world authors.

When a view does opt into its own ranking, it reuses `PlayerListManager`'s incremental model rather than sorting from scratch: per dirty player, compute that player's view total (~18 local array reads) and binary-insert into that view's list. Cost is (opted-in views) × the existing sort cost, which is small and already sliced. Constraints: only maintain lists for views that asked for one, cache each player's per-view total so it's computed once per change, and cap them at 4.

**Row position is the part that isn't cheap**, and it's now equally opt-in. The score page slices its row window around the local player using `PlayerManager.ScoreboardPositionByScore` / `ByTime` — one int per player, stamped by `StampPositions()`. Extended naively, that re-walks every list after every batch: O(players × views × 2) per time-sliced batch, several times a second. So per-view stamps are span-limited (3.4), and `PlayerManager`'s two ints stay global and untouched — they're read in exactly one live place plus the commented-out rank display in `OpenPuttUIController`, which wants the global rank specifically. Per-view stamps live in the manager's own arrays, keeping all of it inside `Scoreboards2/`.

With ranking opt-in, a world that just wants a Front-18 board next to the front nine needs **no runtime code beyond what the single-view case already has** — the view is a generated prefab and the sort order is the one that already exists.

Fix it the same way 3.4 fixes rank churn: **stamp only the affected span.** The binary insert already knows the old and new index, so only players between them changed position. Full re-stamps happen on roster change, not on score change.

### 2.8 Showing and hiding without paying for it

Budget B has a second half the wins table doesn't cover: *how* a thing gets hidden. Three options, wildly different costs:

| Method | Cost to hide/show | Cost while hidden |
|---|---|---|
`SetActive(false)` on a hierarchy | Every `Graphic` under it unregisters; on re-enable they all re-register and re-dirty layout, material and vertices — a full mesh rebuild of all 280 cells, plus `OnEnable` on every component | Nothing |
`Canvas.enabled = false` | The canvas's batch is dropped and rebuilt on re-enable. Graphics stay registered, so nothing re-dirties and no per-cell mesh regen | Nothing rendered, nothing batched |
V1's park at `(0,-100,0)` + `scale 0` | Two transform writes | Still batched, still submitted, still rebuilt whenever dirtied |

So **hide with `Canvas.enabled = false`, never with `SetActive` on anything grid-sized.** V1 already does this correctly for rows and pages (`rowCanvas.enabled`, `scoreboardCanvas.enabled`); its mistake was parking whole boards instead of disabling them, which keeps them in every render and rebuild path.

Rules that follow:
- A board is hidden by disabling its **root canvas** plus its interaction colliders. The GameObject stays active, so lease and release are cheap and repeatable.
- A page is hidden the same way, through `canvasesToToggle`. `contentRoot` stays active — its settings behaviours have no `Update` and cost nothing (3.6), and `wantsPerFrameUpdate` covers the one page that does.
- `SetActive(false)` is barely needed now that views are baked (2.7) — there are no filtered-out columns to hide. Rows past the player count are *not* a candidate for it either, because the roster moves, so they use their row canvas like everything else (3.3). It's repeated toggling that costs.
- A disabled canvas still rebuilds if something dirties a graphic under it, so the refresh gate (win #2) is what actually makes a hidden board free. Canvas-off and refresh-gating are one mechanism, not two.
- Toggling a board's canvas isn't free either, so the anchor pass must not do it twice a second — hence the hysteresis in 3.5.

---

## Part 3 — V2 architecture

Namespace `dev.mikeee324.OpenPutt.Scoreboards`, folder `Runtime/Scripts/Scoreboards2/`, so V1 and V2 coexist during development.

### 3.1 Core objects

```
ScoreboardManager        one per world. Data + leasing + colours + page library
                         + the single current page shared by every board.
  ScoreboardInstance     a pooled board. Chrome + tab strip + N pages.
    ScoreboardPage       abstract base. One per page prefab. Public API.
      ScorePage          the grid. Flat arrays, no per-cell behaviours.
                         One instance per authored view, courses baked in.
      SettingsPage
      InfoPage
      DevPage
      AboutPage
  ScoreboardAnchor       a place in the world a board can appear.
```

**`ScoreboardManager`** — pool array, anchor array, page library, all colours and sizing, `numberOfPlayersToDisplay`, refresh scheduling with its own inlined time-slicing loop (3.4), the sorted player lists (global, plus per-view only if that ships), dev-mode/master access checks, and the cached course-metadata arrays from win #8.

**`hideInactivePlayers` is gone.** Players who haven't started playing are always hidden — it was never a setting worth having, and removing it deletes the cross-behaviour read from `PlayerListManager`'s per-player loop and the per-row read in the grid (1.4). The listing condition becomes purely `IsValid(player) && IsValid(player.Owner) && player.PlayerHasStartedPlaying`.

It also owns two pieces of **global display state**, deliberately:
- `currentPageIndex` — every *anchored* board shows the same page. Clicking a tab on one switches them all.
- `speedGolfMode` — a manager-wide toggle, as in V1, so every board shows scores or times together.

**"Same as V1" was wrong about Settings, and fixing it changes the design.** V1 does not have one global page: `ScoreboardManager.OnPlayerOpenSettings` forces every *other* pooled board back to the score view when one board opens Settings, with the comment "so we don't have to keep them up to date". It iterates `scoreboards` and not `staticScoreboards`, so the portable menu is deliberately exempt — the portable board and a world board can both show Settings at once, and `RefreshAllSettingsMenus` walks `allScoreboards` to keep both current. That exemption is what the recent "settings page refresh works on portable scoreboard too" fix was about.

So V2 splits page state by board mode, which is what the exemption was reaching for:
- **Anchored boards share `currentPageIndex`.** They're scenery; having the three world boards agree is coherent, and it's what preserves the refresh gate below.
- **Owned boards own their page.** The portable menu is personal UI held in your hand. Opening it to change a setting must not flip every board in the world to Settings, and tapping Settings on a distant world board must not change what's in your hand. `ScoreboardInstance` therefore carries its own page index when `mode == Owned`, and the manager's global one applies only to the pool.

The refresh gate survives intact: it becomes two int compares instead of one — the pool's page index, plus each owned board's. V1's "kick other boards off Settings" hack disappears entirely, because keeping N settings pages in sync is a solved problem once `RefreshAllSettingsMenus` walks the pool properly (3.6).

**Pages are identified by int, never by string.** A string key would be an extern to compare, and the gate in 3.4 sits on the hottest path in the system — comparing one would violate rule 5 for no benefit. So:
- Each page has an `int pageId`, explicitly numbered and never renumbered, exactly the convention `SettingId` already uses and for the same reason: it's serialized. Built-ins take `0–99`; author pages take `100+`.
- `currentPageIndex` is an **index into the manager's page library**, resolved once when the page list is baked. That's what runtime comparisons use, so the gate is an int compare against a local.
- `pageId` exists only for stable identity across a rebuild — mapping a saved or externally-requested page back to an index, and catching duplicates in validation. It is never compared per frame.
- `ScoreboardView` disappears, so its two external callers (1.4) need a stable entry point: `manager._ShowScoresPage()`, which resolves the score page's index once at startup. No caller outside the scoreboard folder ever names a page by id.
- Tab buttons carry their page **index** directly, wired by the reconcile — a tab click is `manager._ShowPage(3)`, with no lookup at all.

**Both are local to each player, not synced.** "Global" means across every board *this* player sees, not across players. The manager stays `BehaviourSyncMode.None`, matching V1 — one player opening Settings must not yank another player's board. Two players standing in front of the same board can be looking at different pages, and that's correct.

This works because **every client has its own copy of the scene**, and anything Udon changes without explicit sync — GameObject active state, `Canvas.enabled`, transforms — is per-client. The same property is what makes pooling possible at all: each client independently moves the pooled boards to the anchors *that client* is looking at, so one board object can appear at different places for different players. Nothing about view state, leasing or page switching may ever be synced, or both systems break at once.

Synced state is a separate category and stays where it already is: setting *values* like `practiceMode` are owner-synced on `OpenPutt` and persisted (3.6). It's only the view — which page, scores or times, which board is where — that is purely local.

Global page state is a real performance asset, not just a UX choice: the manager knows exactly one page type is live across the whole pool, so if `currentPageIndex != scorePageIndex`, the entire score-refresh path is skipped for every anchored board in one int compare (win #2).

**`ScoreboardAnchor`** (was `ScoreboardPositioner`) — port V1's visibility maths nearly as-is, it's good. Gains:
- `scoreView` — which score page a board here shows, by index. 0 is the default all-courses view (2.7).
- `maxVisibleRows` — cap rows on a small board (win #4 applied per anchor).
- `isStatic` (default true) — precompute geometry once, never re-read the transform.

The anchor holds **no page or mode state** — that's all on the manager. Course group and row cap are the only things that vary per anchor.

**There is no course-group behaviour.** A view is editor-side configuration that generates a score page (2.7), so at runtime the only trace of it is the `int[] courseIndices` baked into that page and the view index on the anchor. Nothing to instantiate, nothing to keep in sync, nothing for validation to find dangling. Authoring flow in 3.7a.

**One tab, several score pages.** Since anchors can want different views, `currentPageIndex` alone can't say which page a board shows — board A at the front-nine anchor and board B at the back-nine anchor are on different score pages while both are "on the scores tab". So the page library holds **one** Scores entry (which is what the tab strip renders), and each board resolves it locally: `ScoreboardInstance` keeps its own `scoreViews[]` and an `activeScoreView` set on lease. The global page index stays a single int, the refresh gate stays one compare, and the tab strip stays five tabs. An author who wants a view to be *player-switchable* gives it its own library entry, which then behaves like any other page.

**`ScoreboardInstance`** (was `Scoreboard`) — loses almost everything. Keeps `rectTransform`, `myCanvas`, `interactionColliders[]`, `pages[]`, `tabButtons[]`, `CurrentAnchor`, `layoutSignature`. Gains `ShowPage(int)`, `ApplyLease(ScoreboardAnchor)`, `ClearLease()`. Tab-colour and canvas juggling moves into the page base and the tab strip.

It also carries a `mode` — **Anchored** or **Owned** (2.6):
- Anchored boards are pooled and leased. An unleased board has its **root canvas disabled** and its interaction colliders off (2.8) — not V1's park-at-`(0,-100,0)`-with-zero-scale, which leaves it in every render and rebuild path, and not `SetActive(false)`, which makes every lease pay a full 280-cell mesh rebuild. The GameObject stays active either way; leasing is a canvas flag, a transform write and a refresh flag.
- Owned boards skip leasing entirely. Their transform belongs to their owner, their `activeScoreView` is a field on the board rather than something a lease sets, and their visibility is driven by the owner calling `_OnOwnerShown` / `_OnOwnerHidden` — parameterless, because that's all `SendCustomEvent` can do (2.6).

Both modes go through the same page, refresh and filter code. Mode only decides where the transform and the visibility flag come from.

### 3.2 Pages as prefabs

`OpenPuttTimeSlicer` proves abstract UdonSharpBehaviours with abstract/virtual methods work in this project. That's the unlock — pages can be a real class hierarchy. (V2 doesn't *inherit* from it — see 3.4 — but it's still the existence proof for the language feature.)

Worth being precise about what it proves, since the two are easy to conflate: `OpenPuttTimeSlicer` is subclassed and calls its own abstract `_OnUpdateItem` from its own `Update`, which is self-dispatch inside a single behaviour and single Udon program. The page framework needs something stronger — a `ScoreboardPage[]` typed as the abstract base, with `pages[i]._Refresh()` dispatching to the derived override *across a behaviour boundary*, including methods that take parameters.

**That's confirmed working**, so the page hierarchy is a settled foundation rather than a bet, and no spike is needed before Phase 2. It is, however, the single feature every phase after 2 depends on: if a future refactor "simplifies" the abstract base into a concrete class with an `int pageType` and a switch, the public page API goes with it.

**`ScoreboardPage` is a public API.** World authors write their own pages, so the contract below is versioned and documented, not an internal detail:

```csharp
public abstract class ScoreboardPage : UdonSharpBehaviour
{
    [HideInInspector] public ScoreboardInstance board;   // wired by the editor tool

    public int pageId;                  // stable, never renumbered. built-ins 0-99, author pages 100+
    public string tabLabel;
    public Sprite tabIcon;
    public int sortOrder;
    public ScoreboardPageAccess access; // Everyone / InstanceMaster / DevWhitelist
    public bool wantsPerFrameUpdate;    // DevPage only
    public bool wantsDataRefresh;       // ScorePage only

    public GameObject contentRoot;
    public Canvas[] canvasesToToggle;   // the nested-canvas workaround, in one place

    public void _Show() { /* canvasesToToggle on, then _OnShown() */ }
    public void _Hide() { /* canvasesToToggle off, then _OnHidden() */ }

    protected abstract void _OnShown();
    protected abstract void _OnHidden();
    public abstract void _Refresh();
    public virtual void _OnAccessChanged() { }   // re-gate sections/controls; see below
}
```

`_Show` / `_Hide` toggle **canvases only** (2.8). `contentRoot` stays active for the life of the board — it exists so the reconcile knows where to write and so a page can be found, not as a visibility switch. Nothing under a page may assume `Start`/`OnEnable` runs when it becomes visible; `_OnShown` is the hook.

**Access is per-section, not just per-page.** `ScoreboardPageAccess` on the page isn't enough to reproduce V1, which has two distinct dev tiers:
- `LocalPlayerCanAccessDevMode` — whitelist, or `enableDevModeForAll`.
- `LocalPlayerCanAccessToolbox` — the same, **or** `devModeTaps >= 30`, an easter egg where tapping the Settings tab 30 times unlocks the dev tab. The counter resets on leaving Settings while still below 30.

The dev *tab* appears for either tier, but `devModeSettingsBox` inside it only appears for the stricter one. So the page needs an access gate on **sections within it**, not only on itself: a small `ScoreboardPageSection` with its own access level that the page shows or hides. The 30-tap unlock keeps working because the tab strip owns tab clicks (3.2) and can count them — but it's the kind of undocumented behaviour a rewrite drops silently, and people rely on it.

**Individual controls need gating too**, not just pages and sections. `SettingId.PracticeMode` lives on the ordinary Settings page but is instance-master-only (1.4), so `ScoreboardSetting` carries its own access level (3.6). Three tiers of gate — page, section, control — all reading the same evaluator on the manager.

**Access is re-evaluated at runtime, not once at startup.** Three things move it mid-session (1.4): `enableDevModeForAll` being toggled in Settings, the 30-tap unlock crossing its threshold, and the instance master leaving. So the manager exposes `_ReevaluateAccess()`, which recomputes both dev tiers plus master status, then walks pages calling `_OnAccessChanged()` and re-runs the tab strip layout. It's called from `OnPlayerLeft`, from the dev-toggle setting's apply path, and from the tap counter — never per frame, and never per refresh. Only the current page needs re-gating immediately; the rest re-gate in `_OnShown`.

Because the page API is public, the following are requirements rather than nice-to-haves:
- **The field set above is frozen.** Additions get defaults that mean "behave as before"; nothing is renamed or removed without a major version.
- **`pageId` uniqueness is enforced** by the reconcile, with a clear error naming both offending prefabs. Built-ins reserve `0–99`, so an author page in that range is a validation error rather than a silent collision.
- **The reconcile is defensive.** A page prefab missing its `ScoreboardPage`, missing `contentRoot`, or throwing during reconcile is reported and skipped — it must never leave a half-built board or block a scene build.
- **Custom pages survive package updates** for free, because the tool only ever writes to board instances in the author's scene (3.7).
- **Docs are a deliverable**, not an afterthought: the contract, a minimal example page, and what each lifecycle method may assume about `board` and the manager being ready.

Each built-in page is its own prefab in `Runtime/Prefabs/UI/ScoreboardPages/`, page behaviour on the root. The exception is `ScorePage`, whose prefab is *generated* into the author's `Assets/` because its size depends on their course count (3.7) — the script ships in the package, the prefab doesn't.

**Page library on the manager:** `GameObject[] pagePrefabs`, in display order. That's the "one simple place in the editor" — drop a prefab in the list, hit rebuild, every board gets it with a tab.

**Tab strip is generated,** not hand-wired. One `ScoreboardTab.prefab` per page, label/icon pulled from the page, `onClick` wired to `manager._ShowPage(index)` with the index baked in by the reconcile — which switches every board, since the current page is global (3.1). Deletes the 8 hand-wired tab `Button` fields and the `UpdateTabColours()` cascade; the strip owns selected/unselected colouring uniformly. Access-gated tabs hide themselves on `_ReevaluateAccess()` and the strip re-lays-out with a real layout instead of V1's hardcoded `rightTabsPanel.sizeDelta = 1.01f/0.81f`.

`wantsPerFrameUpdate` drives `page.enabled` — a page's `Update` only runs while it's the current page on a leased board. That replaces `ScoreboardDevModeUpdater` and closes budget C.

**And "per frame" is the wrong rate for the only page that wants it.** `ScoreboardDevModeUpdater` currently does four interpolated TMP writes with `:F2` format specifiers every frame, plus a lazily-resolved `scoreboard.manager.openPutt` chain and reads through `localPlayerManager.golfClubHead` / `.golfBall` / `.golfClub` / `openPutt.controllerTracker` — property chains re-walked 90 times a second for text nobody can read that fast. So `DevPage`:
- **Ticks at a fixed low rate** (~10 Hz) off its own accumulator, not every frame. Debug readouts don't need frame parity, and this alone is a 9× cut on the whole page.
- **Hoists every reference into a field once** when the page is shown — club head, ball, club, controller tracker — so a tick is array-free local reads plus the formatting.
- Still only runs while it's the current page on a visible board, which is what `wantsPerFrameUpdate` already buys.

### 3.2a Layout

**The layout gets its own design pass before any page prefab is built. Nothing below is settled.** This section is a sketch of what the rebuild makes *possible*, not a specification — the real layout is designed at the top of Phase 2, and the page prefabs are built against that, not against this list. Treat every bullet here as "worth considering", and expect the design pass to overrule some of them.

Sketch:

- **One tab row, not two.** V1 splits into `TabsLeft` and `TabsRight` and hardcodes widths when the dev tab appears/disappears. One row with a real layout group would handle both.
- **Possibly five tabs instead of six**, if the speed-golf tab becomes a toggle on the score page driving `manager.speedGolfMode` (3.1), leaving Scores, Info, Settings, About and Dev.
- **No ScrollRect** (3.3).
- **Chrome trimmed** toward header + content, so a page prefab mostly has to fill one rect. Whatever the design pass lands on, the simpler the frame a page has to fit, the easier the public page API is to write against.

Two constraints the design pass does **not** get to overrule:

- **The Info page keeps its VR / Desktop / Mobile buttons.** Auto-selecting the local player's platform stays, but a player can still switch — useful when someone's helping a friend on another device. The bespoke colour capture/restore can be replaced with the tab strip's uniform selected/unselected colouring, but the capability stays. This also means the "don't yank a player who already switched tabs" guard in `SelectInfoTabForPlatform` stays relevant (3.8).
- **No LayoutGroups in the score grid** (3.3). The redesign applies to the chrome; the grid stays arithmetic.

Three things the redesign has to carry across, all currently buried in `Scoreboard.Start`:

- **The board's rect is forced to `z = 0.01`** — "so the laser pointer works properly". A board with real thickness breaks UI raycasting, and an anchor with a non-uniform scale can reintroduce it, since the lease writes scale from the anchor's lossy scale. Bake the thin-Z on the generated page and clamp it on lease, or this resurfaces as "the board is there but nothing is clickable", which is a miserable thing to debug.
- **The About page substitutes `{OpenPuttCurrVer}`** in its credits text and appends `manager.extraCreditsText` if the author set one. Both are requirements, not decoration: the version string is the first thing to ask for in a bug report, and `extraCreditsText` is a world author's own credit.
- **The default page is Info, not Scores.** Both `Scoreboard.Start` and `ScoreboardManager.requestedScoreboardView` initialise to Info, so a player entering the world is shown the instructions first. Keep that, and state it in the startup order (3.8) — "resolve the current page" is otherwise going to resolve to whatever's at index 0.



### 3.3 The score grid

Delete `ScoreboardPlayerRow` and `ScoreboardPlayerColumn` as behaviours. `ScorePage` holds flat arrays:

```csharp
public int rowCount;      // players + 2 (header, par)
public int columnCount;   // courses + 2 (name, total)

public RectTransform[] rowTransforms;    // rowCount
public Canvas[] rowCanvases;             // rowCount
public RectTransform[] cellTransforms;   // rowCount * columnCount, row-major
public Image[] cellBackgrounds;          // rowCount * columnCount
public TextMeshProUGUI[] cellTexts;      // rowCount * columnCount

// shadowed source values, all int - win #6. never shadow strings or Colors
private int[] lastValue;       // the encoded quantity that produced the text (see below)
private int[] lastPaletteId;   // which palette entry the background is showing
private int[] lastFgPaletteId;

// which player each row slot currently shows, by PlayerID. -1 for empty, header, par
private int[] rowPlayerId;
private PlayerManager[] rowPlayers;

// which course each visual column shows; -1 for name/total - win from 2.7
public int[] courseForColumn;
public int[] columnForCourse;   // inverse map, -1 when the course isn't in this view
```

Indexing is `row * columnCount + col`. Header and par are rows 0 and 1 of the same grid.

**`rowPlayerId` is what makes win #1 dispatchable.** The manager needs player→row to route "player X's course C changed" to two cells, and the page needs row→player to know whether a slot's occupant changed. `PlayerID` ints are the right key: comparing them is a VM op, where comparing `PlayerManager` references would tempt an `Utilities.IsValid` per row (rule 4). Keep the reference alongside for the actual data reads.

**Encode the whole displayed quantity into `lastValue`, and get player changes for free.** The guard is only sound if the shadow captures *everything* that determines the text — so the sentinel cases fold into the int rather than sitting beside it as separate state: a distinct negative for "no score", one for `NotStarted`, one for `Playing`-and-masked. Then:

- **A row changing occupant needs no shadow invalidation at all.** If row 3 goes from player A to player B and B's score on course 5 is also 4, the text is already correct and the write is correctly skipped. The shadow describes the *cell*, not the player. An implementer's instinct will be to clear the shadows when a player moves rows — doing that throws away most of the win.
- **But a change in what the number *means* must invalidate.** A speed-golf toggle turns "4 strokes" into "4 seconds", and `lastValue` compares equal while the correct text differs — score 4 and a 4-second time both encode as 4, and the second renders `0:04`. So the speed-golf toggle resets the shadow arrays to an impossible sentinel before the following full refresh. With views baked (2.7) that's the *only* case: a column never changes which course it shows, so there's no filter path to invalidate on.

That asymmetry — invalidate on mode change, never on player change — is the whole reason the shadow is on the value rather than on the player.

**`columnForCourse` is the inverse map, and it's what keeps the steady-state path O(1).** `_RefreshLiveCells` is handed a *course* index, but cells are addressed by column. Without the inverse map that's a scan of up to 36 columns per live update, on the hottest path in the system, to find one cell. It also gives the cheapest possible early-out: on a filtered view, a change to a course this page doesn't show returns immediately instead of doing anything at all. Both directions are baked by the generator (2.7) and never written at runtime.

**The API is built around win #1**, two paths not one:

```csharp
// steady state: 2 cells. called when one player's live score changes
public void _RefreshLiveCells(int row, int courseIndex);

// rare: full row. lease, first show, reset, rank change, course completion
public void _RefreshRow(int row);

// time-sliced bulk path - the loop lives HERE, not in the caller (rule 2)
public void _RefreshRowRange(int startRow, int count);

// header and par rows. speed-golf toggle only, never routine
public void _RefreshStaticRows();
```

Inner-loop rules, all from 2.2:
- Read `player.courseScores` / `courseStates` / `courseTimes` **once per row** into locals, then index locally (win #7).
- Read course par/type/name from the manager's cached flat `int[]`/`string[]`, never `openPutt.courses[i].parScore` (win #8).
- Guard every write on an `int` compare against the shadow arrays (win #6).
- `Utilities.IsValid` on the player once per row, before the column loop — never per cell.
- Skip rows past the player count entirely — disable the row canvas and `return` before the column loop (win #4).
- **Guard the row canvas write like any other.** V1's `IsVisible` setter writes `rowCanvas.enabled = value` unconditionally, every refresh, for every row — an extern write plus a canvas state change even when nothing changed. Shadow it as a bool and only write on a transition. It's win #6 applied to canvases, and it matters more than a colour write because toggling a canvas drops and rebuilds its batch (2.8).
- Hoist `columnCount` and the row base index into locals before looping. With views baked there is no separate "visible" count — every column in the grid is shown, which removes a whole class of off-by-one between the grid's size and what's on screen.
- Look up score strings in a precomputed `string[]`; build `m:ss` from cached parts rather than `TimeSpan.ToString` (win #12).

**Bake a display mode per column, don't branch per cell.** V1's cell code switches on `course.courseType` and re-derives the special cases every refresh. Course type is static and a column's course never changes (2.7), so the generator bakes `columnDisplayMode[]` alongside `courseForColumn[]`: name / total / standard score / distance-in-metres / target count / time. The inner loop switches on a local int with no course lookup at all, and speed golf selects between the score and time variants without touching the array.

The display rules that mode array has to carry, all from V1 and all easy to lose in a rewrite:
- **`DrivingRangeDistance`** — text is `{score}m`, and the par comparison is **inverted**: shorter than par is bad, at-or-over par is good. Needs its own precomputed `string[]` with the `m` suffix.
- **`DrivingRangeWithTargets`** — plain count, no par colouring at all.
- **Both driving-range types are excluded** from the "finished all courses" test that decides whether the total counts as under par.
- **`999999` is a sentinel** for no score / no time and shows `-`.
- **`DidWeirdThingHappen()`** appends `*` to the total.
- **`CourseState.Playing`** takes the current-course colours, and shows `-` instead of a score for remote players when `playerSyncType != All`, because the value would be stale.
- **`CourseState.NotStarted`** shows `-`.
- **`Skipped` and `PlayedAndSkipped` are different states**, and the under-par test accepts only `Completed` and `PlayedAndSkipped` — a plain `Skipped` course never counts. Both need explicit text, which is the next point.
- **Every state must produce text.** V1's `DrivingRangeDistance` branch only writes for `Completed` and `Playing`, so a `Skipped` driving range cell keeps whatever was in it. With fixed row slots (below) and int-shadowed guards, "keep the old text" means showing the *previous player's* value, so the mode switch has to be exhaustive per state. Treat any missing combination as a bug rather than a don't-care.

**Times are not ints, and mid-course they aren't times.** `PlayerManager.courseTimes` is a `long[]` and `PlayerTotalTime` is a `double`, and while a course is in progress `courseTimes[i]` holds the *start* unix timestamp, not a duration — it only becomes a duration when the course finishes. That's why V1 shows `-` for a `Playing` course in speed-golf mode and why its live-elapsed-time attempt is commented out. Consequences:

- **Speed golf shows `-` for `Playing` for everyone**, local player included. This is a different rule from the `playerSyncType` one above, which only affects remote players in score mode. Miss it and the cell renders a ten-digit unix timestamp.
- **Shadow the displayed value, not the source.** `lastValue[i]` stays `int` (win #6 depends on it) and holds the *rendered* quantity — seconds for a time cell, clamped into the table range. Never the raw `long`/`double`.
- The under-par test for times carries an extra condition V1 has and nothing documents: `PlayerTotalTime > 0` as well as `Completed`.

**The header and par rows have their own display rules**, which the checklist above doesn't cover because they aren't score cells. Both need the same `columnDisplayMode` treatment rather than ad-hoc branching:

- **Par row, name cell:** the literal `"Par"`. **Total cell:** `TotalParTime` as `m:ss` in speed golf, else `TotalParScore` — filtered to the board's group (2.7), not the world-wide value.
- **Par row, course cells:** `m:ss` of `parTime` in speed golf; `{parScore}m` for `DrivingRangeDistance`; **`-` for `DrivingRangeWithTargets`**, which has no par at all; otherwise `{parScore}`.
- **Header row, name cell:** the literal `"#"`. **Total cell:** empty string, not `"Total"`.
- **Header row, course cells:** `scoreboardShortName` when the author set one.
- Both rows use a single palette entry for text and background (`text` on `nameBackground2` today), so they're palette-shadowed like everything else.

**The header's fallback label breaks under filtering, and it's worth fixing while it's being rewritten.** V1 falls back to the *display column number* when `scoreboardShortName` is blank — fine in a world with one course set, actively wrong on a filtered board, where a Back-18 board would label holes 19–36 as `1`–`18`. Fall back to the course's hole number instead, so a column always names the hole it shows regardless of which board it lands on. That also makes the runtime label agree with the authoring toggle grid (3.7a), which labels blank-named courses `Hole {holeNumber}`.

**String tables need declared bounds and a fallback.** The precomputed `string[]`s (win #12) are: scores `0..maxTableScore`, distances `0..maxTableScore` with the `m` suffix, and `m:ss` for `0..maxTableSeconds`. Both bounds are computed at startup from the actual courses — `sum(maxScore)` and `sum(maxTime)` — so they're exactly as big as they need to be, and anything outside falls back to formatting rather than indexing out of range. The sentinel and `-` are table entries too, not special cases in the loop.

**Two GameObjects per cell is the floor, not an inefficiency.** The generator produces a cell (Image) with a text child (TMP), as V1 does, so 280 cells is ~560 objects. That's structural: `Graphic` requires a `CanvasRenderer`, a GameObject has exactly one, and `Image` and `TextMeshProUGUI` are both Graphics — put them on the same object and only one renders. Anyone reading the object count and reaching for the obvious halving should stop here.

**No LayoutGroups anywhere in the grid.** Column and row positions are computed arithmetically, as V1 does. A `HorizontalLayoutGroup` or `ContentSizeFitter` on hundreds of cells triggers layout rebuilds on every change and would undo most of Part 2. This is the easiest thing to accidentally reintroduce during the layout redesign (3.2a) — the redesign applies to the chrome, not the grid.

**Fixed row slots, positional striping.** Rows never move; content moves between rows. The alternating row background is therefore a property of the slot, not the player, so a rank change needs no extra colour bookkeeping — V1's `SetPosition` returning "did even/odd flip" disappears.

Grid cell prefab changes: **auto-sizing off** with a fixed font size, **rich text off**, `parseCtrlCharacters` off (win #11). Keep `raycastTarget` at 0 — already correct.

Rich text goes off on **name cells too**, not just numeric ones. V1 has `m_isRichText: 1` on every cell and names come from `displayName`, so today a display name containing markup renders as formatting instead of as text. Off is both cheaper and correct. Name cells still need overflow set to ellipsis rather than auto-size, since names vary in length and the column doesn't.

**Fonts: fix the broken reference, keep static atlases.** Decided, so it doesn't get relitigated while building the generator:

- `PlayerListColumn.prefab`'s text points at a font asset GUID that **doesn't exist in the project** — there's no `Roboto-Bold SDF` on disk or in git, only references to it from that prefab and three `Roboto-Bold*.mat` materials (two of which are the golf ball name labels). So cells currently fall back to TMP's default font at runtime instead of rendering as intended. The generator sets the font explicitly on every cell, so this has to be resolved either way: add the missing asset, or repoint to the `Roboto-Light SDF` that is in the package.
- **Atlases stay static** (`m_AtlasPopulationMode: 0`, as they are today) with no fallback chain. Nothing grows at runtime, nothing re-uploads a texture, memory is fixed — which is the right trade on Quest and needs no per-frame thought.
- **Consequence, accepted deliberately:** a display name using characters outside the baked atlas renders blank. That's exactly V1's behaviour today, so it's not a regression, and it's a font-asset configuration change rather than a scoreboard one if it's ever revisited.
- Numeric cells were always going to be fine — they only ever show digits, `-`, `*`, `m` and `:`.

**Keep per-row canvases.** Each `Canvas` costs a little memory and breaks batching (≈1 draw call per row), but it means changing one cell rebuilds only that row's mesh instead of the whole board's 280 cells. For frequently-changing data that's the right trade. Don't let anyone "optimise" them away. On Quest this is the main draw-call line item — 14 per board, ×3 boards — so it's worth confirming against a real headset before the layout is locked.

**No ScrollRect.** V1's scroll code is dead (1.4) and the ScrollRect drags in a raycast-target panel and a viewport mask. Rows are sized to fit the board, so there's nothing to scroll. When there are more players than rows, the window slices around the local player exactly as V1's `GetPlayerList()` already does — that behaviour is preserved.

### 3.4 Refresh scheduling

Keep the two-tier scheme:
- **1 Hz** anchor visibility + leasing pass (`SendCustomEventDelayedSeconds` loop, as V1).
- **On-demand** data refresh with a fixed per-frame work quantum, hand-rolled on the manager rather than inherited (below).

**Inline the slicing; don't inherit `OpenPuttTimeSlicer`.** `ScoreboardManager` is its only subclass, so the base class exists solely to serve the scoreboard and there's no shared-code argument for keeping it. `PlayerListManager` already hand-rolls the identical pattern in its `LateUpdate`, so a local loop is the codebase norm, not a deviation.

What inheriting actually costs, per `Update` tick while the slicer is enabled:
- `_StartUpdateFrame()` — a virtual call, unconditional.
- `_stopwatch.Restart()` — an extern, unconditional, **even when `numberOfObjects == 0`**.
- `_currentUpdateIndex` and `numberOfObjects` are behaviour fields, so the loop counter and bound are program-variable accesses rather than locals (rule 8's problem, one level up).
- `_stopwatch.Elapsed.TotalMilliseconds` **per item** — an extern returning a `TimeSpan`, then a property converting to `double`, on every iteration.
- `_OnUpdateItem(index)` — a virtual call per item.

It's also asymmetric: the `numberOfObjects == 0` early-out returns after `_StartUpdateFrame()` has run but before `_EndUpdateFrame()`, so the two don't pair. Not a bug in current use, but not something to build on.

**Most of that evaporates from win #9 alone**, which is worth being clear about so the inlining isn't credited with the wrong saving. Once each item is a *chunk of rows* rather than a row, there are a handful of items per full refresh instead of hundreds, and per-item virtual dispatch and clock reads stop mattering. What's left, and what inlining actually buys:

- **No clock in the refresh path at all.** Budget the work by *counting*, not timing: a fixed quantum of cells per frame. `Stopwatch.Elapsed` is an extern, so a time budget pays an extern to decide whether it can afford the next extern — and the stopwatches in V1 exist mostly so their timings could be printed while debugging, not because the loop needs a clock to be correct.
- **The quantum is cells, not rows**, derived once at startup from the page's `columnCount` so a 36-course board and a 9-course board move the same amount of work per frame without separate tuning. The manager converts it to a row count, calls `page._RefreshRowRange(start, count)`, and the page loops with no clock reads and no per-item checks whatsoever — just a counter, a bound and a hoisted base index (rules 1 and 8).
- **Locals for the loop**, which the base class structurally cannot do because its loop and its work live in different behaviours.
- The honest tradeoff: a time budget self-corrects when a device is slow, a fixed quantum doesn't. It's acceptable here because Udon cost per cell is instruction count, which barely varies by device, and because win #1 makes the bulk path rare — it runs on lease, rank churn and course completion, not continuously. Set the quantum conservatively and it's strictly cheaper than measuring. If profiling later shows a device-specific problem, one `Time.deltaTime` read per *frame* (not per item) can scale the quantum down, and that stays a single extern.
- **Stopwatches only exist under `debugMode`.** The measurement harness (3.9) still needs real timings, and that's the right place for them: created when debug is on, absent otherwise. Nothing in the normal path allocates or reads one — which also removes V1's unconditional `Restart()` before the work check and `GetPlayerList`'s `Stopwatch.StartNew()` on every call.
- **No work, no `Update`.** The `enabled = false`-when-idle pattern is V1's and is correct (2.1, budget C) — keep it.

**Calibrate the quantum once, in development, then ship without the clock.** The obvious objection to counting instead of timing is "how do you pick the count?" — you measure it with the harness (3.9), which has a stopwatch precisely because it's debug-only: run the fake-player load, read ms per cell, pick a quantum that lands the chunk comfortably under ~0.3 ms, and make that the default constant. The clock is a development instrument, not a runtime dependency. Re-calibrate if the cell refresh code changes materially; the harness is already scheduled before the grid work for exactly this kind of question.

`OpenPuttTimeSlicer` becomes unused when V1 goes. Leave the file in place — it's a reasonable utility for world authors and costs nothing unsubclassed — but it stops being part of the scoreboard's story.

**Slice by (board, row range), not by row (win #9).** V1's slicer calls `_OnUpdateItem(rowIndex)` and then loops boards inside it, calling across the behaviour boundary once per row per board — ~960 cross-behaviour calls per full refresh. Instead each slicer item is a chunk of one board's rows, dispatched as a single `page._RefreshRowRange(start, count)` call, with `count` sized from the frame's remaining cell quantum. Cross-behaviour calls per refresh drop to a handful, and the row loop runs inside the page where every access is local (rule 2).

Other changes:
- **Coalesce.** Make `maxRefreshInterval` real: a request inside the window sets a flag rather than queueing another pass (win #5). V1's `updatesRequested` counter goes away. Do it with a scheduled flush, not a timestamp comparison — the first request calls `SendCustomEventDelayedSeconds(nameof(_FlushRefresh), maxRefreshInterval)` and every request after that just sets a bool. The VRChat scheduler does the timekeeping, so coalescing costs one extern per *window* rather than a clock read per request, and rule 10 holds here too.
- **Gate.** One int compare of `currentPageIndex` against the cached score-page index skips the whole score path for the entire pool; then only leased boards are refreshed (win #2). Both operands are ints on the manager, so the gate costs nothing (3.1). Owned boards carry their own page index, so they're a second compare against the same cached value — and a visibility check, since a stowed portable menu needs no refresh at all.
- **Route live score changes to `_RefreshLiveCells`,** not to a full pass. The bulk path is for lease, rank change and course completion only (win #1).
- **Honour dirty flags — but the manager owns clearing them, not the page.** `scoreboardRowNeedsUpdating` is one flag per *player* while there are several boards showing that player. V1 clears it inside `ScoreboardPlayerRow.Refresh`, which is harmless only because nothing reads it (1.4); the moment it actually gates work, the first board to refresh a row clears the flag and **every other board skips that player**, showing stale scores on two of three boards. So the flag is consumed by the refresh pass and cleared once at the end of it, after all leased boards have been through — never by the page. A per-board "needs full refresh" flag, set on lease, covers a board returning from unleased.
- Sorted lists computed once per ranking source per refresh, not per board — and for most worlds there's exactly one, the existing global list (2.7).
- Preallocate the `bool[]`/`float[]` scratch arrays the 1Hz pass currently news up every second. If the anchor pass ever needs slicing, it slices by anchor count, not by elapsed time — same rule as above.

**The blind spot in win #1 is rank churn.** A rank change shifts every row between the old and new position, and each of those rows needs a full 20-cell refresh. Player join, leave, and score reset do the same. In a busy instance that can fire often enough to undo the incremental saving, so:
- Refresh **only the affected span** on a rank change, not the whole board. `PlayerListManager` already knows the old and new index from its binary insert, so the span is known exactly. The same span drives position stamping (2.7) — `StampPositions()` stops re-walking whole lists and only re-stamps between the old and new index, with full re-stamps reserved for roster changes.
- Rank churn only matters while the score page is open, and the 1 Hz + coalescing gate already caps how often any of it lands.
- Leaving players are the one case that must not be deferred, since a stale row would show a departed player. Treat leave as an immediate span refresh rather than a queued one.

**The window is worse than rank churn, and the plan didn't account for it.** When there are more players than rows, `GetPlayerList()` slices a window centred on the local player. Recentring the window by even one position changes the player in **every** row, so every shadowed value mismatches and the whole board does a full refresh — the exact opposite of win #1. And it triggers on things that happen constantly in a busy instance: anyone above the local player gaining a stroke, anyone joining or leaving.

So the window gets the same treatment as leases:
- **Sticky window with a margin.** Keep the current `startPos` and only re-slice when the local player would come within N rows of either edge (N ≈ 2), then re-centre. A player mid-table stops moving the window at all while others shuffle around them.
- **Clamp before comparing.** Re-slicing when the window would land in the same place is free to detect and saves the full refresh.
- Only the score page cares, and only when `playerCount > rowCount` — the common small-instance case never re-slices because the window is everyone.
- This is also the case the fake-player debug mode (3.9) must cover, since it's invisible below the row count and unmissable above it.

### 3.5 Anchor pass

Port the maths, cut the externs (win #13):
- **Bake the anchor's rect explicitly**, rather than reading it off whatever canvas happens to be there. V1's `ClosestPointOnBoard` derives the board rectangle from `backgroundCanvas`'s `RectTransform`, falling back to `nearbyCenterTransform` and then the anchor's own position — so the placeholder visual and the collision geometry are the same object, and an anchor with no placeholder silently degrades to a point. In V2 the editor tool bakes corners onto the anchor, and the placeholder canvas (3.5) is purely cosmetic. Decoupling them means "what it looks like when empty" and "how big it is" stop being the same decision.
- Cache the RectTransform at build time — V1 calls `GetComponent<RectTransform>()` inside `ClosestPointOnBoard`, which runs **two or three** times per anchor per second: once for the head, once for the screen/photo camera when active, and once more in `ShouldBeInteractable` for anchors that hold a board.
- For `isStatic` anchors, compute world corners, position, rotation and lossy scale **once** and never touch the transform again. `GetWorldCorners` runs on every one of those calls today, for boards that never move.
- `boardIsFacingPlayer` reads `transform.forward` per call — another cached value for static anchors.
- Compare squared distances where only an ordering is needed; skip the `sqrt`.
- Cache `ViewDotThreshold`'s trig per FOV value — only the `Mathf.Lerp` over proximity varies.
- Skip transform writes that wouldn't change anything, instead of re-parking already-parked boards every second.
- **Single-pass selection.** V1 loops all anchors to qualify them, then for *each* anchor it needs to drop, rescans the whole `claimed[]` array to find the farthest — an O(excess × anchors) nested loop. Collect qualifying anchors into a preallocated array in one pass while tracking the farthest few inline, so oversubscription costs no extra scans.
- Time-slice the anchor pass itself if a world has many anchors, so the "can take 1+ms" spike never lands in one frame.

**Sticky leasing** replaces `currentVisibleScoreboardID++`: an anchor that had a board last pass keeps the same board; only newly-qualifying anchors draw from the free list. With V1's park-and-reassign, a churning lease cost two transform writes; now it costs a canvas batch rebuild and a full refresh (2.8), so stickiness matters more than it did. Those two are the whole reason — with views baked (2.7) there's no relayout to avoid and no preference about *which* board serves an anchor, so any free board will do.

**Leases need hysteresis.** Qualifying on a single radius test at 1 Hz means a player standing near `nearbyMaxRadius` — or slowly turning their head at the edge of the FOV cone — toggles a board on and off once a second forever, and each toggle is now a batch rebuild plus a full 280-cell refresh. So:
- The release test uses a **wider radius and a wider cone** than the acquire test. One `leaseHysteresis` value (a fraction, ~1.15×) covers both; it's the same trick V1's `closeRangeFullVisibilityRadius` uses in spirit.
- An anchor that stops qualifying is released after **N consecutive failing passes** (2–3), not the first, so a single bad frame of head pose doesn't drop a board.
- **Never release a board someone is interacting with.** Disabling the canvas mid-slider-drag kills the drag. Defer release while the board reports an active pointer, and treat that as "still qualifying" for the dwell counter.
- A board being stolen for a closer anchor bypasses the dwell only when the pool is genuinely oversubscribed — otherwise a free board is used.
- **Hysteresis applies to the geometric tests only.** `NearbyAndCourseFinished` also gates on `LocalPlayerManager.courseStates[attachedToCourse] == Completed`, which is game state, not geometry: it flips deliberately when the player finishes or resets a course, and it must take effect on the next pass rather than being held for two or three by the dwell counter. Radius and cone get hysteresis; state does not.

On lease: apply transform + scale (clamping the thin Z from 3.2a), set `activeScoreView` from the anchor, show `manager.currentPageIndex`, enable the root canvas and interaction colliders, flag a full refresh. On release: disable the root canvas and colliders and clear the refresh flag; the GameObject stays active (2.8). There's no per-anchor state to save — the pool's page is shared and mode is fixed at bake time (3.1), so the lease carries geometry and a view index, both of which are plain assignments.

**Baked geometry goes stale, and that's the cost of baking it.** Every extern cut above depends on precomputing an anchor's corners, position, rotation and scale — which means an author who nudges an anchor, scales the board model, or resizes the backing mesh gets a board that leases to where the anchor *used to be*, with a hit area to match. Nothing in the scene tells them why. So:
- The reconcile re-bakes anchor geometry unconditionally, and the build processor does too. Baking is cheap; deciding whether it's needed is not worth the bug.
- The anchor's inspector shows its baked position and size, and flags a mismatch against the live transform — a one-line "geometry is stale, rebuild" warning is the difference between a five-second fix and an afternoon.
- `isStatic` defaults to true because almost no board moves, but the failure mode when it's wrong (board sits where the anchor started) is confusing enough to earn an explicit tooltip. A board on an elevator or a moving platform must turn it off, and then pays the per-pass transform reads deliberately.
- The world-author board prefab (Phase 6) is where this bites hardest, since scaling the model is the normal way to get a bigger board. Its anchor child must derive its rect from the backing mesh, and rebuilding must be part of the documented "I resized my scoreboard" flow.

**The anchor's placeholder canvas is part of lease state, and the plan above forgets it.** V1's anchors own a `backgroundCanvas` — the blank board face an author sees when no board is leased there, so a physical scoreboard model isn't a hole in the world. The 1 Hz pass disables it when a board arrives and re-enables it on release, but only if it was enabled to begin with (`CanvasWasEnabledAtStart`, captured in `Start`), so an author who deliberately turned it off doesn't get it forced back on. V2 keeps all of that:
- On lease, disable the anchor's placeholder. On release, re-enable it if it was on at bake time.
- The initial state is **baked by the editor tool**, not captured in `Start` — one less thing derived at runtime (3.8).
- Placeholder and board are never both visible, including during the dwell before a release, since the board is what's still showing.

### 3.6 Settings

Keep the `ScoreboardControl` pattern — the hand-written switch is unavoidable because Udon can't reflect UdonSharp properties. Rename to `ScoreboardSetting`; `SettingsPage`/`DevPage` own `ScoreboardSetting[]` instead of the board. Keep explicit integer values on `SettingId` (V1 already does this with the right comment about never renumbering — `pageId` follows the same rule for the same reason). `labelFormat`, `refreshMenuOnChange`, `controlsToRefreshOnChange`, `saveOnChange` and the HSV ball-colour cache all port across; the HSV cache moves from the board to `SettingsPage`.

**Settings changes must now fan out across the pool, and that's a direct consequence of 3.1.** V1's `AfterChange()` refreshes only its *own* board's controls, which was sufficient precisely because `OnPlayerOpenSettings` guaranteed no other pooled board was showing Settings. Once anchored boards share a page, three boards display the same settings simultaneously, and changing a toggle on one must update the same toggle on the others or they sit there visibly stale. So:

- `AfterChange()` asks the manager to refresh that setting across boards, rather than refreshing locally. Only **leased** boards need it — the rest are canvas-off and get refreshed on lease anyway (win #2 applies here too).
- **`suppressCallback` becomes load-bearing.** V1 sets it around programmatic `slider.value` / `dropdown.value` writes so the widget's own `OnValueChanged` doesn't re-enter. A fan-out that writes widget values on other boards without setting their suppression flag first gives you `OnChanged` → write → fan out → `OnChanged`, i.e. an infinite loop on the first slider drag. Set it on the *target* control, not the source.
- `saveOnChange` fires `_SavePersistantData()` once per change, not once per board.

**Changing a global setting steals ownership**, and that has to be carried over deliberately rather than rediscovered. V1's `ScoreboardControl` does `OpenPuttUtils.SetOwner(localPlayer, OpenPutt.gameObject)` then `OpenPutt.RequestSerialization()` for the settings that live on `OpenPutt` (`practiceMode`, `enableDevModeForAll`), and `Player.RequestSerialization()` for the per-player ones. So a world-wide setting change is an ownership transfer of the `OpenPutt` object, which is fine but worth being explicit about: master-gating `PracticeMode` limits *who* can do it, it doesn't avoid the transfer. Per-player settings never touch `OpenPutt`'s ownership.

**Per-control access.** V1 hardcodes the master check inside the `PracticeMode` case. In V2 each `ScoreboardSetting` carries an access level so the gate is declarative and the switch only does the bind, and it re-gates on `_OnAccessChanged()` (3.2) so a master change is reflected without reopening the page. A gated control that the local player can't change is shown disabled rather than hidden — it's information about the instance, and hiding it makes the page jump around when the master leaves.

`hideInactivePlayers` needs no `SettingId` — it was never a runtime control, just an inspector field on the manager, so removing it is one field plus its two readers (1.4).

Cost is load-time only — ~26 settings × 4 boards (3 anchored + the portable one) is ~104 behaviours, and they have no `Update` and sit on a hidden page, so they cost nothing per frame.

**Move the reset-scores logic out of the scoreboard.** V1's `OnResetConfirm` lives on `Scoreboard` and does far more than reset a score. That's game-state surgery driven from a UI script, duplicated onto every board. In V2 it becomes one method on `PlayerManager` (or `OpenPutt`) that the settings page calls; the page keeps only the three-button confirm/cancel dance. The moved method has to do all of this, in order, because every step is load-bearing:

1. `_ResetPlayerScores()`.
2. **Fire `eventHandler.OnPlayerScoreReset(owner)`** — this is a documented `OpenPuttEventListener` hook, so world authors have code attached to it. Easiest thing in the list to drop and the most visible when it goes.
3. Drop the club pickup and respawn the club via its `openPuttSync._Respawn()`.
4. Drop the ball and respawn **the ball**, then clear `BallIsMoving`.
5. `_RequestSync()`, `_UpdateTotals()`, `openPutt._OnPlayerUpdate(pm)`.

**Step 4 is currently broken and should be ported fixed, not faithfully.** V1's ball branch calls `pm.golfClub.openPuttSync._Respawn()` — the *club's* sync, inside the ball block — so the club is respawned twice and the ball is never respawned at all. `GolfBallController` has its own `openPuttSync` field; that's what the ball branch should use. Also replace the `GetComponent<VRCPickup>()` calls at click time with baked references.

### 3.7 Editor tooling

The tool's only job is **getting the right pages into every board**. It never creates, destroys or counts pool instances — the pool is hand-authored (2.6).

**Nothing under `Packages/` is ever written to.** A package update replaces the folder wholesale, so anything the tool wrote there is destroyed — silently, and at the worst possible time. That rules out the obvious option of baking the author's grid into the shipped board prefab. Package prefabs contain only what's baked at *release* time, by the package author, from content that doesn't depend on the world.

But the grid can't be shipped either: its column count comes from the author's course count and its row count from `numberOfPlayersToDisplay`, neither of which the package knows. And it can't live in the scene — V1's board prefab is ~20,600 lines of YAML and a grid is ~280 cell objects per board, so as added-object overrides every scene containing OpenPutt would carry six figures of YAML, rebuilds would rewrite the scene file wholesale, and `OpenPuttDemoScene.unity` would conflict on every merge. Three places, all wrong.

**So generate the score page as a prefab asset in the author's `Assets/`.** Default path `Assets/OpenPutt/Generated/OpenPuttScorePage.prefab`, with the manager holding the reference — the path is only a default for creation, so two scenes in one project can each have their own. It's outside the package, so updates can't touch it; outside the scene, so scenes stay small; and under the author's version control, where a world-specific artifact belongs.

**The generated asset *is* the score page, not a grid the page points at.** `ScorePage` sits on its root and every flat array — `cellTexts`, `cellBackgrounds`, `cellTransforms`, `rowCanvases`, `courseForColumn`, `columnDisplayMode` — is wired to its own children. Three consequences, all good:
- **No scene YAML.** Every reference is internal to the asset, so a board instance stores one nested prefab instance and nothing else. The ~840 object references per board that a page-points-at-grid split would serialize as overrides simply don't exist.
- **No cross-behaviour hops.** The arrays are fields on the behaviour that owns the loop, which is what rules 1 and 2 demand. A separate `ScoreboardGrid` behaviour would put an extern between the loop and every cell — the exact mistake V1 makes with per-cell behaviours, reintroduced one level up.
- **Regenerate once, every board updates.** Unity's prefab system propagates it. The reconcile stops rebuilding N grids and rebuilds one asset, which also means boards can never disagree about their layout.

There's precedent for nesting it: V1 already nests `Scoreboard.prefab`, behaviours and all, inside `PortableMenu.prefab`.

**Generation must be deterministic.** The asset is committed, so stable child names and ordering matter — a rebuild that reshuffles objects produces a churning diff and turns every regeneration into a merge conflict. Same input, byte-identical output.

**If it's missing, regenerate it.** An author can delete it, fail to commit it, or check out a branch without it. Validation reports it and the rebuild recreates it; the build processor treats a missing generated page as a stale signature, not an error. It also carries an obvious "generated — don't hand-edit, changes are overwritten" marker, because someone will try.

**Author pages and the tab strip stay as scene overrides**, which is fine — a handful of objects per board rather than hundreds. The page list lives on the manager; the reconcile walks each board instance and reconciles its page children against that list. It's idempotent, so reverting overrides and rebuilding puts everything back.

**Package updates are the dangerous case**, and they need explicit handling — this is where duplicate and orphaned pages come from:
- A page instance whose source prefab no longer exists (renamed, removed from the package) is reported by GUID and deleted, not silently left as a broken tab.
- `pageId` is what survives a GUID change, so a built-in that gets a new prefab keeps its id and the reconcile can recognise it as the same page rather than adding a second one.
- The generated score page is unaffected by definition — it's in `Assets/`. That's the point of putting it there.

**`ReconcilePages(ScoreboardManager manager)`**:
0. Refuse to run in play mode or from inside prefab-isolation editing, and wrap the whole pass in a single undo group (`Undo.RegisterCreatedObjectUndo` / `RegisterFullObjectHierarchyUndo`) so one Ctrl+Z reverts it. V1's build processor only ever ran at build time, so it never had to care; an inspector button that destroys and recreates objects does.
1. Validate the page library: every prefab has a `ScoreboardPage` on its root, `pageId`s are unique and outside the reserved built-in range for author pages, `contentRoot` is set. Report and skip bad entries; never half-build.
2. **Regenerate the score page assets** if the signature is stale or they're missing — once each, not per board — one page per configured view (2.7, 3.7a), with that view's courses, column count, display modes, both course↔column maps and par totals baked in, and every flat array wired to its own children. Then `SaveAsPrefabAsset` to the manager's referenced path, creating the folder on first run.
3. For each board instance in the pool, diff its `PagesRoot` children against `manager.pagePrefabs` plus every generated score page, **by source prefab GUID**. Instantiate missing with `PrefabUtility.InstantiatePrefab` so instances stay linked to their source and later edits still propagate. Delete removed and duplicated, reorder to match, set `page.board`, and fill the board's `scoreViews[]` in view order.
4. Regenerate that board's tab strip from the page list, baking each tab's page **index** into its click handler (3.1). Resolve and cache the Scores library index on the manager, and default every anchor's `scoreView` that points at a deleted view back to 0.
5. Wire every reference the runtime needs, so nothing is derived in `Start()`. Bake the anchor geometry caches and placeholder-canvas states (3.5).
6. `EditorUtility.SetDirty` + `PrefabUtility.RecordPrefabInstancePropertyModifications` per board.

A hand-duplicated board arrives with its page children already attached, and the next reconcile picks it up like any other pool member — which is what makes manual pooling (2.6) work.

**Cheap staleness check.** Two signatures, because there are now two things that go stale:
- On **each generated asset**: that view's course list + `numberOfPlayersToDisplay` + cell prefab GUID. Drives step 2 and is the expensive rebuild. Per-view, so editing one view doesn't regenerate the others.
- On each **board instance**: page prefab GUIDs in order + the generated assets' signatures. Drives steps 3–4, which are cheap.

`NeedsRebuild()` stays a string compare, replacing V1's deep hierarchy walk. Editor-only code, so strings are free here — the no-strings rule (3.1) is about runtime Udon.

**Triggers:** the **Scoreboards tab of the OpenPutt main menu window** (`Editor/OpenPuttMainMenu.cs`), which is where authors already go and which already shows a needs-rebuild state, a needed-column readout and the positions list. That's the normal path and where the new validation lands; the manager inspector keeps a button as a shortcut. `IProcessSceneWithReport` at build time stays as the safety net, rebuilding on a stale signature and logging loudly like V1 does. The existing "add position" button becomes "add anchor", and the Score Views section (3.7a) sits alongside it.

**Validation** as warnings in that tab (and on the manager inspector) — the guardrails that stop authors accidentally paying for things, and the main defence for a public page API:
- duplicate `pageId` between two page prefabs, naming both.
- an author page using a reserved built-in `pageId`.
- a page prefab with no `ScoreboardPage`, or a missing `contentRoot`.
- a page instance whose source prefab is missing.
- a generated score page asset missing, or its reference on the manager unset → offer to regenerate.
- a generated asset sitting anywhere under `Packages/` → it will be destroyed on the next update.
- pool size < number of `AlwaysVisible` anchors → some anchors never get a board.
- `numberOfPlayersToDisplay × (cells across all views) × poolSize` above a threshold → warn about build time and per-refresh cost, showing the computed cell count. Views make this the number authors can most easily inflate (3.7a).
- a board in the pool array that has no `PagesRoot` → it can't receive pages.
- an anchor pointing at a view index that no longer exists.
- grid cell prefab has TMP auto-sizing or rich text on.
- the cell font asset reference is missing or its atlas mode isn't static (3.3).

### 3.7a Authoring score views

Views are opt-in and invisible until used — a world with one 18-hole set never sees them (2.7). Setup reuses the idiom the Courses tab and the positions list already use: a `+ Add` button, undo registration, append to a serialized list on the manager, then `ApplyModifiedProperties` → `RecordPrefabInstancePropertyModifications` → `MarkSceneDirty` → `ExitGUI`. Nothing new to learn, and unlike the anchor and course buttons this one creates no scene objects at all.

**Where:** a "Score Views" section in the Scoreboards tab of the main menu window, under the rebuild section. Views exist only to serve boards, so they live next to boards rather than in the Courses tab.

**There's always a view 0**, covering every course, created implicitly and not deletable. A world that never opens this section has exactly one view and never pays for the feature — the section is empty of decisions rather than empty of function.

**`+ Add Score View`** appends a config entry to a serialized list on the manager. No prefab is instantiated and no GameObject is created — a view is data that the generator turns into a score page (2.7), which is the whole reason this got simpler.

**Each view is a foldout:**
- A name. Shown in the board header at runtime so a player can tell which set they're looking at, and used for the tab label if the author exposes the view as its own tab (3.1).
- A **toggle grid of every course** in `openPutt.courses` order, labelled with `scoreboardShortName`, falling back to `Hole {holeNumber}` when it's blank — which it is by default, so the fallback is the common case in a new world. Select all / none / invert, plus a "1–18" range field for the boring case.
- **Rank by:** all courses (default) or this view's courses. The dropdown is where the cost lives, so it says so — the second option is the only one that adds runtime work (2.7).
- A readout: courses selected, summed par, resulting column count, and cells added.
- Delete, which also repoints any anchor referencing it back to view 0.

**Views store `CourseManager[]`, not indices.** Indices into `openPutt.courses` shift the moment an author inserts or reorders a course, which would silently change what every board shows — the sort of breakage nobody notices until a player complains the Back 9 board is showing the Front 9. Authoring holds object references; the generator bakes them into the page's flat `int[] courseIndices` and reports any reference that's no longer in `openPutt.courses`.

**Anchors pick a view from a dropdown**, built from the manager's view list with view 0 first. A popup can only produce a valid index, where an object field would let an author point at something unlisted. Owned boards (2.6) get the same control on the board itself, defaulting to view 0 for the portable menu.

**A running total at the top of the section**, because this is now the main way to make a world expensive: total cells across all views × pool size, with the same threshold warning as 3.7. Overlapping views are the case to surface — "All 36" plus both halves triples the grid.

**Validation** in the same section:
- a view with no courses selected.
- a view referencing a course that's no longer in `openPutt.courses`.
- the same course listed twice in one view.
- a view no anchor references and no tab exposes — it's generating cells for nothing.
- more than 4 views set to rank by their own courses (the cap from 2.7).
- a view ranking by its own courses that contains **only** driving-range courses, which makes every total zero (2.7).

Two anchors sharing a view is correct and gets no warning — authoring it once is the entire point.

### 3.8 Startup

V1 has no startup contract, and it shows: `Scoreboard.Start` disables itself if the manager is missing, schedules `InitUI` at +0.05s, and schedules `SelectInfoTabForPlatform` at +1s which then **retries itself every second until `Networking.LocalPlayer` becomes valid**. The manager separately keeps an `allScoreboardsInitialised` flag, polls every board's `HasInitializedUI` inside the 1 Hz visibility pass, and schedules its first pass at +0.5s. Four independent timers racing each other.

V2 has one rule: **the manager owns readiness; boards never self-initialise.**

- All boards start with their **root canvas disabled** (2.8), so nothing can render half-built. Not `SetActive(false)` — the behaviours need to exist and be wired before the first pass.
- The manager waits for `Networking.LocalPlayer` to be valid *and* `openPutt.LocalPlayerManager` to be assigned by the object pool — one retry loop, in one place.
- Once ready it does a single ordered pass: cache course metadata, build the palette and string tables, resolve the score page index, **set the current page to Info** (V1's default on both the board and the manager — a new player gets the instructions, not the scores), evaluate access, then start the 1 Hz anchor loop.
- Platform detection sets the Info page's initial content in that same pass, which retires V1's `SelectInfoTabForPlatform` retry loop — that existed only because it ran before the local player was valid, and here the local player is known valid. Its *other* guard stays: the sub-tab buttons are being kept (3.2a), so re-selecting the platform must still only apply while the Info page is showing, or it yanks a player who deliberately switched to read another platform's controls.
- **The first refresh goes through the chunked path like any other**, not as one blocking pass. It lands during world load, when everything else is also initialising, and it's the largest refresh the system ever does (every row on every board, nothing shadowed yet). Only boards that the first anchor pass actually leases need it at all.
- Everything a board needs is already wired by the editor tool (3.7), so `Start` derives nothing. V1's `initializedUI`, `HasInitializedUI` and the child-count-walking in `Scoreboard.Start` all disappear.
- Platform detection for the Info page happens once in that ready pass, not on a retry timer, because the local player is known valid by then.

**Readiness is not the same as having data**, and that's the part V1 gets right by accident. Persisted scores are restored from `PlayerData` after the local player is restored, which can be *after* the ready pass completes — that's why `OpenPutt.cs:440` pokes the scoreboard once it's done (1.4). So the ready pass is not the last word:

- `OpenPutt`'s restore path calls `manager._OnScoresRestored()` — a full refresh plus `_ShowScoresPage()` — instead of poking `requestedScoreboardView` directly.
- Remote players' data can also land late (`OnPlayerDataUpdated`), so an update for a listed player routes through the normal dirty-player path rather than being special-cased.
- The ready pass must be safe to run before any player has scores: an empty grid is a valid state, not a reason to defer readiness.
- Boards leased before data exists are correct because a lease always flags a full refresh (3.5) — a board can never be showing older data than the last refresh it was flagged for.

### 3.9 Verifying it

The layout is being redesigned (3.2a), so there's no V1 side-by-side to diff against. That has to be replaced with something, or regressions will only surface in a live instance:

- **A fake-player debug mode.** Populate N rows with synthetic players, scores and course states, driven from the manager inspector. This is the only practical way to see 12 rows, rank churn, driving-range columns and the sentinel/`*` cases without 12 people in the instance. V1 half-gestures at this with `hideInactivePlayers` ("should stay enabled unless you're debugging the scoreboard player lists") — that field is being deleted (3.1), and this replaces the one legitimate use of it. Fake players must therefore report as having started playing, since that's now the only listing condition.
- **The measurement harness** from Phase 3: refresh ms, refreshes per second, cell writes per refresh, cross-behaviour calls per refresh. Fake players are what give it a realistic load. Check `Runtime/Scripts/MerlinProfiler/` (`OPProfileHandler` / `OPProfileKickoff`) first — if it already gives per-behaviour timing, the harness only has to add the two counters that matter. **This is the only place a `Stopwatch` appears**, behind `debugMode`, and the counters are the more useful half anyway: cell writes and cross-behaviour calls predict Udon cost without a clock, so they can stay on for cheap even when timing is off.
- **Lease-churn check.** Stand at the edge of an anchor's radius, and at the edge of its FOV cone, and confirm the board doesn't toggle (3.5). This is the regression the canvas-based hiding introduces, and it's invisible unless someone specifically stands still in the wrong place.
- **A display-rules checklist** covering every case in 3.3 — the two driving-range types, `999999`, `DidWeirdThingHappen`, `Playing` with `playerSyncType != All`, speed-golf `Playing` for the local player, `Skipped` vs `PlayedAndSkipped`, `NotStarted`, under/on/over par, filtered totals, and every state × display-mode combination producing text. These are the details a rewrite silently drops.
- **A cross-board behaviour checklist**, which the mode split in 3.1 makes necessary: change a setting on one world board and confirm the other leased boards update the same control; confirm the portable menu keeps its own page when a world board switches; confirm a slider drag doesn't re-enter through the fan-out; confirm settings on a stowed portable board cost nothing.
- **A window-slide check** with more players than rows — walk the local player up and down the rankings and confirm the window only re-slices at the margin, not on every score change (3.4).
- **Quest check** before the layout is locked, for the per-row canvas draw calls.

---

## Part 4 — Build order

**Phase 1 — leasing skeleton.** Manager + instance + anchor, one blank page. The startup contract from 3.8 goes in here, first — it's the thing every later phase assumes. Then port the visibility maths with the extern cuts from 3.5 baked in. Prove sticky leasing, hysteresis and the dwell counter, positioning, scale conversion, interactable toggling, canvas-off on release. No grid, no tabs.

**Phase 2 — page framework and layout.** `ScoreboardPage` base as a frozen public contract with int `pageId`s, page library, generated tab strip with baked indices, `_ReevaluateAccess()` and the three gate tiers, the reconcile + `layoutSignature` + the main-menu Scoreboards tab + build processor + validation. **The layout design pass comes first in this phase** — it gates every page prefab, so it can't come later, and 3.2a is a sketch to react to rather than a spec to implement. Build `InfoPage` and `AboutPage` first: static content, so any bug is a wiring bug rather than a data bug.

**Phase 3 — score grid.** Flat-array `ScorePage` with the two-path API from 3.3 — build `_RefreshLiveCells` first, not last, because the whole point is that the bulk path stays rare. Then the grid generator, static header/par rows, int-shadowed guards, cached course metadata, per-row array reads, precomputed strings, range-sliced dispatch, coalesced refresh, dirty-row skipping, row-count gating.

Establish the fake-player mode and the measurement harness here, before any of it (3.9): refresh ms, refreshes per second, **cell writes per refresh**, and **cross-behaviour calls per refresh**. Those last two are the numbers that actually predict Udon cost, and without them later phases will silently regress. Fake players are also the only way to exercise the display-rules checklist and rank churn without a full instance.

**Phase 4 — score views.** Much smaller than it was, because the runtime half is gone (2.7). The generator emits one score page per configured view; the anchor gets its view dropdown; the board resolves the Scores tab to its own view instance; per-view par and player totals get baked or summed by `_UpdateTotals`' rules. The authoring UI and validation from 3.7a land here.

Per-view *ranking* is the last item and can be deferred past the cutover without blocking anything — it's opt-in, no world needs it to ship, and everything before it works with the existing global sorted lists. If it slips, nothing else waits on it.

**Phase 5 — settings + dev.** `SettingsPage`, `DevPage`, `ScoreboardControl` → `ScoreboardSetting`, the cross-board settings fan-out and its `suppressCallback` trap (3.6), the reset-scores flow with its ball-respawn fix, three-tier access gating with runtime re-evaluation, and low-rate visibility-gated dev readouts.

**Phase 6 — polish, public API, cutover.** Anchor gizmos and scene handles — and the gizmos need to stop lying: V1 draws `nearbyMaxRadius` and friends as wire spheres around the anchor's centre, while the actual test measures from the *closest point on the board rectangle*. On a large board those differ by metres, so authors are tuning radii against a shape that isn't the one being tested. Draw the rect and offset the radius from its surface. Then remaining validation warnings, the world-author board prefab. The page-API deliverables: documented contract, a minimal example custom page, a pass over the reconcile's failure messages. **Docs go in `Docs/`, as markdown, next to this plan** — in the repo, where a world author writing a custom page is already looking, and where they diff and review like code. The example custom page ships as a prefab under `Samples/`, where it gets built and therefore stays compiling.

Then the cutover, which is real work and not a delete key:
- Rebuild the portable menu's board as an Owned instance (2.6) and rewire its open/close events.
- Update `Samples/OpenPutt/OpenPuttDemoScene.unity` — it's how anyone actually sees this, and it currently contains V1 boards, positioners and a pool.
- Remove V1. Note `ScoreboardPlayerColumn` sits in the global namespace rather than `dev.mikeee324.OpenPutt`, so anything outside the package could be referencing it.

**Other people's worlds are the part with no undo.** "No migration path" (top of this document) is a statement about the *code*, and it's the right call there. It is not a decision about published worlds: OpenPutt ships as a VCC package, so deleting V1 means every author who updates finds their placed boards and positioners gone, with scene references to deleted scripts. Getting that wrong costs other people's work, so:

- **Major version bump**, with the scoreboard rebuild called out as the headline breaking change in the release notes, not a bullet near the bottom.
- **A one-shot converter**, run from the Scoreboards tab: for each `ScoreboardPositioner` in the scene, create a `ScoreboardAnchor` at the same transform and copy `scoreboardVisiblility`, `nearbyMaxRadius`, `interactionMaxRadius`, `closeRangeFullVisibilityRadius`, `attachedToCourse` and `nearbyCenterTransform`. Anchor placement and tuning is the only authored data in a V1 setup worth preserving — everything else is generated — and it's the tedious part to redo by hand. Colours and sizing on the manager come across the same way.
- **Detect and say so.** If the scene contains V1 components after an update, the window leads with "this world uses the old scoreboards" and the convert button, rather than an inspector full of missing-script errors.
- **Don't delete anything the author placed.** The converter adds anchors and leaves the V1 objects in place, disabled, so a mistake is recoverable by hand. Removing them is a second, explicit button.
- The converter is throwaway code with a stated shelf life — one minor version — and the release notes should say so.

### Work outside the scoreboard folder

More than the two obvious items, though all of it is small:

- `PlayerListManager` / `PlayerManager` must report **which course** changed, not just that something did, so win #1 can route to `_RefreshLiveCells`. The dirty-flag plumbing already exists; this adds the course index alongside it. *(Phase 3)*
- `PlayerListManager` gains per-view sorted lists **only if per-view ranking ships**, using the same incremental binary-insert it already does for the global lists, plus span-limited stamping (2.7). The global lists and `PlayerManager`'s two position ints are unchanged either way. This is the one outside-the-folder item that's optional — a world using views but ranking by all courses needs none of it. *(Phase 4, deferrable)*
- `PlayerListManager.LateUpdate` and `ScoreboardPlayerRow` drop their `hideInactivePlayers` reads; the listing condition becomes `PlayerHasStartedPlaying` unconditionally. Deletes a cross-behaviour read from a per-player loop. *(Phase 3)*
- **Make `PlayerHasStartedPlaying` a cached bool.** It's now the sole listing condition, and it's a property that loops every `courseStates` entry behind an `IsReady` check that reads three array lengths — evaluated per dirty player, per batch, across a behaviour boundary (rules 1 and 3). Set the flag when a player first leaves `NotStarted` on any course and clear it on reset; the getter becomes a field read. Same line is already being edited to remove `hideInactivePlayers`. *(Phase 3)*
- `PlayerListManager.LateUpdate` allocates a `Stopwatch` on **every frame it runs** and reads `Elapsed.TotalMilliseconds` per dirty player. Same treatment as 3.4: a fixed quantum of players per frame, no clock, and the debug log's timing behind `debugMode`. Optional but it's the same pattern in the same hot path, and it removes a per-frame allocation. *(Phase 3)*
- `OpenPutt.cs:440` and `PlayerManager.cs:696` stop assigning `requestedScoreboardView` and call `manager._ShowScoresPage()` / `_OnScoresRestored()` instead (3.1, 3.8). *(Phase 1–2)*
- `OpenPutt.RefreshAllSettingsMenus()` has two external callers — `OpenPutt.cs:277` and `GolfClub.cs:933` — that need the V2 equivalent, which is a refresh of the settings page across the pool rather than per board. *(Phase 5)*
- Something has to call `_ReevaluateAccess()` on `OnPlayerLeft` for the master change (3.2). `OpenPutt.OnPlayerLeft` already exists and is the natural place. *(Phase 5)*
- `OpenPuttPortableMenu`'s `eventReceiver` gets pointed at its Owned board, with the two event-name strings set to `_OnOwnerShown` / `_OnOwnerHidden` (2.6). *(Phase 6)*
