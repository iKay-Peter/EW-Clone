# EasyWorship 7 Architecture Findings

Status: Stage 3 technical findings  
Last updated: 2026-10-26  
Target: EasyWorship 7.4.0.7 on Windows  
Companion documents: `research/easyworship-inventory.md`, `research/feature-matrix.md`, `research/behavioural-specification.md`, `research/known-unknowns.md`

## 1. Scope and evidence

This document records architecture that is supported by artifacts, runtime observation, or captured IPC. It does not claim access to EasyWorship source code. Where a conclusion is inferential, it is labelled.

Primary evidence:

| Source | Use |
|---|---|
| `research/easyworship-inventory.md` | Installation, binaries, data roots, schemas, settings, Stage 3 addendum |
| `research/easyworship-schema.txt` | SQLite table/column evidence |
| `research/easyworship-archives.txt` | `.ewsx`/`.ewtx` archive structure |
| `research/stage3/ezw-spawn.log` | Main-process pipe and startup handshake |
| `research/stage3/onboarding-tree.json` | WebView2 onboarding failure |
| `research/stage3/ui-tree.json` | Main UI control classes and layout |
| `research/stage3/live-output-tree.json` | Live output window composition |
| `research/stage3/helper-imports.json` | Helper imports |
| `research/stage3/main-imports.json` | Main imports |

## 2. System overview

```text
┌─────────────────────────────────────────────────────────────┐
│ EasyWorship.exe (PE32 x86, Delphi/VCL, MPRESS-packed)       │
│                                                             │
│  Operator UI shell                                          │
│    ├─ menus, toolbar, Schedule / Live / Live Output panes   │
│    ├─ library tabs and selectors                            │
│    ├─ editor / inspector / options windows                  │
│    └─ WebView2 surfaces (onboarding, web/help/live content) │
│                                                             │
│  Local data access                                          │
│    └─ SQLite databases, custom UTF8_U_CI collation          │
│                                                             │
│  Live output windows                                        │
│    └─ D3D9/D3D10/D3D11 + WebView2 composition               │
│                                                             │
│  IPC                                                        │
│    ├─ server: \\.\pipe\EZW_PIPE                             │
│    └─ client: \\.\pipe\EZW_HELPER_PIPE                      │
└──────────────────────────────┬──────────────────────────────┘
                               │ named pipe
┌──────────────────────────────▼──────────────────────────────┐
│ EasyWorshipHelper.exe (PE32 x86, signed, fully loaded)      │
│    ├─ EZW_HELPER_PIPE responder                             │
│    ├─ profile-scoped SQLite data fetch                      │
│    └─ media/render libraries (FFmpeg, NDI, DScaler, D3D)    │
│       — exact ownership is partly inferential               │
└─────────────────────────────────────────────────────────────┘
```

**Confirmed:** the helper is a pipe responder that served a SQLite database at startup.  
**Inferred:** the helper also participates in media/rendering work; its loaded modules and embedded Delphi units support this, but the observed request was a data fetch.  
**Confirmed:** local live-output windows are created by the main process, not by the helper.

## 3. Process and executable model

### 3.1 Main executable

| Property | Finding |
|---|---|
| Path | `C:\Program Files (x86)\Softouch\EasyWorship 7\EasyWorship.exe` |
| Size | 6,041,088 bytes |
| SHA256 | `8223063a36c7438aaff86b4dae227f7abdeafc900e0a8fa58299932766825e0e` |
| Format | PE32, x86, GUI subsystem; no CLI header; not managed |
| Packer | MPRESS; entry point in `.MPRESS2` |
| Signature | No valid Authenticode; certificate directory out of file range |
| Compile timestamp | `2022-07-28 23:56:55 UTC` |
| Compiler | Not directly confirmed because of packing; likely same Delphi 10.4-family toolchain as other components |

### 3.2 Helper executable

