# Stage 4 Implementation Plan

Status: Stage 4 planning deliverable; Stage 5 M1 complete, M2 in progress  
Last updated: 2026-10-27  
Baseline: EasyWorship 7.4.0.7 on Windows  
Architecture: `research/reconstruction-design.md`  
Inputs: `research/feature-matrix.md`, `research/behavioural-specification.md`, `research/architecture-findings.md`, `research/known-unknowns.md`, `research/test-cases.md`

## 0. Progress

| Milestone | Status | Evidence |
|---|---|---|
| M0 prerequisites | Partial | .NET 8.0.301 pinned; build/restore/WPF launch verified. FFmpeg and oracle-capture decisions remain. |
| M1 solution scaffold | Complete | `EasyWorshipClone.sln`, 9 source projects, 8 test projects, DI/logging/settings shell, architecture tests, build scripts. Build has 0 warnings; 21 tests pass; shell launches and exits 0. |
| M2 data foundation | Partial | Clone schema v4 + migration runner, 19 core tables + 5 FTS5 tables, provisional `UTF8_U_CI` registration, profile/song/presentation/resource/media/theme/service-interval/setting repositories, read-only song, presentation graph, media, theme, and service-interval readers, `.ewsx`/`.ewtx` main.db and media extraction, extracted-media registration, presentation-resource media linking, RTF parsing and slide splitting, legacy INI settings parsing/import validated against 7 observed files (194 settings imported, 0 warnings; second pass updated 194), raw sidecar preservation, theme graph preservation, idempotent imports by legacy UID, song/media/presentation/theme FTS, transactional rollback, hash-verified backups with SQLite integrity checks, and synthetic fixtures. Build 0 warnings; 71 tests pass. Remaining: Bible import, settings import wiring, and unified import reporting. |
| M3 operator shell | Partial | Main window, library tabs, imports, selection, preview/live panes, Go Live/next/previous/black/clear commands, and ListBox accessibility are implemented and manually smoke-tested. Search/sort breadth, window persistence, central hotkeys, and FlaUI smoke tests remain. |

## 1. Purpose

Convert the approved architecture into an incremental Stage 5 delivery sequence. The plan prioritises a working end-to-end vertical slice over breadth, keeps the operator UI responsive, and avoids claiming parity where the oracle has not been verified.

This plan does not authorise launching EasyWorship again, modifying production data, or copying proprietary assets.

## 2. Entry conditions

The following must be true before Stage 5 code starts:

1. `research/reconstruction-design.md` is accepted as the working architecture. **Accepted 2026-10-26.**
2. The .NET runtime decision is made: .NET 8 for the prototype, with a .NET 10 retarget before release. **Approved 2026-10-26.**
3. v1 scope is confirmed: core shell, schedule/live, rendering, media, and public-domain Bible support in; NDI, PowerPoint, DVD, remote/MIDI, streaming, and online store optional or deferred. **Approved 2026-10-26.**
4. Import-first legacy compatibility is confirmed: read-only source access, new clone schema, raw-value sidecar. **Approved 2026-10-26.**
5. FFmpeg distribution approach is chosen: LGPL-compatible build, notices, and source-offer plan. **Open.**
6. Synthetic fixtures exist for songs, media, presentations, themes, and schedules; no production data is used. **Open.**
7. The oracle capture method is safe and repeatable. D3D-aware capture remains an open requirement. **Open.**

Resolved runtime decision:

| Option | Decision |
|---|---|
| .NET 8 prototype | **Selected 2026-10-26.** Uses installed SDK 8.0.301. Prototype only; retarget to .NET 10 before release. |
| .NET 10 user-local | Deferred to the release retarget. |
| .NET 10 machine-wide | Not required for the prototype. |

## 3. Fixed architecture constraints

