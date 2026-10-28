# EW Clone - Project Status

Status: Stage 4 planning complete; Stage 5 M1 complete, M2 and M3 partially complete  
Last updated: 2026-10-28  
Baseline: EasyWorship 7.4.0.7 on Windows  
Repository: https://github.com/iKay-Peter/EW-Clone.git

This document is the current record of what was investigated, what was built,
how it was verified, and what remains. It intentionally separates verified
observations from provisional or unknown behaviour.

## 1. Project goal

Build a Windows desktop application that reproduces the observable functionality
of EasyWorship 7.4.0.7 without copying proprietary code, assets, fonts, help
content, or Bible text.

The clone uses an import-first compatibility model:

- Legacy EasyWorship profiles and databases are opened read-only.
- New data is written only to the clone database.
- Legacy IDs, UIDs, unknown enum values, and raw source values are preserved in
  sidecar JSON.
- The installed EasyWorship application is not launched without explicit
  approval, and production data is not modified.

## 2. Ground rules and non-negotiables

- No proprietary EasyWorship branding, artwork, help content, fonts, or
  licensed Bible text.
- One process, modular monolith; no internal IPC between clone modules in v1.
- WPF owns the operator shell; D3D11 owns live composition.
- The WPF UI thread never waits on SQLite, media decode, or GPU work.
- Legacy sources are never migrated in place.
- Every milestone must build with zero warnings and pass its tests.
- Unknown behaviour stays configurable or explicitly unknown; it is not guessed
  and presented as verified parity.

## 3. Environment and fixed decisions

| Area | Decision |
|---|---|
| OS | Windows 10.0.26200 x64 |
| Shell | PowerShell 5.1 |
| Prototype runtime | .NET SDK 8.0.301, pinned in `global.json` |
| Release runtime | Retarget to .NET 10 LTS before release |
| UI | WPF with MVVM |
| Graphics | D3D11 plus Direct2D/DirectWrite through Vortice |
| Media | FFmpeg.AutoGen/WASAPI planned; not implemented |
| Database | SQLite with FTS5 |
| Installer | WiX MSI, self-contained `win-x64` planned |
| v1 outputs | One live output plus preview |
| Deferred | NDI, PowerPoint, DVD, remote/MIDI, streaming, store, cloud sync |
| Source policy | Read-only legacy access, clone-owned writes, raw sidecar JSON |

The app must be launched through Explorer for interactive smoke tests;
`Start-Process` alone can be terminated when the automation tool call ends.

## 4. Completed work

### 4.1 Stage 1 - Inventory and surface mapping

- Inventoried the installed EasyWorship 7.4.0.7 files, profiles, resources,
  databases, archives, settings, and helper processes.
- Mapped the visible application surface: shell, libraries, schedule, live
  output, menus, dialogs, and settings.
- Identified the main process as a 32-bit MPRESS-packed executable.
- Identified the helper executable and its role in media/rendering support.
- Recorded the baseline in `research/easyworship-inventory.md`.

### 4.2 Stage 2 - Schema, archive, and data reconnaissance

- Dumped the legacy SQLite schemas and recorded them in
  `research/easyworship-schema.txt`.
- Inspected song, song-word, media, presentation-layout, service-interval, and
  settings data.
- Analysed `.ewsx`, `.ewtx`, `.ewo`, `.ewb`, and `.dfx` containers.
- Established that `.ewsx` and `.ewtx` contain an embedded `main.db` plus
  `media/` entries; media identity is tied to SHA-256 hashes.
- Analysed the schedule archive (`11-24.ewsx`) and proved the `file_hash` to
  media relationship.
- Recorded archive findings in `research/easyworship-archives.txt`.

### 4.3 Stage 3 - Runtime and IPC probing

- Inspected the main executable's PE imports and loaded modules.
- Probed helper imports, file access, and named-pipe traffic with Frida.
- Captured UIA trees and screenshots for onboarding, the main window, and live
  output.
- Confirmed the renderer uses legacy D3D9/D3DX9 and that output capture through
  `PrintWindow`/`CopyFromScreen` returns black.
- Did not recover usable native pseudocode because the main executable is
  MPRESS-packed and no suitable native decompiler is available.
- Did not establish the full IPC command schema or the `EZW_PIPE` reverse
  channel.
- Preserved probe scripts and selected JSON captures under `research/stage3/`;
  raw logs and screenshots remain local and are ignored by Git.

### 4.4 Stage 4 - Architecture and planning

