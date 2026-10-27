# EasyWorship 7 Known Unknowns

Status: Stage 3 consolidation  
Last updated: 2026-10-26  
Purpose: track unresolved questions that affect reconstruction design, fidelity, and test coverage.

## 1. How to read this

| Field | Meaning |
|---|---|
| Priority | **P0** blocks Stage 4 architecture or core fidelity; **P1** affects a major feature; **P2** affects polish/edge cases. |
| Impact | What is at risk if the unknown remains unresolved. |
| Evidence | What is currently known. |
| Next probe | The smallest safe action likely to resolve it. |

Do not treat an inferred behaviour as settled. Do not launch additional EasyWorship sessions or modify production data without explicit approval.

## 2. Resolved in Stage 2/3

| ID | Question | Resolution | Evidence |
|---|---|---|---|
| R-01 | Is the helper a dormant WOW64 shell? | No. It is fully loaded with 135 modules; Stage 2’s ntdll/WOW64-only view was a `tasklist /m` limitation. | `ezw-spawn.log`, helper probes |
| R-02 | What are the helper pipe names/roles? | Main creates `\\.\pipe\EZW_PIPE`; main opens `\\.\pipe\EZW_HELPER_PIPE`; helper responds. | `ezw-spawn.log` |
| R-03 | Is there an IPC startup handshake? | Yes. Timestamp frame, 144-byte request, `Response 13`, `Result=1`, ack, SQLite stream. | `ezw-spawn.log` |
| R-04 | Why does Getting Started fail? | The app encodes `?` as `%3F`, so Chromium looks for a literal filename and reports `ERR_FILE_NOT_FOUND`; the file exists. | `onboarding-tree.json`, `onboarding.png` |
| R-05 | Does the helper own local live output? | No evidence. Local `TForm_LiveOutput` windows are created by the main process. Helper ownership of NDI/render is still open. | `live-output-tree.json` |
| R-06 | What data did the helper serve at startup? | A profile-scoped SQLite database matching `ServiceIntervals.db`. | `ezw-spawn.log` |
| R-07 | Is the main window handle reliable? | No. `MainWindowHandle` can return `TApplication`; target `TFormEZWorshipMain` by class. | `ui-tree.json`, Stage 3 UIA |
| R-08 | Does the onboarding failure block the main UI? | No. Closing onboarding reveals the main form. | Stage 3 UIA |

## 3. P0 — blockers for Stage 4 architecture

### U-01 — Main EXE internals

| Field | Detail |
|---|---|
| Question | What Delphi units, classes, strings, and command handlers exist in `EasyWorship.exe`? |
| Impact | Blocks exact menu command behavior, output state logic, URL construction, and schedule/song algorithms. |
| Evidence | Main EXE is MPRESS-packed; entry point in `.MPRESS2`; no valid signature; no decompiler installed. |
| Next probe | Unpack or dump the main EXE with a dedicated workflow, then inspect Delphi RTTI/strings. Validate against runtime observations. |

### U-02 — Official binary baseline

| Field | Detail |
|---|---|
| Question | Is the installed unsigned MPRESS main EXE an unmodified official 7.4.0.7 build? |
| Impact | Affects trust in all behavioural evidence and whether observed bugs are product bugs or tampering. |
| Evidence | Main SHA256 `8223063a...`; helper is validly signed; main certificate directory is out of range. |
| Next probe | Compare against an official installer/hash from a trusted source; verify installer signature and file provenance. |

### U-03 — Full IPC command set

| Field | Detail |
|---|---|
| Question | What `RequestType`, `RecType`, and `RowID` combinations exist, and what do response codes mean? |
| Impact | Blocks helper compatibility, data-fetch replication, and any design that relies on the helper. |
| Evidence | One startup request fetched `ServiceIntervals.db`; `Response 13`; `Result=1`. |
| Next probe | Enumerate requests in an isolated session with synthetic data; correlate each with file I/O and UI actions. Do not fuzz production pipes. |

### U-04 — IPC value encoding

| Field | Detail |
|---|---|
| Question | Is the first value-block uint32 a type tag or a byte length? How are variable strings/arrays encoded? |
| Impact | Needed to build a compatible parser/writer. |
| Evidence | Observed blocks: 4/int32, 8/int64, 12-byte `Stage3` string. |
| Next probe | Capture requests with different string lengths and integer widths; compare encoding. |

### U-05 — `EZW_PIPE` reverse channel

