# EasyWorship 7 Feature Matrix

Status: Stage 3 evidence-backed inventory  
Last updated: 2026-10-26  
Scope: EasyWorship 7.4.0.7 on Windows, using read-only inspection and an isolated Stage 3 profile.

This matrix records what was directly observed, what is confirmed by artifacts, and what is only inferred. It is not a promise that every listed feature is fully specified. Unknown semantics are tracked in `research/known-unknowns.md` and are not invented here.

## Confidence key

| Code | Meaning |
|---|---|
| **C** | Confirmed by a file, schema, database, binary, or configuration artifact. |
| **O** | Observed in a runtime session, UI Automation tree, screenshot, or captured process data. |
| **I** | Inferred from the schema, configuration, control names, or architecture; not exercised end-to-end. |
| **U** | Unknown or not evidenced; listed only because the product surface implies it. |

## Evidence references

- `INV` — `research/easyworship-inventory.md`
- `SCHEMA` — `research/easyworship-schema.txt`
- `ARCH` — `research/easyworship-archives.txt`
- `STG3/UI` — `research/stage3/ui-main.png`, `ui-after-clicks.png`, `ui-tree.json`
- `STG3/ONB` — `research/stage3/onboarding.png`, `onboarding-tree.json`
- `STG3/LIVE` — `research/stage3/live-output-tree.json`, `live-output*.png`
- `STG3/IPC` — `research/stage3/ezw-spawn.log`
- `STG3/HELPER` — `research/stage3/helper-imports.json`, `helper-runtime-probe.js`
- `CFG` — EasyWorship INI/DB settings files described in `INV` section 7

## Coverage summary

- **Strong evidence:** application shell, libraries/tabs, presentation graph, schedule/theme archives, output-setting databases, media types, settings files, helper process, named-pipe startup handshake.
- **Partial evidence:** live output behavior, transitions, Bible workflows, media playback, theme application, auto-advance, remote/MIDI, online flows.
- **Weak or absent evidence:** exact UI menu commands, most dialogs, song-slide splitting rules, rendering math, error recovery, licensing, accessibility, localization, performance.

---

## A. Startup, shell, and navigation

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| A-01 | First-run onboarding | Opens `TForm_Onboarding` (“Getting Started with EasyWorship”), 890x339, WebView2 surface. | STG3/ONB | O | Exact first-run trigger and dismissal state. |
| A-02 | Onboarding page load | Requests `welcome.html%3F...`; Chromium shows `ERR_FILE_NOT_FOUND` although `welcome.html` exists (354,328 bytes). | INV §12.3; STG3/ONB | O | Whether this is reproducible on a clean official install. |
| A-03 | Main window reveal | Closing onboarding exposes `TFormEZWorshipMain`; title includes profile name; 121 UIA elements at 810x608. | INV §12.4; STG3/UI | O | Whether any initialization is blocked by the onboarding failure. |
| A-04 | Menu bar | Visible labels: File, Edit, Live, Profiles, View, Help. | STG3/UI | O | Every menu command, accelerator, and enabled/disabled state. |
| A-05 | Main toolbar | Visible controls: New, Open, Save, Store, Web, Remote, Go Live, Alerts, Logo, Black, Clear, Live. | STG3/UI | O | Exact action of Store/Web/Remote/Alerts and dropdown variants. |
| A-06 | Workspace panes | Schedule, Live, and Live Output panes; movable splitters; sizes persisted. | STG3/UI; CFG | O/I | Minimum sizes, collapse behavior, multi-monitor placement. |
| A-07 | Library tabs | Songs, Scriptures, Media, Presentations, Themes. | STG3/UI | O | Tab-specific toolbars, context menus, and empty states. |
| A-08 | Library selector | Song list displays Title, Author, Copyright columns; “433 songs” count visible. | STG3/UI | O | Sort/filter semantics, multi-select, paging. |
| A-09 | Search | Search field and indexed `word_list`/`word_key` tables exist. | STG3/UI; SCHEMA | O/C | Tokenization, ranking, field flags, fuzzy matching. |
| A-10 | Keyboard shortcuts | `HotKeys.2.ini` defines slide next/prev, Go Live next/prev, black/clear/logo, jump-to-section, schedule navigation. | CFG | C | Whether all mappings are active and how conflicts resolve. |
| A-11 | Window persistence | Main/editor/inspector/options positions and splitter percentages stored in `WindowSettings.ini`. | CFG | C | Restore behavior when a monitor is removed or DPI changes. |
| A-12 | Interface mode | `InterfaceMode=1`; onboarding URL carries `interfaceMode=ResourcePreview`. | CFG; STG3/ONB | C/O | Full meaning of each interface mode and theme. |
| A-13 | Remote control | Remote toolbar button, `RemoteControlSettings.ini`, paired-client DB, remote UI entry point. | STG3/UI; CFG | O/C | Protocol, pairing flow, permissions, network transport. |
| A-14 | Alerts | Alerts toolbar button; `Alerts.db` contains alert/token tables. | STG3/UI; INV §6.5 | O/C | Alert creation, display, expiry, and output routing. |
| A-15 | Store and Web | Store and Web buttons present; WebView2 and help assets installed. | STG3/UI; INV §4 | O | Whether these open embedded pages, external browser, or online services. |
| A-16 | Clipboard and drag/drop | Registers `EasyWorship Presentation Data`, `EasyWorship Clipboard Source ID`, HTML, RTF, GIF, Delphi Picture formats. | STG3/IPC | O | Exact payloads, drag/drop targets, copy/paste semantics. |
| A-17 | Multi-user metadata | `Users.db` has user UID/name, last login, sequence, run count. | SCHEMA | C | Whether the UI exposes users, and how identity is selected. |
| A-18 | Accessibility | Custom `Tsd*` controls expose limited UIA; `InvokePattern` unsupported on `TsdSpeedButton`. | INV §12.4 | O | Keyboard/screen-reader coverage and focus order. |

