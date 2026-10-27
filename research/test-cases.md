# EasyWorship 7 Test Cases

Status: Stage 3 black-box test plan  
Last updated: 2026-10-26  
Oracle: installed EasyWorship 7.4.0.7  
Purpose: verify the reconstruction against observable behaviour without relying on proprietary source.

## 1. Rules and safety

1. Use an isolated profile such as `EW-Test`; never test against the production `New Profile`.
2. Use synthetic content only: original lyrics/text, generated images, short generated video/audio, and a small synthetic `.pptx`.
3. Do not use personal schedules, media, lyrics, or paths in evidence.
4. Do not leave full-screen output open while the user is working. Close output windows and stop the test instance when done.
5. Do not modify the installed application directory or global plugin folders.
6. Do not run two test instances against the same profile.
7. Capture output with a D3D-aware method; `PrintWindow`/`CopyFromScreen` produced black and is not a valid oracle.
8. A test is not “passed” because the UI looks plausible. Record the oracle result, clone result, and any deviation.

## 2. Status key

| Status | Meaning |
|---|---|
| **Observed** | Behaviour was captured in Stage 3; no new run required. |
| **Partial** | Some evidence exists, but the full workflow was not exercised. |
| **Planned** | Test is defined but not yet run. |
| **Blocked** | Requires a missing account, hardware, media asset, or permission. |

## 3. Test data set

Create a disposable test profile with:

- **Songs:** `Test Song A` (3 verses + chorus, section labels), `Test Song B` (Unicode title/lyrics), `Test Song C` (long lines for wrapping).
- **Images:** high-contrast 1920x1080 target image, transparent PNG, gradient/background image.
- **Video:** 10-second synthetic clip with tone and visible frame counter.
- **Audio:** 10-second synthetic tone.
- **Presentation:** 3-slide synthetic `.pptx`.
- **Theme:** one `.ewtx` with distinct title/body styles.
- **Schedule:** one `.ewsx` with a song, scripture, and media item.
- **Bible:** KJV or another available public-domain translation; use short references only.
- **Monitor config:** primary 1920x1080; optional secondary 1024x768. Record actual DPI.

## 4. Startup and profile tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| START-001 | First launch shows onboarding | Fresh test profile; no onboarding-dismissed state; launch app; wait for windows. | `TForm_Onboarding` appears (or documented equivalent), with a WebView2 surface. | Observed: `onboarding-tree.json` |
| START-002 | Onboarding URL failure | Inspect requested URL and page body. | Oracle shows `ERR_FILE_NOT_FOUND` due to `%3F`. Clone should show a valid welcome page or graceful fallback; record the deviation. | Observed: `onboarding.png` |
| START-003 | Close onboarding | Close the onboarding window. | Main `TFormEZWorshipMain` becomes visible and usable. | Observed: `ui-tree.json` |
| START-004 | Active profile resolution | Set `History.ini` to a known test profile; launch. | Title/data root reflect the selected profile. | Partial: Stage 3 used `Stage3` |
| START-005 | Missing profile | Point `History.ini` at a nonexistent `.ewp`; launch. | Clear profile-selection/error path; no crash or silent production fallback. | Planned |
| START-006 | Helper absent | Stop the helper; launch the main app. | UI remains usable; helper-dependent behaviour reports or degrades cleanly. | Planned |
| START-007 | Helper startup handshake | Launch with helper running; capture `EZW_HELPER_PIPE`. | Timestamp frame, request, `Response 13`, `Result=1`, ack, SQLite stream. | Observed: `ezw-spawn.log` |
| START-008 | Startup data fetch | Correlate the first request with the returned DB. | Returned DB matches `ServiceIntervals.db` schema/size. | Observed |
| START-009 | Clean shutdown | Close the main window normally. | Settings flush; DB handles close; output windows close; helper may remain. | Partial: Stage 3 stopped main, helper remained |
| START-010 | Window restore | Move/resize windows and splitters; restart. | Bounds and splitter percentages restore. | Planned; settings schema confirmed |
| START-011 | Profile switch | Switch between two test profiles. | Libraries, schedules, themes, and output settings change; no cross-profile data leak. | Planned |
| START-012 | Concurrent use / lock | Launch a second instance against the same profile. | Defined lock/warning behavior; no DB corruption. | Planned; `Protocol.elck` exists |

