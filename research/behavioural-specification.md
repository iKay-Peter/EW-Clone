# EasyWorship 7 Behavioural Specification

Status: Stage 3 draft for reconstruction planning  
Last updated: 2026-10-26  
Target: EasyWorship 7.4.0.7 on Windows  
Companion documents: `research/feature-matrix.md`, `research/architecture-findings.md`, `research/known-unknowns.md`, `research/test-cases.md`

## 1. Purpose and conventions

This document describes observable behaviour and candidate reconstruction requirements. It deliberately separates evidence from design intent.

| Tag | Meaning |
|---|---|
| **[O]** | Observed directly in Stage 3 or earlier runtime evidence. |
| **[C]** | Confirmed by a file, schema, configuration, or captured artifact. |
| **[I]** | Inferred from artifacts/control names; not exercised end-to-end. |
| **[P]** | Provisional reconstruction requirement; must be validated by a test. |
| **[U]** | Unknown; do not implement as a settled behaviour. |

**Important:** Stage 3 used an isolated profile named `Stage3`. The production profile `New Profile` and its settings were restored after capture. Paths below refer to the production layout unless explicitly marked as Stage 3 evidence.

## 2. Startup, profile selection, and shutdown

### BS-2.1 Launch

1. **[O]** Launching `EasyWorship.exe` starts a native Win32 process with a Delphi `TApplication` window and a main form.
2. **[O]** The main form class is `TFormEZWorshipMain`; its title includes the active profile name. In Stage 3 it was `EasyWorship - Stage3`.
3. **[C]** The active profile is read from `History.ini` (`default_profile`). The `.ewp` profile INI supplies `Id`, `AppInstDataDir`, `Options`, and `DesiredLocale`.
4. **[I]** If the active profile is missing or invalid, the application should prompt for or select a profile; the actual fallback is unverified.
5. **[O]** `Show_Live_On_Startup=1` is configured. Whether the live output window opens automatically at startup was not established.

### BS-2.2 First-run / Getting Started window

1. **[O]** A first launch may open `TForm_Onboarding`, “Getting Started with EasyWorship”, 890x339.
2. **[O]** The window hosts a WebView2 control (`TewWebBrowserCloud` → `Chrome_WidgetWin_1` → `RootView`).
3. **[O]** The requested URL is malformed:

   ```text
   file:///C:/ProgramData/Softouch/Easyworship.v7/Plugins/Web/welcome.html%3Fregistered=false&show=true&noprompt=false&colorTheme=dark&font=Segoe%2520UI&ver=7.4.0.7&interfaceMode=ResourcePreview
   ```

   Chromium treats `welcome.html%3F...` as a literal filename and displays `ERR_FILE_NOT_FOUND`.
4. **[C]** `welcome.html` exists at 354,328 bytes. The failure is therefore an application-side URL-construction bug, not a missing asset.
5. **[O]** Closing the onboarding window reveals the main form and its library/workspace.
6. **[P]** A reconstruction should emit a valid `welcome.html?...` URL and degrade gracefully if the help/welcome page is missing or offline.
7. **[U]** The exact first-run condition, dismissal state, and whether onboarding appears after an upgrade are not established.

### BS-2.3 Helper process and pipe handshake

1. **[O]** `EasyWorshipHelper.exe` can be pre-existing with command line `... EasyWorshipHelper.exe AUTO`; it is not necessarily started by the main process.
2. **[O]** The helper is a fully loaded 135-module native process, not a dormant WOW64 shell. Stage 2’s ntdll/WOW64-only view was a `tasklist /m` limitation.
3. **[O]** The main process creates the named-pipe server `\\.\pipe\EZW_PIPE` and opens `\\.\pipe\EZW_HELPER_PIPE` as a client.
4. **[O]** Startup sequence on `EZW_HELPER_PIPE`:

   ```text
   main → helper: version/time frame
   main → helper: 144-byte request
   helper → main: "Response 13"
   helper → main: "Result" = 1
   main → helper: 0xFFFFFFFFFFFFFFFF
   helper → main: SQLite database stream
   ```

