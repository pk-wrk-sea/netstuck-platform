# Architecture

## Overview

NetStuck is a Windows Forms application targeting .NET Framework 4.x. One GUI process owns application state, polling and presentation, while bounded child processes are launched for the existing custom-DNS (`nslookup.exe`), Config Collector (Plink) and open-folder flows. It is intentionally portable and compiled directly with `csc.exe`; there is currently no `.sln`, `.csproj`, dependency injection container or separate service layer.

Most behavior lives in one `partial MainForm`, split by historical feature layers. This keeps the binary simple but makes shared mutable state, private-member names and UI-thread boundaries important maintenance constraints.

## Source ownership

| Path | Primary responsibility |
| --- | --- |
| `src/NetStuck/NetOpsCore.cs` | Pure target parsing, CIDR expansion, MAC/IP extraction, subnet calculation, unit conversion and CSV escaping. |
| `src/NetStuck/NetStuck.UiFoundation.cs` | Phase A shared tokens, action roles, accessibility helpers, read-only result-grid convention and semantic-state presenter used only by the shell, Calculators and Event Log pilots. |
| `src/NetStuck/NetStuck.cs` | Assembly/version values, application shell, shared controls/tables, profiles, MAC/WAN lookup, calculators, event log, export, theming and shutdown. |
| `src/NetStuck/NetStuck.V103.cs` | Active Live Ping and dual-session Traceroute, cadence schedulers, ICMP/TCP/UDP probes, adaptive TTL, DNS/ISP cache, network identity and v1.2.3 layout. |
| `src/NetStuck/NetStuck.Release1.cs` | Cross-cutting UI/performance behavior, double buffering/copy support, activity indicators, batched Ping UI updates, hop descriptions and DNS polling. |
| `src/NetStuck/NetStuck.Features.cs` | State schema, Config Collector SSH/Telnet, streamed capture, terminal batching, error CSV, NTP-backed clock and zoom. |
| `tests/*.cs` | Console regression harnesses. UI suites use reflection against private `MainForm` fields and methods. |

## Major runtime flows

### Live Ping

1. Targets are parsed by `NetOpsCore`, including CIDR expansion up to 1,024 targets.
2. Poll cycles use a monotonic due-time schedule and a bounded outstanding-work count.
3. Probe completions enter a batched UI queue instead of directly repainting the grid.
4. Statistics and history accept late results, but sequence checks prevent stale status from replacing newer state.

### Traceroute

1. Each of two sessions owns independent controls, cancellation, statistics, event tables and route history.
2. ICMP TTL probes discover route hops. TCP/UDP modes add a destination service check; they do not replace hop discovery.
3. Stable routes use adaptive TTL rotation. Full discovery runs initially and periodically, and route changes force full cycles.
4. Only changed rows are refreshed. Selection and vertical/horizontal scroll positions are restored.
5. The run installs an explicit WinForms synchronization context before its first asynchronous yield. Bound hop/event tables are mutated only on the UI thread; pause is checked before completed probes are applied, and stop/close invalidates late DNS/provider callbacks before they can update controls.
6. Every hop/service probe and cycle task is registered with its owning run. `StopTraceSessionAsync` stops scheduling, cancels cancellable work and awaits the run's task registry asynchronously. Non-cancellable ICMP work remains owned until terminal completion; results/exceptions are observed and obsolete output is not applied. START remains disabled until the registry reaches zero tasks and zero ownership callbacks.
7. The drain timeout is derived from the selected probe timeout plus the bounded 2–8-cycle concurrency allowance. A timeout is an explicit incomplete-stop failure, not success; the old run remains active and continues observing work until it is genuinely quiescent.

### Config Collector

1. Device lines are parsed into host, optional port, authentication slot and description.
2. A `SemaphoreSlim` bounds parallel collection between 1 and 32 devices.
3. AUTH1 is attempted before AUTH2. Fallback occurs only for authentication-class failures, not transport or command failures.
4. SSH credentials are provided through redirected input to the active Plink prompt-aware transport; passwords are not command-line arguments.
5. Large output streams to a temporary capture. A bounded tail is retained for prompt recognition and a bounded terminal preview is drained in batches.
6. Successful captures finalize to TXT and optional JSON; error export contains only failed rows.

## Persistence

The application writes these files under `%LOCALAPPDATA%\NetStuck`:

| File | Data |
| --- | --- |
| `state.json` | Window, menu and non-secret input state. |
| `profiles.json` | Saved Live Ping target lists. |
| `mac-vendors.json` | OUI vendor cache. |
| `trace-lookups.json` | ISP/ASN and reverse-DNS cache with timestamps. |
| `network-identity-v103.json` | Cached public-IP identity. |

Passwords and enable secrets are intentionally absent from the persisted state schema. Other fields can still reveal topology and usernames; see `PRIVACY.md`.

Tests set `NETSTUCK_TEST_ROOT` to a unique OS-temporary root, which redirects `state.json`, `profiles.json`, `mac-vendors.json` and `trace-lookups.json` together. The legacy `NETSTUCK_TEST_STATE_PATH` redirects `state.json`; because the trace cache is colocated with the effective state path, `trace-lookups.json` follows that override's directory, while profiles and the MAC cache remain under LocalAppData. Test owners must remove their exact roots and treat cleanup failure as a failed test.

## Performance invariants

- Poll overlap stays bounded between 2 and 8 slots.
- Ping UI queue drains on a short timer in adaptive batches.
- Collector drains at most 256 messages or 128 KiB per pass and caps the visible terminal at 2,000,000 characters.
- Collector prompt parsing retains a bounded tail rather than a full large configuration in memory.
- ISP/DNS caches are bounded and expire according to result type.
- Traceroute does not reset the entire binding on every cycle.

## Build and provenance boundary

Production compilation uses the six-file allowlist in `scripts/NetStuck.BuildProvenance.ps1`; directory discovery never adds a `.cs` file implicitly. `Build-NetStuck.ps1` invokes the .NET Framework compiler with `/noconfig` and `/nostdlib+`, then supplies `mscorlib` and every required framework reference by resolved absolute path. Raw hashes are recorded for repository inputs, references, compiler/runtime tools and the executable.

The portable identities are intentionally separate: repository source inputs, toolchain, normalized compiler invocation, explicit reference inputs, package inputs, decompressed package content and ZIP container. Actual and normalized compiler argv are emitted from one ordered argument specification, so each compiler option/path remains one atomic argument. Invocation identity uses a binary `v2` serialization containing a fixed ASCII header plus little-endian argument count, index and UTF-8 byte length followed by the UTF-8 bytes. Human-readable quoting is diagnostic only and is never fingerprint input. Canonical file manifests continue to use normalized relative paths, ordinal ordering, byte lengths, raw SHA-256 and UTF-8/LF records; absolute installation paths are diagnostic fields only.

## Known debt

- Version-layer suffixes (`V103`, `V104`, `V120`, etc.) remain in active symbols.
- Legacy UI builders coexist with active builders.
- External HTTP/NTP providers are hard-coded rather than injected.
- Tests depend heavily on private member names and some real machine/network state.
- Persistence errors are frequently swallowed, and cache replacement should eventually become atomic with backup recovery.
- The local time suffix is labeled `ICT` even when Windows uses another timezone.