## 5. Shell and navigation tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| SHELL-001 | Main layout | Launch and close onboarding. | Menus, toolbar, Schedule/Live/Live Output panes, library tabs, selector/list, preview are present. | Observed: `ui-main.png` |
| SHELL-002 | Pane resizing | Drag each splitter; restart. | Panes resize within limits; sizes persist. | Partial: settings confirm persistence; interaction not timed |
| SHELL-003 | Tab switching | Visit Songs, Scriptures, Media, Presentations, Themes. | Correct selector/list content; no stale cross-tab state. | Observed for visible tabs |
| SHELL-004 | Search/filter | Search for a synthetic song/media/theme. | Matching results; clear/reset restores list. | Planned |
| SHELL-005 | Keyboard shortcuts | Test slide next/prev, Go Live next/prev, black/clear/logo, section jumps, schedule next/prev. | Mappings match `HotKeys.2.ini`; no action while editing text unless specified. | Planned |
| SHELL-006 | Clipboard interoperability | Copy a slide and paste into an external RTF/HTML/image target; copy external content back. | Documented formats round-trip; internal format preserved. | Planned |
| SHELL-007 | Remote UI | Open Remote; inspect pairing/enablement. | Matches configured remote settings; no crash if service unavailable. | Planned |
| SHELL-008 | Alerts UI | Open Alerts; create a synthetic alert. | Alert list/editor behaves consistently; output routing documented. | Planned |
| SHELL-009 | Store/Web | Open Store and Web. | Documented navigation/empty/offline behavior; no credential prompts in test. | Planned/Blocked (online) |
| SHELL-010 | Keyboard/UIA access | Navigate without a mouse; inspect UIA tree. | Focus order is usable; custom controls expose what they can; known UIA limitations documented. | Partial: UIA tree captured |
| SHELL-011 | Interface/window modes | Change `InterfaceMode`/preview location; restart. | Layout/theme change matches oracle; settings persist. | Planned |

## 6. Song tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| SONG-001 | List and select | Open Songs; select `Test Song A`. | Metadata list correct; Live pane/preview updates; slide count displayed. | Partial: observed with existing song |
| SONG-002 | Go Live song | With `Test Song A` selected, invoke Go Live. | Current slide appears on live output; operator UI remains responsive. | Partial: Go Live invoked; output capture limited |
| SONG-003 | Slide navigation | Use next/prev keys while a song is live. | Slide advances/reverses in order; position text updates. | Planned |
| SONG-004 | Section labels | Jump to chorus/verse/bridge using configured keys. | Correct labelled section becomes live; behavior matches oracle. | Planned |
| SONG-005 | Create/edit song | Create a song with verses/chorus, labels, Unicode, and long lines. | Save/reopen preserves text, labels, and slide layout. | Planned |
| SONG-006 | Delete/undo | Delete a synthetic song; undo if available. | Defined confirmation/undo behavior; indexes and presentation link stay consistent. | Planned |
| SONG-007 | Song search | Search title, author, and lyric word. | Expected matches; no personal data required. | Planned |
| SONG-008 | Usage history | Open/live a song; inspect history. | `SongHistory.action` records the documented action; UI/report matches. | Planned |
| SONG-009 | Import/export/provider | Import/export a synthetic song; test provider sync if available. | Documented format/round-trip; no silent data loss. | Planned/Blocked |
| SONG-010 | Duplicate title | Create two songs with the same title. | Both can coexist or a defined conflict prompt appears. | Planned |
| SONG-011 | Long/Unicode content | Use long lines, combining characters, and RTL text. | No truncation/corruption; wrapping matches oracle. | Planned |

