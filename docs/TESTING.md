# Testing

## One-command baseline

```powershell
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

The command compiles the app and every test harness into the ignored `artifacts` directory, then runs them from an isolated test working directory.

## Suites

| Suite | Coverage |
| --- | --- |
| `NetOpsCoreTests` | Target parsing, CIDR rules, MAC extraction, subnet calculation, units and CSV escaping. |
| `FeatureTests` | UI/integration behavior, persistence, Ping/Trace/DNS, Collector authentication/transport/export, layout and caching. |
| `PerformanceTests` | Startup, `/24` load, UI dispatch, queue draining, dual Traceroute and memory. |
| `PollingCadenceTests` | Bounded overlap and observable 250 ms versus 1000 ms cadence. |
| `OvernightSoakTests` | Packet loss, route/DNS changes, TACACS rejection, VTY limits, terminal pressure, memory and UI responsiveness. |

The soak harness defaults to eight hours when invoked directly. Always pass `--seconds 10` or a deliberate duration for local validation:

```powershell
.\artifacts\test\OvernightSoakTests.exe --seconds 60
```

## UI acceptance gates

Automated compilation and startup are not sufficient for a UI release. For affected menus:

- Capture normal-width and 1100-pixel-minimum screenshots.
- Check 125%, 150% and 175% Windows scaling when layout changes are material.
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
- Delete or ignore generated state, fake-Plink markers and test binaries.
- Some UI tests use reflection against private members. A field/method rename may require test updates even when visible behavior is unchanged.