| Field | Detail |
|---|---|
| Question | Does `EZW_PIPE` carry notifications, control messages, frame data, or nothing? |
| Impact | Determines whether the main/helper split is a data or rendering architecture. |
| Evidence | Main creates the server with `maxInstances=255`; no traffic captured in the Stage 3 run. |
| Next probe | Capture a live session that exercises Go Live, media playback, NDI, and output changes; inspect both pipes. |

### U-06 — UI action ↔ IPC ↔ rendering mapping

| Field | Detail |
|---|---|
| Question | Which UI actions cause helper requests, which cause local DB writes, and which only change local rendering? |
| Impact | Blocks a faithful process/architecture model and may cause unnecessary helper complexity. |
| Evidence | Go Live/live UI worked; only a startup data request was captured. |
| Next probe | Correlate timestamped UIA actions with pipe, SQLite, and D3D events in an isolated profile. |

### U-07 — Enum mappings

| Field | Detail |
|---|---|
| Question | What do `media_type`, `resource_type`, `element_type`, `element_style_type`, `action_type`, `presentation_type`, `layout_flag`, `end_type`, `feed_type`, `repeating_type`, `usage_type`, and `alpha_channel_mode` mean? |
| Impact | Blocks correct data interpretation, import/export, rendering, and search. |
| Evidence | Column names/types only; no decoded value tables. |
| Next probe | Query distinct values and correlate with known synthetic UI states; avoid extracting personal content. |

### U-08 — Property-key semantics

| Field | Detail |
|---|---|
| Question | What are the keys/value types/precedence in `presentation_property`, `slide_property`, `element_property`, and their groups? |
| Impact | Blocks faithful text/style/theme/layout rendering and editing. |
| Evidence | Tables and `value_type` columns exist; no key catalog. |
| Next probe | Create minimal synthetic presentations with one changed property at a time; diff the DB. |

### U-09 — Transition implementation

| Field | Detail |
|---|---|
| Question | What is encoded in `.dfx.data`, and how do parameters/timing drive rendering? |
| Impact | Blocks transition fidelity; affects live output and preview. |
| Evidence | `.dfx` is SQLite; `Wipe` exposes Direction/BorderSoftness; `data` is opaque. |
| Next probe | Inspect multiple `.dfx` files, capture a transition, and compare parameter changes to rendered frames. |

### U-10 — Rendering pipeline

| Field | Detail |
|---|---|
| Question | How are element geometry, themes, fonts, alpha, resources, and output surfaces composed? |
| Impact | Core visual fidelity and multi-monitor output. |
| Evidence | D3D9/10/11, WebView2, `TsdPresentationWin`, `Intermediate D3D Window`; capture was black. |
| Next probe | Capture with a D3D-aware method or an output mirror; test one synthetic element at a time. |

### U-11 — Output state machine

| Field | Detail |
|---|---|
| Question | What are the states and priority rules for Live, Logo, Black, Clear, Alert, and media? |
| Impact | Blocks correct operator controls and output behavior. |
| Evidence | Controls exist; Go Live observed; Logo/Black/Clear effects not visually verified. |
| Next probe | Use a synthetic, high-contrast slide and record output after each control; test combinations and restore. |

### U-12 — Multi-monitor/output mapping

| Field | Detail |
|---|---|
| Question | How do `.ewo` output settings map to actual windows/monitors, and how are Alt/Foldback/Nursery/Message surfaces routed? |
| Impact | Blocks secondary-monitor, stage-display, and multi-output fidelity. |
| Evidence | Per-output monitor/device/bounds settings; configured 1024x768 vs observed 1920x1080 output. |
| Next probe | Test with two synthetic monitors, change primary/resolution, record window bounds and output content. |

### U-13 — Data migration

| Field | Detail |
|---|---|
| Question | How are 7.3-era `.ewsx`/`.ewtx` schemas migrated to 7.4.1.3? |
| Impact | Blocks opening existing schedules/themes and preserving revisions/UIDs. |
| Evidence | Archive schema 7.3.0.11/27-column presentation; active data 7.4.1.3/28 columns. |
| Next probe | Open a synthetic 7.3 archive in an isolated profile; diff before/after DBs. |

### U-14 — Schedule/auto-advance semantics

| Field | Detail |
|---|---|
| Question | How do contiguous schedule, automatic advance, schedule checks, and live navigation interact? |
| Impact | Blocks live operation fidelity and operator trust. |
| Evidence | Settings exist; behavior not exercised. |
| Next probe | Build a synthetic 3-item schedule; test each flag independently and in combination. |

