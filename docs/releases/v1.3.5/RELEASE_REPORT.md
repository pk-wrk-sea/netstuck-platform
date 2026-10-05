# NetStuck v1.3.5 release verification

The owner requested GitHub publication of the [UI improvements](../../UI_RESOLUTION_THEME_IMPROVEMENT_REPORT.md). The initial v1.3.4 tag failed release packaging in Traceroute lifecycle tests: Stop updated an autosized label without a WinForms synchronization context. That run completed 178 checks and 5/12 suites with one infrastructure failure and produced no release assets. [The failed candidate report](../v1.3.4/RELEASE_REPORT.md) preserves its separate evidence; its passing local/PR/main runs do not override the failed release run.

v1.3.5 retains the UI improvements and repairs Stop's UI context before its first await. The timeout display also checks run ownership and page disposal. A deterministic regression clears the caller's synchronization context and observes the timeout label's thread: before the repair it updated on thread 4 instead of UI thread 1. All failures are observed without an unhandled async event or test-state leak. The lifecycle floor increases from 31 to 32; the full runner now requires 424 checks.

## Fresh validation

Final `scripts/Test-NetStuck.ps1 -SoakSeconds 10` runs against 1.3.5:

| Host | Passed / discovered | Failed / skipped / infrastructure | Required suites / build stages |
| --- | --- | --- | --- |
| Windows PowerShell 5.1.26100.3624 (package run) | 424 / 424 | 0 / 0 / 0 | 12 / 12; 2 / 2 |
| PowerShell 7.6.5 | 424 / 424 | 0 / 0 / 0 | 12 / 12; 2 / 2 |

| Fresh local measurement | PowerShell 5.1 | PowerShell 7 |
| --- | --- | --- |
| Warm UI startup | 1,501 ms | 1,533 ms |
| Worst UI dispatch under /24 load | 40 ms | 26 ms |
| Worst dual-Traceroute UI dispatch | 52 ms | 59 ms |
| Working set in performance suite | 77 MB | 80 MB |
| Ping 250 ms vs 1000 ms, timeout 1000 ms | 12 vs 3 completions | 10 vs 3 completions |
| Traceroute 250 ms vs 1000 ms, timeout 1000 ms | 14 vs 4 samples | 14 vs 4 samples |
| Soak duration / worst dispatch / memory growth | 11.440 s / 17 ms / 11 MB | 11.440 s / 55 ms / 11 MB |

The focused lifecycle suite passed 32/32; the repaired timeout label updates on UI thread 1. Targeted lifecycle stress passed 50/50 cycles with quiescence, UI-thread mutation and owned-state cleanup. The source-input fingerprint is `b330283f137bdb062accaa04fab32298680364c43bc6080c11d185710bf2aa40`; executable version is `1.3.5.0`.

An initial PowerShell 5.1 package run failed the unchanged startup threshold: 2,516 ms versus a required value below 2,500 ms (354/355 checks passed, 8/12 suites completed). Its log/summary remain under `artifacts/release-v1.3.5/*startup-failure*`. The final full package run above passed at 1,501 ms; no threshold or test was weakened. Earlier v1.3.4 totals/fingerprints are not counted as v1.3.5 validation. These are host/run measurements, not universal performance promises.

Local portable packaging passed its exact inventory, per-file SHA256 and extracted-content equivalence gates. PuTTY 0.80 retains its required SHA256 and verified signature. Fresh summaries, full output, focused before/after failure evidence, stress output, package provenance and staged startup smoke are retained under ignored `artifacts/release-v1.3.5/` and `artifacts/release/`.

Staged startup smoke passed all seven checks, including a responsive native window, graceful zero exit, credential/profile-path checks and complete owned-state cleanup. `Capture-UiFoundations.ps1 -DeterminismRuns 5` passed all nine scenarios across all five runs, with semantic/PNG/privacy checks and promotion of the 1.3.5 screenshot set. No majority selection was used. Normal/minimum versioned screenshots were inspected; native popup/terminal coverage remains the separately described UI audit.

## Display and publication boundary

The UI audit covers eight pages in Light/Dark at normal/minimum sizes on a 96-DPI host, with native review of Ping, dropdowns, custom dialogs and Collector scrolling/terminal. The release regenerates its nine canonical screenshots with a strict five-run hash comparison and pixel/geometry stabilization; it does not select a majority image.

Real Windows Scale 125/150/200%, mixed-DPI monitor moves/docking/RDP, Windows accessibility text size, High Contrast and screen-reader traversal remain **UNVERIFIED**. Publication authorization is not an observed manual PASS or a historical waiver.

Tag automation tests/packages the immutable candidate, runs startup smoke and stages a draft. Publication evidence on the GitHub Release records PR/main/tag CI, the accepted workflow package identity, clean-tag source inventory binding, downloaded asset/manifest verification and a startup smoke against the downloaded executable. Binary identity is taken from that candidate's provenance, not inferred from another host's compilation or a historical build.
