# Changelog

All notable changes are recorded here. Versions use `vMAJOR.MINOR.PATCH` in Git tags and `v.MAJOR.MINOR.PATCH` in the legacy application UI.

## v1.2.3 — Baseline

- Replaced fixed-width Traceroute flow rows with deterministic, adjacent grid columns.
- Aligned Target, Max Hops, Timeout and Interval on the first row.
- Aligned Protocol, Port and Packet Size on the second row so the Protocol dropdown cannot cover timing inputs.
- Preserved dedicated Start/Pause/Stop actions and narrow-window safeguards.
- Made SplitContainer startup deterministic on constrained desktops so Traceroute and Collector inputs do not collapse into the default split.
- Made Collector stdin BOM-free under UTF-8 Windows consoles while keeping passwords out of process arguments.
- Deferred the shared result-grid font cleanup until after control teardown to prevent an intermittent Windows shutdown access fault.
- Repository preparation: added reproducible build/test/package scripts, CI, maintenance documentation and an AI skill.
- Repository hardening: removed an unused legacy SSH method that passed passwords through process arguments and replaced organization-specific assembly metadata. Active collector behavior and application version remain unchanged.

## v1.2.2

- Prevented Traceroute timing inputs and action buttons from overlapping at narrow widths or high DPI.
- Standardized enabled and disabled Traceroute input backgrounds.
- Corrected minimum splitter sizing.

## v1.2.0–v1.2.1

- Added changed-row Traceroute updates, adaptive TTL polling and persistent ISP/DNS caches.
- Added batched Collector terminal output and streaming large-config capture.
- Added Collector error-only CSV export and completed removal of Log Sanitizer.
- Refined Traceroute control-panel layout.

## v1.0.0–v1.1.0

- Established Live Ping, Traceroute, DNS, MAC/WAN, Calculators and Config Collector workflows.
- Added profiles, persistent UI state, dual authentication, export logs, copyable grids and performance safeguards.

Detailed historical notes remain in `docs/releases/v1.2.3/` and in the application Updates tab.