## 7. Scripture tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| SCR-001 | Translation list | Open Scriptures; list translations. | Installed Bibles appear with correct names/abbreviations. | Partial: tab observed |
| SCR-002 | Reference lookup | Enter a known reference (e.g. a short Psalm verse). | Correct passage, book/chapter/verse labels, and text. | Planned |
| SCR-003 | Scripture search | Search a distinctive public-domain phrase. | Ranked results; selecting one shows the passage. | Planned |
| SCR-004 | Preview/Go Live | Preview and send a passage live. | Text/slide matches oracle; theme/font/wrap are consistent. | Planned |
| SCR-005 | Add to schedule | Add a passage to the schedule. | Schedule item persists; reopening retains the passage. | Planned |
| SCR-006 | Wrap/font cache | Change font/size and reflow the same passage. | Cache behavior matches; text remains readable. | Planned |
| SCR-007 | Missing Bible | Remove/disable a translation in an isolated profile. | Clear error/fallback; no crash. | Planned |
| SCR-008 | Download/licensing | Attempt a Bible download with no account. | Defined offline/entitlement behavior; no credentials stored in logs. | Blocked |

## 8. Media tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| MEDIA-001 | Import image | Import the target PNG/JPG. | Thumbnail/metadata appear; file path/hash stored. | Planned |
| MEDIA-002 | Import video | Import the 10-second clip. | Duration/dimensions/poster extracted; preview works. | Planned |
| MEDIA-003 | Import audio | Import the 10-second tone. | Duration/size stored; preview/playback works. | Planned |
| MEDIA-004 | Playback controls | Play/pause/stop/seek the video. | State transitions and output match oracle; audio/video stay in sync. | Planned |
| MEDIA-005 | Trim/poster | Set start/end and poster frame. | Playback respects trim; poster persists across restart. | Planned |
| MEDIA-006 | Loop/mute/volume | Toggle repeat/mute and change volume. | Matches oracle; settings persist per item. | Planned |
| MEDIA-007 | Alpha media | Use a transparent PNG/video with alpha. | Correct compositing/alpha output; no black halo. | Planned |
| MEDIA-008 | NDI | Connect a synthetic NDI source if available. | Discovery/selection/preview work; latency acceptable. | Blocked (NDI source) |
| MEDIA-009 | DVD | Insert/point at a DVD source if available. | Disc/clip metadata and playback match oracle. | Blocked (DVD) |
| MEDIA-010 | PowerPoint import | Import the 3-slide `.pptx`. | Slides/thumbnails render; live playback works. | Planned |
| MEDIA-011 | Web media | Add a local test HTML page as a web resource. | Renders in WebView2; navigation/scroll flag behaves as documented. | Planned |
| MEDIA-012 | Missing/unsupported | Rename/delete a media file; import an unsupported file. | Clear error/relink path; no crash or silent disappearance. | Planned |
| MEDIA-013 | Duplicates/relink | Import the same file twice; rename a file and relink. | Defined dedupe/relink behavior; no duplicate DB corruption. | Planned |
| MEDIA-014 | Media search | Search title/author/tags. | Expected results; indexes stay in sync after edits. | Planned |

