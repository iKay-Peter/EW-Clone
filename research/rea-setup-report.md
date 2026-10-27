# REA Setup Report

Date: 2026-10-26
Scope: Stage 1 only. No EasyWorship files were inspected, modified, or executed.

## Result

REA is installed, registered with OpenCode, and operational.

- REA package: `rea-agents@6.8.0`
- MCP server identity reported over stdio: `rea` 6.8.0
- MCP protocol negotiated: `2025-06-18`
- Advertised MCP catalog: 229 tools, 6 prompts
- OpenCode registration state: `aligned`
- Installed REA skill: version 49, `aligned`, 229 tools
- Scoped readiness: `rea doctor --client opencode --skill` returned `healthy: true`

The only remaining action for interactive use is restarting OpenCode so the newly
registered MCP server is loaded into a live session.

## Host environment

| Item | Observed value |
| --- | --- |
| OS | Microsoft Windows NT 10.0.26200.0 (Windows 11), AMD64 |
| PowerShell | 5.1.26100.9168 |
| OpenCode | 1.19.0 |
| Git | 2.52.0.windows.1 |
| .NET SDK | 9.0.308 |
| Python | 3.14.3 |
| System Node.js | v25.6.0, npm 11.8.0 |
| Administrator | No |
| Network | nodejs.org, npm registry, and github.com reachable |

The installed system Node.js v25.6.0 is **not** supported by REA 6.8.0. REA's npm
engine constraint is `^22.23.4 || ^24.22.1 || >=26.12.1`. The system Node.js was
left untouched.

## Installation method

A private, user-local Node.js 24.22.1 runtime was installed so REA runs on a
supported release without changing the system Node.js installation.

1. Downloaded the official Windows x64 archive:
   `https://nodejs.org/dist/v24.22.1/node-v24.22.1-win-x64.zip`
2. Verified SHA-256 against `SHASUMS256.txt`:
   `d2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5`
3. Extracted to:
   `C:\Users\iKay\.local\node24\node-v24.22.1-win-x64`
4. Installed REA globally with that runtime's npm:
   `npm install --global rea-agents@6.8.0`
5. Allowed and ran the Frida install script so its native binding was published:
   `npm install --global rea-agents@6.8.0 --allow-scripts=frida --foreground-scripts`
6. Verified Frida loads:
   `require('frida')` returned usable exports from
   `...\node_modules\rea-agents\node_modules\frida`

Installed versions:

- Node.js: `v24.22.1`
- npm: `11.19.1`
- REA: `6.8.0`
- REA MCP SDK: `2.3.1`
- Frida: `17.23.2`

System Node.js and unrelated global npm packages were not changed or removed.

## Configuration

### OpenCode

File: `C:\Users\iKay\.config\opencode\opencode.jsonc`

Added a local MCP server named `rea`:

- Command:
  `C:\Users\iKay\.local\node24\node-v24.22.1-win-x64\node.exe`
  `C:\Users\iKay\.local\node24\node-v24.22.1-win-x64\node_modules\rea-agents\scripts\rea.mjs`
  `mcp`
- Enabled: `true`
- Timeout: `600000` ms

The timeout was added manually after setup because OpenCode defaults MCP requests
to 30 seconds, which is shorter than typical REA native-analysis operations. REA
still reports the registration as `aligned` with this value.

Backup created by setup:

- `C:\Users\iKay\.config\opencode\opencode.jsonc.rea.backup`

The pre-existing `vision` MCP entry was preserved. Its stored credential is not
reproduced in this report. No unrelated OpenCode settings were edited.

### Skill

- Location: `C:\Users\iKay\.agents\skills\reverse-engineer-anything`
- Installed version: 49
- State: aligned
- OpenCode documents `~/.agents/skills` as a global agent-compatible discovery
  path, so the installed skill is on OpenCode's discovery path.
- `SKILL.md` frontmatter name matches the directory (`reverse-engineer-anything`).

### Environment variables

No REA-specific environment variables were required for the OpenCode
registration. The registration uses absolute paths to Node.js and `rea.mjs`.

### CLI discoverability

`rea` is currently **not** on the system `PATH`; the documented CLI executable is:

`C:\Users\iKay\.local\node24\node-v24.22.1-win-x64\rea.cmd`