- Wrote `research/feature-matrix.md`.
- Wrote `research/behavioural-specification.md`.
- Wrote `research/architecture-findings.md`.
- Wrote `research/known-unknowns.md`.
- Wrote `research/test-cases.md`.
- Wrote and accepted `research/reconstruction-design.md`.
- Wrote `research/stage4-implementation-plan.md` with milestones M0-M10.
- Confirmed v1 scope and the import-first compatibility boundary.

### 4.5 Stage 5 M0/M1 - Prerequisites and solution scaffold

- Pinned .NET 8.0.301 in `global.json`.
- Verified restore, build, and a minimal WPF launch.
- Created `EasyWorshipClone.sln` with nine source projects:
  `EW.App`, `EW.Application`, `EW.Data`, `EW.Domain`, `EW.Infrastructure`,
  `EW.Media`, `EW.Output`, `EW.Platform`, and `EW.Rendering`.
- Created eight test projects:
  `EW.Application.Tests`, `EW.Data.Tests`, `EW.Domain.Tests`,
  `EW.Import.Tests`, `EW.IntegrationTests`, `EW.PerformanceTests`,
  `EW.Rendering.Tests`, and `EW.UiTests`.
- Added central package management, nullable reference types, analyzers,
  warnings-as-errors, deterministic builds, and LF/UTF-8-without-BOM policy.
- Added DI, configuration, logging, crash handling, profile-root resolution, and
  a WPF shell.
- Added `scripts/build.ps1`, `scripts/test.ps1`, and `scripts/verify.ps1`.
- Added architecture tests enforcing module references.
- Verified the shell starts without admin and writes logs to the user scope.

### 4.6 Stage 5 M2 - Clone schema and data foundation

- Implemented clone schema v4 and the migration runner.
- Created 19 core tables: `Profile`, `Song`, `SongSlide`, `Presentation`,
  `Slide`, `Element`, `Resource`, `MediaAsset`, `Theme`, `Schedule`,
  `ScheduleItem`, `ServiceInterval`, `Setting`, `BibleTranslation`,
  `BibleVerse`, `Alert`, `OutputProfile`, `TimerToken`, and `LegacyImport`.
- Created five FTS5 tables: `SongFts`, `MediaFts`, `PresentationFts`,
  `ThemeFts`, and `BibleFts`.
- Added profile, song, presentation, resource, media, theme, service-interval,
  and setting repositories.
- Registered a provisional `UTF8_U_CI` collation using `OrdinalIgnoreCase`.
- Added hash-verified database backups with free-space preflight and SQLite
  `integrity_check`.
- Added transactional import and rollback across song, media, presentation,
  theme, service-interval, and settings importers.
- Preserved legacy IDs/UIDs and raw values in sidecar JSON.
- Added synthetic fixtures for all imported formats used by tests.

### 4.7 Stage 5 M2 - Legacy readers and importers

- Read-only song reader and song import with UID idempotency, raw sidecars,
  FTS maintenance, and transactional rollback.
- Read-only media reader and media import with path resolution, status/error
  preservation, UID idempotency, raw sidecars, and media FTS.
- Read-only presentation graph reader and import for presentations, slides,
  elements, and text/web/shape resources.
- `.ewsx`/`.ewtx` archive extraction with zip-slip checks, duplicate detection,
  size limits, free-space preflight, and SHA-256 output manifests.
- Extraction and registration of `media/` entries as `MediaAsset` rows.
- Presentation image/video/audio/PowerPoint resources linked to media assets by
  case-insensitive SHA-256 when the archive media was registered first.
- Theme archive import with theme graph JSON preservation and theme FTS.
- Service-interval reader and import; the observed source contains zero
  `interval` and zero `revision` rows, so the zero-row result is correct.
- Legacy settings reader and importer for BOM-aware INI files with sections,
  duplicate-key warnings, last-value-wins semantics, source SHA-256, source
  line, raw line, and JSON metadata.

### 4.8 Stage 5 M2 - RTF and slide generation

- Implemented RTF-to-plain-text conversion.
- Ignored `{\*\...}` ignorable destinations.
- Implemented `\sdslidemarker` paragraph-group splitting for marker songs.
- Preserved empty marker segments as blank slides.
- Implemented blank-paragraph splitting for non-marker songs.
- Reimported the production song library read-only:
  - 433 songs.
  - 2,629 slides.
  - 423 multi-slide songs.
  - Minimum 1 and maximum 146 slides per song.
  - Zero songs without slides.
- One known mismatch remains: `A Mighty Fortress Is Our God` produces 6
  text-based slides while its legacy `slide_uids` field yields 4. The text-based
  result is retained.