## 9. Presentations and themes

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| PRES-001 | Import PPTX | Import the synthetic 3-slide deck. | Item appears with slides/thumbnails; source path recorded. | Planned |
| PRES-002 | Presentation metadata | Edit title/author/tags. | Persists and appears in list/search. | Planned |
| PRES-003 | Editor | Open the editor; move/resize a text element. | Geometry changes persist; live/preview update. | Planned |
| PRES-004 | Slide order | Reorder slides; delete/duplicate one. | Order and numbering match oracle; live output follows. | Planned |
| PRES-005 | Presentation Go Live | Send a presentation live; navigate slides. | Correct slide sequence on output. | Planned |
| PRES-006 | Export/package | Export a synthetic package/archive. | Re-import preserves content; documented format. | Planned |
| THEME-001 | Theme list/apply | Apply a synthetic theme to `Test Song A`. | Title/body styles and background match oracle. | Planned |
| THEME-002 | Slide override | Override one slide’s background/text, then reset. | Override persists; reset restores theme behavior. | Planned |
| THEME-003 | Theme save/export | Save/export the modified theme. | Re-import preserves overrides; `.ewtx` valid. | Planned |
| THEME-004 | Legacy theme | Open a synthetic 7.3-era `.ewtx`. | Migration succeeds or fails with a clear message; no partial corruption. | Planned |
| THEME-005 | Apply transition | Apply each of the six transitions to a slide change. | Visual result matches oracle for the same parameters. | Planned |
| THEME-006 | Transition parameters | Change Wipe direction/softness. | Output changes accordingly; invalid values are handled. | Planned |
| THEME-007 | Missing theme | Remove the theme file referenced by a slide. | Clear fallback/error; slide remains editable. | Planned |

## 10. Schedule and live tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| SCH-001 | Create schedule | Create a synthetic schedule; add song, scripture, media. | Items appear in order with correct types/thumbnails. | Planned |
| SCH-002 | Save/reopen | Save, close, reopen the schedule. | All items/state persist; no media loss. | Planned |
| SCH-003 | Reorder/drag | Drag items and multi-select if supported. | Order changes as oracle; undo/redo if available. | Planned |
| SCH-004 | Auto-advance | Enable automatic advance; run a short schedule. | Timing/stop/loop behavior matches oracle. | Planned |
| SCH-005 | Contiguous mode | Toggle `ContiguousScheduleEnabled`. | Visual/operational difference matches oracle. | Planned |
| SCH-006 | Schedule checks | Trigger lyrics-only/popup checks. | Correct warnings/blocks/bypass; no false positives on synthetic data. | Planned |
| SCH-007 | Service interval | Create an interval; observe startup fetch/timer. | Matches oracle; DB/IPC behavior documented. | Partial: startup DB fetch observed |
| SCH-008 | Empty schedule | Open/create an empty schedule. | Empty-state UI; no crash on Go Live/advance. | Planned |
| SCH-009 | Corrupt archive | Corrupt a copy of `.ewsx`. | Clear error; original data untouched; no crash. | Planned |
| SCH-010 | Legacy migration | Open a synthetic 7.3 `.ewsx`. | Migration preserves items/UIDs or fails cleanly. | Planned |
| LIVE-001 | Output window | Go Live with a synthetic slide. | `TForm_LiveOutput` appears on the configured monitor at the correct size. | Partial: window tree captured |
| LIVE-002 | Go Live content | Send `Test Song A` live. | Output shows the selected slide; operator UI stays responsive. | Partial |
| LIVE-003 | Logo/Black/Clear | Invoke each control; capture output after each. | Each state and transition matches oracle; restore path works. | Planned |
| LIVE-004 | Transition | Change slides with each transition. | Timing/visual result matches oracle. | Planned |
| LIVE-005 | Alt output | Enable Alt; assign a second monitor. | Independent Alt content matches oracle. | Planned/Blocked (monitor) |
| LIVE-006 | Foldback | Enable Foldback; observe content. | Stage/foldback behavior matches oracle. | Planned/Blocked (monitor) |
| LIVE-007 | Nursery/message | Trigger nursery/message surfaces. | Correct routing/content; no operator disruption. | Planned |
| LIVE-008 | Monitor mapping | Change live monitor number/resolution; restart. | Output follows configured device/bounds or falls back clearly. | Planned |
| LIVE-009 | Missing monitor/DPI | Disconnect output monitor; change DPI. | No lost session; defined fallback/recovery. | Planned |
| LIVE-010 | NDI output | Enable NewTek/NDI output if available. | Stream appears with expected format/latency. | Blocked (NDI) |
| LIVE-011 | Alpha output | Enable alpha output. | Keying/compositing matches oracle. | Planned/Blocked |
| LIVE-012 | Alert | Trigger a synthetic alert during live. | Alert appears/dismisses per oracle; live content restores. | Planned |
| LIVE-013 | Timer/token | Insert a synthetic countdown/clock. | Counts/formats correctly on preview and live. | Planned |
| LIVE-014 | D3D capture | Capture live output with a D3D-aware method. | Non-black frame matches the preview; record method. | Blocked: prior capture black |