---

## B. Songs and song libraries

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| B-01 | Song library | Song metadata is stored in `Songs.db` with 16 columns, including title, author, copyright, tags, provider, and `presentation_id`. | SCHEMA | C | Field validation, duplicate handling, provider behavior. |
| B-02 | Lyrics storage | `SongWords.db.word.words` stores RTF; `slide_uids`, `slide_layout_revisions`, `slide_revisions` link lyrics to slides. | SCHEMA | C | Exact RTF subset and slide-splitting algorithm. |
| B-03 | Song selection | Selecting a song shows it in the Live pane and preview; observed “Slide 1 of 4” for a four-slide song. | STG3/UI | O | Selection-to-preview timing, multi-select, keyboard navigation. |
| B-04 | Go Live from song | Go Live was invoked; the Live pane showed the selected song and slide list. | STG3/UI | O | Exact output transition and whether preview/live are independent. |
| B-05 | Song sections | `UserSettings.ini` defines labels: BRIDGE, CHORUS, END, ENDING, INTERLUDE, INTRO, INTRODUCTION, MISC, PRE-CHORUS, SLIDE, TAG, VAMP, VERSE, EMPTY, OTHER, SCRIPTURE, with colors. | CFG | C | How labels are detected, edited, and rendered. |
| B-06 | Song search | `SongKeys.db` uses `word_list` + `word_key` index tables. | SCHEMA | C | Which fields are indexed (`field_flag`), ranking, phrase search. |
| B-07 | Song usage history | `SongHistory.db.action` records song id, date, action type, transmit date. | SCHEMA | C | Action type meanings, retention, reporting. |
| B-08 | Song-to-presentation link | `Songs.song.presentation_id` points to `PresentationLayouts.presentation.rowid` across databases. | SCHEMA | C | How the link is repaired after copy/import. |
| B-09 | New song | “New Song...” control exists in the library area. | STG3/UI | O | Dialog fields, validation, save behavior. |
| B-10 | Song edit/delete/import | No complete workflow was exercised. | — | U | Editor, delete/undo, import/export, provider sync. |

---

## C. Scriptures and Bible integration

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| C-01 | Bible plugin format | `.ewb` is SQLite with `header`, `books`, `streams`, `words`; plugin format 1. | INV §4.3; SCHEMA | C | Text encoding, verse numbering, book/verse info blobs. |
| C-02 | Installed translations | Six `.ewb` files installed: HCSB, KJV, NIV1984, NIVA1984, RVA, plus download index. | INV §4.2 | C | Which are licensed/usable and how IDs map. |
| C-03 | Scriptures tab | Scriptures tab is visible and enabled (`Scriptures_Enabled=1`). | STG3/UI; CFG | O/C | Tab layout, translation selector, reference input. |
| C-04 | Last Bible | `BibleSettings.ini` stores `Last_Bible=28`. | CFG | C | How this ID maps to a translation and whether it is stable. |
| C-05 | Passage lookup | Bible tables support books/verses; exact lookup workflow was not exercised. | SCHEMA | I | Reference parsing, ranges, red-letter handling. |
| C-06 | Search | `words` table and external search index exist; UI search was not exercised for scripture. | SCHEMA | I | Search scope, stemming, result ranking. |
| C-07 | Insert into live/schedule | Scripture resources can be represented as `resource_text`; insertion workflow not observed. | SCHEMA | I | Whether verses become text resources, slides, or templates. |
| C-08 | Word wrap | `biblewraps.2.db` caches word-wrap lists by translation/font/size. | INV §6.6; SCHEMA | C | Cache invalidation and layout rules. |
| C-09 | Downloads/licensing | `bible_downloads.1.db` records translation id, abbreviation, error number; license directories were empty. | INV §4.2–4.3 | C | Online download, entitlement, and error-recovery flow. |

