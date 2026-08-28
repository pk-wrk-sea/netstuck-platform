# NetStuck

NetStuck is a portable Windows network diagnostics and configuration-collection application. The current local candidate is **v1.3.0** and targets .NET Framework 4.x without requiring Python or the .NET SDK. The last published stable baseline remains **v1.2.3** until release acceptance is complete.

## Features

- Live Ping: ICMP/TCP monitoring, CIDR expansion, profiles, status history, filtering and resizable columns.
- Traceroute: two concurrent sessions, per-hop latency/loss/jitter, route and DNS events, hop descriptions and ISP/ASN enrichment.
- DNS Resolver: forward and reverse lookup, query latency and continuous polling.
- MAC / WAN Lookup: MAC OUI vendor and public-IP ownership/geolocation lookups.
- Calculators: subnet/CIDR and network-unit conversions with quick references.
- Config Collector: concurrent SSH/Telnet collection, AUTH1/AUTH2 fallback, streamed TXT/JSON output and error CSV export.

## Repository baseline and candidate

The v1.3.0 candidate is prepared from the validated v1.2.3 source. Validation starts with the canonical command:

```powershell
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

Build only:

```powershell
.\build_windows.bat
```

The executable is written to `artifacts\build\NetStuck.exe` and is intentionally excluded from Git history.

## Source layout

```text
src/NetStuck/                 Application source and icon assets
tests/                        Core, UI, integration, cadence, performance and soak harnesses
scripts/                      Reproducible build, test and package commands
docs/                         Architecture, development, privacy and release guidance
.codex/skills/                Repository-local AI maintenance skill
.github/workflows/            Windows CI
third-party/                  Third-party notices and license texts
```

Start with [AGENTS.md](AGENTS.md) when using an AI coding agent. Maintainers should also read [Architecture](docs/ARCHITECTURE.md), [Testing](docs/TESTING.md), [Privacy](PRIVACY.md) and [Releasing](docs/RELEASING.md).

## Runtime files

User settings and lookup caches are stored under `%LOCALAPPDATA%\NetStuck`. Config Collector output defaults to `%USERPROFILE%\Documents\NetStuck Configs`. These locations can contain network topology, usernames and device configuration and must never be committed.

## Distribution

Download the complete ZIP from GitHub Releases and keep `NetStuck.exe`, the `tools` directory and PuTTY license together. `plink.exe` is distributed only in the release package, not in Git history.

This private repository does not currently grant an open-source license. See [Third-party notices](THIRD_PARTY_NOTICES.md) for bundled components.