### 4.9 Stage 5 M2 - Settings

- Added `Setting` with `Key`, `Value`, `ValueType`, and `LegacyMetadata`.
- Added `SettingRepository` with get, list, value lookup, upsert, and delete.
- Added BOM-aware `LegacyIniFileReader`.
- Added transactional `SettingsImportService`.
- Imported all 7 observed INI files into the clone database:
  - 194 settings imported.
  - 0 warnings.
  - Second pass updated 194 and created 0.
  - Source files verified unchanged.
- Settings keys use `<filename>/<section>/<key>`.
- Import metadata includes source file, full path, SHA-256, section, key, line
  number, and raw line.
- `SettingsImportService` supports atomic multi-file batches, so all selected
  INI files commit or roll back together.
- Added `File > Import Settings...` with multi-file selection, warning counts,
  and a read-only Settings tab in the operator shell.
- Library status now reports the imported settings count.

### 4.10 Stage 5 M2 - Search indexes

- Maintained `SongFts` on song insert and update.
- Maintained `MediaFts` on media insert and update.
- Maintained `ThemeFts` on theme insert and update.
- Added `PresentationFts` maintenance on every presentation upsert and
  re-import.
- Verified that a re-import updates the FTS row without duplicating it.
- `BibleFts` exists but is not populated because no Bible source is installed.

### 4.11 Stage 5 M3 - Operator shell

- Implemented the main window with menus, toolbar, status bar, library tabs,
  preview pane, live pane, and output pane.
- Implemented library tabs for songs, media, presentations, themes, scriptures,
  and settings.
- Implemented import commands for the currently supported legacy formats.
- Implemented selection and preview/live display.
- Implemented Go Live, Next, Previous, Black, and Clear controls.
- Logo currently reports `Logo output is not implemented yet`.
- Alerts remain unimplemented and unverified.
- Manually smoke-tested startup, imports, selection, and live controls.

### 4.12 Accessibility and verification

- Overrode `ToString()` on `Song`, `MediaAsset`, `Presentation`, `Theme`, and
  `ServiceInterval` so WPF ListBox items expose meaningful UIA names.
- Verified the app starts as `EW Clone - Default`, responds, and writes the
  expected log entry.
- Verified new source files are UTF-8 without BOM and use LF.
- Verified `dotnet format` reports no changes.

## 5. Current clone database state

Clone database: `%LOCALAPPDATA%\EWClone\ewclone.db`

| Data | Count |
|---|---|
| Songs | 433 |
| Song slides | 2,629 |
| Multi-slide songs | 423 |
| Media assets | 168 |
| Presentations | 156 |
| Presentation resources | 4,033 |
| Themes | 1 |
| Service intervals | 0 |
| Settings | 194 |

Observed legacy imports:

- Songs: `433 new, 0 updated`, then `0 new, 433 updated`.
- Media: `168 new, 0 updated`.
- Presentations: `156 new, 0 updated`.
- Theme: `1 new, 0 updated` (`Special Theme 2.ewtx`).
- Service intervals: `0 new, 0 updated` because the source has no rows.
- Settings: `194 new, 0 updated`, then `0 new, 194 updated`.

## 6. Test and build state

Current solution state:

- Build: zero warnings under the repository warning policy.
- Tests: 73 passing across all projects.

| Test project | Passing |
|---|---|
| EW.Domain.Tests | 4 |
| EW.Data.Tests | 53 |
| EW.Rendering.Tests | 1 |
| EW.Application.Tests | 1 |
| EW.Import.Tests | 1 |
| EW.PerformanceTests | 1 |
| EW.UiTests | 1 |
| EW.IntegrationTests | 11 |
| **Total** | **73** |

Covered by tests:

- Schema version and migration behavior.
- Profile repository behavior.
- Custom collation registration and indexed access.
- Song, media, presentation, theme, service-interval, and settings import.
- Source hash preservation.
- Idempotent re-import.
- Transactional rollback, including multi-file settings batches.
- Archive extraction and media registration.
- Presentation-to-media hash linking.
- RTF parsing, ignorable destinations, marker splitting, and blank slides.
- FTS maintenance, including presentation re-import.
- Module reference rules.

Not covered yet:

- FlaUI startup, tab switching, and search smoke tests.
- Rendering golden images.
- Media playback and audio.
- Bible import and lookup.
- Schedules and full live state-machine workflow.
- Installer and clean-machine install.

## 7. Known limitations and unresolved questions

