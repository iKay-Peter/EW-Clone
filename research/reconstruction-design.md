# EasyWorship Reconstruction Design

Status: Stage 4 architecture decision; Stage 5 M1 complete, M2 data foundation in progress  
Last updated: 2026-10-26  
Baseline: EasyWorship 7.4.0.7 on Windows  
Inputs: `research/feature-matrix.md`, `research/behavioural-specification.md`, `research/architecture-findings.md`, `research/known-unknowns.md`, `research/test-cases.md`  
Delivery plan: `research/stage4-implementation-plan.md`

## 1. Decision summary

| Area | Decision |
|---|---|
| Language/runtime | C# on .NET 8 LTS for the Stage 5 prototype; retarget to .NET 10 LTS before release. .NET 9 is not a target. |
| Operator UI | WPF with MVVM. |
| Process model | Modular monolith in one process for v1, with a dedicated render thread and explicit module boundaries. Keep an `IRenderHost` seam for a future out-of-process renderer. |
| Rendering | D3D11 + Direct2D/DirectWrite via Vortice.Windows; WIC for images; WebView2 for web content. |
| Live output | Borderless top-level output windows, one per active output, driven by the render thread and monitor routing. |
| Media | FFmpeg.AutoGen (LGPL-compatible build) for decode; WASAPI for audio; optional NDI SDK and PowerPoint COM adapters. DVD support deferred. |
| Persistence | New SQLite schema in a new application data root; Microsoft.Data.Sqlite + SQLitePCLRaw. |
| Legacy data | Import-first and read-only. Preserve original IDs and raw values; do not write original EasyWorship databases in v1. |
| Search | SQLite FTS5 for the new schema; legacy search tables are import inputs, not runtime dependencies. |
| Packaging | Self-contained `win-x64` build, WiX MSI, code signing. |
| Visual identity | New branding, icons, themes, and assets. No proprietary artwork, logos, help content, or Bible text. |
| Quality | Golden-image rendering tests, legacy-import fixtures, FlaUI end-to-end tests, and conformance tests against the installed oracle. |

**Why this shape:** it uses the mature Windows desktop stack already available in the environment, keeps operator responsiveness independent from rendering, allows existing EasyWorship content to be migrated without committing to unresolved legacy semantics, and leaves room for NDI/PowerPoint/remote features without making them v1 blockers.

## 2. Requirements and constraints

### 2.1 Functional priorities

1. Operator shell: menus, toolbar, Schedule / Live / Live Output panes, library tabs, search, preview.
2. Content: songs, scriptures, media, presentations, themes.
3. Scheduling: create/open/save, item ordering, live navigation, auto-advance, service intervals.
4. Live output: Go Live, Logo, Black, Clear, alerts, transitions, multi-monitor.
5. Rendering: text, images, backgrounds, shapes, themes, aspect ratio, transitions.
6. Media: video, audio, images, PowerPoint, NDI, alpha; DVD only if scoped.
7. Bible: translation selection, reference/search, preview, insertion.
8. Settings: profiles, output monitors, hotkeys, window state, history, remote/MIDI settings.
9. Error recovery: missing files, corrupt archives, helper/device failures, interrupted writes.

### 2.2 Non-functional requirements

- **Responsiveness:** operator input must not wait on media decode, DB I/O, or GPU work.
- **Live reliability:** a UI stall or media error must not blank or freeze the live output.
- **Multi-monitor:** independent output windows, DPI-aware placement, hot-plug tolerance.
- **Frame rate:** 60 fps target for motion/video where the display permits; stable cadence for slides.
- **Latency:** Go Live/slide change should be perceptually immediate; target to be calibrated against the oracle.
- **Maintainability:** modules separated by responsibility; no operator UI dependency on renderer internals.
- **Testability:** domain/application logic testable without a GPU; rendering testable with WARP/golden images.
- **Security:** signed binaries, validated archives, restricted WebView2 navigation, least privilege.
- **Legal:** clean-room implementation; licensed dependencies; no proprietary assets or text.
- **Deployment:** per-machine install with per-user/shared data separation.

