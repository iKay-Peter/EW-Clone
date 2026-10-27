# EasyWorship 7.4.0.7 — Installed-Application Inventory (Stage 2 + Stage 3 addendum)

Status: **Stage 2 complete; Stage 3 runtime addendum added.** Stage 2 was static/read-only. Stage 3 used an isolated copied profile, temporarily pointed the global active profile at it, then restored the pointer and removed the profile; production profile data was not modified.

Date: 2026-10-26  
Host: Windows NT 10.0.26200.0, AMD64  
Target: EasyWorship 7, version 7.4.0.7, installed 2026-01-31

Evidence files:

- `research/rea-setup-report.md` — Stage 1 REA setup/verification
- `research/easyworship-schema.txt` — full SQLite schema of the active profile databases
- `research/easyworship-archives.txt` — sample `.ewsx`/`.ewtx` container contents and schemas
- `research/easyworship-inventory.md` — this report

Tools used: REA 6.8.0 CLI (`rea.cmd`), REA SQLite inspector (SQLite 3.53.4), PowerShell PE/header reader, .NET ZIP reader, Windows registry and file metadata. Deep native decompilation was unavailable because Ghidra/Hopper/IDA are not installed.

Stage 3 evidence: `research/stage3/ezw-spawn.log` (startup IPC handshake), `research/stage3/onboarding.png` + `onboarding-tree.json` (Getting Started failure), `research/stage3/main-after-onboarding.png` + `main-after-onboarding-tree.json` (main UI after closing onboarding), `research/stage3/live-output-tree.json` (live-output window tree).

---

## 1. Installation identity

