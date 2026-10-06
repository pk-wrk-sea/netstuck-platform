# NetStuck 2.0.0 UI implementation report

Recorded 2026-10-06. This report describes the local release candidate. GitHub PR, tag workflow and published asset verification provide the subsequent publication evidence; an authorized publication does not convert an unperformed manual check to PASS.

## 1. Executive Result

The application now has sidebar navigation, shared page headers, Home and Settings, a recomposed Ping workspace, readable Light/Dark presentation and explicit GitHub version recovery. All original feature groups remain reachable. Runtime is still portable WinForms on .NET Framework 4.x, assembly/file version **2.0.0.0**. No networking framework, polling scheduler or credential transport migration is included.

## 2. Baseline

The clean starting checkout was `322632114c42a60edbda04b48f79db4f4d6d9266` on `codex/release-v1.3.5`; the V2 branch starts from fetched main `85c18f1`. The fresh pre-change baseline passed 424/424 checks. [Baseline inventory and measured performance](V2_UI_BASELINE.md) distinguish those results from V2.

## 3. V2 Scope

Rebuild the shell and Ping presentation; adapt all other pages to the shared shell, smaller content canvas and semantic theme; add Home/Settings, bounded target-file loading, selected-event detail and explicit historical-version installation. The direct owner request authorizes GitHub publication and supersedes the attachment's local-only Git instructions. The supplied mockup is the visual direction; its displayed addresses and runtime counters are not application defaults or test evidence.

## 4. Feature Preservation Matrix

See [the complete baseline inventory](V2_UI_BASELINE.md) and [per-feature migration/evidence matrix](V2_UI_MIGRATION_REPORT.md). Preserved: all eight original destinations, ICMP/TCP Ping, dual ICMP/TCP/UDP Traceroute, DNS, MAC/WAN, calculators, secure Collector, Event Log, updates, saved lists and schema-6 preferences. Removed/deferred functional features: none. Presentation changes include opt-in Ping metric cards and responsive More actions.

## 5. New Application Shell

One table owns the sidebar and flexible workspace. Shared title/description and native Theme selector accompany context actions. Accessible native buttons select stable page IDs; old page indices 0–7 remain unchanged, with Home/Settings appended at 8/9. Native page ownership and keyboard traversal are retained through `WorkspaceTabs`. Ctrl+Shift+U opens Updates / recovery.

## 6. Design System

The [design-system resource table](V2_UI_DESIGN_SYSTEM.md) records spacing, typography, dimensions and semantic roles. Established `UiTokens`/`UiPalette` remain authoritative. V2 dimensions use logical 96-DPI values. Selection/focus/state text and navigation icons follow the active palette. Status always includes readable text; color is supplementary.

## 7. Ping Live V2

Targets + saved lists and Probe settings are separate surfaces. Input content scrolls independently while equal-width Start/Pause/Stop actions stay pinned. Realtime results and selected-host history remain separate bound grids. Search/status, full-row selection, sort, Copy, CSV, resize/reorder and optional metrics retain existing handlers. More retains secondary actions below the toolbar width threshold. History Details remains available when its optional side pane collapses. Target-file loading refuses active Ping and accepts a text file smaller than 256 KiB.

## 8. Traceroute V2

Both independent session tabs and their three-row non-overlapping input grouping are preserved. The configuration/event split now fits the sidebar workspace. Destination termination, changed-hop updates, statistics, enrichment, event filters, columns and hop descriptions remain in the existing lifecycle and presentation owners. The old 690-pixel pane-width assertion is replaced by actual target/timing-width and non-overlap assertions; the 93-check Feature corpus is retained.

## 9. Collector V2

The configuration/review split reserves credential width, scrolls taller input content and retains pinned Collect/Stop. SSH/Telnet, ports/device types, concurrency, AUTH1-before-AUTH2, literal domain username separator, prompt-aware stdin secrets, commands, TXT/JSON streaming, output controls, error-only export and batched terminal remain intact.

## 10. Calculators V2

Subnet and unit-conversion panels retain Phase A field order, inline states and keyboard/accessibility announcements. Shared shell/theme and constrained viewport protection apply without changing calculation behavior.

## 11. Event Log V2

Filter/level, export, Clear and explicit empty/filtered-empty states remain. The read-only grid, selection behavior and announcements retain their existing implementation.

## 12. Settings V2

Settings exposes appearance, text/results zoom, saved preferences and version recovery. Theme selectors synchronize; zoom calls the existing font/row sizing path. Ctrl+mouse-wheel remains available. Home links to every tool. Saved state remains schema 6, with no new credential storage.

## 13. Shared Controls

Existing action factories, cards, grid factories and state presenters remain in use. Sidebar glyphs are privately owned Segoe MDL2 bitmaps, recolored only when needed and disposed with their fonts. Native controls retain roles, keyboard activation and focus cues. No per-event control creation or new polling redraw loop is introduced.

## 14. DPI / Scaling Architecture