5. **[O]** The streamed database is `ServiceIntervals.db` (schema and 5,120-byte size match), so the channel is at least a profile-scoped SQLite fetch protocol.
6. **[I]** The helper also owns or supports media/rendering work, but that is not proven by the observed request alone. The main process creates the local live-output windows.
7. **[U]** The full command set, request/response codes, reverse `EZW_PIPE` traffic, authentication, and failure behavior are unknown. See `research/known-unknowns.md`.

### BS-2.4 Shutdown

1. **[O]** Stopping the main process closes its UI and output windows.
2. **[O]** The pre-existing helper may remain running after the main process exits.
3. **[I]** Normal shutdown should flush settings, close databases, and close both pipe endpoints.
4. **[U]** Crash recovery, unclean shutdown, and stale pipe/lock handling are not established.

## 3. Main shell and navigation

### BS-3.1 Window layout

1. **[O]** The main window contains:
   - menu bar: File, Edit, Live, Profiles, View, Help;
   - toolbar: New, Open, Save, Store, Web, Remote, Go Live, Alerts, Logo, Black, Clear, Live;
   - Schedule pane (left);
   - Live pane (center);
   - Live Output pane (right);
   - library tabs: Songs, Scriptures, Media, Presentations, Themes;
   - library selector/search and list;
   - preview area with slide position text.
2. **[O]** Panes are separated by draggable splitters.
3. **[C]** Splitter percentages and window bounds are persisted in `WindowSettings.ini`.
4. **[P]** The clone should preserve the operator/preview/live separation and allow pane resizing without changing live output.
5. **[U]** The exact minimum sizes, collapse rules, and dock/undock behavior are unknown.

### BS-3.2 Library tabs and selection

1. **[O]** Selecting a tab changes the library selector and list content.
2. **[O]** The Songs tab shows a table with Title, Author, Copyright, and a count such as “433 songs”.
3. **[O]** Selecting a song updates the Live pane content and preview; a slide position such as “Slide 1 of 4” is shown.
4. **[I]** Selection is single-item in the observed workflow; multi-select behavior is unknown.
5. **[I]** The Live pane is the current prepared/on-air item, while the library list is the browsing context. The exact independence of preview and live state is unverified.
6. **[U]** Empty states, sorting, filtering, keyboard navigation, and context menus are not specified.

### BS-3.3 Toolbar and menu commands

| Control | Observed state | Reconstruction note |
|---|---|---|
| New | Present | Likely creates a schedule/presentation; exact target unknown. |
| Open | Present | Likely opens a schedule/presentation/package; dialog not exercised. |
| Save | Present | Likely saves current schedule; scope unknown. |
| Store | Present | Online/store surface; not exercised. |
| Web | Present | Web surface; not exercised. |
| Remote | Present | Remote-control configuration; not exercised. |
| Go Live | Present; invoked | Promotes the selected item to live; output not visually verified. |
| Alerts | Present | Alert surface; not exercised. |
| Logo | Present | Logo output state; effect not visually verified. |
| Black | Present | Black output state; effect not visually verified. |
| Clear | Present | Clear output state; effect not visually verified. |
| Live | Present | Likely output/live mode menu; exact behavior unknown. |

**[P]** Menu and toolbar commands should be implemented only after their exact target and state transitions are confirmed. Labels alone are not sufficient evidence of behaviour.

### BS-3.4 Keyboard shortcuts

**[C]** `HotKeys.2.ini` contains the following mappings. The `C-` prefix appears to encode a Ctrl modifier; that interpretation is **[I]** and must be verified.

| Action | Encoded keys |
|---|---|
| Slide next | `78, 39, 32` (N, Right, Space) |
| Slide previous | `80, 37, 8` (P, Left, Backspace) |
| Slide first | `36` (Home) |
| Slide last | `35` (End) |
| Go Live next | `34` (Page Down) |
| Go Live previous | `33` (Page Up) |
| Schedule next | `40` (Down) |
| Schedule previous | `38` (Up) |
| Black screen | `C-66` |
| Clear screen | `C-67` |
| Logo screen | `C-76` |
| Jump to bridge | `66` |
| Jump to chorus | `67` |
| Jump to end | `69` |
| Jump to intro | `73` |
| Jump to pre-chorus | `82` |
| Jump to slide | `83` |
| Jump to tag | `84` |
| Jump to verse | `86` |