### U-15 — Song slide generation

| Field | Detail |
|---|---|
| Question | How are lyrics split into slides, and how are section labels detected/applied? |
| Impact | Blocks song rendering, slide counts, and jump-to-section behavior. |
| Evidence | RTF lyrics, `slide_uids`, section labels/colors. |
| Next probe | Create synthetic songs with known verse/chorus markers and line lengths; compare generated slides. |

### U-16 — Media playback lifecycle

| Field | Detail |
|---|---|
| Question | What are the exact load/play/pause/stop/end/error states for video, audio, NDI, DVD, PowerPoint, web, and alpha media? |
| Impact | Blocks reliable playback and live output. |
| Evidence | Media schema, FFmpeg/NDI/DScaler dependencies; no full playback trace. |
| Next probe | Use synthetic short media; capture state transitions and output; test unsupported/missing files. |

### U-17 — Bible workflows

| Field | Detail |
|---|---|
| Question | How are references parsed, passages searched/wrapped, and verses inserted into live/schedule? |
| Impact | Blocks scripture feature fidelity. |
| Evidence | `.ewb` schema, wrap cache, last-Bible setting; no lookup/insertion trace. |
| Next probe | Use KJV with synthetic references; compare preview, slide text, and schedule output. |

### U-18 — Full UI command surface

| Field | Detail |
|---|---|
| Question | What do every menu item, toolbar dropdown, context menu, and dialog do? |
| Impact | Blocks complete shell behavior and acceptance tests. |
| Evidence | Only top-level labels and a few controls were captured. |
| Next probe | Enumerate UIA/menus and capture screenshots in an isolated session; map commands to DB/pipe effects. |

## 4. P1 — major feature unknowns

| ID | Question | Impact | Evidence | Next probe |
|---|---|---|---|---|
| U-19 | What are settings precedence, save timing, and profile-switch behavior? | Persistence bugs, lost work. | INI/DB files mapped; timing unknown. | Change one setting at a time; diff files before/after. |
| U-20 | How do Store, Web, updates, licensing, and online downloads work? | Online feature scope and security. | Store/Web buttons, WebView2, updates DB, empty license dirs. | Inspect URLs/network in an isolated session; do not enter credentials. |
| U-21 | What are the remote-control and MIDI protocols? | Integration fidelity. | Config/DB tables exist. | Capture loopback remote traffic; test MIDI with a virtual device. |
| U-22 | What are Foldback/stage/nursery/message semantics? | Stage-display and multi-room features. | Output settings DBs; Foldback disabled. | Enable one synthetic output; capture window and content. |
| U-23 | How do alerts, timers/tokens, and service intervals behave? | Live service operation. | Tables and IPC fetch; no UI trace. | Create synthetic alert/timer/interval; observe output and DB changes. |
| U-24 | How do collections/rules/search work? | Library organization and search fidelity. | Collections tables; no UI trace. | Build a synthetic collection; compare filters/results. |
| U-25 | What is the accessibility/keyboard model? | Usability and test automation. | Custom controls; UIA `InvokePattern` unsupported. | Keyboard-only run; inspect focus order and shortcuts. |
| U-26 | What localizations are supported? | Internationalization scope. | ICU 60.2, `locale.dat`, `DesiredLocale`. | Switch locale; compare UI/assets. |
| U-27 | What are startup, live-latency, and large-library performance targets? | Architecture sizing. | No measurements. | Measure with synthetic libraries and output; record CPU/GPU/memory. |
| U-28 | How are media hashes, duplicates, and relinking handled? | Import correctness. | Hash/path fields; no workflow. | Import duplicates/renamed files; observe DB and UI. |
| U-29 | How complete is the test oracle? | Cannot prove fidelity without coverage. | Only a few workflows exercised. | Expand black-box tests; compare original vs clone. |
| U-30 | What legal/branding/assets constraints apply? | Distribution risk. | Proprietary UI/assets/Bibles. | Define replacement assets and licensing boundaries in Stage 4. |
| U-31 | What are helper lifecycle, security, and multi-client rules? | Process architecture and robustness. | Helper pre-exists; broad pipe modes. | Start/stop helper; force disconnect; inspect ACLs. |
| U-32 | What are the internal clipboard/drag-drop payloads? | Copy/paste and scheduling fidelity. | Registered clipboard formats. | Copy a synthetic slide/song; inspect clipboard bytes. |
| U-33 | How are thumbnails/posters generated and invalidated? | UI responsiveness and cache correctness. | Cache DBs and revision fields. | Modify media/theme; observe cache entries and timestamps. |
| U-34 | What is `Protocol.elck` for? | Profile locking/concurrency. | 1-byte lock file. | Open two profiles/two instances; observe file and behavior. |
| U-35 | How are updates/version checks performed? | Install/upgrade behavior. | `updates.2.db`, web assets. | Inspect update metadata/network without installing. |