The executable remains **system DPI aware**, with WinForms `AutoScaleMode.Dpi` at the 96-DPI design baseline. Runtime window/splitter constraints convert logical values through `LayoutPixels`; fixed-first panes retain logical width and free splitters retain ratio. This is not PerMonitorV2. Windows may scale the system-aware process on a monitor with different DPI. Physical Scale and mixed-monitor acceptance are recorded separately below.

## 15. Responsive Layout Strategy

The nominal minimum remains 1100×700, scaled and capped to the available working area. Startup/restore, move, resize and display-change paths keep the main window on an available monitor. Pages use a minimum usable canvas and scrolling; inputs and results have independent scroll ownership. Optional Ping detail and secondary toolbar actions collapse by measured available width. The V2 matrix requests seven viewport sizes and emits actual clamped bounds.

## 16. Accessibility

New interactive controls have accessible names/descriptions and native roles; selected/running navigation includes descriptive text. Explicit pilot field order is preserved. Layout tests measure at least 4.5:1 enabled body/muted/header/selection/action text contrast in both palettes. Native High Contrast colors and preference-change repaint are implemented. Native-role queries and these 96-DPI renders do not establish physical screen-reader/High Contrast acceptance.

## 17. Cross-machine Protection

No screen-resolution-specific absolute positioning replaces the container layout. Header descriptions wrap, primary controls retain their text, native combo lists are font-aware and dense workspaces scroll. Window fitting and persisted columns/theme/zoom remain covered by the existing maintenance/layout tests. Unknown fonts, larger Windows text settings and monitor transitions remain explicitly unverified.

## 18. Performance

Final-source measurements are recorded in the table below. These are bounded local observations, not universal performance promises or an overnight production soak.

| Observation | Windows PowerShell 5.1 | PowerShell 7 |
| --- | --- | --- |
| Warm UI startup | 1174 ms | 1189 ms |
| /24 worst UI dispatch | 21 ms | 17 ms |
| Dual Traceroute worst dispatch | 58 ms | 43 ms |
| Working set | 83 MB | 85 MB |
| Ping 250 / 1000 ms, timeout 1000 ms | 11 / 3 completions | 10 / 3 completions |
| Trace 250 / 1000 ms, timeout 1000 ms | 14 / 4 samples | 14 / 4 samples |
| Bounded soak duration / worst dispatch / memory growth | 11.223 s / 17 ms / 10 MB | 11.468 s / 19 ms / 10 MB |

## 19. Security Preservation

Git contains no operator state, usernames, network inventories, captures, executable or DLL. Test credentials and all screenshot addresses are synthetic fixtures. Collector secrets stay outside argv/state/logs. Latest checks still only offer newer stable releases. Historical discovery offers older compatible ZIP/checksum pairs from the public repository, numerically ordered, with archived v1.3.1 supported.

Downgrade requires an explicit chosen version and confirmation. The installed helper validates direction and expected installed identity, then exact nine-file inventory, internal manifest and executable version. It retains complete application-file backups and uses the existing fail-closed rollback/restart path. State and unrelated files remain outside the replacement inventory. Checksums provide integrity; publishing-account compromise remains the existing unsigned-release trust limit.

## 20. Tests

Final-source canonical runs: Windows PowerShell 5.1.26100.3624 at 2026-10-06T05:16:51Z and PowerShell 7.6.5 at 2026-10-06T05:20:21Z each PASS **927/927**, 0 failed, 0 skipped, 0 infrastructure failures, 14/14 mandatory suites and two successful stages. Evidence: ignored `artifacts/ui-v2/final-ps51.json`, `final-ps7.json` and their complete logs. The second run is the canonical validation inside the package command. The final focused UI run also passes 460/460, including selected-event detail and removal of stale detail when history is cleared; the focused Functional script passed its 93-check integration corpus.

The first PR CI run found one harness assertion incorrectly requiring a physical 1100-pixel minimum on its 1024×720 working area. All other completed V2 geometry checks passed. The assertion now checks the scaled nominal minimum capped to the actual working area, as specified; production source and its fingerprint are unchanged. The repaired 460-check UI harness is verified under both local hosts, and the corrected commit must pass the complete normal GitHub CI before merge. The failed run `37417840102` remains available as evidence.

The unchanged-behavior corpus remains 130 checks. Existing lifecycle, maintenance, UI foundation/layout, capture-transaction and provenance suites remain mandatory. V2 adds 460 UI checks and 27 recovery checks; ten-page UI layout discovers 107 checks. The complete inventory is 927 across fourteen suites and two build stages. See [profiles, semantics and manual limits](V2_UI_TESTING.md).

Live read-only validation downloaded v1.3.5, v1.3.3, v1.3.2 and archived v1.3.1 through the production engine; all four passed transport size, ZIP SHA256, exact inventory, internal manifest and executable version validation. Installs performed by that transport check: zero. Sandbox tests separately exercised older-file replacement, state preservation, full backup, injected copy failure and exact-hash recovery. v1.3.0/v1.2.3 inventories require manual installation.

## 21. Screenshot Matrix

