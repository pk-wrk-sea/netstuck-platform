# NetStuck v1.3.4 release verification

**Not published.** The tag's Portable Release run [37290733085](https://github.com/pk-wrk-sea/netstuck-platform/actions/runs/37290733085) stopped in Traceroute lifecycle tests with `Error creating window handle` during a Stop timeout label update. It completed 178 checks, 5/12 suites, with one infrastructure failure; no ZIP or draft was uploaded. PR and main CI passed 423/423, but that does not override this failed release run. A deterministic follow-up reproduced Stop state updates on thread 4 instead of UI thread 1. The repaired candidate is v1.3.5; the v1.3.4 tag is retained as failed-candidate history.

The owner requested a new GitHub release after the [UI improvement report](../../UI_RESOLUTION_THEME_IMPROVEMENT_REPORT.md). The release includes those repairs, synchronized 1.3.4 metadata, 91 new UI checks and draft-first release staging with packaged startup verification. Historical v1.3.2/v1.3.3 test skips do not apply to this candidate.

## Candidate and current checks

Fresh local runs of `scripts/Test-NetStuck.ps1 -SoakSeconds 10` against 1.3.4:

| Host | Passed / discovered | Failed / skipped / infrastructure | Required suites / build stages |
| --- | --- | --- | --- |
| Windows PowerShell 5.1.26100.3624 | 423 / 423 | 0 / 0 / 0 | 12 / 12; 2 / 2 |
| PowerShell 7.6.5 | 423 / 423 | 0 / 0 / 0 | 12 / 12; 2 / 2 |

| Fresh local measurement | PowerShell 5.1 | PowerShell 7 |
| --- | --- | --- |
| Warm UI startup | 1,244 ms | 1,192 ms |
| Worst UI dispatch under /24 load | 22 ms | 19 ms |
| Worst dual-Traceroute UI dispatch | 57 ms | 30 ms |
| Working set in performance suite | 82 MB | 78 MB |
| Ping 250 ms vs 1000 ms, timeout 1000 ms | 11 vs 3 completions | 11 vs 3 completions |
| Traceroute 250 ms vs 1000 ms, timeout 1000 ms | 14 vs 4 samples | 14 vs 4 samples |
| Soak duration / worst dispatch / memory growth | 11.480 s / 18 ms / 13 MB | 11.447 s / 13 ms / 11 MB |

The local source-input fingerprint is `5c89714f5a1ac5de044b400388caa187e215657871a8b1d8c23b485891a42f67`; assembly/file version is `1.3.4.0`. Both final supported-host runs include the snapshot-harness repair below and pass the existing 130-check unchanged-behavior corpus and the 91 new layout/theme checks. Full logs and JSON summaries are retained under ignored `artifacts/release-v1.3.4/`; earlier/pre-repair runs are retained separately. Measurements describe this host/run, not universal performance guarantees.

The local package passed exact file inventory, per-file SHA256 and extracted-ZIP content equivalence. PuTTY 0.80 matches the required SHA256 and a valid Simon Tatham Authenticode signature. Staged startup smoke passed all seven checks: responsive window, graceful close, zero exit, owned state, no operator-profile path, no credential state, and complete temporary-state cleanup.

The 1.3.4 screenshot closure initially failed at run 2/5, then 3/5, on the minimum-size empty Event Log. Saved diagnostic runs showed the difference was native horizontal-scrollbar pixels; the prior wait checked geometry only. The snapshot harness now waits for geometry and rendered pixels to stabilize and saves the already verified bitmap. The strict gate then passed all nine scenarios across five runs, including semantic/PNG/privacy checks; it never masks pixels or selects a majority image. Failure logs and diagnostic images remain separate under `artifacts/release-v1.3.4/`.

This pre-commit local package is preparation evidence, not the final GitHub asset identity. The release workflow rebuilds the immutable tag, records its HEAD/source/compiler/package fingerprints, tests and smokes that candidate, and stages it as a draft. Windows PR/main CI and tag-release results, downloaded-candidate hashes and smoke evidence are recorded after execution in the release's publication evidence. Source byte fingerprints may differ across checkout line endings; each candidate retains its own provenance and must be bound to the tag's exact input inventory.

## Manual acceptance boundary

The prior UI audit covered all eight pages at 1460×900 and 1100×700 in Light/Dark through geometry/structural capture review on a 96-DPI desktop. Native review included Ping, theme/protocol dropdowns, hop/column dialogs and Collector terminal/settings scrolling. The 1.3.4 capture gate regenerates the versioned canonical screenshots.

Real Windows Scale 125%, 150%, 200%, mixed-DPI monitor moves/docking/RDP, Windows accessibility text size, High Contrast and screen-reader traversal remain **UNVERIFIED**. The owner's publication request authorizes the release with these stated boundaries; it is not a recorded manual PASS or a transfer of a historical waiver. Portable packaging, normal/minimum geometry, text zoom and automated tests cannot prove these display configurations.

The release workflow stages a draft after canonical regression/package integrity and startup smoke. Downloaded assets must pass hash, exact inventory, manifest and startup verification before the draft is published as Latest. Post-download publication evidence is attached to the GitHub Release so it can describe the immutable tagged candidate without modifying its source history.