| Item | Value |
|---|---|
| Product | EasyWorship 7 |
| Version | 7.4.0.7 |
| Publisher | Softouch Development, Inc. |
| Install location | `C:\Program Files (x86)\Softouch\EasyWorship 7\` |
| Install date | 2026-01-31 |
| Uninstaller | `unins000.exe` (Inno Setup-style `unins000.*`; registry key suffix `_is1`) |
| Registry uninstall key | `{7A192ABE-B12B-40F2-802C-61F34E758EBA}_is1` |
| Main executable | `EasyWorship.exe` |
| Helper executable | `EasyWorshipHelper.exe` |
| Data version | `7.4.1.3` (minimum `6.5.1.0`) |
| OS | Windows 11 / NT 10.0.26200.0, 64-bit, running 32-bit EasyWorship binaries under WOW64 |

The install directory contained 46 files in REA's bounded inventory. REA's desktop-application heuristic found **0 managed/runtime candidates and 0 components**, consistent with a native Win32 application rather than .NET/Electron/Java.

---

## 2. Executables, binaries, and architecture

### 2.1 Main and helper executables

| File | Bytes | Format | Arch | Subsystem | PE timestamp (UTC) | Packing / signing | Version |
|---|---:|---|---|---|---|---|---|
| `EasyWorship.exe` | 6,041,088 | PE32 | x86 (0x014C) | GUI (2) | 2022-07-28 23:56:55 | MPRESS-style `.MPRESS1`/`.MPRESS2`; **not validly signed** | 7.4.0.7 |
| `EasyWorshipHelper.exe` | 20,160,232 | PE32 | x86 (0x014C) | GUI (2) | 2022-07-28 23:51:29 | Not packed; **valid Authenticode signature** | 7.4.0.7 |

Both use linker 2.25, image base `0x400000`, and declare `DllCharacteristics = 0x0` (no ASLR/NX/CFG bits declared).

`EasyWorship.exe` sections:

| Section | Virtual address | Virtual size | Raw offset | Raw size |
|---|---:|---:|---:|---:|
| `.MPRESS1` | `0x00001000` | 29,691,904 | `0x00000200` | 5,975,040 |
| `.MPRESS2` | `0x01C52000` | 4,652 | `0x005B2E00` | 5,120 |
| `.rsrc` | `0x01C54000` | 60,244 | `0x005B4200` | 60,416 |

- SHA-256 `8223063a36c7438aaff86b4dae227f7abdeafc900e0a8fa58299932766825e0e`.
- Entry point `0x01C5263B` falls in `.MPRESS2`, the packer stub.
- `SizeOfImage` is `0x01C63000`; checksum `0x005CED4E`.
- Authenticode status: `NotSigned`. The certificate directory points to offset 29,434,368 + 9,960, which is beyond the 6,041,088-byte file; REA refused certificate parsing for that reason. REA resource inspection also refused because resource ranges were not entirely file-backed in one section. Version resources are still readable.
- The file is a native PE32 image with no CLI header. It is not managed.

`EasyWorshipHelper.exe`:

- 11 sections, large `.text` (15,789,168 bytes) and `.rsrc` (2,765,312 bytes); not packed.
- SHA-256 `59e7f255bf3da905c0a9f9ffea74d3ff38c83c0e1bcd865fd1f78d0c0c7c223e`.
- Signature: valid, signer `CN="Softouch Development, Inc.", O="Softouch Development, Inc.", S=Oklahoma, C=US`; issuer Sectigo Public Code Signing CA R36; timestamped by Sectigo RSA Time Stamping Signer #3.
- Exports only Delphi RTTI helpers: `dbkFCallWrapperAddr`, `__dbk_fcall_wrapper`, `TMethodImplementationIntercept`.

### 2.2 Companion binaries and hooks

| File | Bytes | Arch | Purpose / observed evidence |
|---|---:|---|---|
| `ezwHookpp.32.exe` | 10,552,552 | x86 | 32-bit PowerPoint hook helper; Delphi RTTI exports |
| `ezwHookpp.64.exe` | 15,122,152 | x64 | 64-bit PowerPoint hook helper |
| `sdHookpp.32.dll` | 1,295,592 | x86 | Exports `LoadPowerPointHook`, `UnLoadPowerPointHook` |
| `sdHookpp.64.dll` | 2,075,368 | x64 | 64-bit PowerPoint hook |
| `sdHookDX.32.dll` | 9,513,192 | x86 | Exports `InitHooks`, `SetOutputRect`; DirectX hooking |
| `sdHookDX.64.dll` | 13,465,832 | x64 | 64-bit DirectX hook |
| `sslsparser2.dll` | 1,581,568 | x86 | SongSelect lyric parser extension; exports `ParseLyrics`, `GetLabel`, `CompareLabel`, `GetDLLDataInstanceCount`, `ParseJavaScriptResultForSongData`, `GetSongSelectParserJS` |

`sslsparser2.dll` contains the definitive compiler string:

```text
Embarcadero Delphi for Win32 compiler version 34.0 (27.0.41310.5003)
```

That is the Delphi 10.4 Sydney-family compiler (product 27.0). The helper and hook binaries also contain Delphi RTTI exports plus `FMX.*` and `Vcl.*` type names, strongly indicating the same Delphi toolchain. The main `EasyWorship.exe` is MPRESS-packed, so its compiler is not directly confirmed; the Delphi conclusion for the suite is strong but the main EXE's own compiler version remains a hypothesis.

### 2.3 Imported libraries

`EasyWorship.exe` imports 25 DLLs:

```text
KERNEL32.DLL, d3d9.dll, shlwapi.dll, version.dll, user32.dll,
oleaut32.dll, netapi32.dll, advapi32.dll, msvcrt.dll, winhttp.dll,
SHFolder.dll, wsock32.dll, Avrt.dll, gdi32.dll, mpr.dll, usp10.dll,
winmm.dll, wininet.dll, winspool.drv, comdlg32.dll, comctl32.dll,
shell32.dll, d3dx9_43.dll, ole32.dll, dsound.dll
```

`EasyWorshipHelper.exe` imports the same broad set (25 static DLLs) and adds 20 delay-loaded DLLs:

```text
kernel32, user32, wtsapi32, msimg32, advapi32, rpcrt4,
windowscodecs, uxtheme, imm32, DWMAPI, shell32, Shcore,
gdiplus, d3d10, d3d10_1, d3d9, d3d11, Crypt32
```

Interpretation (observed imports, not runtime proof):

- Main UI/preview: Direct3D 9 + D3DX9, DirectSound, GDI/USER, common controls.
- Helper/output: D3D10/10.1/11 and DXGI are delay-loadable; WIC, GDI+, DWM, Shcore, `wtsapi32`.
- Low-latency audio: `Avrt.dll`.
- Online/network: `winhttp`, `wininet`, `wsock32`, `netapi32`, `mpr`.
- Text: `usp10` (Uniscribe), `imm32`.
- Printing: `winspool.drv`.
- COM: `ole32`, `oleaut32`.

---

## 3. Runtime and dependency stack

| Component | Observed version / evidence | Role |
|---|---|---|
| Native runtime | PE32 x86, GUI; no CLI header; no .NET | Win32 desktop application |
| Compiler | Delphi 10.4-family, compiler 34.0 / 27.0.41310.5003 | Confirmed in `sslsparser2.dll`; Delphi RTTI/FMX/VCL in helper/hooks |
| Direct3D | `d3d9.dll`, `d3dx9_43.dll` 9.29.952.3111; helper delay-loads D3D10/10.1/11 | UI preview and output rendering |
| Audio | `dsound.dll`; `Avrt.dll`; FFmpeg audio codecs | Playback and low-latency scheduling |
| FFmpeg | `FFmpeg4\avcodec-58` 58.54.100, `avformat-58` 58.29.100, `avutil-56` 56.31.100, `avfilter-7` 7.57.100, `avdevice-58` 58.8.100, `swresample-3` 3.5.100, `swscale-5` 5.5.100 | Media decode/convert |
| NDI | `Processing.NDI.Lib.x86.dll` 4.6.3.0 plus NDI plugins and NDI-prefixed FFmpeg DLLs | Network video input/output |
| DScaler5 | `GenDMOProp.dll` 0.0.8.0, `MpegAudio.dll` 0.0.8.0, `MpegVideo.dll` 0.0.6.0 | DirectShow MPEG/DVD decoding |
| WebView2 | Loader `WebView2Loader.dll` 1.0.1072.54; installed runtime 155.0.4283.62 | Embedded web pages (help/login/content) |
| SQLite | Bundled `sqlite3.dll` 584,536 bytes; version string `3.7.15.2` | All local structured state |
| SQLite collation | Schemas use `COLLATE UTF8_U_CI`; helper contains `UTF8_U_CI` and `REINDEX UTF8_U_CI`; bundled `sqlite3.dll` does not | Custom Unicode case-insensitive collation registered by the application |
| ICU | `icudt60.dll` 26,902,528; `icuin60.dll` 2,011,648; `icuuc60.dll` 1,317,888; version 60.2.0.0 | Unicode/segmentation support |
| MSVC runtime | `vcruntime140.dll` 14.24.28127.4 | C/C++ runtime dependency |
| Spell check | `american.adm` 320,029; `autocorrect.adu` 54,453 | Spelling/autocorrect dictionaries |
| UI assets | `optionsbk.jpg`, `optionsbk2.jpg` | Options window backgrounds |

`sqlite3.dll` does not define `UTF8_U_CI`; the helper binary contains both the collation name and a reindex statement. Therefore any reimplementation that opens these databases must register an equivalent `UTF8_U_CI` collation before schema/index use.

---

## 4. Install-directory and plugin inventory

### 4.1 Install root

Notable files:

| File | Bytes | Notes |
|---|---:|---|
| `EasyWorship.exe` | 6,041,088 | MPRESS-packed main UI |
| `EasyWorshipHelper.exe` | 20,160,232 | Signed output/helper binary |
| `d3dx9_43.dll` | 1,998,168 | DirectX 9 shader/runtime DLL |
| `sqlite3.dll` | 584,536 | SQLite 3.7.15.2 |
| `WebView2Loader.dll` | 109,472 | WebView2 loader 1.0.1072.54 |
| `vcruntime140.dll` | 83,224 | MSVC 14.24 runtime |
| `icudt60.dll` / `icuin60.dll` / `icuuc60.dll` | 26,902,528 / 2,011,648 / 1,317,888 | ICU 60.2 |
| `american.adm` / `autocorrect.adu` | 320,029 / 54,453 | Spelling/autocorrect |
| `unins000.exe` / `.dat` / `.msg` | 3,127,528 / 20,633 / 24,131 | Uninstaller |
| `Copyright.rtf` / `License.rtf` | 3,278 / 26,448 | Legal text |

Subdirectories:

| Directory | Files | Bytes | Contents |
|---|---:|---:|---|
| `FFmpeg4` | 8 | 29,163,188 | FFmpeg 4.x DLLs + license |
| `NDI` | 7 | 19,496,288 | NDI 4.6.3 x86 library/plugins + NDI FFmpeg DLLs |
| `DScaler5` | 3 | 835,584 | DirectShow MPEG/DVD filters |

### 4.2 Global data and plugins (`C:\ProgramData\Softouch\EasyWorship.v7`)

| Path | Files | Bytes | Notes |
|---|---:|---:|---|
| `Plugins\Bibles` | 6 | 26,991,616 | `HCSB.ewb`, `KJV.ewb`, `NIV1984.ewb`, `NIVA1984.ewb`, `RVA.ewb`, `bible_downloads.1.db` |
| `Plugins\Extensions` | 1 | 1,581,568 | `sslsparser2.dll` |
| `Plugins\Packages` | 1 | 22,310,013 | `demo.v3.ewpck` |
| `Plugins\Shapes` | 3 | 565,789 | `SD.ShapesList.xml`, `SD.PresetShapes.1.xml`, `shapes.1.db` |
| `Plugins\Transitions` | 6 | 55,296 | `Blend.dfx`, `Cover.dfx`, `Iris.dfx`, `Push.dfx`, `Reveal.dfx`, `Wipe.dfx` |
| `Plugins\Updates` | 1 | 3,072 | `updates.2.db` |
| `Plugins\Web` | 6 | 82,508,640 | `easyworship74.chm` (81,829,009), `precheck.html`, `welcome.html`, `attribs.xml`, GIFs |
| `Profiles` | 3 | 524 | `Default.ewp`, `New Profile.ewp`, `New Profile (2).ewp` |
| `Settings` | 5 | 8,786 | `ComputerSettings.ini`, `HotKeys.2.ini`, `MIDIControls.7.db`, `RemoteControlClients.2.db`, `RemoteControlSettings.ini` |

`Dictionaries`, `Effects`, `License`, `Licenses`, and `Bibles\Locks` were empty at inspection time. `Datacache\webvars.ini` was 3 bytes (effectively empty).

Stage 3 correction: `Plugins\Web\welcome.html` exists (354,328 bytes), but the Getting Started window loads it with a percent-encoded `?` (`welcome.html%3Fregistered=...`), so Chromium reports `ERR_FILE_NOT_FOUND`; see Section 12.

### 4.3 Bible plugin format

Each `.ewb` is a SQLite database (`SQLite format 3`). `KJV.ewb` schema:

- `header` — 10 columns: `rowid, id, key, name, abbrev_name, lang_code, plugin_version, format_version, copyright, license`
- `books` — `rowid, name, abbrev_name, alt_name, book_info BLOB, verse_info BLOB`
- `streams` — `rowid, stream BLOB`
- `words` — `rowid, word, verse_info INT32A`
- `sqlite_sequence`

`KJV.ewb` header row: `rowid=1, id=17, key=0, name=King James Version, abbrev_name=KJV, lang_code=EN, plugin_version=1, format_version=1, copyright=NULL, license=NULL`. Plugin format version is therefore 1.

`bible_downloads.1.db` contains `bible(translation_id, translation_abbrv, error_no)`.

### 4.4 Demo content package

`demo.v3.ewpck` is a ZIP archive (28 entries) containing:

```text
Data/Media.db
Data/PresentationLayouts.db
Data/Presentations.db
Data/Songs.db
Data/SongWords.db
Data/Themes.db
Resources/Audio/
Resources/Images/*.jpg
Resources/Presentations/
Resources/Schedules/
Resources/SharedMedia/
Resources/Themes/
Resources/Videos/*.wmv
```

This confirms the `.ewpck` package format is ZIP + a profile-like `Data`/`Resources` layout.

---

## 5. Profiles, data roots, and active-profile layout

### 5.1 Data roots

| Scope | Path |
|---|---|
| Install | `C:\Program Files (x86)\Softouch\EasyWorship 7\` |
| Global settings/plugins | `C:\ProgramData\Softouch\EasyWorship.v7\` |
| Per-user settings/cache | `C:\Users\iKay\AppData\Roaming\Softouch\Easyworship.v7\` |
| WebView2 user data | `C:\Users\iKay\AppData\Roaming\Softouch\WebView2\EBWebView\` |
| Profile libraries | `C:\Users\Public\Documents\Softouch\Easyworship\<profile>\` |

### 5.2 Profile model

Profiles are INI files in `C:\ProgramData\Softouch\EasyWorship.v7\Profiles`. Each `.ewp` declares an ID, an `AppInstDataDir`, an `Options` set, and an optional locale. Example (`New Profile.ewp`):

```ini
[Profile]
Id=1-A620332A-8465-4C6D-A3B0-CA0A75F4C3A5
AppInstDataDir=C:\Users\Public\Documents\Softouch\Easyworship\New Profile\
Options=Default
DesiredLocale=
```

Available profiles:

| Profile | Data path | Files | Bytes |
|---|---|---:|---:|
| `Default` | `...\Easyworship\Default\` | 151 | 245,004,918 |
| `New Profile` | `...\Easyworship\New Profile\` | 343 | 803,846,473 |
| `New Profile (2)` | `...\Easyworship\New Profile (2)\` | 48 | 83,422,285 |

**Active profile:** `New Profile` (`History.ini` has `default_profile=New Profile.ewp`).

### 5.3 Active-profile directory layout

```text
New Profile\
  Resources\
    Audio\            0 files
    Images\           145 files / 232,505,787 bytes
    Presentations\      1 file  /     319,647 bytes
    Schedules\         74 files / 376,665,343 bytes
    SharedMedia\       15 files /   1,363,243 bytes
    Themes\             3 files /      30,758 bytes
    Videos\            19 files /  94,368,228 bytes
  v6.1\
    Databases\         33 files /  18,840,685 bytes
      Data\            15 SQLite DBs (see section 6)
      Archive\          1 snapshot, 16 files / 3,395,638 bytes
      Locks\           Protocol.elck (1 byte)
    Datacache\         31 files /  11,541,986 bytes
      Posterframes\    cached JPEG poster frames
      Thumbnails\      media.thumb.2.db (5,751,808)
      temp\            one 3,501,149-byte import temp file
    Options\
      Default.ewo\     22 files / 68,210,796 bytes
        Data\          output settings DBs + version/locale
        Thumbnails\    slide.thumb.5.db (66,476,032)
        Archive\       1 snapshot from v7.4.0.4
```

Active-profile extension counts:

| Extension | Count | Bytes |
|---|---:|---:|
| `.ewsx` | 74 | 376,665,343 |
| `.png` | 91 | 210,021,271 |
| `.db` | 47 | 92,803,072 |
| `.mp4` | 11 | 71,065,305 |
| `.jpg` | 75 | 19,435,505 |
| `.wmv` | 4 | 16,591,958 |
| `.gif` | 4 | 6,710,965 |
| `.jpeg` | 23 | 6,701,283 |
| `.tmp` | 1 | 3,501,149 |
| `.pptx` | 1 | 319,647 |
| `.ewtx` | 3 | 30,758 |
| `.dat` | 8 | 216 |
| `.elck` | 1 | 1 |
| **Total** | **343** | **803,846,473** |

---

## 6. Data model and file formats

Nearly all EasyWorship state is SQLite. The active profile's `v6.1\Databases\Data` contains:

| Database | Bytes | Primary role |
|---|---:|---|
| `PresentationLayouts.db` | 7,517,184 | Presentation, slide, element, resource, file, property, token graph |
| `SongWords.db` | 4,267,008 | Song lyrics as RTF plus slide UIDs/revisions |
| `SongKeys.db` | 2,125,824 | Song search word index |
| `SongHistory.db` | 606,208 | Song usage/action history |
| `Songs.db` | 264,192 | Song metadata and `presentation_id` link |
| `Media.db` | 148,480 | Media library and media subtype tables |
| `PresentationKeys.db` | 147,456 | Presentation search index |
| `MediaKeys.db` | 103,424 | Media search index |
| `ThemeKeys.db` | 96,256 | Theme search index |
| `Collections.db` | 93,184 | Collections, rules, layout/library settings |
| `Themes.db` | 43,008 | Theme file index |
| `Presentations.db` | 23,520 | External presentation file index |
| `Users.db` | 6,144 | User identity/run metadata |
| `Packages.db` | 3,072 | Content package status |
| `version.dat` / `locale.dat` | 18 / 36 | `7.4.1.3` / `6.5.1.0`; English (US) |

### 6.1 Core relationships

- `Songs.song.rowid` → `SongWords.word.song_id` (application-managed; no declared FK).
- `Songs.song.presentation_id` → `PresentationLayouts.presentation.rowid` (cross-database, application-managed).
- `PresentationLayouts.presentation.rowid` → `slide.presentation_id` → `element.slide_id`.
- `element.background_resource_id` / `foreground_resource_id` / `shape_resource_id` → `resource.rowid`.
- `resource_*` subtype tables (`resource_image`, `resource_video`, `resource_audio`, `resource_text`, `resource_web`, `resource_gradient`, `resource_shape`, `resource_dvd`, `resource_feed`) extend `resource`; files are referenced through `file.rowid`.
- `Themes.db.file_item` indexes `.ewtx` theme files.
- `Presentations.db.file_item` indexes external presentation files such as `.pptx`.
- `Media.db.media` is extended by `media_dvd`, `media_feed`, `dvd_pc`, `effect`, `effect_property`, etc.
- The `*Keys.db` databases use `word_list` + `word_key` search tables.
- `Collections.db` stores `collection`, `detail`, `condition`, `layout_setting`, `library_setting`, `setting`, `setting_data`.

### 6.2 Key schema details

`PresentationLayouts.db`:

- `presentation` (28 columns) includes `presentation_uid`, `presentation_type`, `aspect_ratio`, `thumbnail_slide_id`, layout/thumbnail revisions, metadata, and `provider_id`.
- `slide` (15 columns) includes `presentation_id`, `slide_uid`, `slide_rev_uid`, `order_index`, `layout_flag`, `theme_id`, `theme_slide_uid`, `modified_theme_id`, `modified_theme_layout_revision`, thumbnails.
- `element` (15 columns) defines position/size (`x`, `y`, `width`, `height`), element type/style, ordering, background/foreground/shape resources, mute state, and `from_master`.
- `resource` + subtype tables cover image, video, audio, text (RTF), web, gradient, shape, DVD, and live feed resources.
- `token` stores timer/token definitions.
- `info` row: `7.4.1.3` / `6.5.1.0`.

`Songs.db`:

- `song` (16 columns): `rowid`, `song_item_uid`, `song_rev_uid`, `song_uid`, `title`, `author`, `copyright`, `administrator`, `description`, `tags`, `reference_number`, `provider_id`, `vendor_id`, `presentation_id`, `layout_revision`, `revision`.
- `revision` row: `revision_num=724`, `tablename=song`, `key=0`.

`SongWords.db`:

- `word` (6 columns): `rowid`, `song_id`, `words RTF`, `slide_uids TEXT`, `slide_layout_revisions INT64A`, `slide_revisions INT64A`.

`Media.db`:

- `media` (46 columns) includes UID, type, metadata, path/filename/hash/size, dimensions, duration, start/end/poster positions, mute/volume/repeat/usage/alpha settings, thumbnail URL, source/vendor fields, and stream counts/types.
- Subtype tables: `media_dvd`, `media_feed`, `dvd_disk`, `dvd_pc`, `dvd_mac`, `effect`, `effect_property`, `feed_pc`.

`Collections.db`:

- `collection`, `detail`, `condition`, `layout_setting`, `library_setting`, `setting`, `setting_data`; includes search/rule state and opaque layout/setting BLOBs.

### 6.3 Schedule and theme archives

`.ewsx` (schedule) and `.ewtx` (theme) are ZIP containers with a `main.db` SQLite database and optional `media` resources.

Sample `.ewsx` structure:

```text
main.db
media/
media/<hashed-name>.png
media/<hashed-name>.wmv
```

Sample `.ewtx` structure:

```text
main.db
```

The archive `main.db` uses the same presentation/slide/element/resource model but an older schema version:

- `info` row: `7.3.0.11` / `6.5.1.0`.
- `presentation` has 27 columns (no `provider_id`).
- `resource_feed_pc` has 10 columns (no `field_dominance`).
- Schedules contain 31 tables total, including `presentation`, `slide`, `element`, `resource*`, `file`, properties, and `token`.

This indicates schedules/themes are portable snapshots that may be migrated upward when opened by the 7.4.1.3 data layer.

### 6.4 Other formats

| Format | Observed structure |
|---|---|
| `.ewb` Bible | SQLite `header` / `books` / `streams` / `words`; plugin format 1 |
| `.dfx` transition | SQLite `info` + `parameter` + opaque `data` BLOB. `Blend.dfx` has no parameters; `Wipe.dfx` exposes `Direction` (0–7) and `BorderSoftness` (0–100, default 5). |
| `.ewo` options set | Directory with `Data\*.db` output-settings databases + `Thumbnails\slide.thumb.5.db`; data version `7.4.1.3`. |
| `.ewp` profile | INI with `[Profile]`, `Id`, `AppInstDataDir`, `Options`, `DesiredLocale`. |
| `.ewpck` package | ZIP with `Data\*.db` + `Resources\...`; demo package has 28 entries. |
| `.elck` lock | 1-byte file at `v6.1\Databases\Locks\Protocol.elck`; purpose not established. |

### 6.5 Output-settings `.ewo` databases

`Default.ewo\Data` contains parallel presentation-layout databases for output surfaces:

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

The `Live`/`Alt`/`Foldback` settings DBs use the same 28-column `presentation` schema as `PresentationLayouts.db`. `Alerts.db` adds `alert`, `alert_token_link`, and `token`. `ServiceIntervals.db` has `interval(start_time, end_time, start_time_fixed, end_time_fixed)`.

### 6.6 Caches and thumbnails

- `Options\Default.ewo\Thumbnails\slide.thumb.5.db` — 66,476,032 bytes; tables `info(last_global_rev_uid, last_feature_flag)` and `thumbnails(item_type, item_id, slide_uid, item_revision, thumbnail_size, thumbnail, global_rev_uid, feature_flag)`.
- `Datacache\Thumbnails\media.thumb.2.db` — 5,751,808 bytes; table `thumbnails(item_id, item_revision, thumbnail_size, thumbnail, valid)`.
- `Datacache\Posterframes\*.jpg` — cached poster frames; filenames encode duration, width/height, video/audio stream flags, and other media metadata.
- `AppData\Roaming\Softouch\Easyworship.v7\Datacache\biblewraps.2.db` — 3,986,432 bytes; table `word_wrap_cache(translation, translation_version, font_name, font_size, word_wrap_list BLOB)`.

---

## 7. Settings and configuration

| File | Bytes | Key observed values |
|---|---:|---|
| `ComputerSettings.ini` | 891 | Live output on monitor 2, 1024×768; Alt output enabled (`Alt_Enabled=1`); Foldback disabled; NewTek/NDI disabled; `Default_Pack_Files=1`; `Disable_Shrink_Filter=0`. |
| `UserSettings.ini` | 1,269 | `InterfaceMode=1`; Scriptures enabled; Show Live on startup; full PowerPoint; build thumbnails with PowerPoint; automatic schedule advance; contiguous schedule; lyrics-only check off; schedule check popup off. |
| `WindowSettings.ini` | 1,507 | Main window 800×600 on monitor 1; Editor 1018×550; Inspector 476×523; Options 675×493; splitter percentages. |
| `HotKeys.2.ini` | 494 | Slide next: `N`, Right, Space; slide prev: `P`, Left, Backspace; black screen `C-66`; clear screen `C-67`; go-live next/prev; jump-to-verse/chorus/etc.; schedule navigation. |
| `RemoteControlSettings.ini` | 133 | `Enabled=1`, `Active=0`, display name `EasyWorship Remote (DESKTOP-C2H3VRP)`, UID `1-FE87DE91-57AC-44D5-B98C-F412777E016D`. |
| `BibleSettings.ini` | 55 | `Last_Bible=28`, no removed Bibles. |
| `History.ini` | 1,165 | Active profile `New Profile.ewp`; schedule path `<schedules>`; last resource path `Downloads`; 10 recent schedule entries. |
| `MIDIControls.7.db` | 5,120 | MIDI control/out/export settings tables. |
| `RemoteControlClients.2.db` | 2,048 | Paired client table (`uid_hash`, `device_name`, `device_type`, `device_permissions`). |

The `History.ini` recent-schedule list and the active profile contain personal schedule/media names. They are intentionally not reproduced in this report; `research/easyworship-archives.txt` retains sample local archive names for traceability and should be treated as private.

WebView2 uses a standard Chromium user-data tree at `C:\Users\iKay\AppData\Roaming\Softouch\WebView2\EBWebView` (cache, GPU cache, cookies/login DBs, local storage). Those contents were not opened or inventoried.

---

## 8. Observed process state

- `EasyWorship.exe` was not running during inspection.
- `EasyWorshipHelper.exe` was running as PID 44480 with command line:

```text
"C:\Program Files (x86)\Softouch\EasyWorship 7\EasyWorshipHelper.exe" AUTO
```

- It was created 2026-10-25 07:11:10.
- Stage 2's `tasklist /m` view showed only `ntdll.dll` and WOW64 modules. **Stage 3 Frida attach corrected this:** the helper is fully loaded (135 modules, including SQLite, FFmpeg, NDI, DScaler, ICU, D3D9/10/11). The Stage 2 result was a WOW64 enumeration limitation, not a dormant helper. `safemon.dll` in the module list is 360 Total Security injection, not EasyWorship.
- The helper is a GUI-subsystem PE32 binary and contains embedded Delphi unit names including `EW.Presentations.*`, `SD.FFmpeg.*`, `SD.NewTekNDI`, `SD.SQLite3`, `Winapi.D3D10`, `Winapi.D3D11`, plus a `\\.\pipe\%s` format string. This supports, but does not prove, the hypothesis that it is the output/media rendering process and uses named-pipe IPC.

---

## 9. Confirmed findings vs hypotheses

### Confirmed (observed evidence)

- EasyWorship 7 v7.4.0.7 is installed at `C:\Program Files (x86)\Softouch\EasyWorship 7`.
- Main and helper executables are native PE32 x86, not managed.
- `EasyWorship.exe` is MPRESS-packed, has an entry point in `.MPRESS2`, and has no valid Authenticode signature; its PE certificate directory is out of range.
- `EasyWorshipHelper.exe` is not packed, is GUI subsystem, and has a valid Softouch Authenticode signature.
- `sslsparser2.dll` was built with Delphi 10.4-family compiler 34.0 / 27.0.41310.5003; helper/hooks expose Delphi RTTI and FMX/VCL identifiers.
- Runtime stack includes D3D9/D3DX9, D3D10/11 delay-loads, DirectSound, AVRT, FFmpeg 4, NDI 4.6.3, DScaler5, WebView2, SQLite 3.7.15.2, ICU 60.2, and MSVC 14.24.
- Nearly all state is SQLite; the active profile is `New Profile`, with 343 files / 803,846,473 bytes.
- `.ewsx`/`.ewtx` are ZIP containers with `main.db`; `.ewb` is SQLite; `.dfx` is SQLite; `.ewo` is a directory of SQLite settings/thumbnail DBs; `.ewpck` is ZIP; `.ewp` is INI.
- Profile/data versions differ: app `7.4.0.7`, data `7.4.1.3` (min `6.5.1.0`), schedule/theme archives `7.3.0.11`.
- `UTF8_U_CI` is a custom collation declared in schemas; helper contains the literal and a `REINDEX` statement; bundled `sqlite3.dll` does not define it.
- WebView2 runtime 155.0.4283.62 is installed; loader 1.0.1072.54 is bundled.
- The helper process was present with `AUTO` command line. Stage 3 Frida attach showed 135 loaded modules; the Stage 2 ntdll/WOW64-only view was a WOW64 enumeration limitation.
- Stage 3 confirmed the helper pipe channel (`EZW_HELPER_PIPE`) and captured its startup request/response framing; the helper streamed a SQLite database matching `ServiceIntervals.db`.
- The Getting Started window (`TForm_Onboarding`) is a WebView2 surface and fails to render `welcome.html` because the URL encodes `?` as `%3F`; the file exists.
- Live-output windows (`TForm_LiveOutput`, `TFrame_PreviewOuput`, `TsdWebBrowser`, `Intermediate D3D Window`) are created in the main process; local rendering capture is limited under the current session.

### Hypotheses (not yet proven)

- `EasyWorshipHelper.exe` is a media/data helper; `EasyWorship.exe` is the control UI. Stage 3 confirms a named-pipe channel but shows the helper responding to a data request, not necessarily owning live output.
- The main EXE was built with the same Delphi 10.4 toolchain, but MPRESS packing prevents direct confirmation.
- `PresentationLayouts.db` is the master layout store; output `.ewo` databases contain per-output overrides/snapshots.
- `.ewsx`/`.ewtx` archives are migrated from 7.3 layout schema to 7.4 on import.
- The `.dfx` `data` BLOB encodes transition shader/implementation data.
- ~~The helper's dormant module list reflects an early-loader/suspended state rather than a normal steady-state helper.~~ **Refuted in Stage 3**; the helper was fully loaded.
- The 7.4.1.3 data version indicates the data layer was upgraded by a newer build or by a 7.4.1.x update before the currently installed 7.4.0.7 binary was restored.

---

## 10. Known unknowns and next probes

Blocked or unresolved:

1. **Native decompilation** — no Ghidra, Hopper, IDA, JADX, or Kuna installed. `EasyWorship.exe` is also MPRESS-packed. Function-level behavior and exact algorithms remain inaccessible with the current toolchain.
2. **Main EXE compiler/build** — MPRESS prevents direct compiler/string confirmation.
3. **Exact enum mappings** — `media_type`, `resource_type`, `element_type`, `element_style_type`, `action_type`, `presentation_type`, `layout_flag`, `end_type`, etc.
4. **Property-key semantics** — `presentation_property`, `slide_property`, `element_property` keys and value encodings.
5. **Transition `data` BLOB** — format and rendering semantics.
6. **Named-pipe protocol** — names, message framing, commands, and main/helper ownership.
7. **Rendering pipeline** — how `element` geometry, themes, fonts, alpha, and output monitors map to D3D9/D3D11 output.
8. **Data migration rules** — 7.3 archive schema → 7.4.1.3 data schema.
9. **Online/WebView2 flows** — login, SongSelect parsing, updates, and licensing endpoints.
10. **Integrity/official baseline** — whether the unsigned MPRESS main EXE is the official distribution or has been modified.

Stage 3 status: item 6 is partially resolved (pipe names, framing, startup handshake, and one `ServiceIntervals.db` request); the full command set remains unknown. Items 3–5 and 7–10 remain open.

Recommended next probes (Stage 3 candidates, only with explicit approval):

- Run EasyWorship normally under Frida/ETW to capture helper launch arguments, named-pipe traffic, SQLite file opens, and D3D calls.
- Inspect the packed main EXE after unpacking (Frida dump or a dedicated unpacking workflow) to recover Delphi units, strings, and RTTI.
- Query enum/property distributions from private DB copies (no lyrics or personal media extraction).
- Capture a short live-output session and correlate UI actions with DB writes and pipe messages.
- Compare the installed main EXE hash/signature against an official 7.4.0.7 installer.

---

## 11. Bottom line

EasyWorship 7.4.0.7 is a **native Delphi Win32 x86 suite**: a packed control/UI process plus a signed helper/data process, backed almost entirely by SQLite. Its content model is a presentation → slide → element → resource graph, with songs, media, themes, schedules, Bibles, transitions, shapes, and output settings stored in related SQLite databases. Schedules and themes are portable ZIP+SQLite snapshots; the global and per-profile data roots are clearly separated, and the active profile is `New Profile`. This is enough architecture and format coverage to define a reconstruction scope, but function-level behavior, enum semantics, and rendering rules remain; Stage 3 resolved the named-pipe handshake and the onboarding failure.

---

## 12. Stage 3 runtime addendum

Status: **complete enough for reconstruction planning; stopped and cleaned up on 2026-10-26.**

### 12.1 Method and safety

- Stage 3 used an isolated copy of the production profile at `C:\Users\iKay\AppData\Local\Temp\opencode\EW-Stage3\Profile\`:
  - `v6.1\Databases\Data` - 16 files / 15,445,046 bytes.
  - `v6.1\Options\Stage3.ewo` - 22 files / 68,210,796 bytes.
  - Total ~83.7 MB; media/resources were not copied.
- A temporary `Stage3.ewp` profile was created under `C:\ProgramData\Softouch\EasyWorship.v7\Profiles\`; `History.ini` was temporarily pointed at it.
- Runtime capture used Frida 17.23.2 via `rea instrument-with-frida` (spawn and attach), plus PowerShell UIAutomation. No debugger or decompiler was used.
- Cleanup: main process stopped; `Stage3.ewp` and the temp profile removed; `History.ini` restored to `default_profile=New Profile.ewp`; ProgramData/Roaming settings and `webvars.ini` restored. The pre-existing helper PID 44480 was left running.

### 12.2 Helper runtime

- `EasyWorshipHelper.exe` PID 44480 is fully loaded and responsive, with 135 modules. Stage 2's ntdll/WOW64-only module list was a `tasklist /m` WOW64 enumeration limitation.
- Loaded modules observed through Frida included SQLite, FFmpeg, NDI, DScaler5, ICU, Direct3D 9/10/11, WebView2, and Delphi runtime libraries.
- `safemon.dll` was present but is **360 Total Security injection**, not an EasyWorship component.
- Helper imports include `CreateNamedPipeW`, `ConnectNamedPipe`, `WaitNamedPipeW`, `DisconnectNamedPipe`, `ReadFile`, `WriteFile`, `CreateFileMappingW`, and `SendMessageW`. This is consistent with the helper acting as a pipe/data responder.

### 12.3 Getting Started window (`TForm_Onboarding`)

- First launch in the copied profile opened `TForm_Onboarding`, titled **"Getting Started with EasyWorship"**, 890x339.
- It is a WebView2 surface: `TewWebBrowserCloud` -> `Chrome_WidgetWin_1` -> `RootView`.
- Requested URL observed in UIA:

```text
file:///C:/ProgramData/Softouch/Easyworship.v7/Plugins/Web/welcome.html%3Fregistered=false&show=true&noprompt=false&colorTheme=dark&font=Segoe%2520UI&ver=7.4.0.7&interfaceMode=ResourcePreview
```

- The `?` separator is percent-encoded as `%3F`, so Chromium treats `welcome.html%3Fregistered=...` as a literal filename. The page renders `ERR_FILE_NOT_FOUND` even though `welcome.html` exists (354,328 bytes).
- This is an application-side URL-construction bug, not a missing asset. A reconstruction should emit `welcome.html?registered=...` and gracefully handle a failed welcome page.
- Evidence: `research/stage3/onboarding.png`, `research/stage3/onboarding-tree.json`.

### 12.4 UI automation behavior

- After a spawned launch, `Get-Process.MainWindowHandle` returned the Delphi `TApplication` window (for example `0x3317F2`), not the main form. UIA searches on that handle found no buttons.
- The reliable target is the top-level window with class `TFormEZWorshipMain` and title `EasyWorship - Stage3`. After closing onboarding, that window exposed 121 UIA elements.
- Buttons such as `Go Live`, `Logo`, `Black`, and `Clear` are custom `TsdSpeedButton` controls; UIA `InvokePattern` is unsupported, so a mouse-click fallback is required.
- Live output creates several top-level `TForm_LiveOutput` windows plus `TForm_LayeredWindow` siblings. The main process, not the helper, creates these local output windows.

### 12.5 Named-pipe IPC

Confirmed pipe roles and startup handshake:

- Main creates the server `\\.\pipe\EZW_PIPE` (`openMode=0x40000003`, `pipeMode=0`, `maxInstances=255`).
- Main opens `\\.\pipe\EZW_HELPER_PIPE` as a client (`access=0xC0000000`, `creation=3`, `GetFileType=3`).
- The helper is the responder on `EZW_HELPER_PIPE`.

Observed packet sequence (little-endian lengths, UTF-16LE text):

```text
main -> helper: 2a000000 00000000 "1.0/26/2026 7:57:40 PM"
main -> helper: 90000000 00000000
main -> helper: <144-byte request>
helper -> main: 16000000 00000000 "Response 13"
helper -> main: 1c000000 00000000 <"Result", value=1>
main -> helper: ffffffffffffffff
helper -> main: <SQLite database stream, 1024-byte chunks>
```

The 144-byte request uses length-prefixed UTF-16 field names. Visible fields:

- `RequestType`
- `ProfileName` = `Stage3`
- `RecType`
- `RowID`

The exact type tags and all integer semantics are not yet decoded. The final `0xFF...` write is likely an end/ack marker, but this is not proven.

The streamed database begins `SQLite format 3` and contains tables `revision` and `interval`. Its schema and 5,120-byte size match `ServiceIntervals.db` from the profile's `.ewo\Data` directory, e.g.:

```text
C:\Users\Public\Documents\Softouch\Easyworship\New Profile\v6.1\Options\Default.ewo\Data\ServiceIntervals.db
```

Thus the helper protocol is at least a **profile-scoped SQLite data-fetch channel**, not only a render-command channel. `EZW_PIPE` is presumably the reverse/main-side channel, but no traffic was captured on it in this run.

### 12.6 Rendering observations

- Local live output is main-side. Captured UIA tree includes `TFrame_PreviewOuput` (spelling as shipped), `TsdPresentationWin`, `TsdWebBrowser` with `about:blank`, and an `Intermediate D3D Window` sized 2304x1536; the output window itself was 1920x1080.
- `PrintWindow` and desktop `CopyFromScreen` both produced black output in this session. This is likely a D3D/WebView composition or capture limitation, not proof that the rendered output was black.
- The main preview did show the selected song and slide count (`Slide 1 of 4`), confirming the UI/state path worked.
- Remaining rendering work: capture with a D3D-aware method, correlate UI actions with DB writes/pipe messages, and test NDI/streaming/recording paths that likely exercise the helper more heavily.

### 12.7 Remaining Stage 3 unknowns

- Full `RequestType` / `RecType` / `RowID` command set and response codes.
- Exact request type tags and value encodings.
- Whether `EZW_PIPE` carries reverse traffic, notifications, or frame data.
- Which UI actions map to helper requests versus purely local main-process rendering.
- Onboarding URL construction and first-run state machine in the packed main EXE.
- D3D/WebView output composition and capture.
- Official hash/signature baseline for the unsigned MPRESS main EXE.

### 12.8 Stage 3 deliverables

- `research/feature-matrix.md` - evidence-backed feature inventory and confidence levels.
- `research/behavioural-specification.md` - observable workflows, provisional state models, and edge cases.
- `research/architecture-findings.md` - process, IPC, data, rendering, media, and packaging findings.
- `research/known-unknowns.md` - prioritized unresolved questions and next probes.
- `research/test-cases.md` - black-box oracle test plan with synthetic test data.