No `PATH` change was made, to avoid altering unrelated shell behavior.

## Verification performed

| Test | Result |
| --- | --- |
| `rea --version` | `6.8.0` |
| `rea doctor --client opencode --skill --json` | `healthy: true`, registration `aligned`, skill `aligned` |
| `opencode mcp list` | `rea connected` using the registered absolute command |
| Direct MCP `initialize` over stdio | Server `rea` 6.8.0, protocol `2025-06-18` |
| Direct MCP `tools/list` | 229 tools; `binary_session` present |
| Direct MCP `tools/call` -> `binary_session` `{}` | Succeeded; reported open .NET static and artifact providers |
| Frida binding load test | `frida loaded function` |
| `inspect-managed-artifact` on `C:\Program Files\dotnet\sdk\9.0.308\dotnet.dll` | Succeeded; classified managed PE, `modern-dotnet`, x86_64, CIL + ReadyToRun, complete metadata |
| `inspect-desktop-application` on `C:\Program Files\nodejs` | Succeeded; returned root identity, files, candidates, components, and next probes |
| Existing OpenCode config preservation | `vision` entry retained; setup backup present |

## Available capabilities and backends

Confirmed available and exercised:

- .NET / PE/CLI static inspection, including metadata, member inventory, CIL
  anchors, declared native boundaries, and assembly references
- Safe desktop-application directory and header inspection
- Artifact graph inspection and evidence generation
- Frida native binding availability for runtime observation

Advertised by the running server (not every operation exercised):

- JavaScript / Electron application analysis
- Browser and web-page observation
- HAR / SAZ / network-capture inspection
- SQLite schema and row inspection
- PE imports, resources, certificates, load configuration, and binary layout
- Process capture and runtime observation workflows
- Managed application graph projection and managed/native boundary verification

Unavailable at setup time:

- Hopper provider: not installed
- Ghidra provider: `GHIDRA_INSTALL_DIR` not set
- IDA provider: no registration
- JADX provider: no JAR configured (Android only, not relevant yet)
- Kuna provider: not configured

The .NET static provider does not load target assemblies, resolve dependencies
through a CLR, decompile C#, or execute target code. It provides metadata, IL,
and boundary evidence. Native decompilation is not available until a native
provider is configured.

## Known limitations and unresolved issues

1. **OpenCode restart required for live MCP tools.** The current OpenCode
   process started before the registration was written. The server is verified
   through the CLI and direct MCP handshake, but a new OpenCode session is needed
   for the agent runtime to expose the 229 REA tools.
2. **No native analysis engine.** Ghidra, Hopper, IDA, and Kuna are unconfigured.
   This does not block managed .NET inspection, desktop artifact inspection, or
   JavaScript analysis. Configure Ghidra (or another provider) only if native
   decompilation is required during Stage 2.
3. **REA CLI is not on `PATH`.** Use the absolute `rea.cmd` path above, or add the
   portable Node.js directory to `PATH` as a separate, deliberate change.
4. **Supported Node.js is private to this installation.** REA's registration and
   global package depend on
   `C:\Users\iKay\.local\node24\node-v24.22.1-win-x64`. Removing or moving that
   directory breaks the registration.
5. **System Node.js remains unsupported.** OpenCode itself runs on the system
   v25.6.0 and works; only REA requires the private v24.22.1 runtime.
6. **Frida runtime observation not exercised on a live target.** Only the native
   binding load was verified.
7. **Unrelated MCP entries have their own status.** A pre-existing `vision`
   server still reports a connection failure. It was not modified and is not part
   of REA.
8. **No target inspection yet.** EasyWorship was not located or inspected in
   Stage 1.

## Stage 2 readiness

Stage 1's completion gate is met: REA's OpenCode integration is configured and
verified, the MCP server initializes, the full 229-tool catalog is advertised,
and harmless operations succeed. Relevant managed-code and desktop inspection
backends are operational.

Before using REA tools interactively, restart OpenCode (or start a new session)
so the `rea` MCP server is loaded. If the server does not appear, run:

```powershell
opencode mcp list
& "$env:USERPROFILE\.local\node24\node-v24.22.1-win-x64\rea.cmd" doctor --client opencode --json
```
