---
name: netstuck-maintainer
description: Maintain, diagnose, test, package, or release the NetStuck Windows network toolbox while preserving its polling cadence, WinForms layout, Config Collector credential safety, and portable release structure. Use for changes inside the NetStuck repository; do not use for unrelated network tools.
---

# NetStuck Maintainer

Start by reading [repository instructions](../../../AGENTS.md). Read only the task-relevant project references:

- UI, polling, persistence or collector internals: [architecture](../../../docs/ARCHITECTURE.md)
- Local setup and ordinary edits: [development](../../../docs/DEVELOPMENT.md)
- Regression, visual or performance work: [testing](../../../docs/TESTING.md)
- State, external APIs or logs: [privacy](../../../PRIVACY.md) and [security](../../../SECURITY.md)
- Version/package work: [versioning](../../../docs/VERSIONING.md) and [releasing](../../../docs/RELEASING.md)

## Work from the validated baseline

- Treat v1.2.3 as the baseline until the user explicitly requests an upgrade.
- Inspect `git status` and preserve unrelated changes.
- Locate the active implementation before editing; historical builders and version-suffixed methods coexist in the partial `MainForm`.
- Make the smallest coherent change and keep documentation/test expectations synchronized.

## Preserve critical behavior

- Keep polling on a monotonic fixed cadence with bounded outstanding work and stale-result protection.
- Keep high-frequency Ping/Trace/Collector UI updates batched.
- Preserve Traceroute destination termination, changed-row updates, selection/scroll state, two sessions and the non-overlapping v1.2.3 grid.
- Preserve one literal backslash in `DOMAIN\username`, AUTH1-before-AUTH2 behavior and credential-free argv/state/logs.
- Never add runtime state, real IP inventories, usernames, device configs or exported logs to the repository.

## Validate proportionally

Run focused checks while iterating. Before handing off a source change, run:

```powershell
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

For UI work, also inspect normal and minimum-width screenshots and exercise dropdowns, scroll, selection, copy, filter and column resizing as applicable. For a release, follow every gate in the release guide, test the packaged executable from its portable folder and report fresh totals/measurements.

Do not push, publish a release, delete an old workspace, or change external state unless the user authorized that action for the current task.
