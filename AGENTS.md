# NetStuck repository instructions

These instructions apply to the entire repository.

## Baseline and scope

- The maintained baseline is NetStuck v1.2.3 on Windows/.NET Framework 4.x.
- Do not change the application version unless the user explicitly requests a version upgrade.
- Preserve the current WinForms interaction model and portable-folder distribution.
- Treat runtime state, collector captures, exported logs, usernames, IP lists and hop descriptions as sensitive operator data. Never add them to Git.

## Read before changing code

- Read `docs/ARCHITECTURE.md` for file ownership and runtime flows.
- Read `docs/TESTING.md` before changing polling, layout, persistence or Config Collector code.
- Read `PRIVACY.md` before changing external lookups or stored state.
- Read `docs/RELEASING.md` before changing a version or producing a package.

## Non-negotiable behavior

- Live Ping and Traceroute use fixed monotonic cadence with bounded overlap. A 250 ms interval must remain materially faster than 1000 ms even when timeout is 1000 ms.
- Late probe completions must not overwrite a newer displayed status.
- Traceroute must stop at the reached target, preserve scroll/selection and update only hops that changed.
- Keep two independent Traceroute sessions and the v1.2.3 non-overlapping input layout.
- Preserve literal `DOMAIN\username` handling, AUTH1-before-AUTH2 fallback rules and password-free process arguments/state/logs.
- Keep UI updates batched; do not bind high-volume probe/terminal events directly to per-event redraws.
- Preserve the 1100×700 minimum window behavior and verify narrow/high-DPI layouts visually for layout changes.

## Implementation boundaries

- Active code is a `partial MainForm` across `src/NetStuck/NetStuck.cs`, `NetStuck.V103.cs`, `NetStuck.Release1.cs` and `NetStuck.Features.cs`.
- Tests use reflection against private member names. Rename fields/methods only when tests and persistence compatibility are addressed together.
- Do not add secrets or credentials to command lines, exception text, diagnostics, state files or fixtures.
- Do not commit executables or DLLs. PuTTY Plink belongs in a release ZIP with its license and verified hash.

## Required validation

- For ordinary source changes, run the focused suite plus `scripts/Test-NetStuck.ps1 -SoakSeconds 10` before handoff.
- For UI changes, inspect a screenshot at normal width and the 1100-pixel minimum width; include dropdown-open state when controls can expand.
- For release changes, follow every gate in `docs/RELEASING.md` and confirm a packaged-executable startup smoke test.
- Report exact pass/fail totals and measured performance; do not copy stale numbers from an older report.