1. One process, modular monolith. Do not build IPC between clone modules in v1.
2. WPF owns the operator shell. D3D11 owns live composition.
3. The WPF UI thread never waits on SQLite, media decode, or GPU work.
4. The renderer consumes immutable scene snapshots; view models are never rendered directly.
5. Legacy EasyWorship profiles are read-only and are never migrated in place.
6. New data uses the clone schema, with legacy IDs and unresolved raw values preserved.
7. No proprietary EasyWorship branding, artwork, help content, fonts, or Bible text.
8. Every milestone must build and pass its tests before the next milestone starts.

## 4. v1 scope

### 4.1 Must deliver

- Application shell with menus, toolbar, Schedule / Live / Live Output panes, library tabs, search, and status.
- Profile creation, selection, locking, settings, window persistence, and clean shutdown.
- Song library: list, select, create, edit, delete, search, and slide generation.
- Schedule: create/open/save, ordered items, live navigation, manual advance, service intervals.
- Presentation model: slides, elements, text, images, backgrounds, themes, and z-order.
- Preview and one live output window, including monitor selection, DPI handling, and hot-plug fallback.
- Live controls: Go Live, next/previous slide, Logo, Black, Clear, and defined restore behavior.
- Text rendering with wrapping, alignment, styles, and font fallback.
- Images, video, and audio import/playback with trim, mute, volume, loop, and error handling.
- Scripture: public-domain translation import, reference lookup, search, and insertion into live/schedule.
- Transition engine: cut, crossfade, and mapped wipe/slide/push effects with graceful fallback.
- Legacy import: songs, presentations, schedules, themes, media references, settings, and Bibles where supported.
- New profile export/import package.
- Installer, notices, diagnostics, and crash recovery.

### 4.2 Should deliver

- Alt output and independent preview/live routing.
- PowerPoint import/playback via COM when Office is available.
- NDI input/output if the SDK and licensing are secured.
- Foldback/stage-style output if the oracle evidence supports it.
- Remote/MIDI adapters behind interfaces, disabled by default.

### 4.3 Deferred

- DVD playback.
- Streaming/recording.
- Online store, account sync, and licensing service.
- Automatic updates.
- Cloud sync and mobile companion apps.
- Bidirectional EasyWorship database compatibility.
- HDR output and advanced color management.

## 5. Delivery strategy

Build in vertical slices. Each slice must produce a runnable application and a testable behaviour, not only infrastructure.

The first useful slice is:

```text
start clone
  -> open profile
  -> list a synthetic song
  -> select it
  -> render slide 1 in preview
  -> Go Live to a borderless output window
  -> advance slides
  -> Black / Logo / Clear
  -> close cleanly and restore window state
```

Only after that slice works end to end should the team add breadth such as themes, media, scripture, and import adapters.

### 5.1 Definition of done for every milestone

- Solution builds with zero warnings under the agreed warning policy.
- New unit tests pass.
- Relevant integration/UI tests pass.
- No production data or installed EasyWorship files are touched.
- Deviations from the oracle are recorded in `research/behavioural-specification.md` or a milestone note.
- The milestone exit criteria are met, or the unmet item is explicitly carried forward.

## 6. Milestones

### M0 — Prerequisites and baseline

**Goal:** remove environment and dependency blockers before creating the solution.

Tasks:

- M0-01 Decide and install the .NET SDK target.
- M0-02 Pin the SDK with `global.json`.
- M0-03 Verify NuGet restore and a throwaway WPF build.
- M0-04 Select and pin an FFmpeg LGPL build; record version, source, and licence.
- M0-05 Decide NDI/PowerPoint/DVD adapter availability; default them to off.
- M0-06 Create synthetic fixture specifications.
- M0-07 Define the oracle capture method and safety rules.
- M0-08 Record free disk space and a rollback note for the SDK installation.

Exit criteria:

- `dotnet --version` returns the chosen SDK.
- A minimal WPF project builds and launches.
- FFmpeg licence/version decision is recorded.
- Test fixtures are specified with no production data.

### M1 — Solution scaffold