### 2.3 Environment and constraints

- Verified environment: Windows 10.0.26200 x64, .NET SDK 9.0.308, WindowsDesktop runtime 9.0.11, .NET 8.0.301 SDK, Visual Studio Build Tools 18, Git, Node.js.
- **Decision (2026-10-26):** use the installed .NET 8 SDK for the Stage 5 prototype. Retarget to .NET 10 LTS before release; .NET 8 support ends in November 2026 and .NET 9 is out of support.
- No decompiler/source access; main EXE is MPRESS-packed.
- No admin guarantee; installers and services must be designed accordingly.
- No proprietary EasyWorship assets may be copied.
- NDI SDK, FFmpeg build, PowerPoint COM, and code-signing certificates are Stage 5 procurement/test dependencies.

## 3. Framework evaluation

Scores are relative to this project: 5 = strong fit, 1 = poor fit.

| Option | Multi-monitor | D3D/media interop | Maintainability | Deployment | WebView2 | Overall |
|---|---:|---:|---:|---:|---:|---:|
| **WPF + .NET** | 5 | 4 | 5 | 5 | 4 | **5** |
| WinUI 3 / Windows App SDK | 4 | 5 | 4 | 3 | 5 | 4 |
| Avalonia | 3 | 3 | 4 | 5 | 2 | 3 |
| Qt/C++ | 5 | 5 | 3 | 4 | 3 | 3 |
| Electron/web | 2 | 2 | 4 | 4 | 5 | 2 |

**Chosen: WPF + .NET.**

- WPF has mature multi-window support, strong data binding, per-monitor DPI support, and broad Windows deployment experience.
- D3D11 can be hosted through `HwndHost`/swap chains for preview and output; text can use DirectWrite inside the renderer.
- WinUI 3 has better native composition but higher deployment/multi-window complexity and a smaller mature ecosystem for this style of desktop app.
- Avalonia and Electron do not provide the required Windows media/D3D integration with acceptable risk.
- Qt/C++ could meet rendering needs but increases development cost and licensing/porting risk.

**Risk:** WPF is an older UI stack. Mitigation: keep the operator UI standard WPF/XAML, put all demanding rendering in D3D11, and avoid relying on WPF for live frame composition.

## 4. Architecture overview

```text
+------------------------------------------------------------------+
| EW.App (WPF operator shell)                                      |
|  menus, toolbar, panes, tabs, selectors, dialogs, editors        |
+-----------------------------+------------------------------------+
                              |
+-----------------------------v------------------------------------+
| EW.Application                                                   |
|  use cases, commands, queries, live state machine, scheduling    |
|  import orchestration, search, settings, undo/redo               |
+------+----------------+----------------+----------------+---------+
       |                |                |                |
+------v------+  +------v------+  +------v------+  +------v------+
| EW.Domain   |  | EW.Data     |  | EW.Rendering|  | EW.Media    |
| entities,   |  | SQLite,     |  | D3D11,      |  | FFmpeg,     |
| values,     |  | migrations, |  | DirectWrite,|  | WASAPI,     |
| policies    |  | repositories|  | scene graph |  | NDI, PPT    |
+-------------+  +------+------+  +------+------+  +------+------+
                        |                |                |
                 +------v------+  +------v------+  +------v------+
                 | EW.Platform |  | EW.Output   |  | EW.Infra    |
                 | Win32, UIA, |  | output      |  | DI, config, |
                 | displays,   |  | windows,    |  | logging,    |
                 | WebView2    |  | routing     |  | packaging   |
                 +-------------+  +-------------+  +-------------+
```

### 4.1 Process and threading model

**v1: one process, multiple execution contexts.**

| Context | Responsibility |
|---|---|
| WPF UI thread | Shell, dialogs, input, view models, output-window creation. |
| Render thread | D3D11 device, scene composition, transitions, preview/output presentation. |
| Media decode threads | FFmpeg decode, frame queue, audio decode. |
| Audio thread | WASAPI rendering and clock. |
| DB/I/O pool | Async SQLite reads/writes, imports, thumbnails. |
| Timers | Auto-advance, service intervals, alerts, debounce. |