---

## D. Media library and playback

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| D-01 | Media library | `Media.db.media` has 46 columns covering type, path, hash, size, dimensions, duration, trim, poster, mute/volume, repeat, alpha, and stream metadata. | SCHEMA | C | UI columns, filters, and import rules. |
| D-02 | Media types | Subtypes cover image, video, audio, DVD, live feed, effects, and web resources. | SCHEMA | C | Enum values and which are exposed in the UI. |
| D-03 | Images | `resource_image` and media image paths; image resources appear in the presentation graph. | SCHEMA; INV §6 | C | Decoding, scaling, transparency, color management. |
| D-04 | Video | `resource_video` tracks start/end/poster, duration, repeat, alpha, stream counts/types. | SCHEMA | C | Decoder selection, seeking, frame timing, alpha behavior. |
| D-05 | Audio | `resource_audio` and DirectSound/AVRT dependencies. | SCHEMA; INV §3 | C | Output device selection, sync, fades, background audio. |
| D-06 | DVD | DVD tables plus DScaler5 MPEG/DVD filters. | SCHEMA; INV §3 | C | Disc detection, chapter/clip workflow, menu handling. |
| D-07 | Live feed / NDI | `media_feed`/`resource_feed` plus NDI 4.6.3 libraries. | SCHEMA; INV §3 | C | Device discovery, formats, latency, NewTek enablement. |
| D-08 | PowerPoint | `resource_powerpoint`, `Presentations.db`, and PowerPoint thumbnail settings. | SCHEMA; CFG | C | Full-coverage vs thumbnail mode, COM automation, editing. |
| D-09 | Web resource | `resource_web` stores URL and scroll-bar flag. | SCHEMA | C | WebView2 lifecycle, navigation, security. |
| D-10 | Playback controls | Mute, volume, repeat, start/end, poster frame, and alpha fields exist. | SCHEMA | C | Exact UI controls and state transitions. |
| D-11 | Thumbnails/posters | `media.thumb.2.db`, `Posterframes/*.jpg`, and dimension/duration metadata. | INV §6.6 | C | Generation timing, invalidation, cache eviction. |
| D-12 | Media search | `MediaKeys.db` word indexes. | SCHEMA | C | Indexed fields and ranking. |
| D-13 | Error state | `media` has `ready`, `error_no`, `error_msg`; presentation has related error fields. | SCHEMA | C | How errors are surfaced, retried, or repaired. |
| D-14 | Effects | `effect`/`effect_property` tables; effect UID and key/value settings. | SCHEMA | C | Available effects and rendering semantics. |

---

## E. Presentations

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| E-01 | Presentation library | Presentations tab visible; `Presentations.db.file_item` indexes external files. | STG3/UI; SCHEMA | O/C | List columns, import targets, supported formats. |
| E-02 | Presentation metadata | `PresentationLayouts.presentation` has 28 columns: UIDs, type, aspect ratio, title/author, tags, provider, revisions, thumbnail. | SCHEMA | C | Enum meanings and mandatory fields. |
| E-03 | External PowerPoint | `resource_powerpoint` and `file_item` support `.pptx`; one PPTX was present in the active profile. | SCHEMA; INV §5.3 | C | Import conversion, fidelity, round-trip export. |
| E-04 | Slide and element graph | Presentation → slide → element → resource/file; geometry and z-order in `element`. | SCHEMA | C | Coordinate space, units, inheritance, grouping. |
| E-05 | Presentation editor | Editor window size is persisted; editor popups for media/scripture/theme exist. | CFG | C | Full editing workflow and commands. |
| E-06 | Thumbnails | Presentation/slide thumbnail fields and `slide.thumb.5.db`. | SCHEMA; INV §6.6 | C | Rendering source, revisioning, cache behavior. |
| E-07 | Presentation search | `PresentationKeys.db` word indexes. | SCHEMA | C | Indexed fields and search behavior. |
| E-08 | Import/export | `.ewsx`/`.ewtx`/`.ewpck` archives and PowerPoint resources exist. | INV §6 | C | UI import/export steps, validation, overwrite rules. |
| E-09 | Presentation clipboard | Registers an EasyWorship-specific presentation clipboard format. | STG3/IPC | O | Serialization format and paste behavior. |

