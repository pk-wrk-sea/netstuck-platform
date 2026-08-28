# Development

## Requirements

- Windows 10 or 11
- .NET Framework 4.x compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
- PowerShell 5.1 or later
- PuTTY `plink.exe` only when exercising or packaging Config Collector SSH

Python and the modern .NET SDK are not required.

## Build

```powershell
.\scripts\Build-NetStuck.ps1
```

Outputs: `artifacts\build\NetStuck.exe` and `artifacts\build\NetStuck.build-provenance.json`.

The production source inventory is the explicit six-file allowlist in `scripts\NetStuck.BuildProvenance.ps1`. The build disables `CSC.RSP` and the default standard library with `/noconfig` and `/nostdlib+`, supplies `mscorlib` plus every framework reference explicitly, and records separate source-input, toolchain, normalized-invocation and reference-input fingerprints. One ordered argument specification emits both the actual `csc.exe` argv and its path-normalized one-to-one representation. The invocation fingerprint uses binary count/index/UTF-8-length/UTF-8-byte serialization; diagnostic command-line quoting is not identity. Do not add wildcard source discovery or a second compiler-argument builder.

`build_windows.bat` is a convenience wrapper around the same command.

## Development workflow

1. Confirm `git status` and preserve unrelated user changes.
2. Read the feature owner and invariants in `ARCHITECTURE.md` and `AGENTS.md`.
3. Make the smallest coherent change. Do not bump the version for an ordinary fix unless requested.
4. Run focused checks while iterating.
5. Run the complete baseline command before handoff:

   ```powershell
   .\scripts\Test-NetStuck.ps1 -SoakSeconds 10
   ```

6. Visually inspect relevant screens when layout, grid behavior, colors, zoom or activity indicators change.

## UI guidance

- Keep the interface minimal and preserve prominent inputs and status visibility.
- Result grids must remain filterable/copyable and columns resizable/reorderable where currently supported.
- Avoid fixed-width combinations that exceed their parent. Use explicit table columns, minimum sizes and validated splitter constraints.
- Preserve user scroll and selection during polling refresh.
- Use UI batching for high-frequency results. Increasing resource use does not justify per-result redraw.

## Runtime diagnostics

For isolated UI or runtime tests, set `NETSTUCK_TEST_ROOT` to a newly created, test-owned temporary directory. It takes precedence over `NETSTUCK_TEST_STATE_PATH`, redirects `state.json`, `profiles.json`, `mac-vendors.json` and `trace-lookups.json`, and suppresses startup NTP/public-IP refresh. The test owner must remove that exact directory and treat cleanup failure as a test failure.

`NETSTUCK_TEST_STATE_PATH` remains the legacy narrow override when `NETSTUCK_TEST_ROOT` is unset. It redirects `state.json`, suppresses startup NTP/public-IP refresh and causes `trace-lookups.json` to use the effective state file's directory. Profiles and `mac-vendors.json` continue to use LocalAppData. New tests must use `NETSTUCK_TEST_ROOT` so all four test-owned files share one fail-closed cleanup boundary.

Never diagnose against or copy files from a production collector-output folder into this repository.

## UI foundation verification

Run the complete suite under each supported PowerShell family. The runner records the host executable/version, every mandatory suite and stage, native exit codes, parsed totals, infrastructure failures and one authoritative `Overall` verdict in its current-run JSON summary:

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Test-NetStuck.ps1 -SoakSeconds 10

pwsh.exe -NoLogo -NoProfile `
  -File .\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

A successful run requires every mandatory suite/stage exactly once, zero parsed failures, zero required skips, zero infrastructure failures and matching child exit semantics. Stage-only output is not the final verdict. Expected negative fixtures may write `stderr` and exit nonzero; the parent asserts that exact diagnostic and exit code without weakening `$ErrorActionPreference`.

The current runner has two mandatory stages and ten mandatory suites. `scripts/Test-NetStuck.ps1` is the single authoritative source for per-suite minimum counts; falling below any floor fails the suite, JSON verdict and process exit even if every discovered check passed. Its unchanged-behavior baseline remains `130/130`; P2 closure infrastructure raises the complete current inventory to `292` without deleting or weakening historical checks.

Regenerate the authoritative Phase A screenshot set only after the suite passes. Normal capture requires two fresh isolated hash-identical runs; the closure gate uses five:

```powershell
.\scripts\Capture-UiFoundations.ps1 -DeterminismRuns 5
```

The capture host selects each target page before interaction, asserts the intended semantic state, freezes the capture-only clock at `2000-01-02 03:04:05 ICT`, normalizes focus/scroll/grid selection, and waits for a stable observable layout signature before and after a warm render. Each candidate is isolated. Publication validates a complete promotion, snapshots prior hashes and retains an owned rollback copy until post-publish validation passes. Any pre/post-promotion failure restores the exact prior hash set and removes owned promotion/backup state; a rollback failure is explicit and retains recoverable evidence. Production time behavior is unchanged.

Packaged startup smoke must use an owned temporary state root and fail closed on process or cleanup residue:

```powershell
.\scripts\Test-PackagedSmoke.ps1 -ExecutablePath .\artifacts\release\NetStuck-v.1.2.3\NetStuck.exe
```

Never place smoke `state.json` under `artifacts` or retain operator-profile-derived state. The canonical runner scans repository test-state files for operator-profile and credential content.

The Phase A state presenter intentionally contains only states used by the shell, Calculators or Event Log. The unused determinate-progress contract found during Round 2 was deleted; progress remains deferred until an authorized production consumer exists.

Packaging records source, toolchain, compiler invocation, explicit references, package inputs, decompressed content and ZIP container separately. `SHA256SUMS.txt` covers every intended pre-manifest package file; `NetStuck-v.1.2.3.provenance.json` is an external sidecar created only after package bytes are final, avoiding a self-referential package fingerprint. ZIP hash differences alone do not prove content differences; compare the decompressed content fingerprint and per-file raw hashes.