**Goal:** create the module boundaries, build, logging, settings, and test harness.

Tasks:

- M1-01 Create `EasyWorshipClone.sln` and the nine architecture projects.
- M1-02 Add test projects from the design.
- M1-03 Add `Directory.Build.props`, nullable, analyzers, deterministic builds, and central package management.
- M1-04 Add dependency injection, configuration, logging, and crash handling.
- M1-05 Add app data-root resolution for machine, shared, user, and install scopes.
- M1-06 Add a minimal WPF shell that starts and exits cleanly.
- M1-07 Add CI build/test scripts and a local verification script.
- M1-08 Add architecture tests that enforce project references.

Exit criteria:

- `dotnet build` and `dotnet test` pass.
- The shell launches without admin and writes logs to the user scope.
- No module references violate the dependency rules.
- A failed startup produces a logged error, not a silent exit.

### M2 — Data foundation and legacy import

**Goal:** establish the new schema and prove read-only legacy access before UI breadth.

Tasks:

- M2-01 Implement the clone SQLite schema and migration runner.
- M2-02 Add repositories for profiles, songs, presentations, schedules, themes, media, settings, and service intervals.
- M2-03 Implement `UTF8_U_CI` registration and prove it against a synthetic legacy copy.
- M2-04 Implement FTS5 indexing and rebuild-on-change.
- M2-05 Implement the legacy profile reader and source snapshot policy.
- M2-06 Implement legacy readers for `.ewsx`, `.ewtx`, `.ewpck`, `.ewb`, `.ewo`, and `.dfx` metadata.
- M2-07 Preserve legacy IDs/UIDs and raw values in sidecar JSON.
- M2-08 Implement idempotent import by source hash/UID.
- M2-09 Add synthetic legacy fixtures with known expected mappings.
- M2-10 Add database backup, integrity check, and transactional import.

Exit criteria:

- New profile creation and reopening work.
- A synthetic legacy song/presentation/schedule imports without modifying the source.
- Unknown enum/property values survive import and are reported.
- Re-import does not duplicate records.
- A failed import leaves the existing profile unchanged.
- Tests cover the custom collation and archived database years.

Maps to: `B-01..B-08`, `E-02..E-08`, `G-02..G-13`, `I-01..I-14`, `SET-005..SET-010`.

### M3 — Operator shell

**Goal:** make the shell usable with real data, before adding live rendering.

Tasks:

- M3-01 Implement the main window layout and pane splitters.
- M3-02 Implement menus, toolbar, library tabs, selector, list, and status bar.
- M3-03 Implement profile selection and switch behavior.
- M3-04 Implement Songs, Media, Presentations, Themes, and Scriptures list views.
- M3-05 Implement search, sort, empty states, and selection.
- M3-06 Implement window, splitter, and recent-item persistence.
- M3-07 Implement central hotkey service with text-entry suppression.
- M3-08 Add accessible names and keyboard focus paths.
- M3-09 Add FlaUI smoke tests for startup, tab switching, and search.

Exit criteria:

- A user can start the app, select a profile, browse and search each library, and close cleanly.
- Window state restores across restarts.
- Keyboard navigation covers the core shell.
- No UI thread database waits on normal navigation.

Maps to: `A-03..A-18`, `B-03`, `C-03`, `D-01`, `E-01`, `F-01`, `SHELL-001..SHELL-011`.

### M4 — Rendering and output vertical slice

**Goal:** render one slide to preview and one live output window, with capture-based tests.

Tasks:

- M4-01 Create the immutable `RenderScene` model.
- M4-02 Implement the D3D11 device, WARP fallback, and render thread.
- M4-03 Host the swap chain in WPF preview via `HwndHost`.
- M4-04 Create the borderless output window and monitor routing.
- M4-05 Implement DirectWrite text layout, wrapping, alignment, and font fallback.
- M4-06 Implement image decode via WIC and basic backgrounds.
- M4-07 Implement scene snapshot handoff and bounded queues.
- M4-08 Add a staging-texture capture path for tests.
- M4-09 Add WARP golden-image tests for text, image, background, and aspect ratio.
- M4-10 Add monitor hot-plug and missing-monitor fallback tests where possible.

