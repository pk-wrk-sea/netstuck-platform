# Testing

## One-command baseline

```powershell
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

Run the command under both supported hosts (`powershell.exe` 5.1 and `pwsh.exe` 7.x). It compiles the app and every test harness into the ignored `artifacts` directory, then runs them from isolated state roots. The runner requires two stages and ten suites exactly once, preserves native stdout/stderr/exit evidence, enforces the authoritative per-suite minimum map, and writes a schema-3 JSON summary whose verdict and process exit come from the same reconciliation result.
It deliberately switches the test console to BOM-free UTF-8 so the Collector integration test reproduces GitHub runner input behavior.
The isolated state override also suppresses live startup NTP/Public-IP calls; this prevents shared-runner network variance and teardown races from contaminating UI cadence measurements.
After all cadence resources are closed and results are flushed, that harness exits explicitly to avoid a legacy CLR/native Ping finalizer race observed only during Windows Server 2025 process teardown.

## Required stages and suites

| Stage | Coverage |
| --- | --- |
| `Test host compilation` | Explicit test/app input compilation with `/noconfig`, `/nostdlib+` and resolved framework references. |
| `Development build` | Production six-source allowlist, build provenance output and `NetStuck.exe` version. |

| Suite | Coverage |
| --- | --- |
| `Test runner infrastructure` | Native success/failure, stderr, parsed FAIL, cleanup, mandatory inventory, per-suite floor negative and repository state-residue scan. |
| `NetOpsCoreTests` | Target parsing, CIDR rules, MAC extraction, subnet calculation, units and CSV escaping. |
| `FeatureTests` | UI/integration behavior, persistence, Ping/Trace/DNS, Collector authentication/transport/export, layout and caching. |
| `TracerouteLifecycleTests` | Real MainForm route/event tables, UI-thread dispatch, zero/one/multiple delayed probes, fault during Stop, derived timeout failure, obsolete run, restart gating, page disposal, active close, exact task observation and fail-closed state cleanup. |
| `UiFoundationTests` | Pilot token/state/action contracts, accessibility, semantic state, layout, interaction and privacy. |
| `PerformanceTests` | Startup, `/24` load, UI dispatch, queue draining, dual Traceroute and memory. |
| `PollingCadenceTests` | Bounded overlap and observable 250 ms versus 1000 ms cadence. |
| `OvernightSoakTests` | Packet loss, route/DNS changes, TACACS rejection, VTY limits, terminal pressure, memory and UI responsiveness. |
| `Capture-UiFoundations infrastructure` | Semantic/current-run evidence, pre/post-promotion rollback, forced rollback failure, exact screenshot inventory, PNG decoding/structure/order/CRC/truncation negatives and privacy. |
| `Build provenance infrastructure` | Production allowlist drift, test-source exclusion, atomic actual/normalized argv, special-character vectors, binary serialization/order/content/display negatives, reference identity, binary drift, package-input drift, toolchain classification and checkout relocation. |

The current authoritative unchanged-behavior corpus is `130/130`: NetOpsCore 16, Feature 93, Performance 10, PollingCadence 3 and OvernightSoak 8. The current complete runner inventory is `292` checks across ten suites: runner 10, NetOpsCore 16, Feature 93, Traceroute lifecycle 31, UI foundation 63, Performance 10, Polling cadence 3, Soak 8, capture infrastructure 39 and build provenance 19. These values are the current floors in the runner; more checks remain valid. Historical `128`, `179`, `207`, `217` and `247` totals remain historical evidence and are not rewritten.

Run the deterministic Traceroute lifecycle stress after lifecycle changes; every cycle owns a form, probe gate, tables, task registry and temporary state root:

```powershell
.\artifacts\test\TracerouteLifecycleTests.exe --cycles 50
```

Run the screenshot closure gate serially with `-DeterminismRuns 5`. All nine scenario hashes must match across all five candidates; the script fails before publication on the first set mismatch and never selects a majority image.

The soak harness defaults to eight hours when invoked directly. Always pass `--seconds 10` or a deliberate duration for local validation:

```powershell
.\artifacts\test\OvernightSoakTests.exe --seconds 60
```

## UI acceptance gates

Automated compilation and startup are not sufficient for a UI release. For affected menus:

- Capture normal-width and 1100-pixel-minimum screenshots.
- Check 125%, 150% and 200% Windows scaling when layout changes are material.
- Open dropdowns to verify they do not cover related inputs.
- Exercise filter, sort, copy, row selection, column resize/reorder and manual scroll.
- Verify Start/Pause/Stop state colors/text and the green running indicator.
- Keep the cursor/scroll position stable while a polling session updates.

## Performance acceptance

- 250 ms polling must be materially faster than 1000 ms under a 1000 ms timeout.
- Outstanding work must remain bounded.
- `/24` Live Ping and two Traceroute sessions must keep UI dispatch responsive.
- Collector terminal queues must drain without per-line redraw.
- Large config capture must remain streamed and memory-bounded.

Performance values vary by machine. Record fresh measurements in the release report rather than treating historical numbers as universal thresholds.

## Test hygiene

- Tests use synthetic credentials such as `company\myname` and `testpass`; never replace them with real credentials.
- Test-owned state must live under a unique OS-temporary `NETSTUCK_TEST_ROOT`; cleanup failure is a test failure. Ignoring a sensitive state file is not acceptable cleanup.
- Some UI tests use reflection against private members. A field/method rename may require test updates even when visible behavior is unchanged.
- Physical pointer, native dropdown, modal-dialog, High Contrast, screen-reader and non-96-DPI acceptance remain manual unless a real interactive session is explicitly recorded; automated metadata, `DrawToBitmap` and synthetic mouse routing do not convert those gates to PASS.