## 11. Settings, persistence, and data tests

| ID | Test | Preconditions / steps | Expected result | Status |
|---|---|---|---|---|
| SET-001 | Setting persistence | Change a user setting; restart. | Value persists in the documented file and affects behavior. | Planned |
| SET-002 | Hotkey rebinding | Change a hotkey; test it. | New mapping works; conflict handling matches oracle. | Planned |
| SET-003 | Window/DPI persistence | Move windows across monitors; restart. | Bounds restore or clamp visibly; no off-screen window. | Planned |
| SET-004 | Profile switch | Switch profiles; verify libraries/schedules. | No cross-profile leakage; active profile updates. | Planned |
| SET-005 | Archive/backup | Trigger a profile/options archive. | Snapshot is valid and restorable. | Planned |
| SET-006 | Package install | Install a synthetic `.ewpck`. | Content appears; status recorded; uninstall if supported. | Planned |
| SET-007 | Update check | Check for updates without installing. | Defined network/offline behavior; no silent install. | Planned/Blocked |
| SET-008 | Profile lock | Hold a profile open; start a second instance. | Defined lock/warning; no corruption. | Planned |
| SET-009 | Corrupt config | Corrupt an isolated INI/DB. | Clear error/fallback; production files untouched. | Planned |
| SET-010 | Data migration | Import 7.3-era synthetic archives. | Documented migration; version fields update correctly. | Planned |
| SET-011 | Permissions | Make profile/resources read-only. | Clear error; no partial write; UI remains responsive. | Planned |
| SET-012 | Localization | Switch locale if supported. | UI strings/assets match documented coverage. | Planned |
| SEC-001 | Pipe access | Inspect pipe ACLs/connection behavior. | Documented local access; no unauthenticated remote exposure. | Planned |
| SEC-002 | Secret hygiene | Inspect test logs/DBs for credentials/personal data. | No secrets or production data in evidence. | Planned |
| SEC-003 | Untrusted archive | Import a malformed/malicious ZIP/DB copy. | Bounded parsing, clear error, no code execution/path traversal. | Planned |
| PERF-001 | Startup | Measure cold/warm launch with the test profile. | Record baseline; clone has no unexplained multi-second stall. | Planned |
| PERF-002 | Large library | Load 1,000 synthetic songs/media items; search/scroll. | UI remains responsive; search results correct. | Planned |
| PERF-003 | Live latency | Measure select → preview → Go Live → visible output. | Record baseline; clone meets agreed target. | Planned |
| PERF-004 | Media playback | Play 1080p video for 10 minutes; monitor CPU/GPU/memory. | Stable playback; no unbounded memory growth. | Planned |
| PERF-005 | Multi-output | Run Live + Alt + Foldback with synthetic content. | No dropped frames/lockups; record baseline. | Planned/Blocked |

## 12. Exit criteria for Stage 3

Stage 3 investigation is complete enough for Stage 4 when:

1. The five Stage 3 documents exist and agree on facts.
2. P0 unknowns are either resolved or explicitly accepted as design risks.
3. The test plan covers every major feature area in `feature-matrix.md`.
4. Stage 4 can choose data compatibility, process architecture, rendering, and media stack without pretending unresolved behaviour is known.
5. No production data or lingering test processes remain.

Current result: **documents complete; P0 unknowns remain open and are explicitly carried into Stage 4.**