## 5. P2 — edge cases and polish

| ID | Question | Impact | Evidence | Next probe |
|---|---|---|---|---|
| U-36 | How are invalid references/books handled? | Bible UX. | Schema only. | Test invalid/edge references. |
| U-37 | How are Unicode/long paths handled? | Import/playback reliability. | Paths in DBs; no stress test. | Synthetic long/Unicode filenames. |
| U-38 | How does the UI behave at high DPI? | Layout/output quality. | Window settings; no DPI test. | Test 100/150/200% scaling. |
| U-39 | How are missing output windows handled? | Operator recovery. | Output windows observed; no failure test. | Close output window while live; observe recovery. |
| U-40 | How are corrupt DBs/archives reported? | Data safety. | Error/ready fields; no test. | Corrupt an isolated copy; observe error handling. |
| U-41 | How are theme overrides reset? | Editing UX. | Modified-theme fields. | Change/reset a theme property; diff DB. |
| U-42 | How does audio ducking/volume behave? | Service audio quality. | Volume/mute fields. | Test media + microphone-like audio; measure. |
| U-43 | How are transitions interrupted/queued? | Live reliability. | `.dfx` files; no trace. | Rapidly trigger transitions; observe state. |
| U-44 | How are alerts dismissed/expired? | Live operation. | Alert tables. | Trigger/dismiss/expire a synthetic alert. |
| U-45 | How does the app behave with no Bibles/themes? | Empty-state quality. | Plugins installed; no empty test. | Use an isolated profile with missing plugins. |
| U-46 | What spell-check/autocorrect behavior exists? | Song editor UX. | Dictionaries installed. | Type known misspellings; observe suggestions. |
| U-47 | How are recent items/privacy handled? | Data hygiene. | `History.ini` recents. | Clear/repopulate; observe. |
| U-48 | What is `Users.db.run_count` used for? | Telemetry/UX. | Schema only. | Compare runs; inspect UI/network. |
| U-49 | How are packages installed/uninstalled? | Content workflow. | `.ewpck`, `Packages.db`. | Install a synthetic package in an isolated profile. |
| U-50 | What are the output safe-area/margin rules? | Visual fidelity. | Margin settings. | Change margins; capture output. |

## 6. Assumptions that must not be made

Do not assume any of the following without new evidence:

- The helper owns live rendering or NDI output.
- `EZW_PIPE` is a render/frame channel.
- `RequestType=1` always means `ServiceIntervals.db`.
- The 144-byte request is the only startup request.
- Logo, Black, and Clear are simple output states with obvious priority.
- Foldback is the stage display.
- Alt and Foldback are independent outputs.
- All `.ewo` databases are simple overrides of `PresentationLayouts.db`.
- Archive migration is lossless.
- The main EXE is an official unmodified build.
- UI labels imply complete command semantics.
- The observed 1920x1080 output maps directly to the configured 1024x768 monitor.
- The clone can ignore `UTF8_U_CI` and still read original databases correctly.

## 7. Recommended evidence order

1. **No new runtime launches unless approved.** Finish Stage 4 design around known constraints first.
2. If runtime work is approved: capture one end-to-end synthetic workflow (song → schedule → live) with pipe, SQLite, and D3D-aware output capture.
3. Unpack/analyse the main EXE to resolve U-01 and U-11/U-18.
4. Capture output state transitions and transitions/rendering with synthetic assets.
5. Explore media playback and Bible insertion.
6. Only then expand to remote/MIDI/online/advanced outputs.

## 8. Relationship to Stage 4

Stage 4 can begin architecture design with these unknowns explicitly scoped:

- Choose import-only vs bidirectional data compatibility.
- Choose whether to use the original helper or an internal data/media service.
- Choose a rendering approach that can be validated against the original.
- Keep uncertain behaviors behind adapters and tests.
- Do not claim parity for any feature still marked P0/P1.

The five Stage 3 documents are intended to make these deferrals explicit rather than silently guessing.