---

## F. Themes, styles, and rendering

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| F-01 | Theme library | Themes tab visible; `Themes.db.file_item` indexes `.ewtx` files. | STG3/UI; SCHEMA | O/C | List behavior, preview, apply target. |
| F-02 | Theme archive | `.ewtx` is ZIP + `main.db`, using the presentation/slide/element model with a 7.3-era schema. | INV §6.3; ARCH | C | Migration and theme-specific properties. |
| F-03 | Theme application | Presentation/slide rows carry `theme_id`, `theme_slide_uid`, `auto_theme`, and modified-theme fields. | SCHEMA | C | Inheritance, override, reset, and “modified theme” semantics. |
| F-04 | Slide overrides | `slide_property`, `element_property`, and group tables store key/value overrides. | SCHEMA | C | Key names, value types, and precedence. |
| F-05 | Element layers | Background, foreground, and shape resource IDs plus `order_index`. | SCHEMA | C | Compositing order, blending, masks. |
| F-06 | Text | `resource_text.rtf`; text geometry and style properties. | SCHEMA | C | RTF subset, font fallback, autofit, alignment. |
| F-07 | Colors and gradients | `resource_color` and `resource_gradient`/`resource_gradient_color`. | SCHEMA | C | Color space, transparency, ramp behavior. |
| F-08 | Shapes | `resource_shape.shape_data` plus shape plugin XML/DB. | SCHEMA; INV §4.2 | C | Shape data format, editing, rendering. |
| F-09 | Geometry | `element.x/y/width/height` are doubles; aspect ratio is stored on presentation. | SCHEMA | C | Units, origin, rotation (if any), DPI handling. |
| F-10 | Transitions | Six `.dfx` files; `.dfx` is SQLite with `info`, `parameter`, opaque `data` BLOB. | INV §6.4 | C | Shader/algorithm in `data`, parameter semantics, timing. |
| F-11 | Preview/live composition | Live output uses `TsdPresentationWin`, `TsdWebBrowser`, and an `Intermediate D3D Window`. | STG3/LIVE | O | How D3D/WebView layers are composed and captured. |
| F-12 | Output resolution | Live output window observed at 1920x1080; `ComputerSettings.ini` describes a 1024x768 monitor 2. | STG3/LIVE; CFG | O/C | Why the configured size differs; monitor mapping rules. |
| F-13 | Alpha output | `Alpha_Monitor_Number`, `Alpha_Primary`, `Alpha_DeviceID`, and media alpha fields exist. | CFG; SCHEMA | C | What alpha output renders and how it is keyed. |
| F-14 | Capture limitation | `PrintWindow` and `CopyFromScreen` produced black; not proof that output was black. | INV §12.6 | O | D3D-aware capture and actual pixel verification. |

---