Eighty sanitized structural captures cover ten pages × two themes × four requested sizes: 1366×768, 1440×900, 1920×1080 and 1100×700. Observed process DPI is 96; working area is 1920×1152. Twelve additional native dropdown renders cover Theme, Trace protocol and previous-version list in both themes at normal/minimum width. They render only owned HWNDs over the sanitized form and never read the desktop. Physical pointer/modal acceptance is not asserted.

The checked-in [screenshot selection and hash inventory](ui-v2/README.md) contains every page at normal Light and minimum Dark, plus six native dropdown examples. Full local matrix: ignored `artifacts/ui-v2/final`. Existing Phase A closure: PASS: 9/9 scenarios, identical hashes across 5/5 runs, semantic and PNG/privacy gates PASS. Screenshots contain documentation-range addresses, example inputs and a frozen fixture clock.

## 22. Build / Package

Production uses the explicit ten-source allowlist with the existing `/noconfig`, `/nostdlib+` and explicit reference/provenance path. Final source input fingerprint: `4b3f1cba0af3a0f754e45d8dd695dc0de26179638e68501827558ebc68531e5b`.

PowerShell 7 packaging passed the canonical verifier, unchanged-source/provenance checks, exact staged/extracted inventory, internal SHA256 manifest and content equality. The staged executable passed isolated packaged startup: responsive native window, graceful close, zero exit, no process residue, one owned state file, no operator-profile/credential content and complete owned-root cleanup.

The portable ZIP still contains exactly nine files, including pinned PuTTY Plink 0.80 and its license. Plink SHA256 is `06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3`; local Authenticode validation was Valid with Simon Tatham as signer. Local package identity is below. The GitHub tag workflow produces its own candidate; its downloaded ZIP must match the accepted workflow ZIP, companion checksum and asset digest before publication. Local and CI ZIP hashes can differ because executable/build/report identities differ.

## 23. Known Follow-ups

Physical DPI, mixed monitors, Windows text-size/font variance, High Contrast and screen reader checks below remain open. Native Windows dialogs/chrome follow Windows. Older versions may ignore newer selected-page/preferences fields. The updater does not monitor application health after input-idle startup. Earlier incompatible package inventories require manual GitHub installation. No automatic downgrade is introduced.

## 24. Manual Acceptance Required

| Gate | Observed result |
| --- | --- |
| Physical pointer selection/scroll/copy/column drag and modal recovery | NOT RUN; automated wiring/geometry only |
| Native dropdown | Owned native HWND opened/rendered at 96 DPI; physical pointer acceptance NOT RUN |
| Windows Scale 125%, 150%, 175%, 200% | NOT RUN |
| Mixed-DPI monitor moves / Windows text-size / fallback fonts | NOT RUN |
| Windows High Contrast | System-color implementation present; real mode acceptance NOT RUN |
| Screen reader | Native metadata/roles tested; real reader acceptance NOT RUN |

These gates have no inferred PASS. Owner publication authorization is recorded separately from observed acceptance.

## 25. Files Changed

New production presentation: `src/NetStuck/NetStuck.UiV2.cs`. Existing shell/version, Ping/Trace/Collector sizing, window/splitter fitting, theme callback and updater owners are changed in their original partial files. The manifest, explicit build allowlist, version defaults, CI/release artifact names and test runner are synchronized. New tests cover V2 UI/recovery and synthetic captures; focused scripts and baseline/architecture/design/migration/testing/report documentation accompany them. Legacy field names, package inventory and schema are preserved.

## 26. Git State

Repository: `pk-wrk-sea/netstuck-platform`. Candidate branch: `ui/v2-redesign`, based on fetched main `85c18f1`. Initial unrelated dirty changes: none. Commit/push/PR, SHA-locked merge, annotated `v2.0.0` tag and normal tag workflow are the authorized publication sequence. Exact commits, checks and release status are recorded by GitHub; this source report does not claim a future operation already passed.

## 27. Candidate Artifact

Local filename: `NetStuck-v.2.0.0.zip` under ignored `artifacts/release`; assembly/file version 2.0.0.0.

- Local ZIP SHA256: `5016065c79e21477cf02b00411410503f74887c1f30fd0f999082e19ab012bc3`.
- Local executable SHA256: `e0231bfbd5b9f9a48bf318921c3627d879860ec9afa80737028939c23d5ec240`.
- Package input fingerprint: `0a3e401076a97aaeeba3dd2024e52af167c5ae183cba3e342177bb08750eb180`.
- Decompressed package content fingerprint: `846eb8956cada9ad5db156f99ca613cd41ba4734a0a146d0d24fa3d56c1d5578`.
- External sidecar: ignored `artifacts/release/NetStuck-v.2.0.0.provenance.json`; disposition `PROVENANCE_VERIFIED`. This local working-tree candidate predates the source commit; it is distinct from the subsequently verified GitHub workflow package.

Published destination after accepted tag verification: [NetStuck v2.0.0](https://github.com/pk-wrk-sea/netstuck-platform/releases/tag/v2.0.0). Recovery fallback: [v1.3.5](https://github.com/pk-wrk-sea/netstuck-platform/releases/tag/v1.3.5). Keep the complete portable folder; executable-only distribution is unsupported.