Exit criteria:

- A synthetic song slide renders identically in preview and live output.
- Capture is non-black and does not use `PrintWindow`.
- A GPU device failure falls back to WARP with a logged warning.
- The UI remains responsive while the render thread presents.
- Output window closes without leaving the app in a bad state.

Maps to: `F-06..F-14`, `G-09`, `G-17`, `LIVE-001`, `LIVE-002`, `LIVE-008`, `LIVE-014`.

### M5 — Live operation and scheduling

**Goal:** implement the operator's core service workflow.

Tasks:

- M5-01 Implement the live state machine as a policy table.
- M5-02 Implement Go Live, next/previous slide, and schedule navigation.
- M5-03 Implement Logo, Black, Clear, and restore semantics.
- M5-04 Implement slide-transition orchestration and interruption policy.
- M5-05 Implement schedule create/open/save and ordered items.
- M5-06 Implement item types for songs, scriptures, media, and presentations.
- M5-07 Implement service intervals, timers/tokens, and auto-advance as verified.
- M5-08 Implement alerts as an overlay with a defined lifecycle.
- M5-09 Add state-machine unit tests and live workflow tests.

Exit criteria:

- A synthetic song can be selected, previewed, sent live, and advanced.
- Logo, Black, Clear, and Alert enter and leave defined states.
- The schedule survives save/reopen.
- A render or media failure does not blank or freeze the live output.
- Unknown priority rules remain configurable and documented.

Maps to: `G-01..G-16`, `SCH-001..SCH-010`, `LIVE-003..LIVE-007`, `LIVE-012`, `LIVE-013`.

### M6 — Songs, presentations, and themes

**Goal:** add authoring breadth on top of the working live path.

Tasks:

- M6-01 Implement song create/edit/delete and metadata validation.
- M6-02 Implement RTF parsing and the internal text model.
- M6-03 Implement slide splitting and section-label detection.
- M6-04 Implement song search and usage history.
- M6-05 Implement presentation metadata and slide ordering.
- M6-06 Implement the presentation editor for text, image, and geometry changes.
- M6-07 Implement theme apply, override, reset, save, and export.
- M6-08 Implement the transition library and parameter mapping.
- M6-09 Add import/export tests and round-trip fixture tests.

Exit criteria:

- A synthetic song with verses/chorus produces the expected slides.
- Editing a slide updates preview/live through the scene snapshot.
- Theme application and reset persist correctly.
- Each supported transition renders and falls back for unknown effects.
- Search and history update after edits.

Maps to: `B-01..B-10`, `E-02..E-09`, `F-01..F-10`, `SONG-001..SONG-011`, `PRES-001..PRES-006`, `THEME-001..THEME-007`.

### M7 — Media

**Goal:** add reliable image, video, and audio playback without blocking live output.

Tasks:

- M7-01 Pin FFmpeg bindings and native libraries.
- M7-02 Implement media probing, hashing, thumbnails, and metadata.
- M7-03 Implement image import and alpha handling.
- M7-04 Implement video decode, frame queue, seek, trim, loop, and poster frame.
- M7-05 Implement WASAPI audio output and A/V clock.
- M7-06 Implement mute, volume, repeat, and end-of-media behavior.
- M7-07 Implement relink, missing-file, and unsupported-codec errors.
- M7-08 Implement the optional PowerPoint adapter behind a feature flag.
- M7-09 Add synthetic media fixtures and playback/error tests.

Exit criteria:

- Synthetic image, video, and audio play with correct metadata.
- Seeking, trim, loop, mute, and volume work and persist.
- A missing or corrupt file produces a clear error and a live-output fallback.
- The render thread never blocks on decode.
- FFmpeg notices and licence files are included.