| Property | Finding |
|---|---|
| Path | `C:\Program Files (x86)\Softouch\EasyWorship 7\EasyWorshipHelper.exe` |
| Size | 20,160,232 bytes |
| SHA256 | `59e7f255bf3da905c0a9f9ffea74d3ff38c83c0e1bcd865fd1f78d0c0c7c223e` |
| Format | PE32, x86, GUI subsystem; unpacked |
| Signature | Valid Softouch Authenticode signature |
| Compile timestamp | `2022-07-28 23:51:29 UTC` |
| Runtime observation | PID 44480, command line `... EasyWorshipHelper.exe AUTO`, 135 loaded modules |

### 3.3 Process lifecycle

1. **[O]** The helper can already be running before the main process starts. It is not necessarily a child of `EasyWorship.exe`.
2. **[O]** Stage 3 attached to the running helper (PID 44480) and confirmed 135 modules, including SQLite, FFmpeg, NDI, DScaler5, ICU, D3D9/10/11, WebView2, and Delphi runtime libraries.
3. **[O]** Stopping the main process does not necessarily stop the helper.
4. **[I]** `AUTO` likely selects an automatic startup/operation mode; its exact meaning is unknown.
5. **[U]** Service registration, restart policy, and crash recovery are unknown.

### 3.4 Packing and analysis constraints

- The main EXE is MPRESS-packed, so direct string/RTTI/unit extraction is blocked.
- No Ghidra, Hopper, IDA, JADX, or Kuna was installed in the environment.
- REA static resource inspection fails on the packed PE’s resource section.
- The helper is unpacked and signed, making it the better target for Delphi unit/import/RTTI evidence.
- Any claim about the main EXE’s internal unit structure is a hypothesis until it is unpacked or captured at runtime.

## 4. UI architecture

### 4.1 Framework and controls

- **[O]** The UI is native Delphi VCL/custom-control based. Control classes use the `Tsd*` prefix: `TsdPanel`, `TsdSpeedButton`, `TsdTabBar`, `TsdTabPanel`, `TsdImageButton`, `TsdImagePanel`, `TsdCustomIncEdit`, `TsdOutlineEdit`, `TsdRichEditScrollBar`, `TsdSplitterPanel`, `TsdPresentationWin`, `TsdPresentationWinShared`, `TsdWebBrowser`.
- **[C]** `sslsparser2.dll` confirms a Delphi 10.4-family toolchain (compiler 34.0 / 27.0.41310.5003); helper/hooks expose Delphi RTTI and FMX/VCL identifiers.
- **[O]** Main form class: `TFormEZWorshipMain`; onboarding: `TForm_Onboarding`; live output: `TForm_LiveOutput`, `TForm_LayeredWindow`.
- **[O]** The main window exposes 121 UIA elements after onboarding is closed.

### 4.2 UI Automation limitations

- **[O]** `Get-Process.MainWindowHandle` can return `TApplication` (for example `0x3317F2`) rather than `TFormEZWorshipMain`.
- **[O]** Targeting by class `TFormEZWorshipMain` is reliable; targeting the process’s “main window” handle is not.
- **[O]** `TsdSpeedButton` does not support UIA `InvokePattern`; a mouse-click fallback is required.
- **[I]** Any automation or accessibility layer must account for custom-drawn controls and WebView2 child windows.

### 4.3 WebView2

- **[C]** Loader `WebView2Loader.dll` 1.0.1072.54 is bundled; installed runtime is 155.0.4283.62.
- **[O]** Onboarding uses `TewWebBrowserCloud` → `Chrome_WidgetWin_1` → `RootView`.
- **[O]** Live output includes a `TsdWebBrowser` with `about:blank` and a WebView2 `Intermediate D3D Window`.
- **[O]** The onboarding URL bug percent-encodes `?` as `%3F`, producing `ERR_FILE_NOT_FOUND` despite the asset existing.
- **[U]** Which product features depend on WebView2 (help, store, login, online content, live pages) is not mapped.

## 5. IPC architecture

### 5.1 Pipe roles

| Pipe | Owner/role | Evidence |
|---|---|---|
| `\\.\pipe\EZW_PIPE` | Main creates a server (`openMode=0x40000003`, `pipeMode=0`, `maxInstances=255`) | `ezw-spawn.log` |
| `\\.\pipe\EZW_HELPER_PIPE` | Main opens a client (`access=0xC0000000`, `creation=3`, `GetFileType=3`) | `ezw-spawn.log` |
| `EZW_HELPER_PIPE` responder | Helper | Observed reads/writes |