## G. Schedule, live operation, and outputs

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| G-01 | Schedule pane | Schedule pane is present; Stage 3 profile had no schedule resources copied, so it appeared empty. | STG3/UI | O | Empty-state text and drag targets. |
| G-02 | Schedule format | `.ewsx` is ZIP + `main.db` + optional `media/`; 31-table presentation snapshot. | INV §6.3; ARCH | C | Version migration, media deduplication, portability. |
| G-03 | Schedule items | Presentation/slide/element graph is reused for schedule content. | SCHEMA | C | Item types (song, scripture, media, presentation) and metadata. |
| G-04 | Open/save schedule | No complete open/save workflow was exercised. | — | U | Dialog behavior, autosave, overwrite, recovery. |
| G-05 | Reorder/drag | `ScheduleItemAutoExpandDrag=1`; drag behavior not exercised. | CFG | I | Drop rules, multi-item drag, undo. |
| G-06 | Live navigation | Hotkeys define next/prev slide, Go Live next/prev, and schedule next/prev. | CFG | C | Whether schedule and live navigation are independent. |
| G-07 | Go Live | Go Live control observed; selected song appeared in Live pane. | STG3/UI | O | Exact output state and transition behavior. |
| G-08 | Logo / Black / Clear | Controls present; exact output effects were not visually verified. | STG3/UI | O | State priority, restore behavior, transition interaction. |
| G-09 | Live Output | Opens local `TForm_LiveOutput` windows in the main process. | STG3/LIVE | O | Borderless/fullscreen behavior, focus, closure. |
| G-10 | Auto-advance | `Advance_Schedule_Automatically=1`; behavior not exercised. | CFG | I | Trigger timing, loop/stop rules, manual override. |
| G-11 | Contiguous schedule | `ContiguousScheduleEnabled=1`; behavior not exercised. | CFG | I | What “contiguous” changes visually and functionally. |
| G-12 | Schedule checks | `ScheduleCheckLyricsOnly=0`, `ScheduleCheckPopup=0`; behavior not exercised. | CFG | I | Validation categories, warning UI, bypass. |
| G-13 | Service intervals | `ServiceIntervals.db` stores start/end and fixed flags; helper streamed it at startup. | INV §6.5, §12.5 | C/O | UI semantics, IPC command mapping, timer behavior. |
| G-14 | Timers/tokens | `token` table stores name, type, format, interval, time value, overrun flag, value. | SCHEMA | C | Timer UI, countdown/clock behavior, output rendering. |
| G-15 | Alerts | Alerts button and `Alerts.db` tables; display behavior not exercised. | STG3/UI; INV §6.5 | O/C | Alert lifecycle and output routing. |
| G-16 | Output sets | `Live`, `Alt`, `Foldback`, `LiveMessage`, `AltOutMessage`, `AltOutNursery`, `LiveNursery`, `FoldbackMessage` settings DBs. | INV §6.5 | C | Which surfaces are user-selectable and how they differ. |
| G-17 | Multi-monitor | Per-output monitor number, device ID, bounds, and margins are persisted. | CFG | C | Hot-plug, DPI, EDID, and fallback behavior. |
| G-18 | NDI/NewTek | NDI 4.6.3 installed; NewTek flags exist with inconsistent values across settings. | INV §3–4; CFG | C | Actual NDI input/output enablement and formats. |
| G-19 | Foldback/stage output | Foldback settings exist and are disabled in the observed configuration. | CFG; INV §6.5 | C | Whether Foldback is the stage-display feature and what it renders. |

---

## H. Audio/video and advanced media

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| H-01 | FFmpeg media engine | FFmpeg 4 DLLs for codec, format, filter, device, resample, scale. | INV §3 | C | Codec support matrix and hardware acceleration. |
| H-02 | Audio output | DirectSound and AVRT dependencies. | INV §3 | C | Device selection, exclusive mode, latency. |
| H-03 | Video output | D3D9/D3DX9 plus helper delay-loaded D3D10/11. | INV §3 | C | Which path is used for preview vs live. |
| H-04 | NDI media | NDI runtime/plugins and NDI-prefixed FFmpeg DLLs. | INV §3–4 | C | Discovery, stream selection, reconnect behavior. |
| H-05 | DVD media | DScaler5 filters and DVD metadata tables. | INV §3; SCHEMA | C | Region, menus, copy protection, chapter navigation. |
| H-06 | PowerPoint media | PowerPoint resource and thumbnail settings. | SCHEMA; CFG | C | COM automation, slide timing, transitions. |
| H-07 | Web media | `resource_web` and WebView2 runtime. | SCHEMA; INV §3 | C | URL allowlist, cookies, offline behavior. |
| H-08 | Media errors | Error/ready fields in media and presentation tables. | SCHEMA | C | Retry, relink, and user-facing messages. |

---