Maps to: `D-01..D-14`, `H-01..H-08`, `MEDIA-001..MEDIA-014`, `PERF-004`.

### M8 — Scripture

**Goal:** deliver the verified scripture workflow with public-domain content only.

Tasks:

- M8-01 Implement the `.ewb` reader and public-domain fixture import.
- M8-02 Implement translation selection, book/chapter/verse lookup, and validation.
- M8-03 Implement scripture search and result ranking.
- M8-04 Implement passage wrapping and presentation layout.
- M8-05 Implement insert into preview, live, and schedule.
- M8-06 Implement missing-translation and invalid-reference errors.
- M8-07 Add reference, search, insertion, and cache tests.

Exit criteria:

- A public-domain passage can be looked up, previewed, sent live, and scheduled.
- Wrapping and styling match the documented oracle behavior where verified.
- Invalid references and missing translations fail cleanly.
- No licensed Bible text is bundled or committed.

Maps to: `C-01..C-09`, `SCR-001..SCR-008`.

### M9 — Secondary outputs and advanced features

**Goal:** add multi-output and optional adapters without destabilising the core.

Tasks:

- M9-01 Implement Alt output and independent content routing.
- M9-02 Implement Foldback/nursery/message only if evidence supports the semantics.
- M9-03 Implement alpha output if verified.
- M9-04 Implement NDI input/output behind `INdiService` if the SDK/licence is available.
- M9-05 Implement remote/MIDI adapters behind feature flags, disabled by default.
- M9-06 Implement online/help surfaces with restricted navigation.
- M9-07 Add multi-output, hot-plug, and adapter failure tests.

Exit criteria:

- At least one secondary output works independently of Live.
- Missing monitors/devices degrade cleanly.
- Optional adapters can be disabled without affecting core startup.
- No unverified feature is presented as oracle-equivalent.

Maps to: `G-16..G-19`, `H-04`, `J-01..J-12`, `LIVE-005..LIVE-011`, `MEDIA-008..MEDIA-011`.

### M10 — Hardening, conformance, and packaging

**Goal:** make the clone installable, diagnosable, and demonstrably close to the verified oracle.

Tasks:

- M10-01 Run the full conformance suite against the oracle where safe and approved.
- M10-02 Fix high-impact deviations; document accepted differences.
- M10-03 Add performance benchmarks and tune startup/search/live latency.
- M10-04 Add crash dumps, safe-mode startup, and log redaction.
- M10-05 Add profile backup/restore and corruption recovery.
- M10-06 Add archive validation and untrusted-input tests.
- M10-07 Build the self-contained `win-x64` application.
- M10-08 Build the WiX MSI, notices, checksums, and signing plan.
- M10-09 Run accessibility and high-DPI checks.
- M10-10 Write the user-facing compatibility and limitations document.

Exit criteria:

- Clean machine install and uninstall work.
- User content survives uninstall by default.
- Conformance results and known deviations are documented.
- Startup, Go Live, and search meet the agreed budgets or have measured exceptions.
- The installer contains no proprietary EasyWorship assets.

Maps to: full test plan, especially `PERF-001..PERF-005`, `SET-001..SET-012`, `SEC-001..SEC-003`.

## 7. Testing strategy

| Layer | When | Gate |
|---|---|---|
| Domain/application unit tests | Every milestone | Must pass before merge. |
| Data/import tests | M2 onward | Fixture-based, transaction-safe, no production data. |
| Rendering golden tests | M4 onward | WARP/headless first; hardware skipped with a recorded reason. |
| Media tests | M7 onward | Synthetic clips/audio; no licensed content. |
| UI smoke tests | M3 onward | FlaUI; startup, navigation, live controls. |
| Oracle conformance | M10 and targeted checks earlier | Only with approval and safe capture. |
| Performance | M4 onward, full run at M10 | Record environment and GPU/driver. |

Rules:

- A test that depends on production data is invalid.
- A skipped hardware/NDI/DVD test is a recorded skip, not a pass.
- Every fixed bug gets a regression test.
- Golden images include a tolerance policy and are reviewed as binary artifacts.
- No test may leave an output window open or a helper process orphaned.

## 8. Build and verification commands

Expected local commands after scaffolding:

```powershell
dotnet restore EasyWorshipClone.sln
dotnet build EasyWorshipClone.sln -c Debug --no-restore
dotnet test EasyWorshipClone.sln -c Debug --no-build
```

Optional gates:

```powershell
dotnet format EasyWorshipClone.sln --verify-no-changes
dotnet publish src/EW.App/EW.App.csproj -c Release -r win-x64 --self-contained true
```

The actual commands must be recorded in `AGENTS.md` once the solution exists.

## 9. Dependencies and procurement

| Dependency | Needed by | Decision |
|---|---|---|
| .NET 10 SDK | M0 | Install user-local or choose .NET 8 prototype. |
| Vortice.Windows | M4 | Pin version after a WPF/D3D11 spike. |
| CommunityToolkit.Mvvm | M1/M3 | Pin version. |
| Microsoft.Data.Sqlite / SQLitePCLRaw | M2 | Pin version; verify collation API. |
| FFmpeg.AutoGen + native build | M7 | Choose LGPL-compatible build and notices. |
| WebView2 runtime | M3/M9 | Detect runtime; native fallback. |
| NDI SDK | M9 | Optional; licence required. |
| PowerPoint COM | M7 | Optional; Office required. |
| WiX Toolset | M10 | Pin version. |
| Code-signing certificate | M10 | Procurement/approval required. |
| FlaUI | M3 | Pin version. |

## 10. Risk gates

| Gate | Trigger | Required action |
|---|---|---|
| G-01 | .NET 10 cannot be installed or disk space is insufficient | Use .NET 8 prototype only with explicit approval; retarget before release. |
| G-02 | WPF/D3D11 hosting fails or is unstable | Spike a single output window before building M4 breadth. |
| G-03 | Golden capture remains black | Stop visual-fidelity claims; keep structural tests and document the limitation. |
| G-04 | FFmpeg licence/build unresolved | Defer media release until resolved; do not ship an unlicensed binary. |
| G-05 | Legacy import loses unknown data | Stop import scope expansion; add raw sidecar and warnings before continuing. |
| G-06 | Live state priority remains unknown | Keep the policy table configurable; do not hard-code guesses. |
| G-07 | Oracle testing requires another EasyWorship launch | Ask for explicit approval; use an isolated profile and synthetic data only. |
| G-08 | Installer requires admin and no admin is available | Produce a per-user portable/dev build first; defer per-machine MSI. |

## 11. Immediate next actions

1. Create the synthetic fixture specification and first fixture files.
2. Resolve the FFmpeg LGPL build and notices before M7.
3. Define the D3D-aware oracle capture method before visual-fidelity claims.
4. Wire the legacy INI settings importer into profile import and surface its warnings.
5. Add read-only legacy readers for Bible metadata.
6. Add unified import reporting and close the remaining M2 checklist.
7. Keep the M2 import boundary read-only and transactional.
8. Do not launch EasyWorship or modify production data without explicit approval.

## 12. Decision log