Rules:

1. No DB I/O, media decode, or GPU wait on the WPF UI thread.
2. The render thread owns D3D11 device/context and swap chains.
3. Output windows are created on the UI thread but present from the render thread through a controlled host.
4. Media frames enter through bounded queues with explicit drop/backpressure policy.
5. A watchdog records render-thread stalls; the UI surfaces a recoverable error rather than blocking.

**Future option:** extract `IRenderHost` into a separate process if GPU/media isolation or crash containment becomes necessary. Do not build IPC in v1 unless a concrete requirement appears.

### 4.2 Module responsibilities

| Module | Owns | Must not own |
|---|---|---|
| `EW.App` | WPF views, view models, commands, navigation, dialogs | SQL, D3D devices, FFmpeg |
| `EW.Application` | Use cases, live/schedule state machines, import orchestration, validation | UI controls, renderer internals |
| `EW.Domain` | Entities, value objects, IDs, domain policies, invariants | Persistence or UI |
| `EW.Data` | SQLite schema, migrations, repositories, legacy readers, FTS | Rendering or UI |
| `EW.Rendering` | Scene graph, D3D11, DirectWrite, transitions, frame capture | Business rules or library queries |
| `EW.Media` | FFmpeg decode, audio, NDI, PowerPoint adapters | Operator UI or schedule policy |
| `EW.Output` | Output surfaces, monitor routing, live presentation | Data import or song editing |
| `EW.Platform` | Win32, display APIs, hotkeys, clipboard, WebView2 hosting | Domain decisions |
| `EW.Infrastructure` | DI, configuration, logging, crash reports, packaging hooks | Feature logic |

### 4.3 Solution layout

```text
EasyWorshipClone.sln
src/
  EW.App/
  EW.Application/
  EW.Domain/
  EW.Data/
  EW.Rendering/
  EW.Media/
  EW.Output/
  EW.Platform/
  EW.Infrastructure/
tests/
  EW.Domain.Tests/
  EW.Application.Tests/
  EW.Data.Tests/
  EW.Rendering.Tests/
  EW.Import.Tests/
  EW.IntegrationTests/
  EW.UiTests/
  EW.PerformanceTests/
tools/
  fixtures/
  capture/
  packaging/
```

Keep project count stable; add folders/feature slices inside `EW.Application` rather than new projects.

## 5. Operator UI design

### 5.1 Shell

- Menu: File, Edit, Live, Profiles, View, Help.
- Toolbar: New, Open, Save, Store/Web equivalent, Remote, Go Live, Alerts, Logo, Black, Clear, Live.
- Workspace: Schedule (left), Live (center), Live Output preview (right).
- Library: tabs for Songs, Scriptures, Media, Presentations, Themes; selector/search; list; preview.
- Status: active profile, output state, media/helper health, schedule position.

Use WPF `Grid`/`DockPanel` with persisted splitter ratios. Do not hard-code pixel positions from the oracle; reproduce workflow and relative layout.

### 5.2 MVVM and commands

- `CommunityToolkit.Mvvm` for observable properties and commands.
- `ICommandBus`/`IQueryBus` in `EW.Application`; view models call use cases, not repositories.
- Long-running commands return `Task<Result<T>>`; view models surface progress/errors.
- No business logic in code-behind except view-specific input/interop.

### 5.3 State model

- `AppState`: profile, libraries, selected items, schedule, live state, output health.
- `LiveState`: current item, slide index, output state (Live/Logo/Black/Clear/Alert/Media), transition state.
- `OutputState`: monitor, resolution, DPI, visible, health.
- State changes are immutable snapshots/events; the renderer consumes a snapshot, not view models.

### 5.4 Keyboard, mouse, and accessibility

