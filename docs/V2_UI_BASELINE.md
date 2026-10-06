# NetStuck V2 functional baseline

Recorded 2026-10-06 (Asia/Bangkok). The clean checkout was on `codex/release-v1.3.5` at `322632114c42a60edbda04b48f79db4f4d6d9266`. After fetching, current main was `85c18f1`; `ui/v2-redesign` starts from that main merge, preserving all 1.x history. Runtime baseline: 1.3.5.0. The first canonical Windows PowerShell 5.1 run passed 424/424, zero failures/skips/infrastructure failures, two build stages and 12 mandatory suites. Evidence: `artifacts/test/v2-baseline-summary.json`.

Fresh baseline observations: startup 1,078 ms; /24 worst dispatch 20 ms; dual Traceroute worst dispatch 32 ms; working set 78 MB. At timeout 1,000 ms, Ping 250/1,000 ms produced 12/3 completions and Traceroute 14/4 samples. The ten-second bounded soak measured 11.472 seconds, 16 ms worst dispatch and 12 MB memory growth. These are machine-specific baseline observations, not V2 results.

Production is direct-csc WinForms on .NET Framework 4.x, with a system-DPI manifest and 96-DPI `AutoScaleMode.Dpi`. No SDK/framework migration is required. The portable folder and exact nine-file updater inventory remain the distribution contract.

| Existing feature | Baseline entry | V2 entry / preserved contract |
| --- | --- | --- |
| Live Ping | Tab 0 | Ping Live sidebar; ICMP/TCP, bounded monotonic cadence, source/packet/port/DNS options, CIDR limits, stale-result protection |
| Target management | Live Ping | Targets + saved lists, Load / Save / Delete; local profiles and optional descriptions |
| Ping presentation / export | Live Ping | Search/status filter, sorting, resizing/reordering, full-row selection, Copy, CSV, optional metrics, selected-host history and log export |
| Realtime Traceroute | Tab 1 | Traceroute sidebar; two independent session tabs, target history, ICMP/TCP/UDP service checks, reached-target termination, adaptive TTL |
| Trace presentation / export | Sessions 1 and 2 | Per-hop statistics, route/DNS events and filters, column visibility, copy, hop descriptions, scroll/selection preservation |
| DNS | Tab 2 | DNS Resolver sidebar; forward/reverse lookup, custom/system resolver, continuous polling and exports |
| Lookup | Tab 3 | MAC / WAN Lookup sidebar; OUI cache, local/multicast exclusion, public-IP enrichment and exports |
| Calculators | Tab 4 | Calculators sidebar; IPv4 subnet/CIDR, unit conversion, quick reference and inline validation |
| Config Collector | Tab 5 | Config Collector sidebar; SSH/Telnet, device types/ports, concurrency, AUTH1-before-AUTH2, prompt-aware stdin credentials |
| Collector output | Config Collector | Basic/collect commands, TXT/JSON streaming, output-folder controls, Ping all, completed-file opening, error-only CSV and batched terminal |
| Event Log | Tab 6 | Event Log sidebar; filter/level, export/clear, explicit empty states and accessibility announcements |
| Updates | Tab 7 | Updates / recovery sidebar; optional daily check, latest status, verified install, restart and local recovery |
| Appearance / state | Header and local state | Native theme selector plus Settings; existing schema 6, non-secret inputs, window bounds, text zoom and optional columns |
| Other UI | Dialogs / status bar | Save-list, column and hop dialogs, file dialogs, global clock/local/public-IP status remain reachable |

The eight original page indices are retained; Home and Settings append indices 8/9. An older executable ignores those new selected-page indices and otherwise receives the same schema-6 fields. No passwords, enable secrets, operator inventories or captures enter Git or screenshots.

The attached design brief's local-only publication rules are superseded by the owner's direct chat request to publish the completed version to GitHub and add downgrade recovery. Publication is authorization, not evidence that an unperformed manual display test passed.