**[C]** `Allow_Live_Navigation_From_Schedule=1` and `Allow_Schedule_Navigation_From_Selector=1`.

**[U]** Focus rules, text-entry suppression, conflicts, and international keyboard layouts are unknown.

### BS-3.5 Clipboard and drag/drop

1. **[O]** The process registers clipboard/window messages named `EasyWorship Presentation Data`, `EasyWorship Clipboard Source ID`, `HTML Format`, `Rich Text Format`, `GIF Image`, and `Delphi Picture`.
2. **[I]** Copy/paste can carry internal presentation data and interoperate with HTML/RTF/image formats.
3. **[U]** The internal payload, drag sources/targets, and paste transformations are not decoded.

### BS-3.6 Window and interface persistence

1. **[C]** `WindowSettings.ini` stores main, editor, inspector, options, and popup bounds; splitter percentages; monitor numbers; primary flags; and window state.
2. **[C]** `UserSettings.ini` stores `InterfaceMode=1`, `LivePreviewLocation=1`, `EditorInspectorVisible=0`, and related flags.
3. **[U]** The mapping from interface mode to layout/theme and the save timing are unknown.

## 4. Songs

### BS-4.1 Browse and select

1. **[O]** The Songs tab lists song metadata and a count.
2. **[O]** Selecting a row populates the Live pane and preview.
3. **[C]** Song metadata is stored in `Songs.db.song`; lyrics are stored as RTF in `SongWords.db.word`.
4. **[I]** The song’s slides are derived from the linked presentation and `slide_uids`/revision fields.
5. **[U]** The exact slide-splitting rules (verse/chorus boundaries, line wrapping, manual breaks) are unknown.

### BS-4.2 Section labels

1. **[C]** `UserSettings.ini` defines section labels and colors: BRIDGE, CHORUS, END, ENDING, INTERLUDE, INTRO, INTRODUCTION, MISC, PRE-CHORUS, SLIDE, TAG, VAMP, VERSE, EMPTY, OTHER, SCRIPTURE.
2. **[O]** The live pane displayed numbered verse blocks for the selected song.
3. **[I]** Labels are detected/edited in the song editor and can be used for jump-to-section shortcuts.
4. **[U]** Label parsing, case sensitivity, localization, and rendering are unknown.

### BS-4.3 Live control

1. **[O]** Go Live was invoked with a selected song; the Live pane showed the song and slides.
2. **[I]** Slide next/previous and section jumps operate on the live song.
3. **[U]** Whether the preview and live slide positions can diverge, and how “Go Live” handles a multi-slide selection, is unknown.

### BS-4.4 Create, edit, delete, import

1. **[O]** A “New Song...” control exists.
2. **[U]** The editor dialog, validation, duplicate handling, delete/undo, import/export, and provider synchronization were not exercised.
3. **[C]** Song search indexes and usage history tables exist, so search and history are real product concerns even though their UI was not exercised.

## 5. Scriptures

### BS-5.1 Selection and lookup

1. **[O]** The Scriptures tab exists and is enabled.
2. **[C]** `.ewb` Bibles are SQLite databases with `header`, `books`, `streams`, and `words`.
3. **[C]** Six translations are installed; `BibleSettings.ini` stores `Last_Bible=28`.
4. **[I]** The operator selects a translation, enters a reference or searches, previews the passage, and sends it live or into the schedule.
5. **[U]** The exact reference syntax, book/verse numbering, range handling, and search ranking are unknown.

### BS-5.2 Display and insertion

1. **[C]** `biblewraps.2.db` caches word-wrapped text by translation, translation version, font, and size.
2. **[I]** Scripture text is converted into text resources/slides using the current theme.
3. **[U]** Whether insertion creates a new slide, replaces the live background, or reuses a template is unknown.
4. **[U]** Red-letter, footnotes, cross-references, and apocrypha behavior are unknown.

### BS-5.3 Downloads and licensing