| Date | Decision | Status |
|---|---|---|
| 2026-10-26 | WPF on .NET 8 for the prototype; retarget .NET 10 before release | Approved. |
| 2026-10-26 | Modular monolith, one process | Approved. |
| 2026-10-26 | D3D11 + Direct2D/DirectWrite via Vortice | Approved. |
| 2026-10-26 | Import-first, read-only legacy compatibility | Approved. |
| 2026-10-26 | One live output plus preview in the first vertical slice | Approved. |
| 2026-10-26 | NDI/PowerPoint optional; DVD/remote/MIDI deferred | Approved. |
| 2026-10-26 | Stage 5 M1 scaffold completed and verified | Complete. |
| 2026-10-26 | M2 clone schema, migration runner, FTS tables, and profile repository implemented | Complete. |
| 2026-10-26 | `UTF8_U_CI` registered provisionally with `OrdinalIgnoreCase`; exact legacy folding remains unverified | Provisional; fixture tests only prove indexed access, not ordering parity. |
| 2026-10-26 | M2 song import vertical slice implemented: read-only legacy source, raw sidecar, UID idempotency, FTS, atomic rollback | Complete; source hashes verified unchanged. |
| 2026-10-26 | M2 presentation graph import implemented: presentations, slides, elements, text/web/shape resources, raw sidecar, UID idempotency, atomic rollback | Complete; source hash verified unchanged. |
| 2026-10-26 | Image/video/audio/DVD/feed resources are preserved as raw metadata but not yet materialized as media assets | Partial; media materialization remains M2/M7 work. |
| 2026-10-26 | `.ewsx`/`.ewtx` `main.db` extraction implemented with size limits, temp cleanup, and archive-hash source identity | Complete for presentation graph; media entries not yet imported. |
| 2026-10-26 | Media metadata import implemented: read-only `Media.db` reader, path resolution, legacy UID idempotency, raw sidecar, status/error preservation, and media FTS | Complete; duration/trim units still unknown and are preserved raw rather than guessed. |
| 2026-10-26 | Legacy backup service implemented with free-space preflight, hash verification, and SQLite `integrity_check` | Complete; source files remain unchanged. |
| 2026-10-26 | Theme archive import implemented: `.ewtx` main.db extraction, theme graph JSON preservation, legacy UID idempotency, raw archive metadata, and theme FTS | Complete for theme graph metadata; referenced theme media files are not materialized yet. |
| 2026-10-26 | Service interval import implemented: schema v4 legacy identity, read-only `ServiceIntervals.db` reader, raw sidecar, and idempotent transactional import | Complete for import; interval/timer UI semantics remain unknown, so `IsFixed` is provisional (`start_fixed OR end_fixed`). |
| 2026-10-26 | Archive media extraction implemented: `media/` entries only, zip-slip validation, duplicate detection, per-entry/total limits, free-space preflight, and SHA-256 output manifests | Complete for extraction; extracted files are not yet registered as `MediaAsset` rows. |
| 2026-10-26 | Extracted archive media registered as `MediaAsset` rows using content SHA-256 identity, idempotent upsert, media FTS, and archive/entry metadata | Complete for registration; presentation resource rows are not yet linked to these assets. |
| 2026-10-26 | Presentation image/video/audio/PowerPoint resources enriched with `file` metadata and linked to `MediaAsset` by SHA-256 during import | Complete when archive media is registered first; missing media preserves hash/filename metadata and leaves the link null. Hash matching is case-insensitive because archive hashes are lowercase. |
| 2026-10-27 | Legacy RTF splits on `\sdslidemarker` paragraph groups for marker songs and on blank paragraphs otherwise; empty marker segments are preserved as blank slides | Complete. Reimport produced 433 songs and 2,629 slides (423 multi-slide). Only `A Mighty Fortress Is Our God` still differs: 6 text slides versus 4 `slide_uids`. Section labels remain undecoded. |
| 2026-10-27 | Added `SettingRepository` plus BOM-aware legacy INI reader/import with source metadata, duplicate-key warnings, and transactional last-value-wins behavior | Complete for INI files; all 7 observed files imported into the clone (194 settings, 0 warnings; second pass updated 194). Settings import is not yet exposed in the UI or profile-import command, and DB-backed settings remain unimplemented. |
| 2026-10-27 | Populated `PresentationFts` on every presentation upsert and re-import | Complete; presentation search UI still filters loaded items in memory. |
| 2026-10-27 | Domain records overriding `ToString()` so WPF ListBox items expose meaningful UIA names | Complete; songs, media, presentations, themes, and service intervals are covered. |