No traffic was captured on `EZW_PIPE` in the Stage 3 run.

### 5.2 Framing

Observed message envelope:

```text
offset 0: uint32 payload_length
offset 4: uint32 reserved (zero in all observed messages)
offset 8: payload[payload_length]
```

The payload length excludes the 8-byte header. The captured startup sequence:

```text
main → helper: len=42   "1.0/26/2026 7:57:40 PM"   (34 UTF-16LE bytes) + 8 zero bytes
main → helper: len=144  request
helper → main: len=22   "Response 13"
helper → main: len=28   "Result" + int32 value 1
main → helper: len=8    0xFFFFFFFFFFFFFFFF
helper → main: SQLite database stream, first read 100 bytes, then 1,024-byte chunks
```

The original first write was 50 bytes on the wire (8-byte header + 42-byte payload). The 144-byte request was 152 bytes on the wire.

### 5.3 Field encoding

The request uses length-prefixed UTF-16LE names and a value block:

```text
uint32 name_length_bytes
UTF-16LE name[name_length_bytes / 2]
value_block
```

Observed value blocks:

| Field | Bytes after name | Interpretation |
|---|---|---|
| `RequestType` | `04 00 00 00 00 00 00 00 01 00 00 00` | 12 bytes; int32 value 1 |
| `ProfileName` | `0c 00 00 00 00 00 00 00` + `Stage3` (12 bytes) | 12-byte string |
| `RecType` | `04 00 00 00 00 00 00 00 00 00 00 00` | 12 bytes; int32 value 0 |
| `RowID` | `08 00 00 00 00 00 00 00` + 8 zero bytes | 16 bytes; int64 value 0 |

**Confirmed:** the field names and values above.  
**Inferred:** the first uint32 of the value block is a type/length tag (4 = int32, 8 = int64, 12 = 12-byte string).  
**Unknown:** whether the tag is a type ID or byte length; how variable-length strings are distinguished; whether the reserved uint32 is alignment or a flag.

### 5.4 Observed request semantics

The request produced `ServiceIntervals.db`, a 5,120-byte SQLite database with `revision` and `interval` tables. The path in the production profile is:

```text
C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Options\Default.ewo\Data\ServiceIntervals.db
```

The exact mapping from `RequestType=1`, `RecType=0`, `RowID=0` to that table is not decoded. It is likely a profile-scoped data-fetch request, but the command space is unknown.

### 5.5 Security and robustness

- **[O]** No authentication or encryption was visible in the captured startup sequence.
- **[U]** It is not proven that no authentication/encryption exists for other commands.
- **[I]** Local pipe access is governed by Windows ACLs; the captured `openMode`/`access` values suggest broad local access.
- **[U]** Message size limits, malformed-message handling, reconnect, and multi-client behavior are unknown.

## 6. Data architecture

### 6.1 Data roots