| Area | Current state |
|---|---|
| Main executable internals | MPRESS-packed; no usable native decompiler. |
| IPC | Command semantics and `EZW_PIPE` reverse channel are unknown. |
| UI action to IPC mapping | Unknown. |
| Live output state machine | Exact priority and restore rules remain unknown. |
| Rendering pipeline | Exact EasyWorship composition and transition behavior unverified. |
| `.dfx` | BLOB semantics unknown. |
| Enums/properties | Many legacy enum and property meanings remain provisional. |
| Migrations | Legacy migration behavior beyond observed data is unknown. |
| Media duration/trim | Units remain unknown and are preserved raw. |
| Slide UIDs | Non-marker `slide_uids` semantics unresolved; one known mismatch. |
| Section labels | Song section labels are not decoded. |
| Output capture | D3D/WebView capture through `PrintWindow`/`CopyFromScreen` is black. |
| Bible | No `.ewb` file is installed; public-domain import awaits a source. |
| FFmpeg | LGPL-compatible build, notices, and version decision remain open. |
| NDI | SDK and licensing not secured. |
| PowerPoint | COM adapter not implemented. |
| Packaging | Code-signing certificate and WiX release pipeline not started. |
| Collation | `UTF8_U_CI` is provisional `OrdinalIgnoreCase`; exact legacy folding is unverified. |
| Service intervals | Zero source rows; timer/interval UI semantics remain unknown. |

## 8. What remains

### 8.1 M0 - Prerequisites

- Choose and pin an LGPL-compatible FFmpeg build; record version, source, and
  notices.
- Define a safe, repeatable D3D-aware oracle capture method.
- Finalise synthetic fixture specifications beyond the current import fixtures.
- Record the release retarget and packaging prerequisites.

### 8.2 M2 - Data foundation

- Add read-only Bible metadata and public-domain translation import.
- Add one-step profile settings discovery instead of manual multi-file
  selection across the global and per-user settings folders.
- Add unified import reporting across songs, media, presentations, themes,
  service intervals, and settings.
- Close the remaining M2 checklist and update the plan/status document.

### 8.3 M3 - Operator shell

- Complete search and sort behavior across every library.
- Persist window, splitter, and recent-item state.
- Implement the central hotkey service with text-entry suppression.
- Add FlaUI smoke tests for startup, tab switching, and search.
- Verify keyboard navigation and accessible names across the shell.

### 8.4 M4 - Rendering and output

- Implement the immutable render scene.
- Implement D3D11 device creation, WARP fallback, and the render thread.
- Host preview via `HwndHost`.
- Create the borderless live output window and monitor routing.
- Implement DirectWrite text layout, wrapping, alignment, and font fallback.
- Implement WIC image decoding and backgrounds.
- Add bounded scene handoff and staging-texture capture.
- Add WARP golden-image and monitor hot-plug tests.

### 8.5 M5 - Live operation and scheduling

- Implement the live state machine as an explicit policy table.
- Complete Go Live, next/previous, schedule navigation, and restore behavior.
- Implement Logo, Black, Clear, and transition orchestration.
- Implement schedule create/open/save and ordered item types.
- Implement service intervals, timers/tokens, auto-advance, and alerts.
- Add workflow and failure-recovery tests.

### 8.6 M6 - Songs, presentations, and themes

- Implement song create/edit/delete and metadata validation.
- Complete section-label detection and song usage history.
- Implement the presentation editor for text, image, and geometry changes.
- Implement theme apply, override, reset, save, and export.
- Implement the transition library and parameter mapping.
- Add round-trip and authoring tests.

### 8.7 M7 - Media

- Pin FFmpeg bindings and native libraries.
- Implement probing, hashing, thumbnails, and metadata.
- Implement image import and alpha handling.
- Implement video decode, frame queue, seek, trim, loop, and poster frames.
- Implement WASAPI audio output and the A/V clock.
- Implement mute, volume, repeat, and end-of-media behavior.
- Implement relink, missing-file, and unsupported-codec errors.
- Add synthetic playback and error tests.
- Add optional PowerPoint support behind a feature flag.

### 8.8 M8 - Scripture

- Implement the `.ewb` reader and public-domain fixture import.
- Implement translation selection, book/chapter/verse lookup, and validation.
- Implement scripture search and ranking.
- Implement passage wrapping and presentation layout.
- Implement insertion into preview, live, and schedule.
- Add reference, search, insertion, and cache tests.
- Do not bundle or commit licensed Bible text.

### 8.9 M9 - Secondary outputs and advanced features