1. **[C]** `bible_downloads.1.db` records translation id, abbreviation, and error number.
2. **[C]** `Bibles\Locks` and license directories were empty in the inspected environment.
3. **[U]** Online download, entitlement, activation, and error-recovery flows are unknown.

## 6. Media

### BS-6.1 Browse and import

1. **[O]** The Media tab exists.
2. **[C]** `Media.db.media` stores path, filename, hash, size, dimensions, duration, trim points, poster position, mute/volume, repeat, alpha, and stream metadata.
3. **[I]** Import creates a media row, hashes the file, extracts metadata/poster frame, and generates a thumbnail.
4. **[U]** Supported formats, duplicate policy, relinking, and folder watching are unknown.

### BS-6.2 Preview and live playback

1. **[I]** Selecting media previews it; Go Live sends it to the live output.
2. **[C]** Playback fields include start/end, poster frame, mute, volume, repeating, and alpha mode.
3. **[C]** FFmpeg 4, DirectSound, AVRT, NDI, and DScaler5 are installed.
4. **[U]** Playback state transitions (loading, playing, paused, ended, error), seeking behavior, and audio/video sync are unknown.

### BS-6.3 Special media

| Media kind | Evidence | Status |
|---|---|---|
| Image | `resource_image`, image resources | Confirmed data model; UI workflow unverified. |
| Video | `resource_video`, FFmpeg/D3D | Confirmed dependencies; playback unverified. |
| Audio | `resource_audio`, DirectSound/AVRT | Confirmed dependencies; playback unverified. |
| DVD | DVD tables, DScaler5 | Confirmed data model; disc workflow unverified. |
| NDI/live feed | feed tables, NDI 4.6.3 | Confirmed dependency; discovery/latency unverified. |
| PowerPoint | `resource_powerpoint`, `Presentations.db` | Confirmed data model; import/playback unverified. |
| Web | `resource_web`, WebView2 | Confirmed data model; URL policy unverified. |

### BS-6.4 Error handling

1. **[C]** Media rows carry `ready`, `error_no`, and `error_msg`.
2. **[I]** Missing or unsupported files should be marked unavailable and surfaced to the operator.
3. **[U]** Retry, relink, and correction workflows are unknown.

## 7. Presentations

### BS-7.1 Library and import

1. **[O]** The Presentations tab exists.
2. **[C]** `Presentations.db.file_item` indexes external presentation files; `resource_powerpoint` represents slide resources.
3. **[I]** Importing a PowerPoint file creates a presentation record, thumbnail, and slide resources.
4. **[U]** Supported PowerPoint versions, conversion fidelity, and round-trip export are unknown.

### BS-7.2 Editing and live use

1. **[C]** `PresentationLayouts` stores presentation, slide, element, resource, file, property, and token graphs.
2. **[C]** The editor window and media/scripture/theme popups have persisted geometry.
3. **[I]** The editor changes slide order, element geometry/style, text, background, and theme assignments.
4. **[U]** The complete editor command set, undo/redo, and multi-select behavior are unknown.

## 8. Themes and rendering

### BS-8.1 Theme application

1. **[O]** The Themes tab exists.
2. **[C]** Themes are stored as `.ewtx` ZIP+SQLite files and indexed in `Themes.db`.
3. **[C]** Slides carry `theme_id`, `theme_slide_uid`, `auto_theme`, and modified-theme revision fields.
4. **[I]** Applying a theme updates the presentation/slide theme references; per-slide overrides can override the theme.
5. **[U]** Theme inheritance, reset, and “modified theme” semantics are unknown.

### BS-8.2 Composition model

1. **[C]** A presentation contains ordered slides; a slide contains ordered elements; each element has geometry, type/style, and background/foreground/shape resources.
2. **[C]** Resource subtypes include image, video, audio, text (RTF), web, gradient, color, shape, DVD, and live feed.
3. **[I]** Rendering order is background → foreground → shape/text overlays, respecting `order_index` and element type.
4. **[U]** Blend modes, opacity, masks, rotation, and coordinate units are unknown.

### BS-8.3 Text and resources