- Central `HotkeyService` with configurable mappings and text-entry suppression.
- `AutomationProperties` on standard controls; custom controls expose names/patterns where possible.
- Keyboard-only path for all core operations; focus order is part of acceptance tests.
- High-contrast and scaling checks in tests.
- Avoid custom-drawn-only controls where a standard WPF control can meet the need.

### 5.5 WebView2

- Use `Microsoft.Web.WebView2` for onboarding/help/web resources.
- Separate WebView2 user-data folder per app version/profile.
- Restrict navigation to expected schemes/hosts; no arbitrary remote navigation without a prompt.
- Handle missing runtime/page with a native fallback.
- Do not reproduce the oracle's `%3F` URL bug.

## 6. Live output design

### 6.1 Output surfaces

- `IOutputSurface` abstraction: monitor, bounds, DPI, swap chain, visible state, capture.
- One borderless top-level window per configured output (Live, Alt, Foldback, Nursery/Message where enabled).
- Window placement uses monitor device ID + bounds; clamp to a valid work area when a monitor disappears.
- Output window has no WPF chrome/layout dependency; it hosts the D3D11 swap chain.
- Operator UI can show a low-resolution preview; live output uses full output resolution.

### 6.2 Live state machine

The oracle's exact priority is unknown. Implement a **policy-driven provisional state machine** with tests that can be updated when the oracle is verified.

```text
Idle -> Live -> (Logo | Black | Clear | Alert | Media)
             \-> Transition -> Live
```

Candidate policy:

| Layer | Examples | Priority |
|---|---|---|
| Base | Live item, media, idle | Lowest |
| Content overlay | Alert, lower-third/message | Above base |
| Blanking | Black, Clear | Explicit operator action; policy configurable |
| Standby | Logo | Explicit operator action; policy configurable |

Rules:

1. State transitions are commands with reasons and timestamps.
2. Output rendering consumes the current state snapshot; UI never mutates the renderer directly.
3. Unknown priority/restore behavior is encoded as a policy table, not scattered conditionals.
4. Tests cover all combinations of Live/Logo/Black/Clear/Alert/Media.
5. Do not claim oracle parity until `LIVE-003`, `LIVE-012`, and related tests pass.

### 6.3 Transitions

- Transition engine takes `fromScene`, `toScene`, `effect`, `duration`, `parameters`.
- v1 effects: cut, crossfade, and shader-based wipe/slide/push equivalents.
- Imported `.dfx` names/parameters map to built-in effects; unknown effects fall back to crossfade and are logged.
- Transition progress is driven by the render clock; live output never waits on a UI animation.

### 6.4 Multi-monitor and DPI

- Enumerate monitors via Win32 display APIs; use stable device IDs and bounds.
- Set per-monitor DPI awareness in the manifest and handle `WM_DPICHANGED`.
- Persist logical bounds and monitor identity; validate on startup.
- Test hot-plug, resolution change, primary change, and missing monitor.
- Do not assume the configured resolution equals the observed output window size until verified.

## 7. Rendering design

### 7.1 Scene graph

```text
Presentation
  -> Slide (ordered)
       -> Element (ordered, geometry, style)
            -> Resource (text/image/video/audio/web/shape/gradient)
```

- Use a renderer-owned immutable `RenderScene`; no WPF visuals in the scene.
- Coordinates: normalized `0..1` relative to a design canvas; convert to pixels per output with aspect-ratio policy.
- Z-order: element `order_index`; layers for background, content, overlays.
- Culling/dirty rectangles are optional; start with full-frame redraw for correctness.
- Cache static textures (images, text layouts) with revision keys.

### 7.2 D3D11 pipeline

- `Vortice.Direct3D11` device/context; WARP fallback if hardware device creation fails.
- Swap chains per output; `DXGI_SWAP_EFFECT_FLIP_DISCARD`; vsync policy configurable.
- Texture pool for images/video frames; staging texture for capture/golden tests.
- Direct2D/DirectWrite for text and vector shapes; WIC for image decode.
- Shader library for transitions and color/alpha operations.
- Color space: sRGB SDR for v1; HDR deferred.
- Preview can render at lower resolution and reuse the same scene graph.

