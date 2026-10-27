# Mission: Set Up REA, Then Reconstruct EasyWorship

You are operating as an autonomous software engineering and reverse-engineering agent through OpenCode.

Your goal is to build a Windows desktop application that reproduces EasyWorship's functionality and observable behaviour as closely as possible, using **Reverse Engineer Anything (REA)** as the primary reverse-engineering toolkit.

This is a multi-stage project. Complete each stage in order. Do not skip prerequisites, fabricate tool capabilities, or start implementing the target application prematurely.

## Stage 1: Install and configure REA

**This stage is mandatory. Nothing else takes priority until REA is operational.**

### Step 1.1 — Inspect the environment

Determine:
- Operating system and architecture.
- Available runtimes, package managers, and development tools.
- Whether OpenCode is installed and operational.
- Whether REA is already installed or partially configured.
- Existing project instructions and agent configuration.
- Whether Git is available.
- Available permissions and network access.

Do not assume a tool is installed simply because its name appears in a configuration file.

### Step 1.2 — Obtain the official REA instructions

Use REA's official repository:

https://github.com/morluto/rea

Consult its current README, installation instructions, prerequisites, supported integrations, and configuration documentation.

Determine the correct installation procedure for the current environment and OpenCode setup.

**Do not invent installation commands, configuration keys, package names, or REA capabilities.** Verify each against official documentation or the actual repository.

If the repository redirects to different documentation, follow the official project's documented location.

### Step 1.3 — Install REA

Follow the documented installation process.

1. Install any required prerequisites that are missing and that you have permission to install.
2. Install REA using its documented method.
3. Configure its integration with OpenCode as documented.
4. Configure any required analysis tools or supported backends.
5. Ensure the relevant configuration is available to the environment in which OpenCode executes.
6. Preserve existing working configuration. Make targeted changes instead of overwriting unrelated settings.
7. Record installation and configuration changes.

If installation requires an interactive login, elevated permissions, user approval, or an unavailable dependency, stop at that specific blocker and explain what is required. Do not claim installation succeeded when it has not.

### Step 1.4 — Verify REA operationally

Do not rely solely on a successful installation command.

Verify that:
- REA is discoverable through the documented interface.
- OpenCode can access its integration.
- Required tools are registered and callable.
- A harmless test operation succeeds.
- Any required backend or analysis engine is operational.
- Failures and missing capabilities are identified.

Use an appropriate non-destructive test supported by the actual REA documentation.

Produce `research/rea-setup-report.md` containing:
- Installed version or verified revision.
- Installation method.
- Configuration locations, without exposing secrets.
- Available tools and backends.
- Verification tests and their results.
- Known limitations and unresolved issues.

**Completion gate:** Proceed only when REA's required integration has been verified. If it cannot be made operational, report the exact blocker and wait rather than silently switching to an unrelated workflow.

## Stage 2: Inspect EasyWorship

Begin this stage only after Stage 1 is complete.

Identify the installed EasyWorship version, executable architecture, runtime, dependencies, supporting files, and available application data.

Use REA's actual capabilities to determine which investigation methods are appropriate. These may include static analysis, resource inspection, runtime observation, debugging, decompilation, or tracing, depending on the target and the available tools.

Do not assume every technique is supported or necessary.

Use read-only inspection and isolated test copies wherever practical. Do not modify the original installation or its data during experiments.

## Stage 3: Reverse-engineer the application

Build an evidence-backed feature inventory and investigate the application subsystem by subsystem.

Prioritise:
1. Main interface, panels, menus, and shortcuts.
2. Song and content libraries.
3. Schedule and slide management.
4. Preview and live presentation output.
5. Multi-monitor and projector support.
6. Text, backgrounds, transitions, and rendering.
7. Video, audio, and image playback.
8. Bible passages and translation integration.
9. Stage display and other confirmed advanced features.
10. Settings, persistence, import/export, and error recovery.

For each feature, document its observable behaviour, relevant findings, confidence level, and remaining unknowns. Distinguish confirmed evidence from hypotheses.

Create:
- `research/feature-matrix.md`
- `research/behavioural-specification.md`
- `research/architecture-findings.md`
- `research/known-unknowns.md`
- `research/test-cases.md`

Do not fabricate internal implementation details when the evidence is insufficient.

## Stage 4: Design the reconstruction

After the investigation, choose an appropriate Windows desktop architecture.

Prefer C# and modern .NET, evaluating WPF or another suitable framework against the requirements for multi-monitor rendering, video playback, responsiveness, and maintainability.

Keep the operator interface, preview, live output, media playback, scheduling, and persistence logically separated.

Build independently from verified behaviour. Do not copy proprietary source code, branding, artwork, or protected content. Respect applicable licences and technical access restrictions.

## Stage 5: Implement incrementally

Implement and verify:
1. Desktop shell.
2. Local content library.
3. Song and schedule management.
4. Slide sequencing.
5. Preview and live output.
6. Secondary-monitor support.
7. Text and background rendering.
8. Media playback.
9. Confirmed advanced features.
10. Fidelity, reliability, and performance improvements.

Build and test after each meaningful change. Compare equivalent workflows against the installed EasyWorship version and document differences.

Do not claim complete or pixel-perfect reproduction without sufficient evidence and testing.

## Execution rules

- Execute the stages in order.
- Complete setup tasks rather than merely describing them.
- Use available tools directly when authorised.
- Ask for user intervention only when genuinely required.
- Never claim a command, installation, or test succeeded without verifying its outcome.
- Never invent tool names, installation steps, or analysis results.
- Preserve existing configurations and unrelated project files.
- Maintain progress in project documentation so work can resume across sessions.
- If blocked, report the precise blocker, its cause, and the next required action.

## Immediate task

**Execute Stage 1 only.**

Inspect the environment, consult REA's official installation instructions, install and configure REA for OpenCode, and verify that its integration works.

Do not inspect EasyWorship, design the reconstruction, or write application code yet.

When Stage 1 is complete, provide a concise report of the actual installation, configuration, verification results, and any remaining limitations.