1. **[C]** Text is stored as RTF; fonts/sizes are also cached for Bible wrapping.
2. **[C]** Gradients have angle, transparency, color stops, and ramp type.
3. **[C]** Shapes use `shape_data` plus plugin catalogs.
4. **[U]** Font fallback, text autofit, alignment, and color management are unknown.

### BS-8.4 Transitions

1. **[C]** Six `.dfx` files exist: Blend, Cover, Iris, Push, Reveal, Wipe.
2. **[C]** `.dfx` is SQLite with `info`, `parameter`, and opaque `data`.
3. **[C]** `Wipe.dfx` exposes `Direction` (0–7) and `BorderSoftness` (0–100, default 5); `Blend.dfx` has no parameters.
4. **[U]** The `data` BLOB, timing, easing, and renderer implementation are unknown.

### BS-8.5 Output composition and capture

1. **[O]** Local live output is created in the main process: `TForm_LiveOutput`, `TFrame_PreviewOuput` (shipped spelling), `TsdPresentationWin`, `TsdWebBrowser`, and an `Intermediate D3D Window`.
2. **[O]** The output window was 1920x1080; the D3D intermediate window was 2304x1536.
3. **[O]** `PrintWindow` and `CopyFromScreen` returned black. This is not evidence that the rendered output was black.
4. **[I]** D3D/WebView composition requires a D3D-aware capture or an output mirror for verification.
5. **[U]** The exact layer order, color space, and scaling rules are unknown.

## 9. Schedule

### BS-9.1 Schedule model

1. **[O]** A Schedule pane is present.
2. **[C]** `.ewsx` is a ZIP with `main.db` and optional `media/`; `main.db` uses the presentation/slide/element/resource model.
3. **[I]** A schedule is an ordered list of items referencing presentations, songs, scriptures, media, and other resources.
4. **[U]** The exact item types and their on-disk representation are not fully mapped.

### BS-9.2 Authoring

1. **[O]** The Stage 3 profile had no schedule resources copied, so the pane was empty; this is not an empty-state design finding.
2. **[U]** New/open/save, add/remove, reorder, duplicate, and drag/drop flows were not exercised.
3. **[C]** `ScheduleItemAutoExpandDrag=1` suggests drag behavior; its effect is inferred.
4. **[P]** The clone should preserve schedule order and item state across save/reopen.

### BS-9.3 Live schedule operation

1. **[C]** Hotkeys provide schedule next/previous and Go Live next/previous.
2. **[C]** `Advance_Schedule_Automatically=1` and `ContiguousScheduleEnabled=1` are configured.
3. **[I]** Advancing the live slide may advance the schedule selection; the exact coupling is unverified.
4. **[U]** Auto-advance timing, loop/stop rules, and manual override are unknown.

### BS-9.4 Schedule checks and service intervals

1. **[C]** `ScheduleCheckLyricsOnly=0` and `ScheduleCheckPopup=0`; validation behavior is unexercised.
2. **[C]** `ServiceIntervals.db` stores interval start/end and fixed flags.
3. **[O]** The helper streamed `ServiceIntervals.db` at startup.
4. **[U]** The UI meaning (service planning, countdown, auto-advance window) and IPC command mapping are unknown.

## 10. Live output and output surfaces

### BS-10.1 Provisional output state model

Only control presence and parts of the UI were observed. The following is a **candidate** state model for reconstruction and testing, not a confirmed state machine.

| State | Entry (candidate) | Observable expectation | Status |
|---|---|---|---|
| Idle | No live item | Output blank/hidden or shows standby | [U] |
| Preview | Select library item | Preview pane updates; output unchanged | [I] |
| Live | Go Live | Current slide/media appears on live output | [O/I] |
| Logo | Logo control | Logo/standby image replaces live content | [O/I] |
| Black | Black control | Output becomes black | [O/I] |
| Clear | Clear control | Output cleared/transparent | [U] |
| Alert | Alert control/event | Alert appears over or instead of content | [I] |
| Media | Media Go Live | Media plays on output | [I] |
| Transition | Slide/media change | Animated transition between states | [I] |

**[P]** State priority (e.g. Black vs Alert vs Logo), restore behavior, and whether Clear and Black differ must be tested before implementation.