- Implement Alt output and independent content routing.
- Evaluate Foldback/nursery/message semantics only where evidence supports them.
- Evaluate alpha output.
- Add NDI behind `INdiService` if the SDK and licence are available.
- Add remote/MIDI adapters behind feature flags, disabled by default.
- Add restricted online/help surfaces.
- Add multi-output, hot-plug, and adapter-failure tests.

### 8.10 M10 - Hardening and packaging

- Run the full conformance suite where safe and approved.
- Fix high-impact deviations and document accepted differences.
- Add performance benchmarks and tune startup/search/live latency.
- Add crash dumps, safe-mode startup, and log redaction.
- Add profile backup/restore and corruption recovery.
- Add archive validation and untrusted-input tests.
- Build the self-contained `win-x64` application.
- Build the WiX MSI, notices, checksums, and signing plan.
- Run accessibility and high-DPI checks.
- Write the user-facing compatibility and limitations document.

## 9. Immediate next actions

1. Add read-only Bible metadata support and a public-domain fixture.
2. Add one-step profile settings discovery across the global and per-user
   settings folders.
3. Add unified import reporting and close the remaining M2 checklist.
4. Finish M3 search/sort, persistence, hotkeys, and FlaUI smoke tests.
5. Resolve the FFmpeg LGPL build and notices before M7.
6. Define the D3D-aware oracle capture method before visual-fidelity claims.
7. Keep all legacy access read-only and all imports transactional.

## 10. Build, test, and run

Prerequisites: .NET SDK 8.0.301, Windows, and the repository root as the working
directory.

```powershell
dotnet restore EasyWorshipClone.sln
dotnet build EasyWorshipClone.sln -c Debug --no-restore
dotnet test EasyWorshipClone.sln -c Debug --no-build
dotnet format EasyWorshipClone.sln --verify-no-changes --no-restore
```

Interactive launch:

```powershell
$exe = Resolve-Path 'src\EW.App\bin\Debug\net8.0-windows\EWClone.exe'
Start-Process explorer.exe -ArgumentList "`"$exe`""
```

Local verification scripts:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\verify.ps1
```

Clone data and logs:

- Database: `%LOCALAPPDATA%\EWClone\ewclone.db`
- Log: `%LOCALAPPDATA%\EWClone\Logs\ewclone.log`

## 11. Source and artifact locations

| Item | Path |
|---|---|
| Mission and stage requirements | `Reconstruct EasyWorship.md` |
| Architecture | `research/reconstruction-design.md` |
| Implementation plan | `research/stage4-implementation-plan.md` |
| Feature matrix | `research/feature-matrix.md` |
| Behavioural specification | `research/behavioural-specification.md` |
| Architecture findings | `research/architecture-findings.md` |
| Known unknowns | `research/known-unknowns.md` |
| Test cases | `research/test-cases.md` |
| Inventory | `research/easyworship-inventory.md` |
| Schema dump | `research/easyworship-schema.txt` |
| Archive analysis | `research/easyworship-archives.txt` |
| Runtime probes | `research/stage3/` (scripts and JSON; logs/screenshots local) |
| Application shell | `src/EW.App/` |
| Data layer and importers | `src/EW.Data/` |
| Domain model | `src/EW.Domain/` |
| Clone database | `%LOCALAPPDATA%\EWClone\ewclone.db` |

Legacy source paths used for read-only verification:

- `C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Databases\Data\Songs.db`
- `C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Databases\Data\SongWords.db`
- `C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Databases\Data\Media.db`
- `C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Databases\Data\PresentationLayouts.db`
- `C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Options\Default.ewo\Data\ServiceIntervals.db`
- `C:\ProgramData\Softouch\EasyWorship.v7\Settings\`
- `C:\Users\iKay\AppData\Roaming\Softouch\Easyworship.v7\Settings\`

## 12. Data-handling and safety notes

- Legacy databases and settings are read-only inputs; their hashes are checked
  after import in tests where applicable.
- Clone writes are transactional and rolled back on failure.
- No production database was migrated in place.
- No licensed Bible text or proprietary EasyWorship media is included in the
  clone database or source tree.
- Raw research captures may contain machine-local paths and process addresses;
  they are reference artifacts, not application inputs.
- The EasyWorship main executable must not be launched without explicit
  approval.

## 13. Documentation maintenance

Update this document when a milestone changes state, when the clone database
counts change, when tests are added, or when a previously unknown behaviour is
verified. The implementation plan remains the detailed milestone source;
this document is the concise project-level status and handoff record.