| Scope | Path |
|---|---|
| Install | `C:\Program Files (x86)\Softouch\EasyWorship 7\` |
| Global settings/plugins | `C:\ProgramData\Softouch\EasyWorship.v7\` |
| Per-user settings/cache | `C:\Users\iKay\AppData\Roaming\Softouch\Easyworship.v7\` |
| WebView2 user data | `C:\Users\iKay\AppData\Roaming\Softouch\WebView2\EBWebView\` |
| Profile libraries | `C:\Users\Public\Documents\Softouch\Easyworship\<profile>\` |

### 6.2 Profile model

`.ewp` is an INI file:

```ini
[Profile]
Id=1-...
AppInstDataDir=C:\Users\Public\Documents\Softouch\Easyworship\<profile>\
Options=Default
DesiredLocale=
```

The active profile is selected by `History.ini` (`default_profile`). Profiles separate libraries, schedules, media, themes, archives, caches, and output options.

### 6.3 Core databases

| Database | Role |
|---|---|
| `PresentationLayouts.db` | Presentation → slide → element → resource/file graph |
| `Songs.db` | Song metadata and presentation link |
| `SongWords.db` | RTF lyrics and slide UID/revision links |
| `SongKeys.db` | Song search index |
| `SongHistory.db` | Song usage/action history |
| `Media.db` | Media library and media subtypes |
| `MediaKeys.db` | Media search index |
| `Presentations.db` | External presentation file index |
| `PresentationKeys.db` | Presentation search index |
| `Themes.db` | Theme file index |
| `ThemeKeys.db` | Theme search index |
| `Collections.db` | Collections, rules, layout/library settings |
| `Users.db` | User identity/run metadata |
| `Packages.db` | Content package status |
| `version.dat` / `locale.dat` | Data version/locale |

Data version is `7.4.1.3`; minimum is `6.5.1.0`.

### 6.4 Core graph

```text
Songs.song.presentation_id
    └─ PresentationLayouts.presentation
         ├─ slide.presentation_id
         │    └─ element.slide_id
         │         ├─ background_resource_id ─┐
         │         ├─ foreground_resource_id ─┤
         │         └─ shape_resource_id ──────┤
         │                                    └─ resource
         │                                         ├─ resource_image
         │                                         ├─ resource_video
         │                                         ├─ resource_audio
         │                                         ├─ resource_text
         │                                         ├─ resource_web
         │                                         ├─ resource_gradient
         │                                         ├─ resource_shape
         │                                         ├─ resource_dvd
         │                                         └─ resource_feed
         └─ token