### BS-10.2 Output windows and routing

1. **[O]** Live output creates top-level `TForm_LiveOutput` windows plus `TForm_LayeredWindow` siblings.
2. **[C]** Output settings DBs exist for Live, Alt, Foldback, LiveMessage, AltOutMessage, AltOutNursery, LiveNursery, and FoldbackMessage.
3. **[C]** Each output has monitor number, device ID, bounds, and margins in `ComputerSettings.ini`.
4. **[O]** The observed live output window was 1920x1080 while the configured live monitor entry described 1024x768. The mapping is unresolved.
5. **[U]** Which surfaces are independent, mirrored, or overlays is unknown.
6. **[U]** Alt/Foldback/Nursery/Message semantics, stage-display behavior, and operator preview are not established.

### BS-10.3 Multi-monitor and display changes

1. **[C]** Monitor selection uses device IDs and monitor numbers.
2. **[P]** The clone should tolerate monitor removal, resolution changes, DPI changes, and output-window closure without losing the operator session.
3. **[U]** Actual fallback behavior in EasyWorship is unknown.

### BS-10.4 NDI, alpha, and special outputs

1. **[C]** NDI 4.6.3 runtime/plugins are installed; NewTek flags exist in settings.
2. **[C]** Alpha monitor/device fields and media alpha modes exist.
3. **[U]** NDI input/output discovery, alpha keying, and streaming formats are unverified.
4. **[U]** Whether the helper or main process owns NDI output is unresolved.

## 11. Media playback behaviour

1. **[C]** FFmpeg 4 handles decode/convert; DirectSound/AVRT handle audio; D3D9/10/11 handle rendering.
2. **[I]** A media item has a lifecycle: loading → ready → playing → paused/ended → error.
3. **[C]** Start/end positions, poster frame, repeat, mute, volume, and alpha are stored per item.
4. **[I]** Seeking and trim points affect playback start/end without modifying the source file.
5. **[U]** Exact state transitions, stop vs pause behavior, gapless playback, and audio/video sync are unknown.
6. **[U]** Hardware acceleration and multi-stream behavior are unknown.

## 12. Bible behaviour

1. **[C]** Translation list is backed by `.ewb` SQLite files; `Last_Bible` persists the last selection.
2. **[I]** The operator chooses a translation, enters a reference or search term, previews results, and sends a passage live or to the schedule.
3. **[C]** Word wrapping is cached by translation/version/font/size.
4. **[U]** Reference parsing, search ranking, red-letter handling, and insertion templates are unknown.
5. **[C]** Download metadata exists; entitlement and online flows are unknown.

## 13. Settings and persistence

### BS-13.1 Configuration layers

| Layer | Examples | Evidence |
|---|---|---|
| Machine/global | `ComputerSettings.ini`, `HotKeys.2.ini`, MIDI/remote settings | [C] |
| User/roaming | `UserSettings.ini`, `WindowSettings.ini`, `History.ini`, Bible settings | [C] |
| Profile | `.ewp` with data directory and options set | [C] |
| Options set | `.ewo/Data/*.db`, slide thumbnails | [C] |
| Profile data | `v6.1/Databases/Data/*.db`, resources, archives | [C] |
| Caches | media/slide thumbnails, poster frames, Bible wraps | [C] |

1. **[U]** Precedence when machine, user, and profile values conflict is not established.
2. **[I]** Settings are likely written on change and/or shutdown; exact timing is unverified.
3. **[P]** The clone must not use production profile data during tests.

### BS-13.2 Profiles and archives

1. **[C]** Profile switching changes `AppInstDataDir` and therefore the visible library/schedules.
2. **[C]** Profile and options `Archive` snapshots exist.
3. **[C]** `.ewsx`/`.ewtx` archives may contain 7.3-era schemas; the active data is 7.4.1.3.
4. **[I]** Opening an older archive triggers an upward migration.
5. **[U]** Migration failure, downgrade, and conflict handling are unknown.

### BS-13.3 Packages and updates

1. **[C]** `.ewpck` is a ZIP with `Data/*.db` and `Resources/*`; `Packages.db` tracks status.
2. **[C]** `updates.2.db` and web/update assets exist.
3. **[U]** Install/uninstall, signature checks, and rollback behavior are unknown.