## I. Settings, persistence, and data management

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| I-01 | Profile model | `.ewp` INI with `Id`, `AppInstDataDir`, `Options`, `DesiredLocale`; active profile is `New Profile`. | INV §5.2 | C | Profile create/rename/delete/switch UI. |
| I-02 | Data roots | Install, ProgramData, Roaming, WebView2, and per-profile public-document roots. | INV §5.1 | C | Permission and roaming behavior. |
| I-03 | Global settings | `ComputerSettings.ini`, `HotKeys.2.ini`, MIDI/remote settings. | CFG | C | Which settings are per-machine vs per-user. |
| I-04 | User settings | `UserSettings.ini` stores interface mode, scripture enablement, auto-advance, PowerPoint, checks, labels. | CFG | C | Save timing and migration. |
| I-05 | Window state | `WindowSettings.ini` stores windows, monitors, splitters, editor popups. | CFG | C | Validation on missing monitors. |
| I-06 | History | `History.ini` stores active profile, schedule path, last resource path, recents. | CFG | C | Privacy/retention and recent-item limits. |
| I-07 | Archives/backups | Profile `Archive` and options `Archive` snapshots observed. | INV §5.3 | C | Automatic backup cadence and restore UI. |
| I-08 | Packages | `.ewpck` ZIP with `Data/*.db` and `Resources/*`; `Packages.db` tracks status. | INV §4.4, §6 | C | Install/uninstall/update semantics. |
| I-09 | Updates | `updates.2.db` and web/update assets. | INV §4.2 | C | Update source, signature checks, rollback. |
| I-10 | Locking | `Protocol.elck` 1-byte file in profile locks. | INV §6.4 | C | Whether it prevents concurrent profile use or is a protocol marker. |
| I-11 | Data migration | Archives use 7.3-era schema; active data is 7.4.1.3. | INV §6.3 | I | Migration rules, failure handling, downgrade behavior. |
| I-12 | Licensing | License directories were empty; no activation workflow observed. | INV §4.2 | O | Activation, trial, and entitlement behavior. |
| I-13 | Localization | `locale.dat`, `DesiredLocale`, ICU 60.2. | INV §3, §5.2 | C | Supported locales and UI translation coverage. |
| I-14 | Import/export | `.ewsx`, `.ewtx`, `.ewb`, `.ewpck`, `.pptx` formats exist. | INV §4–6 | C | UI steps, validation, conflict resolution. |
| I-15 | Error recovery | No recovery workflow was exercised. | — | U | Crash recovery, corrupt DB, missing media, interrupted import. |

---

## J. Online, integrations, and auxiliary features

| ID | Feature | Observable behavior / finding | Evidence | Confidence | Remaining unknowns |
|---|---|---|---|---|---|
| J-01 | WebView2 surfaces | Onboarding and live output use WebView2; a standard EBWebView user-data tree exists. | INV §3, §5.1 | C/O | Which features are web-based vs native. |
| J-02 | Online store | Store control present; demo package and web assets installed. | STG3/UI; INV §4 | O | Account, purchase, download, install flow. |
| J-03 | Remote control | Enabled configuration, display name, UID, paired-client permissions. | CFG | C | Protocol, discovery, authentication, supported clients. |
| J-04 | MIDI | `MIDIControls.7.db` with control/out/export settings tables. | CFG | C | MIDI message mapping and live behavior. |
| J-05 | Alerts | Alert tables and toolbar control. | INV §6.5; STG3/UI | O/C | Alert types, scheduling, output behavior. |
| J-06 | Service intervals | `ServiceIntervals.db` and startup IPC fetch. | INV §6.5, §12.5 | C/O | UI, meaning, and response codes. |
| J-07 | Collections | `Collections.db` stores collections, rules, conditions, layout/library settings. | SCHEMA | C | UI exposure and rule evaluation. |
| J-08 | Spell check | `american.adm` and `autocorrect.adu` dictionaries. | INV §3–4 | C | Which editors use them and how suggestions appear. |
| J-09 | Shapes | Shape plugin XML/DB plus `resource_shape`. | INV §4.2; SCHEMA | C | Shape catalog, editing, and rendering. |
| J-10 | Transitions | Six transition files with parameters and opaque data. | INV §6.4 | C | Rendering, timing, and custom transition support. |
| J-11 | Users/run metadata | `Users.db` user identity and run count. | SCHEMA | C | Whether users are selectable or only telemetry. |
| J-12 | Help | `easyworship74.chm` help asset. | INV §4.2 | C | Context-sensitive help mapping. |

---

## Features not evidenced

The following were not demonstrated and should not be assumed without new evidence:

- Cloud sync or multi-device synchronization.
- Live streaming/recording destinations beyond NDI hints.
- Copyright reporting or CCLI-style workflows.
- Presentation remote APIs or third-party control protocols.
- Automatic crash recovery or session restore.
- Mobile companion apps.
- Advanced stage-display layouts beyond the Foldback settings surface.
- Custom plugin SDK beyond the installed plugin folders.

## Stage 3 conclusion

The evidence is sufficient to define a reconstruction architecture and a black-box test plan, but not to claim behavioral or pixel-perfect parity. The highest-value next evidence is the full helper command set, exact enum/property semantics, rendering capture, and a small number of end-to-end workflows (song → schedule → live, scripture → live, media → live, theme → slide).