### 7.3 Text

- RTF import to an internal text model (paragraphs/runs/styles).
- DirectWrite layout with font fallback, wrapping, alignment, and line spacing.
- Preserve imported style values where possible; normalize unsupported RTF to safe runs.
- Fonts: use installed fonts; package only redistributable fonts; never copy EasyWorship fonts.
- Bible/lyrics wrapping uses the same text engine plus a wrap cache.

### 7.4 Images, shapes, and video

- Images: WIC decode, sRGB, alpha; scale with high-quality sampling.
- Shapes: vector path data; imported `shape_data` mapped where possible; fallback rectangle.
- Video: decode to texture; alpha modes; letterbox/fill policy per media settings.
- Web: WebView2 visual hosting is a separate composition path; start with a dedicated window/overlay, not D3D texture composition, and document the limitation.

### 7.5 Rendering verification

- Headless/WARP golden-image tests for text, image, shape, gradient, and transition frames.
- Capture from live output via staging texture, not `PrintWindow`.
- Tolerance-based image comparison (layout/color thresholds) plus structural checks.
- Record GPU/driver in test output; skip hardware-only tests when unavailable.

## 8. Media design

### 8.1 Decode and playback

- `FFmpeg.AutoGen` with a pinned, LGPL-compatible FFmpeg build; ship notices/source offer as required.
- Demux/decode video on worker threads; convert to BGRA/NV12 textures.
- Decode audio separately; resample to the WASAPI mix format; use a shared media clock.
- Bounded frame queues; drop late video frames rather than blocking the render thread.
- Seek by keyframe + decode-forward; expose accurate seek only where needed.
- Looping, start/end trim, poster frame, mute, volume, and alpha map to the new media model.

### 8.2 Audio

- WASAPI shared mode for v1; exclusive mode optional.
- Device change handling and default-device fallback.
- Separate media and alert/notification buses; ducking only if verified.
- A/V sync driven by the audio clock; video presentation timestamps aligned to it.

### 8.3 Optional adapters

| Adapter | v1 | Notes |
|---|---|---|
| NDI input/output | Optional | Requires NDI SDK and licensing; isolate behind `INdiService`. |
| PowerPoint | Optional | COM automation if Office installed; otherwise import/render fallback. |
| DVD | Deferred | Legacy DirectShow complexity; not required for core fidelity. |
| WebView2 media | Optional | Separate composition path; do not block core renderer. |
| Streaming/recording | Deferred | Define after NDI/output architecture is stable. |

### 8.4 Media errors

- Validate path, hash, and probed streams before playback.
- Surface `MediaError` with code/message and a relink action.
- Never crash the app or blank live output on a decode failure; transition to a defined fallback.

## 9. Data design

### 9.1 Compatibility decision

**Chosen: import-first, read-only legacy access.**

Rationale:

- Bidirectional compatibility requires unresolved enum/property/migration semantics (`U-07`, `U-08`, `U-13`) and exact custom-collation behavior.
- Import-first lets users bring existing libraries into the clone while keeping legacy files untouched.
- Raw legacy values are retained in a sidecar so future mappings do not lose information.
- A legacy writer can be added later once the semantics and oracle tests are sufficient.

Rules:

1. Never open the production EasyWorship profile read-write.
2. Import from a copied/snapshot directory or a user-selected read-only source.
3. Preserve legacy IDs/UIDs in `LegacyId` fields.
4. Store unresolved raw fields as JSON in `LegacyMetadata`; do not guess.
5. Report unsupported content explicitly; skip nothing silently.
6. Keep import idempotent: re-import updates/links rather than duplicating by hash/UID.

### 9.2 New schema

Use a new SQLite database per profile, plus a shared catalog where appropriate.

Core tables:

| Table | Purpose |
|---|---|
| `Profile` | Profile identity, library root, schema version, settings. |
| `Song` | Title, author, copyright, tags, legacy IDs, search text. |
| `SongSlide` | Ordered slide content, section label, layout revision. |
| `MediaAsset` | Path, hash, type, dimensions, duration, trim, poster, alpha, status. |
| `Presentation` | Metadata, aspect ratio, theme, legacy IDs. |
| `Slide` | Order, theme/override references. |
| `Element` | Type, geometry, z-order, style, resource reference. |
| `Resource` | Text/RTF, image, video, audio, web, shape, gradient, feed. |
| `Theme` | Theme metadata and style model. |
| `Schedule` | Name, dates, metadata. |
| `ScheduleItem` | Ordered item reference and item state. |
| `BibleTranslation` | Translation metadata and source. |
| `BibleVerse` | Book/chapter/verse text (public-domain fixtures only in tests). |
| `OutputProfile` | Monitor, resolution, margins, output kind. |
| `ServiceInterval` | Start/end/fixed flags. |
| `Alert` / `TimerToken` | Alert/timer definitions. |
| `Setting` | Typed settings and raw legacy values. |
| `LegacyImport` | Source path/hash, status, warnings, raw sidecar. |

Search: use FTS5 tables for songs, media, presentations, themes, and Bible text. Keep a plain-text projection for indexing; preserve RTF separately.

### 9.3 File store

- Store imported/copied media under a content-addressed `Media/<hash>` tree with original filename metadata.
- Keep source paths for relink and diagnostics; do not depend on them.
- Generate thumbnails/posters asynchronously; store in `Cache/`.
- Use atomic temp-file + rename writes for DB and exports.
- Back up the DB before migrations and destructive imports.

### 9.4 Legacy import pipeline

```text
select source
  -> copy/snapshot or read-only open
  -> detect profile/version
  -> validate schema/archives
  -> import songs/media/presentations/themes/schedules
  -> import Bibles/transitions/settings where supported
  -> build FTS indexes and thumbnails
  -> report warnings/unsupported rows
  -> commit new profile
```

Legacy adapters:

| Source | Adapter | v1 behavior |
|---|---|---|
| `.ewp` | `LegacyProfileReader` | Resolve data root, options, locale. |
| `PresentationLayouts.db` + related DBs | `LegacyPresentationReader` | Map graph; preserve raw enums/properties. |
| `.ewsx` | `LegacyScheduleReader` | Read ZIP + `main.db`; import items/media. |
| `.ewtx` | `LegacyThemeReader` | Read ZIP + `main.db`; map supported styles. |
| `.ewb` | `LegacyBibleReader` | Import translation metadata/text for licensed/public-domain sources. |
| `.ewpck` | `LegacyPackageReader` | Import ZIP data/resources. |
| `.dfx` | `LegacyTransitionReader` | Map known parameters; fallback for opaque data. |
| `.ewo` | `LegacyOutputReader` | Import monitor/output settings where known. |

Legacy SQLite access:

- Register `UTF8_U_CI` using `sqlite3_create_collation` before reading indexes/schema that require it.
- Open read-only where possible; otherwise operate on a copy.
- Never run migrations against the source.

### 9.5 New package/export format

- `.ewclone` (working name): ZIP + `manifest.json` + `data.db` + `media/`.
- Manifest: schema version, app version, content IDs, hashes, licenses/notices.
- Export is for the clone only in v1; no claim of EasyWorship compatibility.
- Import is transactional; a failed import leaves the existing profile unchanged.

## 10. Settings, profiles, and data roots

Proposed roots:

| Scope | Path | Purpose |
|---|---|---|
| Machine | `%ProgramData%\EWClone\` | Shared settings, catalog, logs, machine plugins. |
| Shared library | `%PUBLIC%\Documents\EWClone\` (configurable) | Profiles, media, schedules, themes for multi-user use. |
| User | `%LocalAppData%\EWClone\` | Per-user cache, WebView2 data, preferences, crash reports. |
| Install | `%ProgramFiles%\EWClone\` | Binaries, native libraries, read-only assets. |

Settings:

- Typed settings model with schema version and validation.
- Store as JSON or SQLite; choose one and keep it versioned. Recommendation: SQLite for transactional profile state, JSON for machine/user preferences.
- Import legacy INI values into typed settings; keep raw values in `LegacyMetadata`.
- Precedence: command line > user > profile > machine > defaults; document and test.
- Save on change with debounce and on clean shutdown; atomic writes.
- Profile lock via a lock file with PID/start time; stale-lock recovery.

## 11. Security, privacy, and legal

1. Sign binaries and the installer; verify update packages.
2. Validate ZIP entries (path traversal, size limits, compression ratio) and SQLite inputs.
3. Do not load arbitrary native plugins from untrusted paths; use an allowlist/signature policy.
4. Restrict WebView2 navigation and disable unneeded web features.
5. Keep logs free of credentials, tokens, and personal content.
6. Use synthetic data in tests; never commit production media/lyrics.
7. Do not copy EasyWorship branding, artwork, help, fonts, or Bible text.
8. Comply with FFmpeg (LGPL/GPL depending on build), NDI SDK, codec patent, and font licenses.
9. Provide third-party notices and source-offer obligations where required.
10. Treat the original unsigned main EXE as untrusted input; never execute it from the clone.

## 12. Performance and reliability budgets

These are initial budgets to calibrate against the oracle; they are not claims about EasyWorship.

| Metric | Initial target |
|---|---|
| Warm startup to usable shell | < 2 s |
| Cold startup to usable shell | < 5 s |
| Go Live / slide change to output | < 50 ms perceived |
| Render cadence | 60 fps target; no unbounded frame queue |
| UI thread block | < 50 ms for normal commands |
| Memory (idle shell) | < 300 MB; media/output additional |
| Import | Progress + cancel; no UI freeze; transactional commit |
| Crash recovery | Last good DB backup; no partial import |

Reliability:

- Bounded queues and drop policies for media/render.
- Watchdog for render thread; log and recover.
- Global exception handler with crash dump and safe-mode startup.
- DB backup before migration/import; integrity check on open.
- Output fallback: if a monitor/window fails, keep operator UI alive and show a clear error.

## 13. Testing and verification

| Layer | Tooling | Focus |
|---|---|---|
| Domain/application | xUnit | State machines, scheduling, import mapping, validation. |
| Data | xUnit + SQLite fixtures | Schema, migrations, FTS, legacy readers, collation. |
| Rendering | xUnit + WARP + golden images | Text/image/shape/gradient/transition frames. |
| Media | Synthetic fixtures | Decode, seek, loop, AV sync, error paths. |
| Import | Synthetic legacy DBs/archives | Mapping, raw preservation, idempotence. |
| UI | FlaUI | Shell, tabs, dialogs, keyboard, live controls. |
| Oracle conformance | `research/test-cases.md` | Original vs clone workflows and outputs. |
| Performance | Benchmark harness | Startup, search, live latency, memory, frame pacing. |

Rules:

- No test may depend on production data.
- Golden images are versioned with a tolerance policy.
- Hardware-specific tests are skipped with a recorded reason, not silently passed.
- Every feature is "done" only with tests and documented deviations.

## 14. Packaging and deployment

- Self-contained `win-x64` publish; no machine-wide .NET runtime requirement.
- WiX MSI for per-machine install; optional per-user install later.
- Install native dependencies (FFmpeg, NDI optional, WebView2 bootstrap/evergreen check) with license notices.
- Create data roots/ACLs during install; do not require admin for normal use.
- Code-sign EXE/MSI; provide SHA256 checksums.
- Uninstall preserves user content by default; explicit remove-data option.
- No auto-update in v1; add a signed update channel after the core is stable.

## 15. Architecture decision records

| ADR | Decision | Rationale | Revisit when |
|---|---|---|---|
| ADR-001 | WPF on .NET 8 prototype; retarget .NET 10 before release | Mature Windows desktop, multi-window, D3D hosting, tooling; .NET 8 is already installed. | Release packaging or .NET 8 support expiry. |
| ADR-002 | Modular monolith, one process | Simpler than IPC; clean seams; faster delivery. | Render/media isolation or crash containment becomes necessary. |
| ADR-003 | Import-first legacy compatibility | Avoids unresolved bidirectional semantics; preserves existing content. | Enum/property/migration semantics and oracle tests are sufficient. |
| ADR-004 | D3D11 + Direct2D/DirectWrite | Control over text, video, transitions, multi-monitor output. | A simpler composition stack meets fidelity. |
| ADR-005 | FFmpeg.AutoGen + WASAPI | Broad codec support and audio control; matches original media stack. | Licensing or stability favors another engine. |
| ADR-006 | Borderless output windows | Independent, monitor-targeted live surfaces. | A single compositor/window proves sufficient. |
| ADR-007 | WiX MSI, self-contained x64 | Native deps and per-machine install fit desktop deployment. | MSIX/store distribution is required. |
| ADR-008 | New schema + raw legacy sidecar | Preserves unknown data without guessing. | Legacy writers are implemented. |
| ADR-009 | No proprietary assets | Legal/clean-room requirement. | Never for protected content. |
| ADR-010 | Test oracle = installed 7.4.0.7 | Only reliable behavioral reference. | Official baseline changes or app is unavailable. |

## 16. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| WPF/D3D11 interop complexity | Rendering delays | Prototype preview + one output window early; use WARP tests. |
| WebView2 composition/capture | Live web content issues | Isolate web surfaces; start with overlay/window path; test separately. |
| FFmpeg licensing/native packaging | Distribution risk | Use LGPL build, notices, legal review; pin versions. |
| NDI SDK/licensing | Advanced feature blocked | Keep optional adapter; defer if SDK unavailable. |
| PowerPoint COM availability | Import fidelity | Optional adapter; support image/PDF/video fallback. |
| Unknown enums/properties | Import data loss | Raw sidecar; warnings; no silent guessing. |
| Multi-monitor/DPI edge cases | Output failures | Dedicated output tests; clamp/fallback; hot-plug tests. |
| Live state priority unknown | Operator confusion | Policy-driven state machine; oracle tests; configurable policy. |
| Scope creep (DVD/streaming/remote) | Schedule slip | Explicit v1 scope; deferred adapters. |
| .NET 10 not installed | Build blocked | Install user-local/with approval; .NET 8 prototype fallback only. |
| Custom collation mismatch | Legacy read failures | Register `UTF8_U_CI`; fixture tests; read-only copies. The clone currently uses provisional `OrdinalIgnoreCase`; exact legacy folding/ordering remains unverified. |
| Performance with large libraries | Poor UX | FTS, paging, async I/O, thumbnail pipeline; benchmark early. |

## 17. Stage 5 entry criteria

Before implementation:

1. Use .NET 8 LTS for the Stage 5 prototype; plan the .NET 10 retarget before release. **Approved 2026-10-26.**
2. Approve v1 scope: core shell/rendering/media; NDI, PowerPoint, DVD, remote/MIDI as optional/deferred. **Approved 2026-10-26.**
3. Confirm import-first data strategy. **Approved 2026-10-26.**
4. Obtain/verify FFmpeg build and licenses; decide NDI/PowerPoint SDK needs.
5. Create synthetic fixtures and legacy-import fixtures (no production data).
6. Scaffold the solution, CI, logging, settings, and test harness.
7. Establish the oracle test environment and capture method.

## 18. Open design decisions

- .NET 10 retarget timing and release packaging.
- Whether live output should eventually move out-of-process.
- Whether WebView2 content is composed into the live scene or kept as an overlay/window.
- NDI/PowerPoint/DVD/remote/MIDI v1 scope.
- Exact output state priority after oracle tests.
- Bidirectional legacy compatibility timeline.
- Installer trust model and update channel.
- Accessibility conformance target.