### BS-13.4 Locking and error recovery

1. **[C]** `v6.1/Databases/Locks/Protocol.elck` is a 1-byte file.
2. **[U]** Whether it prevents concurrent profile use or marks a protocol version is unresolved.
3. **[U]** Crash recovery, corrupt database handling, and interrupted import recovery are unknown.

## 14. Helper IPC behaviour

### BS-14.1 Framing

Observed on `\\.\pipe\EZW_HELPER_PIPE`:

```text
uint32 payload_length
uint32 reserved (zero in observed messages)
payload[payload_length]
```

- The first observed message has payload length 42: a 34-byte UTF-16LE timestamp string plus 8 bytes of zero/reserved data.
- The second message has payload length 144: the request.
- Response `"Response 13"` has payload length 22.
- The `"Result"` response has payload length 28.
- The database stream begins with `SQLite format 3` and continues in 1,024-byte reads.

### BS-14.2 Request fields

The 144-byte request contains length-prefixed UTF-16LE names:

| Field | Observed value | Encoding note |
|---|---|---|
| `RequestType` | 1 | 12-byte value block: `04 00 00 00 00 00 00 00 01 00 00 00` |
| `ProfileName` | `Stage3` | 12-byte string block, length-prefixed |
| `RecType` | 0 | int32 value block |
| `RowID` | 0 | int64 value block |

The exact type-tag semantics and whether the response is always a SQLite database are unknown. The observed request produced `ServiceIntervals.db`.

### BS-14.3 Reverse channel and ownership

1. **[O]** The main process is the server for `EZW_PIPE`; no traffic was captured on it.
2. **[O]** The main process creates local live-output windows; the helper is not required for those windows to exist.
3. **[U]** Whether `EZW_PIPE` carries notifications, frame data, or control messages is unknown.
4. **[U]** Pipe authentication, reconnect, and multi-client behavior are unknown.

## 15. Edge cases and failure behaviour

| Case | Expected/required behaviour | Evidence | Status |
|---|---|---|---|
| Malformed onboarding URL | Show a usable fallback, not a raw Chromium error page | [O] bug observed | Must fix in clone |
| Missing `welcome.html` | Degrade gracefully | [I] | Test |
| Missing active profile | Prompt/select a valid profile | [I] | Test |
| Corrupt SQLite DB | Report and isolate the failure; do not crash all libraries | [I] | Test |
| Missing media file | Mark unavailable; allow relink | [I] | Test |
| Unsupported codec | Show a media error; keep UI responsive | [I] | Test |
| Output monitor absent | Fall back to a valid monitor or show a clear error | [I] | Test |
| Helper absent or crashed | Main UI remains usable; retry/reconnect or disable helper features | [I] | Test |
| Pipe disconnect | Do not hang the UI; surface an error | [I] | Test |
| Version mismatch (7.3 archive) | Migrate or reject with a clear message | [I] | Test |
| Duplicate import | Deduplicate by hash or ask the operator | [I] | Test |
| Low disk | Abort import/save cleanly; no partial DB corruption | [I] | Test |
| Concurrent profile use | Respect lock or warn | [C] lock file, [U] behavior | Test |
| DPI/monitor change | Keep output/operator windows usable | [I] | Test |
| Unicode/long paths | Preserve paths and text | [I] | Test |
| Permission denied | Report the path and required access | [I] | Test |

## 16. Deferred specification work

The following must be specified before or during Stage 4/5:

1. Exact output state machine and priority rules.
2. Slide-splitting and section-label algorithms.
3. Rendering math, text layout, and transition implementation.
4. Full IPC command/response schema.
5. Data migration and archive conflict rules.
6. Media/NDI/PowerPoint/DVD playback lifecycles.
7. Bible lookup/search/insertion workflows.
8. Auto-advance and schedule-check semantics.
9. Error recovery and profile-lock behavior.
10. Accessibility, localization, and performance targets.

Until these are resolved, the reconstruction should treat the corresponding behaviours as provisional and keep them behind tests.
