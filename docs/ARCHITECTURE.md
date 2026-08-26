# Architecture

## Overview

NetStuck is a single-process Windows Forms application targeting .NET Framework 4.x. It is intentionally portable and compiled directly with `csc.exe`; there is currently no `.sln`, `.csproj`, dependency injection container or separate service layer.

Most behavior lives in one `partial MainForm`, split by historical feature layers. This keeps the binary simple but makes shared mutable state, private-member names and UI-thread boundaries important maintenance constraints.

## Source ownership

| Path | Primary responsibility |
| --- | --- |
| `src/NetStuck/NetOpsCore.cs` | Pure target parsing, CIDR expansion, MAC/IP extraction, subnet calculation, unit conversion and CSV escaping. |
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

## Performance invariants

- Poll overlap stays bounded between 2 and 8 slots.
- Ping UI queue drains on a short timer in adaptive batches.
- Collector drains at most 256 messages or 128 KiB per pass and caps the visible terminal at 2,000,000 characters.
- Collector prompt parsing retains a bounded tail rather than a full large configuration in memory.
- ISP/DNS caches are bounded and expire according to result type.
- Traceroute does not reset the entire binding on every cycle.

## Known debt

- Version-layer suffixes (`V103`, `V104`, `V120`, etc.) remain in active symbols.
- Legacy UI builders coexist with active builders.
- External HTTP/NTP providers are hard-coded rather than injected.
- Tests depend heavily on private member names and some real machine/network state.
- Persistence errors are frequently swallowed, and cache replacement should eventually become atomic with backup recovery.
- The local time suffix is labeled `ICT` even when Windows uses another timezone.