```

- **[C]** Cross-database relationships are application-managed; declared foreign keys are limited.
- **[C]** `element` has geometry (`x`, `y`, `width`, `height`), order, type/style, resource IDs, mute, and `from_master`.
- **[C]** `presentation` has UIDs, type, aspect ratio, theme/thumbnail/revision fields, metadata, provider/vendor IDs, and error/ready fields.
- **[C]** `slide` has order, layout flags, theme references, modified-theme fields, and thumbnails.

### 6.5 SQLite constraints

- **[C]** Bundled `sqlite3.dll` version string is `3.7.15.2`.
- **[C]** Schemas use `COLLATE UTF8_U_CI`.
- **[C]** The bundled SQLite does not define `UTF8_U_CI`; the helper contains the literal and a `REINDEX UTF8_U_CI` statement.
- **[P]** Any reader/editor of the original databases must register an equivalent Unicode case-insensitive collation before schema/index use. Otherwise indexes and queries may fail or behave differently.
- **[U]** Exact collation semantics (case folding, accent handling, normalization) are not fully characterized.

### 6.6 Output settings databases

`Options\<set>.ewo\Data` contains:

```text
Live.Settings.db
Alt.Settings.db
Foldback.Settings.db
LiveMessage.Settings.db
AltOutMessage.Settings.db
AltOutNursery.Settings.db
LiveNursery.Settings.db
FoldbackMessage.Settings.db
Alerts.db
ServiceIntervals.db
```

- **[C]** Live/Alt/Foldback settings use the same 28-column `presentation` schema.
- **[C]** `Alerts.db` adds alert/token tables.
- **[C]** `ServiceIntervals.db` has interval start/end/fixed fields.
- **[I]** Output settings databases are per-output overrides/snapshots of the main presentation graph.
- **[U]** Resolution of conflicts between `PresentationLayouts.db` and output settings is unknown.

### 6.7 Archives and packages

| Format | Structure | Status |
|---|---|---|
| `.ewsx` schedule | ZIP + `main.db` + optional `media/` | Confirmed |
| `.ewtx` theme | ZIP + `main.db` | Confirmed |
| `.ewpck` package | ZIP + `Data/*.db` + `Resources/*` | Confirmed |
| `.ewb` Bible | SQLite `header`/`books`/`streams`/`words` | Confirmed |
| `.dfx` transition | SQLite `info`/`parameter`/`data` BLOB | Confirmed |
| `.ewo` options | Directory of SQLite DBs + thumbnails | Confirmed |
| `.ewp` profile | INI | Confirmed |
| `.elck` lock | 1-byte file | Purpose unknown |

Schedule/theme `main.db` uses a 7.3.0.11-era schema (27-column presentation, no `provider_id`; 10-column `resource_feed_pc`). The active data layer is 7.4.1.3, so upward migration is inferred.

### 6.8 Caches

- `slide.thumb.5.db` — slide thumbnails, revisioned.
- `media.thumb.2.db` — media thumbnails.
- `Posterframes/*.jpg` — video poster frames.
- `biblewraps.2.db` — Bible word-wrap cache.

These indicate asynchronous thumbnail/poster/wrap generation with revision tracking; exact scheduling and invalidation are unknown.

## 7. Rendering and media architecture

### 7.1 Rendering stack

- **[C]** D3D9/D3DX9 9.29.952.3111; helper delay-loads D3D10/10.1/11.
- **[O]** Live output tree includes `TsdPresentationWin`, `TsdWebBrowser`, and an `Intermediate D3D Window`.
- **[O]** Live output window 1920x1080; D3D intermediate window 2304x1536.
- **[O]** `PrintWindow` and `CopyFromScreen` produced black output; this is a capture limitation, not a rendering result.
- **[U]** Exact composition order, color space, scaling, alpha, and GPU/software fallback are unknown.

### 7.2 Media stack

- **[C]** FFmpeg 4: `avcodec-58`, `avformat-58`, `avutil-56`, `avfilter-7`, `avdevice-58`, `swresample-3`, `swscale-5`.
- **[C]** NDI 4.6.3 plus NDI plugins and NDI-prefixed FFmpeg DLLs.
- **[C]** DScaler5 MPEG/DVD filters.
- **[C]** DirectSound and AVRT for audio.
- **[C]** WebView2 for web surfaces.
- **[I]** Media decode/render likely runs in the helper or a shared media layer; the observed startup request was data-only, so ownership is not fully proven.

### 7.3 Multi-monitor outputs

- **[C]** `ComputerSettings.ini` stores per-output monitor number, device ID, bounds, and margins for Live/Alt/Foldback.
- **[C]** Alpha monitor/device fields exist.
- **[O]** Configured live monitor was 1024x768 while the observed live output window was 1920x1080.
- **[U]** The mapping from settings to actual output surfaces, and the handling of DPI/EDID/hot-plug, is unresolved.

## 8. Configuration architecture

| File/DB | Scope | Role |
|---|---|---|
| `ComputerSettings.ini` | Global | Output monitors, margins, Alt/Foldback/NewTek flags, packaging, DVD settings |
| `UserSettings.ini` | User | Interface mode, scripture enablement, auto-advance, PowerPoint, checks, section labels/colors |
| `WindowSettings.ini` | User | Main/editor/inspector/options bounds, splitters, popup sizes |
| `HotKeys.2.ini` | Global | Keyboard mappings and navigation permissions |
| `BibleSettings.ini` | User | Last Bible, removed Bibles |
| `History.ini` | User | Active profile, schedule path, last resource path, recents |
| `RemoteControlSettings.ini` | Global | Remote enablement, display name, UID |
| `MIDIControls.7.db` | Global | MIDI control/out/export settings |
| `RemoteControlClients.2.db` | Global | Paired clients and permissions |
| `.ewp` profiles | Global | Profile identity/data root/options/locale |
| `.ewo` options | Profile | Output settings and slide thumbnails |

Settings precedence and save timing are not fully established.

## 9. Plugins and external assets

Global `C:\ProgramData\Softouch\EasyWorship.v7`:

| Folder | Contents | Role |
|---|---|---|
| `Plugins\Bibles` | HCSB, KJV, NIV1984, NIVA1984, RVA, download index | Scripture translations |
| `Plugins\Extensions` | `sslsparser2.dll` | Delphi extension/parser component |
| `Plugins\Packages` | `demo.v3.ewpck` | Content package |
| `Plugins\Shapes` | shape XML + `shapes.1.db` | Shape catalog |
| `Plugins\Transitions` | Blend, Cover, Iris, Push, Reveal, Wipe `.dfx` | Transitions |
| `Plugins\Updates` | `updates.2.db` | Update state |
| `Plugins\Web` | `easyworship74.chm`, `precheck.html`, `welcome.html`, assets | Help/web content |
| `Profiles` | `.ewp` profiles | Profile definitions |
| `Settings` | INI/DB settings | Machine-level configuration |

## 10. Security, integrity, and legal constraints

1. **[C]** The main EXE is unsigned and MPRESS-packed; its PE certificate directory is out of range.
2. **[C]** The helper is validly signed by Softouch.
3. **[U]** Whether the installed unsigned main EXE is an official build or has been modified is unresolved.
4. **[P]** Do not trust the main EXE as a security boundary; validate inputs and isolate test profiles.
5. **[P]** Do not copy proprietary source code, branding, artwork, help content, Bible text, or other protected assets into the reconstruction.
6. **[P]** Keep user data out of research artifacts; use synthetic test content.
7. **[P]** Treat database and archive files as untrusted input; validate sizes, paths, and SQL parameters.

## 11. Reconstruction implications

### 11.1 Architecture constraints

- **Operator/live separation:** keep the operator UI, preview, live output, media playback, scheduling, and persistence logically separate. This matches the observed three-pane design and avoids UI stalls during output.
- **Multi-window output:** the original uses separate top-level live-output windows. A reconstruction can use separate windows or a render-target abstraction, but must preserve monitor targeting and operator independence.
- **Rendering:** a C#/.NET implementation can use WPF/WinUI with D3D interop, or a dedicated D3D renderer plus a UI layer. The original’s D3D/WebView2 mix suggests planning for both native rendering and embedded web content.
- **Media:** FFmpeg (or an equivalent) is required for broad codec support; NDI and DVD support are optional scope decisions. PowerPoint integration may require COM automation or a conversion pipeline.
- **Persistence:** SQLite is a natural fit, but data compatibility with the original requires the custom `UTF8_U_CI` collation, schema/version handling, and archive migration.
- **IPC:** reproducing the original helper protocol is not required for a standalone clone unless compatibility with the installed helper or its data channel is a goal. If the clone reads original data directly, the helper can be replaced by an internal data service.
- **UI shell:** the original uses custom Delphi controls. A reconstruction should prioritize keyboard/mouse workflows, pane persistence, and clear live/preview state over pixel-identical custom drawing.

### 11.2 Data compatibility decision

Stage 4 must choose one of:

1. **Import-only compatibility:** read original `.db`/archives once and migrate into a new schema. Lower coupling; easier to evolve.
2. **Bidirectional compatibility:** read/write original schemas so the original application can open clone data. Much higher cost; requires exact collation, revisions, archives, and output settings.
3. **No data compatibility:** implement a new format. Lowest cost; fails fidelity for users with existing libraries.

This choice should be explicit before implementation.

### 11.3 Test oracle

The installed EasyWorship 7.4.0.7 remains the behavioural oracle. Tests must compare equivalent workflows using synthetic data and isolated profiles. Do not use production media, schedules, lyrics, or personal paths.

### 11.4 Highest-risk areas

1. Rendering and transitions (D3D/WebView2 composition, text layout).
2. Media playback (codecs, NDI, DVD, PowerPoint, audio sync).
3. Full IPC command space and helper ownership.
4. Data migration and custom collation.
5. Output state machine and multi-monitor behavior.
6. Error recovery and profile locking.
7. Performance with large libraries/schedules.

## 12. Open architecture questions

- Is the helper a general media/render service, a data service, or both?
- Does `EZW_PIPE` carry reverse control/notifications or frame data?
- Which process owns NDI output and live composition?
- How do output settings databases relate to `PresentationLayouts.db`?
- How are themes applied and overridden at the element level?
- What is the `data` BLOB in `.dfx` transitions?
- How does the application migrate 7.3 archives to 7.4.1.3?
- How are thumbnails/poster frames invalidated and regenerated?
- How does the application handle monitor removal, DPI changes, and output-window closure?
- Is the unsigned MPRESS main EXE an official distribution?

These are tracked with impact and next probes in `research/known-unknowns.md`.
