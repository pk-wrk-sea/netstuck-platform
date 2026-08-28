# NetStuck — Whole-Repository UI Audit and Modernization Report

> สถานะเอกสาร: audit/report only — **ไม่มีการแก้ production source code, dependency, version, behavior, commit, tag หรือ remote state**
>
> วันที่ตรวจ: 2026-08-26 (Asia/Bangkok)
> Baseline: `v1.2.3` / `2f35f72988fac0a44292a6bd69196e0842cbfc73`

## 1. Executive Summary

NetStuck เป็น monolithic Windows Forms host บน .NET Framework 4.x มี `partial MainForm` เป็นศูนย์กลาง UI และใช้ `TabControl` 8 tabs เป็น navigation หลัก; บาง workflow เรียก child process เช่น `nslookup.exe` และ PuTTY Plink จึงไม่เรียกว่า single-process แบบเคร่งครัด. ไม่พบ `MenuStrip`, `ToolStripMenuItem` หรือ `ContextMenuStrip` ใน production source หลักฐานอยู่ที่ `src/NetStuck/NetStuck.cs:188-283`, `:1048-1072`, `src/NetStuck/NetStuck.Features.cs:204-217` และ shared UI `src/NetStuck/NetStuck.cs:1664-1669`

Audit นี้พบ **30 UI surface groups** ได้แก่ shell, 8 top-level tabs, nested sessions/panes, custom dialogs และ native dialog families รายงานสรุป **22 findings: P0 0 / P1 7 / P2 13 / P3 2** ไม่มีหลักฐาน credential exposure หรือ UI ที่ใช้งานไม่ได้ใน baseline ปัจจุบัน แต่มี P1 ที่ควรแก้ก่อน redesign เชิงภาพ ได้แก่ accessible naming/labels, contrast/High Contrast, Traceroute stale-cycle topology race และ state/performance risks ของ Config Collector

ผล runtime สดผ่าน `128/128` checks: Core 16/16, Feature/UI/Integration 91/91, Performance 10/10, Cadence 3/3 และ accelerated soak 8/8 โปรแกรมเปิดและตอบสนองได้; warm startup 926 ms, `/24` Live Ping worst dispatch 19 ms, dual Traceroute worst dispatch 46 ms, working set 73 MB และ soak worst dispatch 19 ms อย่างไรก็ดี ตัวเลขเหล่านี้ไม่ยืนยัน Narrator, High Contrast, DPI มากกว่า 100%, long Thai/English หรือ performance risks ที่ต้องใช้ fixture เฉพาะ

ลำดับ implementation ที่แนะนำคือ: (1) accessibility/state/test foundations, (2) Config Collector + Live Ping + Traceroute, (3) DNS/lookup/calculators/log/dialogs, (4) hardening ที่ 100/125/150/200% DPI, Narrator, High Contrast, visual regression และ responsiveness profiling โดยต้องรักษา fixed cadence, two-session Traceroute, v1.2.3 deterministic grid, scroll/selection preservation และ password-free argv/state/logs

### Top 5

1. **A11Y-01:** 130 interactive controls มี explicit `AccessibleName`/`AccessibleDescription` เป็น 0; credential rows ใช้ cue-banner เป็น label หลัก
2. **ASYNC-01:** Traceroute cycle เก่าสามารถลด `KnownDestinationHop` และลบ rows ก่อน freshness guard
3. **PERF-01:** Collector stream/finalize path มี synchronous file work บน captured UI context และ terminal drain ไม่ได้ enforce 128 KiB ต่อ pass สำหรับ oversized item
4. **STATE-01:** Collector `Ping all` ไม่มี cancel/re-entry/mutual-exclusion state และทำทีละ device
5. **STATE-02:** Collector ยังอ่าน credentials/options จาก live controls หลัง run เริ่ม ทำให้ queued devices อาจใช้ configuration คนละ snapshot

## 2. Audit Scope and Limitations

### ขอบเขตที่ตรวจ

- อ่าน `AGENTS.md`, `.codex/skills/netstuck-maintainer/SKILL.md`, `README-TH.md`, `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT.md`, `docs/TESTING.md`, `PRIVACY.md`, `SECURITY.md` และ release reports ก่อน source audit
- ตรวจ production C# ทั้ง 5 files, test harnesses, build/test scripts, assets และ documentation ที่เกี่ยวข้อง
- ทำ static inventory ของ navigation, pages, panels, dialogs, shared UI helpers, state, threading, batching, persistence และ tests
- Build/test ด้วย repository command และตรวจ runtime screenshots ของ top-level tabs ที่ 1100×900 และ 1460×900 โดยใช้ isolated/synthetic fixture; source ประกาศ minimum 1100×700 แต่รอบนี้ตรวจอัตโนมัติเฉพาะ width 1100 โดย height ยังเป็น 900
- ตรวจ official primary sources ของ Microsoft และ W3C; ไม่ใช้ design blog เป็น authority

### ข้อจำกัด

- Environment เปิดมาที่ `C:\Codex_Project\NetStuck` ซึ่งเป็น directory ว่าง; repository จริงอยู่ที่ `C:\Codex\_Project\NetStuck` ตาม request จึงตรวจ repository จริงโดยไม่ย้าย checkout
- Runtime screenshot เป็น 100% display scaling เท่านั้น; 125%, 150% และ 200% **Not verified**
- ไม่ได้ใช้ Narrator/NVDA/Accessibility Insights, Windows High Contrast หรือ screen-reader automation
- `DrawToBitmap` ไม่บันทึก native ComboBox popup; ไฟล์ `runtime-traceroute-protocol-open-*.png` ยืนยัน focus/layout ของ control แต่ไม่ยืนยัน popup overlay รายการจริง
- Screenshot harness instantiate `MainForm` จาก `NetStuck.UI.dll`; Form `Icon` มาจาก host process จึงกระทบทั้ง title bar และ header PictureBox (`Icon.ToBitmap()`); ทั้งคู่ไม่ใช่หลักฐาน fidelity ของ packaged EXE icon
- Dialogs ไม่ถูกเปิดแบบ interactive ใน runtime รอบนี้; custom/native dialogs จึงเป็น source-verified เท่านั้น
- ใช้ `NETSTUCK_TEST_STATE_PATH` เพื่อหยุด startup NTP/Public-IP calls แต่ override ปัจจุบันไม่ isolate `profiles.json`/`mac-vendors.json`; harness ล้าง/แทนค่าที่แสดงก่อน capture และไม่บันทึกข้อมูลจริง
- ไม่ต่อ production host/device, ไม่ใช้ credential จริง และไม่ capture `%LOCALAPPDATA%\NetStuck` หรือ collector output

### วิธีอ่านสถานะ

- **Verified:** ยืนยันจาก source และ runtime/test/screenshot รอบนี้
- **Partially verified:** ยืนยันจาก source หรือ automated behavior แต่ยังขาด manual assistive-tech/DPI state
- **Not verified:** ไม่ได้เห็น runtime state นั้นและไม่แต่งผล
- **Inference / profiling risk:** source บ่งชี้ความเสี่ยงแต่ยังไม่ reproduce/benchmark เฉพาะกรณี
- **Recommendation:** future implementation plan ไม่ใช่ behavior ปัจจุบัน

## 3. Repository and Baseline Verification

| Check | Result | Status |
| --- | --- | --- |
| Repository examined | `C:\Codex\_Project\NetStuck` | Verified |
| `git status --short` before audit | empty | Clean baseline |
| `git rev-parse HEAD` | `2f35f72988fac0a44292a6bd69196e0842cbfc73` | Matches immutable baseline |
| `git describe --tags --always` | `v1.2.3` | Matches tag |
| Version in source | `v.1.2.3`, `src/NetStuck/NetStuck.cs:82-84` | Verified |
| File version after build | `1.2.3.0` | Verified runtime output |
| Framework/compiler | .NET Framework 4.x, `csc.exe`, `scripts/Build-NetStuck.ps1:7-47` | Verified |
| Fresh automated baseline | 128/128 | Passed |
| Existing internal manifest baseline | 8/8 stated in the request; no matching repository artifact was located | Request-supplied, not independently verified |
| Production code changed | none | Verified at completion gate |

Baseline documents declare the same 128/128 split at `docs/releases/v1.2.3/TEST-REPORT.txt:11-17`; this audit nevertheless reran the suite and reports fresh measurements rather than copying the document.

## 4. UI Technology Stack

| Layer | Verified fact | Evidence |
| --- | --- | --- |
| Language/runtime | C#, .NET Framework 4.x, AnyCPU WinExe | `docs/ARCHITECTURE.md:3-8`; `scripts/Build-NetStuck.ps1:38-45` |
| Distribution | portable Windows folder/ZIP; application, tools and required third-party license remain together | `README-TH.md:3`, `README-TH.md:41`; `docs/RELEASING.md:6-7` |
| Executable entry point | `[STAThread] Program.Main` enables visual styles, selects compatible text rendering and calls `Application.Run(new MainForm())` | `src/NetStuck/NetStuck.cs:1888-1897` |
| UI framework | Windows Forms | `src/NetStuck/NetStuck.cs:15-20`, `src/NetStuck/NetStuck.cs:80` |
| Composition | One `partial MainForm`; no `.sln`/`.csproj`, DI or service layer | `docs/ARCHITECTURE.md:3-8`, `docs/ARCHITECTURE.md:13-20` |
| State/event handling | control fields, DataTables/BindingSources, CTS/timers and direct WinForms event delegates live across `partial MainForm`; no centralized UI state reducer | `docs/ARCHITECTURE.md:13-38`; active workflows cited in findings |
| Navigation | Owner-drawn top-level `TabControl` with 8 pages | `src/NetStuck/NetStuck.cs:96-98`, `src/NetStuck/NetStuck.cs:261-267` |
| Shared UI | Programmatic helpers: `NewPage`, `Card`, `SectionHeader`, buttons, field/grid/split helpers | `src/NetStuck/NetStuck.cs:1664-1805` |
| Layout | `TableLayoutPanel`, `FlowLayoutPanel`, `SplitContainer`, fixed/percent logical geometry | active builders cited in inventory |
| Tables | `DataTable` + `BindingSource` + `DataGridView` | `src/NetStuck/NetStuck.cs:1740-1760` and builders |
| Async model | `async/await`, `Task`, `CancellationTokenSource`, timers, concurrent queues, UI marshaling | architecture flows; `NetStuck.Release1.cs:130-165`; `NetStuck.Features.cs:589-687` |
| Rendering | Form/grid double buffering; batched Ping/Collector updates | `NetStuck.cs:188-204`; `NetStuck.Release1.cs:54-64`, `:130-165`; `NetStuck.Features.cs:1343-1385` |
| Theme | Hard-coded light palette plus owner draw | `NetStuck.cs:86-94`; `NetStuck.Release1.cs:89-112`; `NetStuck.V103.cs:540-580` |
| Assets | ICO is supplied to the compiler via `/win32icon`; PNG is a separate packaged `NetStuck-Icon.png`, not embedded in the EXE | `scripts/Build-NetStuck.ps1:18-45`; `scripts/Package-NetStuck.ps1:46` |
| DPI | `AutoScaleMode.Dpi`; no manifest/config in repository | `NetStuck.cs:211`; build arguments above |
| Localization | UI strings hard-coded English; no `.resx` | repository file inventory + builders |
| Supported Windows | Windows 10/11 | `docs/DEVELOPMENT.md:5-9` |

Active ownership must be respected: shell/shared UI in `NetStuck.cs`, active Live Ping/Traceroute in `NetStuck.V103.cs`, cross-cutting rendering/DNS in `NetStuck.Release1.cs`, Collector/state/zoom in `NetStuck.Features.cs`. Legacy builders coexist and must not be mistaken for active code.

## 5. Application Navigation Map

```text
MainForm
├─ Header: logo | NetStuck | subtitle | version
├─ Top-level TabControl (peer destinations)
│  ├─ Live Ping
│  │  ├─ Metrics
│  │  ├─ Targets + saved lists + probe settings + Start/Pause/Stop
│  │  └─ Filter/actions + realtime result grid + selected-target history
│  ├─ Traceroute
│  │  └─ Session tabs: Session 1 | Session 2
│  │     ├─ deterministic three-row input/action panel
│  │     ├─ hop result grid
│  │     └─ event filter + event grid
│  ├─ DNS Resolver
│  ├─ MAC / WAN Lookup
│  │  ├─ MAC Vendor pane
│  │  └─ WAN IP Intelligence pane
│  ├─ Calculators
│  │  ├─ IPv4 Subnet Calculator
│  │  └─ Unit Converter + quick reference
│  ├─ Config Collector
│  │  ├─ transport/device/credential form
│  │  ├─ command tabs: Basic commands | Collect commands
│  │  ├─ devices/output/actions
│  │  └─ result grid + terminal preview
│  ├─ Event Log
│  └─ Updates
└─ StatusStrip: global operation | time source/clock | local/public identity

Modal/native branches
├─ Save target list (name prompt)
├─ Replace/Delete profile confirmations
├─ Traceroute hop descriptions
├─ Realtime result columns
├─ SaveFileDialog exports
├─ FolderBrowserDialog
└─ Validation/warning/error MessageBox family
```

ไม่พบ Settings page, About dialog, Help page, first-run wizard, system tray/notification-area icon, context menu หรือ network-update workflow ใน production source. `Updates` เป็น offline read-only release notes ไม่ใช่ updater.

## 6. Complete UI Inventory

| Area | Screen/Menu | Purpose | Entry point | Main controls | Source | Runtime verified | Screenshot |
| ---- | ----------- | ------- | ----------- | ------------- | ------ | ---------------- | ---------- |
| Shell | MainForm header/status | identity, navigation, operation/time/network status | app launch | Panel, PictureBox, TabControl, StatusStrip | `NetStuck.cs:188-283` | Verified screenshots 1100×900/1460×900 | all runtime shots |
| Live Ping | Configuration + metrics | define targets/profile/probe and control session | `Live Ping` tab | TextBox, ComboBoxes, NumericUpDown, CheckBox, buttons, metric panels | `NetStuck.V103.cs:221-308` | Verified idle + automated active state | [1100](ui-audit/screenshots/runtime-live-ping-1100.png), [1460](ui-audit/screenshots/runtime-live-ping-1460.png) |
| Live Ping | Realtime results/filter/actions | inspect/sort/filter/copy/export/customize results | right pane | filter TextBox, status ComboBox, 5 buttons, DataGridView | `NetStuck.V103.cs:310-355` | Verified empty + automated populated behavior | same |
| Live Ping | Selected-target history | view/export per-target samples | click result row | SplitContainer, DataGridView, Export button | `NetStuck.V103.cs:357-374`; `NetStuck.Features.cs:1470-1494` | Partially verified | same |
| Live Ping | Saved profiles | load/save/delete local target lists | top-left section | ComboBox, Load/Save/Delete, name prompt | `NetStuck.V103.cs:248-262`; `NetStuck.cs:1477-1545` | Partially verified | same |
| Traceroute | Session navigation | two independent traces | `Traceroute` tab | nested TabControl | `NetStuck.V103.cs:377-391` | Verified | [1100](ui-audit/screenshots/runtime-traceroute-1100.png), [1460](ui-audit/screenshots/runtime-traceroute-1460.png) |
| Traceroute | Session 1 | configure/run/inspect primary trace | Session 1 tab | editable target ComboBox, 6 numeric/select fields, Start/Pause/Stop, grid | `NetStuck.V103.cs:394-480` | Verified idle + automated active | same |
| Traceroute | Session 2 | independent second trace | Session 2 tab | same session controls | `NetStuck.V103.cs:394-480` | Automated behavior; screenshot session 1 | same |
| Traceroute | Event Log | filter route/DNS/service/error events | right pane per session | ComboBox + DataGridView | `NetStuck.V103.cs:482-501` | Verified empty + automated event state | same |
| DNS | DNS Resolver | one-shot/poll forward and reverse DNS | `DNS Resolver` tab | multiline TextBox, CheckBox, server TextBox, NumericUpDown, 3 buttons, grid | `NetStuck.cs:507-545`; `NetStuck.Release1.cs:228-354` | Verified idle + automated resolving/polling | [1100](ui-audit/screenshots/runtime-dns-resolver-1100.png), [1460](ui-audit/screenshots/runtime-dns-resolver-1460.png) |
| Lookup | MAC Vendor | classify local/multicast MACs, use OUI cache, or call vendor API on registered cache miss | left half of `MAC / WAN Lookup` | multiline TextBox, Lookup button, grid | `NetStuck.cs:547-559`, `:1089-1157` | Verified idle + cached/local paths; external cache-miss call not run | [1100](ui-audit/screenshots/runtime-mac-wan-lookup-1100.png), [1460](ui-audit/screenshots/runtime-mac-wan-lookup-1460.png) |
| Lookup | WAN IP Intelligence | external public-IP metadata lookup | right half | multiline TextBox, Lookup button, grid | `NetStuck.cs:559-570`, `:1160-1195` | Verified idle; external live call not performed | same |
| Calculators | IPv4 Subnet Calculator | calculate subnet details | left half of `Calculators` | TextBox, Calculate button, read-only TextBox | `NetStuck.cs:573-586` | Verified idle + core tests | [1100](ui-audit/screenshots/runtime-calculators-1100.png), [1460](ui-audit/screenshots/runtime-calculators-1460.png) |
| Calculators | Unit Converter | convert rate/storage units | right half | value TextBox, 2 ComboBoxes, Convert, result Label/reference | `NetStuck.cs:586-610` | Verified idle + core tests | same |
| Collector | Transport/device/credential form | configure SSH/Telnet and AUTH1/AUTH2 | `Config Collector` left pane | ComboBoxes, NumericUpDowns, CheckBoxes, credential TextBoxes | `NetStuck.Features.cs:408-463` | Verified synthetic/no secrets | [1100](ui-audit/screenshots/runtime-config-collector-1100.png), [1460](ui-audit/screenshots/runtime-config-collector-1460.png) |
| Collector | Basic commands | terminal preparation commands | nested tab | multiline TextBox | `NetStuck.Features.cs:464-473` | Verified visible | same |
| Collector | Collect commands | commands to capture | nested tab | multiline TextBox | `NetStuck.Features.cs:474-477` | Verified via tab existence; not separate screenshot | same |
| Collector | Devices/output/actions | device list, output folder, preflight/export/run/cancel | lower left | multiline TextBox, path TextBox, Browse, five actions | `NetStuck.Features.cs:479-504` | Verified synthetic | same |
| Collector | Result grid | per-device state and output link | right/top | DataGridView | `NetStuck.Features.cs:506-519`, `:649-687` | Verified empty + mock integration tests | same |
| Collector | Terminal preview | bounded batched session preview | right/bottom | RichTextBox | `NetStuck.Features.cs:514-519`, `:1343-1385` | Verified synthetic + tests | same |
| Log | Realtime Event Log | filter/export/clear operational events | `Event Log` tab | search, level ComboBox, Export/Clear, grid | `NetStuck.cs:613-631` | Verified synthetic row | [1100](ui-audit/screenshots/runtime-event-log-1100.png), [1460](ui-audit/screenshots/runtime-event-log-1460.png) |
| Support | Updates | offline release notes | `Updates` tab | read-only multiline TextBox | `NetStuck.cs:633-718` | Verified | [1100](ui-audit/screenshots/runtime-updates-1100.png), [1460](ui-audit/screenshots/runtime-updates-1460.png) |
| Dialog | Save target list | name a profile | Save list | fixed Form, Label, TextBox, Save/Cancel | `NetStuck.cs:1808-1819` | Source only | — |
| Dialog | Hop descriptions | edit address-description mappings | Traceroute button | resizable Form, hint, multiline TextBox, Save/Cancel | `NetStuck.Release1.cs:168-202` | Source only | — |
| Dialog | Realtime result columns | show/hide Ping columns | Columns button | CheckedListBox, Apply, Select all | `NetStuck.Features.cs:1503-1518` | Source + automated action existence | — |
| Native dialog | CSV export | choose output file | Ping/history/log/Collector export | SaveFileDialog | `NetStuck.cs:1464-1474`; `NetStuck.Features.cs:1439-1453` | Source only | — |
| Native dialog | Collector output folder | choose capture directory | Browse | FolderBrowserDialog | `NetStuck.Features.cs:1456-1460` | Source only | — |
| Confirmation | Replace/Delete profile | protect saved-list mutation | save existing/delete | MessageBox Yes/No | `NetStuck.cs:1521-1525`, `:1538-1545` | Source only | — |
| Confirmation | Large target session | confirm >512 targets | Start Ping | MessageBox Yes/No | `NetStuck.cs:721-729` | Source + branch in tests not interactive | — |
| Feedback | Validation/error dialog family | report invalid input/failure | multiple workflows | MessageBox | Ping/DNS/MAC/WAN/Collector call sites | Source only | — |

### Missing surface/state inventory

Verified absent: global settings, first-run, About/help, system tray, context menus and native notification toasts. Empty areas are rendered as blank grids/text regions rather than a shared empty-state component. Loading/progress exists unevenly as row text, button text and global status; details are in sections 15–16.

## 7. Runtime Inspection Results

### Commands and fresh results

```powershell
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
```

| Suite | Result | Fresh measurement/evidence |
| --- | ---: | --- |
| `NetOpsCoreTests` | 16/16 | parsing, CIDR, subnet, units, CSV |
| `FeatureTests` | 91/91 | UI/integration/layout/security/state |
| `PerformanceTests` | 10/10 | startup 926 ms; 11 double-buffered grids; `/24` 2,412 probes; 19 ms worst dispatch; dual Trace 46 ms; 73 MB |
| `PollingCadenceTests` | 3/3 | bounded slots 6/3; Ping 11 vs 3; Trace 12 vs 4 |
| `OvernightSoakTests --seconds 10` | 8/8 | 11.45 s; worst dispatch 19 ms; memory growth 8 MB |
| Total | **128/128** | all suites passed |

### Visual runtime coverage

- แสดงทุก top-level tab ด้วย sanitized identity และ RFC 5737 active-target fixtures ที่ 1100×900 และ 1460×900; built-in documentation/default examples เช่น public DNS และ private-range examples ยังคงปรากฏแต่ไม่ได้ถูก probe; ไม่พบ overlap ในภาพ 1100×900 ที่ตรวจ
- Traceroute source กำหนด deterministic input rows, dedicated action row และ 120px Protocol/Port/Packet fields ที่ `NetStuck.V103.cs:426-439`; `FeatureTests.cs:202-233` ยืนยัน frame bounds, adjacency, equal service-field widths และ no overlap ที่ form width 1100/height 900 แต่ไม่ assert ค่า 120 หรือ 700-height
- Collector ที่ 1100×900 ยังเข้าถึง transport, AUTH rows, device list, actions, command tabs, result grid และ terminal ได้ แต่ density สูงและมี fixed rows/columns จึงยังต้อง minimum-height/DPI/zoom visual test
- Initial/empty grids ที่ตรวจใน Live Ping, Trace, DNS, MAC/WAN และ Collector เป็นพื้นที่ว่าง ไม่มี explanatory text/next action; Event Log capture จงใจมี synthetic row จึงไม่ใช่ empty-state visual evidence
- `runtime-traceroute-protocol-open-*` ไม่เห็น popup overlay เพราะ capture API limitation; จึงไม่ถือว่า dropdown-open visual gate ผ่าน

### Accessibility runtime inventory

จาก isolated form instance:

- interactive controls: 130
- controls with explicit `AccessibleName`: 0
- controls with explicit `AccessibleDescription`: 0
- interactive controls with `TabIndex == 0`: 53
- buttons with mnemonic marker `&`: 0
- interactive controls with `TabStop == false`: 0

ผลนี้ยืนยัน metadata gap แต่ยังไม่เท่ากับผล Narrator/UI Automation จริง เพราะ native controls อาจ derive name จาก `Text`; field association, traversal order และ live announcement ยังต้อง manual/tool verification.

### Runtime status by requested scenario

| Scenario | Status |
| --- | --- |
| Initial window / all top-level tabs | Verified at 100%, 1100×900 and 1460×900 |
| Major screen layout / minimum 1100×700 | 1100-width geometry passed at height 900; 700-height geometry/runtime visual Not verified |
| Active Ping/Trace/DNS/Collector states | Automated tests passed; safe screenshots are idle/synthetic |
| Filter/sort/copy/resize/reorder | Existing automated checks/source support; manual full interaction not repeated for every grid |
| Keyboard-only / tab order / visible focus | Partially/Not verified |
| Disabled/validation states | Source + automated state tests; dialogs not visually captured |
| Long Thai/English labels | Not verified |
| 125/150/200% DPI | Not verified |
| High Contrast / screen reader | Not verified |
| Loading/failure/empty states | Partially verified; matrix in section 16 |
| Real external network/device | Not performed by design |

## 8. Top Findings

Priority meaning: **P0** ใช้งานไม่ได้/ข้อมูลหรือความปลอดภัยเสียหายทันที, **P1** กระทบงานหลักหรือผู้ใช้บางกลุ่มอย่างมีนัยสำคัญ, **P2** คุณภาพ/ความสม่ำเสมอ/robustness ที่ควรวางแผน, **P3** polish หรือ debt ความเสี่ยงต่ำ. Effort เป็น implementation estimate แบบสัมพัทธ์: **S** ไม่เกินประมาณ 2 วัน, **M** 3–5 วัน, **L** มากกว่า 1 สัปดาห์ รวม tests แต่ไม่รวม external certification.

| ID | Priority | Screen | Category | Problem | Evidence | Recommendation | Effort |
| -- | -------- | ------ | -------- | ------- | -------- | -------------- | ------ |
| A11Y-01 | P1 | All/Collector | Accessibility | ไม่มี explicit names/field association | runtime 130 controls, 0 explicit names; `Features.cs:451-461` | persistent labels + UIA metadata | M |
| A11Y-02 | P1 | All/custom draw | Accessibility/visual | contrast ต่ำและไม่มี High Contrast branch | palette `NetStuck.cs:86-94`; measured 2.56–4.43:1 | semantic/system-color tokens + non-color cues | M |
| ASYNC-01 | P1 | Traceroute | Async state | stale cycle เปลี่ยน topology ก่อน guard | `NetStuck.V103.cs:1011-1022`, guards `:1040-1095` | generation gate ก่อน atomic apply | M |
| PERF-01 | P1 | Collector | Performance | UI-thread file finalize; preview batch ข้าม byte budget | `Features.cs:649-678`, `:1206-1285`, `:1357-1365` | background finalize + bounded/chunked preview | L |
| STATE-01 | P1 | Collector | Operation state | `Ping all` ไม่มี cancel/re-entry/mutual exclusion | `Features.cs:485-496`, `:565-603` | shared state machine + bounded concurrency | M |
| STATE-02 | P1 | Collector | State/security | queued devices อ่าน live controls | `Features.cs:589-622`, `:656-705`, `:837-842` | immutable password-safe run snapshot | M |
| STATE-03 | P1 | Traceroute | Error state | failure อาจค้าง `Discovering` | `NetStuck.V103.cs:869-881`, `:956-967` | explicit terminal-state reducer | S |
| A11Y-03 | P2 | All/dialogs | Keyboard | tab/access-key/focus contract ไม่กำหนด | 53 controls `TabIndex==0`; 0 mnemonics | logical order, mnemonics, focus return | M |
| A11Y-04 | P2 | All result grids | Table semantics | result cells ดู/ประกาศเป็น editable | `NetStuck.cs:1740-1760`; suppression `Release1.cs:66-74` | shared explicit read-only grid | S |
| DPI-01 | P2 | All/Collector/Trace | DPI/layout | ไม่มี verified 125–200%/manifest/long text | `NetStuck.cs:211`; `FeatureTests.cs:39-42` | DPI declaration + responsive test matrix | L |
| VAL-01 | P2 | Input workflows/Collector | Validation | modal field errors; some Collector preflight exceptions occur before handler `try` | trace `V103.cs:869-873`; Collector `Features.cs:540-600`, try at `:614` | validate/catch expected input/path failures before mutation | M |
| STATE-04 | P2 | Shell/all operations | Global state | workflows เขียน `Ready` แข่งกัน | `Release1.cs:76-86`; direct writes cited below | central operation registry + local status | M |
| ASYNC-02 | P2 | Network workflows | Lifecycle | Stop/Close ไม่ cancel underlying work ครบ | close `NetStuck.cs:1835-1859`; source lifecycle review | structured cancellation/shutdown | M |
| PERF-02 | P2 | Trace/zoom/shell | Resources | cache/font/timer bounds/disposal ไม่ครบ | `V103.cs:138-201`; `Features.cs:1616-1628` | LRU + explicit owned-resource disposal | M |
| CTRL-01 | P2 | Action toolbars | Controls | primary/cancel/destructive weight ปะปน | button factories `NetStuck.cs:1696-1720`; screenshots | one primary; separate cancel/destructive | M |
| ARCH-01 | P2 | Repository UI | Architecture | legacy builders/raw metrics ซ้ำ | `NetStuck.cs:285-506`, `:1664-1805`; `V103.cs:221-503` | characterize then consolidate incrementally | L |
| DLG-01 | P2 | Custom dialogs | Dialogs | footer/focus/validation inconsistent | `Release1.cs:168-202`; `Features.cs:1503-1518` | native shared dialog shell | M |
| PERSIST-01 | P2 | Grids/splits | Personalization | width/order/split ไม่ persist | state code `Features.cs:22-90`, `:1646-1727` | versioned sanitized view state | M |
| STATE-05 | P2 | Data screens | Feedback | blank grids; statesไม่ครบ | safe screenshots; helpers `NetStuck.cs:1664-1805` | shared in-place state presenter | M |
| TEST-01 | P2 | Whole app | Testability | DPI/a11y/theme/state isolation gaps | `FeatureTests.cs:39-42`; persistence paths `NetStuck.cs:212-230` | isolated root + semantic/visual matrix | L |
| SHELL-01 | P3 | StatusStrip | Responsive layout | fixed-width local/public identity slotsเสี่ยง truncate | `NetStuck.V103.cs:117-126`; 1100×900 screenshot | retain spring; make identity slots priority-aware/accessibly complete | S |
| UPDATE-01 | P3 | Updates | Content | monolithic TextBox | `NetStuck.cs:633-718`; screenshots | structured offline read-only content | S–M |

### A11Y-01 — Explicit names and field associations

| Field | Detail |
| --- | --- |
| ID | `A11Y-01` |
| Priority | P1 |
| Confidence | High |
| Category | Accessibility / forms |
| Affected menu/screen | ทั้งแอป โดยเฉพาะ Config Collector AUTH1/AUTH2, Live Ping, Traceroute และ dialogs |
| Evidence | Runtime isolated inventory พบ interactive controls 130 รายการ แต่ explicit `AccessibleName` 0 และ `AccessibleDescription` 0; ไม่พบ declarations ใน production source. Collector สร้าง credential fields/cue banners ที่ `src/NetStuck/NetStuck.Features.cs:451-461`; generic field/label helpers ที่ `src/NetStuck/NetStuck.cs:1724-1732`, cue implementation ที่ `:1881-1884`. |
| Current behavior | ผู้ใช้สายตาปกติเห็น label/placeholder และ native controls; assistive technology ต้องอนุมานชื่อจาก `Text`/ลำดับ visual ซึ่งไม่รับประกัน association โดยเฉพาะ placeholder ที่หายเมื่อพิมพ์. |
| Problem | ไม่มีชื่อถาวร คำอธิบาย หรือ programmatic label relation ที่ตรวจได้สำหรับ control สำคัญ. |
| User impact | ผู้ใช้ screen reader อาจไม่ทราบว่า credential box ใดคือ username/password, ค่าใดเป็นหน่วยใด หรือ error อ้างถึง field ไหน. |
| Recommended change | เพิ่ม visible labels ที่คงอยู่, `AccessibleName`/`AccessibleDescription` ตามบริบท, label-to-control association และ automation identity ที่เสถียร; ห้ามใส่ credential value ใน metadata/log. |
| Why this control/pattern is appropriate | Visible label ทำงานกับทั้งการสแกนด้วยตาและ assistive tech; accessible metadata เป็น contract ของ WinForms control โดยไม่เปลี่ยน workflow. |
| Estimated effort | M |
| Dependencies | Naming glossary, reusable labeled-field helper, accessibility test harness. |
| Risk | เปลี่ยน layout แล้วอาจกระทบ minimum width; metadata ซ้ำ/ผิดบริบทหากสร้างจาก generic text อย่างเดียว. |
| Acceptance criteria | ทุก interactive control มีชื่อที่มีความหมายและไม่ซ้ำใน scope; AUTH fields มี visible persistent labels; Narrator ประกาศ label, role, value/state และ validation relation; automated scan ไม่มี unnamed actionable control ยกเว้น documented native exception. |

### A11Y-02 — Contrast and High Contrast

| Field | Detail |
| --- | --- |
| ID | `A11Y-02` |
| Priority | P1 |
| Confidence | Medium — contrast is source/calculation-backed; High Contrast runtime not tested |
| Category | Accessibility / color / theming |
| Affected menu/screen | Shell, Live Ping metrics/status, Traceroute states/events, buttons/tabs และทุก custom-drawn surface |
| Evidence | Hard-coded palette `src/NetStuck/NetStuck.cs:86-94`; Ping semantic fills/text `:1573-1580`; hard theme application `:1822-1832`; owner-drawn tabs `src/NetStuck/NetStuck.Release1.cs:89-112`; Traceroute owner-drawn protocol selector/border `src/NetStuck/NetStuck.V103.cs:540-580`. Calculated pairs include success-on-pale ~3.15:1, warning-on-pale ~3.07:1, danger-on-pale ~4.41:1, muted-on-canvas ~4.43:1 และ trace border ~2.56:1. High Contrast ไม่ได้ทดสอบ. |
| Current behavior | สีเป็น light theme คงที่และใช้สีเพื่อสื่อ success/warning/danger/active ร่วมกับข้อความบางตำแหน่ง. |
| Problem | บาง text/indicator ต่ำกว่า WCAG 4.5:1 และ custom draw อาจไม่เคารพ system colors/High Contrast. |
| User impact | ผู้ใช้สายตาเลือนรางหรือ contrast sensitivity อาจอ่านสถานะ/ขอบ focus ยาก; High Contrast อาจสูญเสีย cue. |
| Recommended change | กำหนด semantic tokens ที่ผ่าน 4.5:1 สำหรับ normal text และ 3:1 สำหรับ non-text/focus, เพิ่ม icon/text/state shape ไม่พึ่งสีอย่างเดียว และใช้ system colors/renderer branch เมื่อ High Contrast. |
| Why this control/pattern is appropriate | Semantic tokens คุม contrast จากจุดเดียว ขณะที่ native/system rendering รักษาพฤติกรรม Windows accessibility. |
| Estimated effort | M |
| Dependencies | Design-system tokens, contrast unit tests, High Contrast visual fixture. |
| Risk | สีเปลี่ยนอาจกระทบ visual identity/snapshot; owner-drawn controls ต้องทดสอบทุก state. |
| Acceptance criteria | ทุกคู่สีที่ใช้งานผ่าน target contrast; focus indicator ≥3:1; success/warning/error มี text หรือ icon; Windows High Contrast แสดงทุก control/state/selection/focus ได้; ไม่มี hard-coded custom color บัง system theme โดยไม่มี documented exception. |

### ASYNC-01 — Stale Traceroute cycle mutates topology

| Field | Detail |
| --- | --- |
| ID | `ASYNC-01` |
| Priority | P1 |
| Confidence | Medium — source-confirmed ordering; runtime race not reproduced |
| Category | Async correctness / state |
| Affected menu/screen | Traceroute Session 1 และ Session 2 |
| Evidence | Cycle completion/apply path `src/NetStuck/NetStuck.V103.cs:902-913`; `ApplyTraceCycle` เปลี่ยน `KnownDestinationHop`/lists และลบ rows ที่ `:1011-1022` ก่อน freshness guards ที่ `:1040-1069` และ `:1081-1095`. |
| Current behavior | รอบ trace หลายรอบทำงาน async; guards ป้องกันบาง row/event updates จาก cycle เก่า แต่ topology mutation บางส่วนเกิดก่อน guard. |
| Problem | completion ล่าช้าของ cycle เก่าอาจลด destination hop หรือ prune rows ที่ cycle ใหม่เพิ่งสร้าง. |
| User impact | เส้นทางกระพริบ/ย้อนกลับ, hop หายชั่วคราว หรือ state ของสองช่วงเวลาไม่สอดคล้อง ทำให้วิเคราะห์ network ผิด. |
| Recommended change | ตรวจ session generation/cycle identity ก่อน topology mutation ทั้งหมด; สร้าง immutable cycle result แล้ว apply บน UI thread แบบ atomic เฉพาะ current generation. |
| Why this control/pattern is appropriate | Generation gate เป็น pattern ตรงกับ polling UI ที่ response อาจกลับไม่เรียงลำดับ และรักษา cadence/two-session architecture เดิม. |
| Estimated effort | M |
| Dependencies | Deterministic delayed-probe fixture, trace state reducer/apply boundary. |
| Risk | ถ้าวาง guard กว้างเกินไปอาจทิ้ง legitimate partial result; ต้องไม่เปลี่ยน fixed cadence. |
| Acceptance criteria | Test บังคับ cycle N กลับหลัง N+1 แล้ว topology/rows/events เท่ากับ N+1 เท่านั้น; cancel/restart generation เก่าเปลี่ยน UI ไม่ได้; cadence และ dual-session tests เดิมผ่าน. |

### PERF-01 — Collector UI-thread finalize and terminal batch bounds

| Field | Detail |
| --- | --- |
| ID | `PERF-01` |
| Priority | P1 |
| Confidence | Medium — source/profiling risk; ไม่ reproduce freeze ใน fresh 10-second suite |
| Category | UI responsiveness / batching |
| Affected menu/screen | Config Collector result/terminal และ post-processing |
| Evidence | Run/finalize flow `src/NetStuck/NetStuck.Features.cs:589-687`; stream pumps ใช้ 2,048-character buffer ที่ `:250-264` และ writer เขียน character chunks ผ่าน UTF-8 `StreamWriter` ใต้ lock ที่ `:141-164`; post-run file scan/redact/JSON work `:649-678`, `:1206-1285`; preview items ได้ถึง 524,288 chars `:141-164`; drain ตรวจ budget ก่อน append ทั้ง item `:1357-1365`; queue `:403-405`, enqueue `:1343-1349`; trim `:1367-1375`. `docs/ARCHITECTURE.md:63` ระบุ 128 KiB/pass แต่ code path oversized item ไม่ enforce แบบแบ่ง chunk. Fresh suite ยังผ่าน worst dispatch 19 ms. |
| Current behavior | Network output ถูก queue แล้ว drain เป็น batch; finalize กลับสู่ captured UI context ก่อนอ่าน/แปลงไฟล์และอัปเดต terminal. |
| Problem | ไฟล์ใหญ่/อุปกรณ์มากอาจ block UI; queue ไม่มี explicit item/byte bound และ item ใหญ่ข้าม intended per-pass budget. |
| User impact | scrolling/cancel/window repaint ช้า, memory spike หรือ app ดูค้างระหว่าง long collection. |
| Recommended change | ทำ file scan/redact/serialization off UI thread, marshal เฉพาะ compact result; chunk terminal items ตาม byte/char budget, ใส่ bounded backpressure/drop policy ที่ไม่ทิ้ง canonical output และ profile worst-case fixture. |
| Why this control/pattern is appropriate | Producer/consumer ที่มี byte budget แยก data capture ที่ถูกต้องออกจาก preview ซึ่งเป็น best-effort และรักษา responsive UI. |
| Estimated effort | L |
| Dependencies | Synthetic large-output device, cancellation design, telemetry/profiling counters. |
| Risk | chunking ผิดอาจตัด ANSI/text boundary; backpressure ห้ามทำ canonical output สูญหาย; redaction ต้องคง password-free invariant. |
| Acceptance criteria | 10+ devices และ ≥10 MiB/device fixture ยัง repaint/cancel ได้; existing dispatch gates ยังผ่านและรายงาน p50/p95/max เพื่ออนุมัติ future SLA; ไม่มี pass เกิน documented byte budget; preview memory bounded; canonical/redacted files byte/secret tests ผ่าน; existing 128 checks ผ่าน. |

### STATE-01 — Collector Ping all operation state

| Field | Detail |
| --- | --- |
| ID | `STATE-01` |
| Priority | P1 |
| Confidence | Medium — source-confirmed state gap; active overlap not reproduced |
| Category | Operation state / responsiveness |
| Affected menu/screen | Config Collector — `Ping all`, `Collect`, `Cancel` |
| Evidence | Button hookup/local implementation `src/NetStuck/NetStuck.Features.cs:485-496`; sequential `foreach await` `:565-585`; Collect clears/uses same table and terminal at `:589-603`; only Collect start button is disabled at `:605-608`. |
| Current behavior | `Ping all` probes devices one by one; button remains callable, has no dedicated CTS/progress/Cancel contract และแชร์ result surface กับ Collect. |
| Problem | ผู้ใช้เริ่มซ้ำหรือเริ่ม Collect ระหว่าง preflight ได้; long list ไม่มี bounded concurrency หรือ explicit cancellation. |
| User impact | mixed/stale rows, unclear operation ownership, wait time ยาว และ Cancel อาจไม่หยุดงานที่ผู้ใช้คิดว่ากำลังทำ. |
| Recommended change | ใช้ shared Collector operation state machine (`Idle/Preflighting/Collecting/Cancelling/Completed/Failed`), mutually exclusive commands, one CTS per generation, bounded probe concurrency และ progress count. |
| Why this control/pattern is appropriate | Explicit state machine ทำให้ enablement/status/cancel deterministic และ bounded concurrency ลดเวลาโดยไม่ flood network. |
| Estimated effort | M |
| Dependencies | Shared operation controller, probe cancellation support, row generation IDs. |
| Risk | concurrency สูงเกินนโยบาย network; cancellation race กับ Collect ต้องมี tests. |
| Acceptance criteria | กด `Ping all` ซ้ำหรือ `Collect` ระหว่าง run ไม่ได้; Cancel หยุด preflight ภายใน configured timeout; progress แสดง completed/total; late result จาก generation เก่าไม่เขียนตาราง; concurrency มี config/tested bound. |

### STATE-02 — Collector configuration is not snapshotted

| Field | Detail |
| --- | --- |
| ID | `STATE-02` |
| Priority | P1 |
| Confidence | Medium — source-confirmed live reads; control mutation during run not reproduced |
| Category | State consistency / security |
| Affected menu/screen | Config Collector |
| Evidence | Start/queue `src/NetStuck/NetStuck.Features.cs:589-622`; credentials ถูกอ่านต่อ device ที่ `:656-659`, `:691-705`; live options/device type/strict behavior ถูกอ่านที่ `:653`, `:737-738`, `:837-842`, `:1247-1253`; disable เฉพาะ start action ที่ `:605-608`. |
| Current behavior | queued work อ่านค่าจาก controls ขณะ run ดำเนินต่อ จึงเปลี่ยน transport/device/credentials/options ระหว่างงานได้. |
| Problem | devices ใน batch เดียวกันอาจรันด้วย configuration ต่างกันโดยไม่มี audit trail; live password field อาจถูกอ่านช้ากว่าที่ผู้ใช้คาด. |
| User impact | collection ไม่ reproducible, fail แบบอธิบายยาก และเพิ่มโอกาสใช้ credential/strict mode ผิดชุด. |
| Recommended change | validate แล้วสร้าง immutable password-safe run snapshot ก่อน queue; disable/lock config section ระหว่าง run หรือทำ changes เป็น next-run draft; secret อยู่ใน memory scope สั้นและไม่ serialize/log. |
| Why this control/pattern is appropriate | Snapshot ทำให้ batch transaction มี input เดียว ขณะที่ draft UI ยังแก้สำหรับ run ถัดไปได้อย่างชัดเจน. |
| Estimated effort | M |
| Dependencies | CollectorRunOptions model, validation summary, secret lifetime policy. |
| Risk | model serialization เผลอบันทึก secrets; must remain password-free argv/state/logs. |
| Acceptance criteria | ทุก device ใน generation ใช้ snapshot hash/options เดียวกัน; เปลี่ยน UI ระหว่าง run ไม่เปลี่ยน active work; snapshot/log/debug output ไม่มี plaintext secret; unit test เปลี่ยน controls หลัง start แล้วยืนยัน transport/commands/options เดิม. |

### STATE-03 — Traceroute failure leaves ambiguous state

| Field | Detail |
| --- | --- |
| ID | `STATE-03` |
| Priority | P1 |
| Confidence | Medium — source-confirmed exit path; failure visual not captured |
| Category | Error state / recovery |
| Affected menu/screen | Traceroute sessions |
| Evidence | Empty-target branch `src/NetStuck/NetStuck.V103.cs:869-873`; state set `Discovering` at `:879-881`; resolution failure logs event at `:956-959`; `finally` re-enables controls without explicit terminal state correction at `:960-967`. |
| Current behavior | Start sets discovering; invalid/resolution-failed path reports message/event and re-enables action, แต่ session status อาจยังสื่อว่ากำลังค้นหา. |
| Problem | UI state, button enablement และ actual operation lifecycle ไม่เป็น transaction เดียว. |
| User impact | ผู้ใช้ไม่แน่ใจว่างานจบ/ล้มเหลวหรือยังและอาจรอโดยไม่จำเป็น. |
| Recommended change | centralize session reducer; every exit sets explicit `Idle`, `Running`, `Paused`, `Cancelled`, `Completed`, `Failed` พร้อม inline reason และ retry. |
| Why this control/pattern is appropriate | Finite states ป้องกัน forgotten branch และทำ automated assertions ได้. |
| Estimated effort | S |
| Dependencies | Session state enum/renderer and failure fixtures. |
| Risk | เปลี่ยนข้อความอาจกระทบ reflection/text tests; preserve control names/layout contract. |
| Acceptance criteria | Empty/invalid/unresolvable target จบที่ `Failed` หรือ `Idle` ตาม documented contract, Start enabled, Pause/Stop disabled, reason visible, focus กลับ field; no status remains `Discovering`; tests ครบทุก exit. |

### A11Y-03 — Keyboard order, access keys and focus return

| Field | Detail |
| --- | --- |
| ID | `A11Y-03` |
| Priority | P2 |
| Confidence | Medium — metadata/source gap confirmed; full traversal/focus behavior not run |
| Category | Accessibility / keyboard |
| Affected menu/screen | ทุก tab และ custom dialogs |
| Evidence | Source ไม่มี explicit `TabIndex` หรือ mnemonic `&`; runtime พบ interactive controls `TabIndex == 0` จำนวน 53 และ mnemonic buttons 0. Custom dialogs ที่ `src/NetStuck/NetStuck.Release1.cs:168-202`, `src/NetStuck/NetStuck.Features.cs:1503-1518`, `src/NetStuck/NetStuck.cs:1808-1819`. Manual keyboard traversal ไม่ได้ตรวจ. |
| Current behavior | WinForms ใช้ creation/container order; Enter/Escape มีเฉพาะบาง dialog ผ่าน Accept/Cancel buttons, top-level actions ไม่มี documented access keys/focus return. |
| Problem | traversal อาจข้าม logical zones/วนผิดลำดับ และ frequent actions ต้อง tab หลายครั้ง. |
| User impact | ผู้ใช้ keyboard-only ใช้งานช้า สูญเสียตำแหน่งหลัง validation/dialog หรือไม่เห็น focus ใน custom-drawn states. |
| Recommended change | กำหนด `TabIndex` ตาม visual flow, mnemonics ที่ไม่ชนกัน, default/cancel semantics, focus initial/return policy และ visible focus ที่ contrast ผ่าน. |
| Why this control/pattern is appropriate | Native keyboard model คาดเดาได้และไม่ต้องเพิ่ม dependency. |
| Estimated effort | M |
| Dependencies | Screen-level keyboard map and localization-ready labels. |
| Risk | mnemonic เปลี่ยน visible labels; dynamic enable/hidden controls ต้องคง traversal. |
| Acceptance criteria | ทุก screen ผ่าน keyboard-only script; focus orderตรง hierarchy; Alt shortcuts unique ต่อ scope; Escape/Enter ทำตาม documented action; หลัง error focus field แรกที่ผิด; หลัง dialog focus กลับ invoker; focus visible ทุก theme. |

### A11Y-04 — Result grids expose edit semantics

| Field | Detail |
| --- | --- |
| ID | `A11Y-04` |
| Priority | P2 |
| Confidence | Medium — source-confirmed properties; UI Automation semantics not inspected |
| Category | Accessibility / data tables |
| Affected menu/screen | Live Ping, Trace, DNS, MAC/WAN, Collector, Event Log grids |
| Evidence | Shared grid defaults at `src/NetStuck/NetStuck.cs:1740-1760` ไม่กำหนด read-only semantics; later editor suppression ที่ `src/NetStuck/NetStuck.Release1.cs:66-74` บังคับบาง interaction แต่ไม่ได้ทำให้ accessibility role/state ชัด. |
| Current behavior | ผู้ใช้เลือก/copy/sort/resize/reorder ได้; edit ถูกสกัดบางเส้นทางภายหลัง. |
| Problem | control/assistive tech อาจประกาศ cell editable หรือเข้า edit mode ทั้งที่ result เป็น read-only. |
| User impact | keyboard/screen-reader interaction สับสนและมี mode transition ไม่จำเป็น. |
| Recommended change | สร้าง explicit read-only result-grid variant (`ReadOnly`, edit mode, selection mode, accessible description) และ editable variant แยกสำหรับอนาคต. |
| Why this control/pattern is appropriate | DataGridView native read-only mode รักษา sort/select/copy ขณะที่ลด false edit affordance. |
| Estimated effort | S |
| Dependencies | Shared grid factory + regression tests for copy/sort/column actions. |
| Risk | setting บางอย่างอาจกระทบ keyboard copy/selection; verify all grids. |
| Acceptance criteria | result grids ไม่เข้า edit modeด้วย mouse/keyboard, Narrator ประกาศ read-only table/cell state, sorting/copying/row selection/resize/reorder ยังผ่าน tests และ editable exception ถูกระบุชัด. |

### DPI-01 — DPI, zoom and localization gates are incomplete

| Field | Detail |
| --- | --- |
| ID | `DPI-01` |
| Priority | P2 |
| Confidence | Medium — source and 100% runtime verified; higher scaling not run |
| Category | Platform / responsive layout / localization |
| Affected menu/screen | ทั้งแอป โดยเฉพาะ Traceroute deterministic row, Collector และ status strip |
| Evidence | `AutoScaleMode.Dpi` ที่ `src/NetStuck/NetStuck.cs:211`; build arguments ไม่มี manifest ที่ `scripts/Build-NetStuck.ps1:38-45`; repository ไม่มี app manifest/config; feature tests disable scaling ที่ `tests/FeatureTests.cs:39-42`; zoom รองรับเฉพาะ TextBox/RichTextBox/grid และ Ctrl+wheel ที่ `src/NetStuck/NetStuck.Features.cs:1595-1629`. Runtime visual รอบนี้มี 100% ที่ 1100×900 และ 1460×900. Source ประกาศ `MinimumSize = 1100×700` ที่ `NetStuck.cs:196`, แต่ test ตั้ง 1460×900 ที่ `FeatureTests.cs:45-46` แล้วเปลี่ยนเฉพาะ width เป็น 1100 ที่ `:226`. |
| Current behavior | minimum size 1100×700 เป็น source-declared contract; มี automated 1100-width geometry ที่ height 900 แต่ไม่มี 700-height gate/capture; font scaling บาง control อาศัย WinForms DPI, app zoom ครอบคลุมเฉพาะบางชนิด; strings hard-coded English. |
| Problem | process DPI awareness ไม่ประกาศชัด, mixed fixed/absolute sizes อาจ clip ที่ 125–200% หรือ long strings; app zoom ไม่ใช่ whole-interface zoom. |
| User impact | fields/actions/status อาจถูกตัด, overlap หรือหลุด viewport บน high-DPI/remote/multi-monitor. |
| Recommended change | ประกาศ supported DPI awareness ที่เข้ากับ .NET Framework target, audit logical units/min sizes, เพิ่ม scroll/flex where needed, กำหนด zoom scope/label และ resource-ready string sizing. |
| Why this control/pattern is appropriate | Platform DPI scaling + adaptive layout ดีกว่าการคูณตำแหน่งเองและรักษา native input metrics. |
| Estimated effort | L |
| Dependencies | DPI-capable test hosts/screenshots, manifest decision, long-string pseudo-locale fixture. |
| Risk | changing manifest can alter all geometry; exact Traceroute 120px service columns and declared 1100×700 contract must be consciously preserved/translated. |
| Acceptance criteria | 100/125/150/200% on single/mixed monitors: no overlap/clipping, all inputs/actions reachable, focus visible; 1100×700 logical contract documented; long Thai/English fixture usable; zoom label accurately describes scope; baseline geometry tests updated intentionally. |

### VAL-01 — Modal validation without field relationship

| Field | Detail |
| --- | --- |
| ID | `VAL-01` |
| Priority | P2 |
| Confidence | Medium — source-confirmed call sites; dialogs not runtime-captured |
| Category | Validation / feedback |
| Affected menu/screen | Ping, Traceroute, DNS, MAC/WAN, Collector and save-profile dialogs |
| Evidence | Multiple validation/error `MessageBox` call sites; empty trace target `src/NetStuck/NetStuck.V103.cs:869-873`; large-target confirmation `src/NetStuck/NetStuck.cs:721-729`. Collector port parsing throws at `src/NetStuck/NetStuck.Features.cs:540-546`; `PingCollectorDevices` and `StartCollector` call it before a protective `try` at `:565-568`, `:589-592`, and output `Directory.CreateDirectory` also occurs before the `try` at `:598-600` versus `:614`. These are `async void` handlers; escape behavior was not runtime reproduced. |
| Current behavior | Most correctable errors interrupt with modal text; after dismiss, offending field is not consistently focused/marked. Invalid Collector port or denied/uncreatable output path can throw before the handler's guarded operation block. |
| Problem | feedback is detached from context and does not scale to multiple errors; selected Collector preflight failures have a source-backed risk of reaching WinForms unhandled-exception handling instead of recoverable UI state. |
| User impact | ผู้ใช้ต้องจำข้อความแล้วค้นหา field, keyboard/screen-reader flow ถูกขัด; malformed device port/output permission can abort the action with an uncontrolled error path. |
| Recommended change | Validate/normalize device ports and output path inside an expected-error boundary before clearing/mutating UI, then show inline summary + per-field/path error and focus. Reserve MessageBox for confirmation, destructive action or non-recoverable app-level failure. |
| Why this control/pattern is appropriate | Inline feedback persists next to correction point and can aggregate multiple fields without modal loop. |
| Estimated effort | M |
| Dependencies | Labeled-field component, validation model and screen-reader announcement. |
| Risk | error text เพิ่มความสูง; must reserve/wrap space without layout jump. Catch only expected parse/path exceptions so programming defects are not silently hidden. |
| Acceptance criteria | Submit invalid fixture แสดงทุก error inline, focus first invalid field, summary links/focuses each field, error persists until corrected, no routine correctable error requires modal dialog, Narrator announces field + reason; invalid/out-of-range device port and denied/uncreatable output path yield recoverable state with unchanged inputs and zero unhandled exception. |

### STATE-04 — Global status ownership and announcements

| Field | Detail |
| --- | --- |
| ID | `STATE-04` |
| Priority | P2 |
| Confidence | Medium — source-confirmed competing writes; concurrent race not reproduced |
| Category | Cross-screen state / accessibility |
| Affected menu/screen | StatusStrip; Live Ping, Traceroute, DNS, MAC/WAN, Collector |
| Evidence | Activity reference behavior `src/NetStuck/NetStuck.Release1.cs:76-86`; Ping/DNS/MAC/WAN paths เขียน `Ready` โดยตรงที่ `src/NetStuck/NetStuck.cs:1377-1388`, `src/NetStuck/NetStuck.Release1.cs:287-295`, `src/NetStuck/NetStuck.cs:1150-1155`, `:1188-1192`; trace aggregation ตรวจเฉพาะ trace sessions ที่ `src/NetStuck/NetStuck.V103.cs:1371-1379`. |
| Current behavior | หลาย operations แชร์ text label เดียว; workflow ที่จบสามารถเขียน `Ready` แม้งานอื่นยัง active; ไม่มี explicit live-region announcement contract. |
| Problem | status ไม่มี single owner/reducer และไม่สะท้อน concurrent operations อย่างน่าเชื่อถือ. |
| User impact | ผู้ใช้อาจคิดว่างานทั้งหมดจบแล้ว; screen-reader user ไม่ได้รับการแจ้ง progress/completion ที่เหมาะสม. |
| Recommended change | central operation registry aggregate active/error/completed state; screen-local status อยู่ใกล้งาน, global status สรุป count/severity; announce only meaningful transitions. |
| Why this control/pattern is appropriate | Aggregator resolves races while local status keeps context and avoids noisy global text. |
| Estimated effort | M |
| Dependencies | Operation IDs/state reducer and accessible announcement helper. |
| Risk | excessive announcements; completed state needs timeout/history policy. |
| Acceptance criteria | Concurrent fixture จบหนึ่งงานแล้ว global status ยังแสดงอีกงาน active; errors outrank ready; no late completion overwrites newer state; assistive tech hears start/failure/completion once, not every sample. |

### ASYNC-02 — Cancellation and close lifecycle gaps

| Field | Detail |
| --- | --- |
| ID | `ASYNC-02` |
| Priority | P2 |
| Confidence | Medium — source review; full socket/close race not reproduced |
| Category | Async lifecycle / cancellation |
| Affected menu/screen | Ping/Trace probes, DNS polling/lookups, WAN lookup, Collector and app close |
| Evidence | Operation implementations mix CTS-aware loops with probe/resolver calls that lack a common cancellation contract; close handling at `src/NetStuck/NetStuck.cs:1835-1859`; Ping/Trace service probes may wait configured timeouts up to 30 s; WAN lookup uses async event path. Fresh tests pass but do not close during every in-flight network phase. |
| Current behavior | Stop cancels scheduling in key screens, but some underlying I/O only ends by timeout; form close cleanup is not expressed as one awaited shutdown barrier. |
| Problem | late callbacks/tasks can outlive screen generation or delay shutdown. |
| User impact | Stop feels slow, window closes while work remains, or stale status/event appears after restart. |
| Recommended change | Thread cancellation token/generation through every cancellable operation, bound non-cancellable calls, suppress late UI marshal after disposal and implement deterministic shutdown coordinator. |
| Why this control/pattern is appropriate | Structured cancellation makes Stop and Close observable contracts rather than best-effort flags. |
| Estimated effort | M |
| Dependencies | API capability audit, fake delayed network implementations, shutdown tests. |
| Risk | forced disposal may abort shared resources; preserve fixed scheduling cadence and partial-result semantics. |
| Acceptance criteria | Stop/Close test at every phase completes within documented bound, no post-dispose UI callback/unobserved exception, restart ignores old generation, all CTS/timers/resources disposed exactly once. |

### PERF-02 — Resource bounds and disposal

| Field | Detail |
| --- | --- |
| ID | `PERF-02` |
| Priority | P2 |
| Confidence | Medium — source risk; short soak did not show runaway growth |
| Category | Resource lifecycle / long-running stability |
| Affected menu/screen | Traceroute cache, app zoom, shell clock and long sessions |
| Evidence | Trace caches declared `src/NetStuck/NetStuck.V103.cs:138-145`, loaded/inserted at `:147-163`, `:1192-1194`, `:1269-1273`; cap `.Take(1000)` only while save at `:193-201`. Zoom creates new Fonts at `src/NetStuck/NetStuck.Features.cs:1616-1628`; clock timer starts at `src/NetStuck/NetStuck.cs:101`, `:276-278`; close cleanup `:1835-1859` does not make every resource lifecycle explicit. Fresh 11.45 s soak grew 8 MB. |
| Current behavior | Caches/fonts/timer work in normal run; persistence truncates trace cache, preview trims content, form close cancels principal work. |
| Problem | in-memory cache can exceed persisted cap; repeated zoom may retain GDI Font objects; timer ownership/disposal is implicit. |
| User impact | long-running app may accumulate memory/GDI handles or show degraded rendering. |
| Recommended change | enforce in-memory LRU/size cap, reuse/dispose owned fonts safely, explicitly stop/dispose timers and add resource counters to soak. |
| Why this control/pattern is appropriate | Bounded caches and ownership rules make long-run consumption predictable without changing visible behavior. |
| Estimated effort | M |
| Dependencies | Ownership map, handle/memory soak fixture, cache eviction policy. |
| Risk | disposing a Font still referenced by a control causes paint errors; eviction can reduce cache hit rate. |
| Acceptance criteria | Repository-default 8-hour synthetic soak has stable slope/handles within agreed threshold; cache never exceeds cap; 100 zoom cycles do not leak GDI handles; close disposes timer/fonts/resources with zero UI exceptions. |

### CTRL-01 — Action hierarchy conflates roles

| Field | Detail |
| --- | --- |
| ID | `CTRL-01` |
| Priority | P2 |
| Confidence | High |
| Category | Control usage / visual hierarchy |
| Affected menu/screen | Live Ping profiles/actions, Traceroute actions, DNS polling, Collector, Event Log |
| Evidence | Shared action/danger factories `src/NetStuck/NetStuck.cs:1696-1720`; screenshots show multiple equal-weight actions; Stop/Delete/Clear use adjacent button treatments across builders. Event Log `Clear` immediately mutates table at `src/NetStuck/NetStuck.cs:617-628`. |
| Current behavior | Primary start, operational stop, secondary export/settings and destructive delete/clear often sit in one row with similar weight; danger color semantics are not consistently scoped. |
| Problem | Stop (reversible operation cancellation) is visually conflated with destructive data/list actions; multiple apparent primaries weaken scan path. |
| User impact | slower decision, accidental deletion/clear and uncertain default action. |
| Recommended change | one primary per operation pane; secondary outline/native actions; Stop as operation-cancel state; Delete/Clear as destructive with confirmation/undo policy; overflow only for low-frequency actions. |
| Why this control/pattern is appropriate | Hierarchy matches action consequence/frequency, not merely button color. |
| Estimated effort | M |
| Dependencies | Control matrix/design tokens and action audit per screen. |
| Risk | moving actions can break muscle memory/reflection tests; retain names/shortcuts and document change. |
| Acceptance criteria | Each pane has at most one enabled primary; cancel and destructive actions are visually/semantically distinct; keyboard default maps to safe primary; destructive data loss confirms or supports undo; tests map old behavior to new placement. |

### ARCH-01 — Duplicated builders and raw layout metrics

| Field | Detail |
| --- | --- |
| ID | `ARCH-01` |
| Priority | P2 |
| Confidence | High |
| Category | UI architecture / maintainability |
| Affected menu/screen | Whole repository; Live Ping/Traceroute most sensitive |
| Evidence | Shared helpers at `src/NetStuck/NetStuck.cs:1664-1805`; legacy builders coexist at `src/NetStuck/NetStuck.cs:285-506`; active builders at `src/NetStuck/NetStuck.V103.cs:221-503`; raw spacing/heights/widths repeated across files. |
| Current behavior | Active v1.2.3 screens use newer deterministic builders while older implementations remain compiled; helpers cover some controls but not screen states/field rows/table toolbars. |
| Problem | audit/fixes can target inactive code; visual/state changes drift and regression surface grows. |
| User impact | indirect: fixes arrive inconsistently, layout bugs recur and release risk increases. |
| Recommended change | first map active entry points, then retire/dead-gate legacy builders with tests; extract small WinForms-native tokens/components without broad rewrite or new UI framework. |
| Why this control/pattern is appropriate | Incremental shared components preserve tested architecture and reduce duplicate metrics while avoiding migration risk. |
| Estimated effort | L |
| Dependencies | Reflection/coverage map, characterization screenshots/tests, staged deletion approval. |
| Risk | legacy methods may be reflection-tested or fallback paths; do not remove before call/coverage proof. |
| Acceptance criteria | Every screen has one documented active builder; no duplicate unreachable builder remains without rationale; spacing/control states use shared tokens; 128 baseline and visual contracts pass; binary/version/behavior changes are explicitly reviewed. |

### DLG-01 — Custom dialog contract is inconsistent

| Field | Detail |
| --- | --- |
| ID | `DLG-01` |
| Priority | P2 |
| Confidence | Medium — source-confirmed implementations; dialogs not runtime-captured |
| Category | Dialogs / keyboard / validation |
| Affected menu/screen | Save list, Hop descriptions, Realtime result columns, confirmations |
| Evidence | Hop editor `src/NetStuck/NetStuck.Release1.cs:168-202`; column chooser `src/NetStuck/NetStuck.Features.cs:1503-1518`; name prompt `src/NetStuck/NetStuck.cs:1808-1819`. They use different layout/footer/initial focus patterns; runtime dialogs were not captured. |
| Current behavior | Native modal forms provide basic Save/Cancel or Apply action, but consistent owner, minimum sizing, accessible labels, error placement and focus-return are not centrally enforced. |
| Problem | keyboard and resize behavior varies; field validation/confirmation wording can drift. |
| User impact | modal interactions feel unpredictable and may be harder with keyboard/screen reader. |
| Recommended change | shared dialog shell: owner-centered, 12/16 content padding, 52px footer, right-aligned primary/cancel, Accept/Cancel buttons, initial focus, validation region and focus return. |
| Why this control/pattern is appropriate | A small native Form shell standardizes interaction without replacing platform dialogs. |
| Estimated effort | M |
| Dependencies | Dialog inventory, accessibility labels and screenshot harness capable of modal capture. |
| Risk | fixed footer/padding may clip translated text; use AutoSize/wrap/minimum size. |
| Acceptance criteria | All custom dialogs pass Enter/Escape/tab/resize/long-text scripts, expose name/role, show inline validation, return focus to invoker and have consistent footer; native Save/Folder dialogs remain platform-native. |

### PERSIST-01 — View geometry is not persisted

| Field | Detail |
| --- | --- |
| ID | `PERSIST-01` |
| Priority | P2 |
| Confidence | Medium — schema/source confirmed; restart personalization scenario not run |
| Category | Persistence / tables / layout |
| Affected menu/screen | All DataGridViews; Live Ping and Collector split panes |
| Evidence | UI state schema/load-save in `src/NetStuck/NetStuck.Features.cs:22-90`, `:1646-1649`, `:1724-1727` covers selected/visible options but no verified column width/display-order or splitter-distance persistence. Current grids permit resize/reorder. |
| Current behavior | Live Ping column visibility is already saved/restored, but user-adjusted column widths, display order and split positions return to defaults after restart. |
| Problem | high-frequency operational workspace ต้องจัดใหม่ซ้ำ; hidden/visible state กับ actual geometry แยกกัน. |
| User impact | เสียเวลาและลดความสามารถในการสร้าง view สำหรับหน้าจอ/งานเฉพาะ. |
| Recommended change | persist versioned, sanitized view state per grid/split; clamp restored values to current columns/window/DPI and offer Reset layout. |
| Why this control/pattern is appropriate | Local view preference ไม่แตะ network behavior และทำให้ resizable/reorderable promise มีคุณค่าข้าม session. |
| Estimated effort | M |
| Dependencies | Versioned state schema, atomic write, test-path isolation for all persisted files. |
| Risk | stale schema/offscreen split; do not store data/targets/credentials as incidental view state. |
| Acceptance criteria | Resize/reorder/hide/split → restart restores within current DPI bounds, including preserving existing Ping visibility behavior; missing/renamed columns ignored safely; Reset returns baseline; corrupt state falls back without crash; file contains no secret/sensitive results. |

### STATE-05 — Loading, empty and progress patterns are incomplete

| Field | Detail |
| --- | --- |
| ID | `STATE-05` |
| Priority | P2 |
| Confidence | High |
| Category | Loading / empty / progress feedback |
| Affected menu/screen | All data tabs, especially Ping, Trace, DNS, MAC/WAN and Collector |
| Evidence | Safe screenshots show blank initial result regions on Live Ping, Trace, DNS, MAC/WAN and Collector; Event Log intentionally contains a synthetic row. Progress is variously button text, global status, row status or terminal text; no shared empty-state component in helpers `src/NetStuck/NetStuck.cs:1664-1805`. |
| Current behavior | Initial/no-result states are visually empty; long operations generally disable/start/stop in screen-specific ways, with determinate count only in some rows. |
| Problem | Blank grid cannot distinguish “not started”, “no matches”, “no results”, “failed” or “loading”; feedback drifts between screens. |
| User impact | ผู้ใช้ไม่รู้ next action หรือ whether filter/network removed results; perceived latency increases. |
| Recommended change | Overlay/state presenter inside data region for Initial, Loading, Empty, Filtered-empty, Partial, Error, Cancelled and Complete; preserve headers/scroll/selection and expose retry/clear-filter where appropriate. |
| Why this control/pattern is appropriate | In-place state keeps context, avoids modal interruption and can share one state model across tables. |
| Estimated effort | M |
| Dependencies | Screen state reducers, accessible announcements, non-blocking overlay component. |
| Risk | overlay must not intercept grid keyboard/copy when rows exist or cause repaint churn. |
| Acceptance criteria | Each screen has deterministic fixtures for required states; empty copy explains cause + next action; loading keeps Cancel reachable; filter-empty offers Clear filter; partial/error retains valid rows; state changes do not reset sort/column widths/selection/scroll. |

### TEST-01 — UI coverage and state isolation gaps

| Field | Detail |
| --- | --- |
| ID | `TEST-01` |
| Priority | P2 |
| Confidence | High |
| Category | Test strategy / privacy |
| Affected menu/screen | Whole application and screenshot harnesses |
| Evidence | Fresh suite is 128/128 but tests explicitly disable scaling at `tests/FeatureTests.cs:39-42`; comment `:41` says 1100×700, while setup remains 1460×900 at `:45-46` and `:226` changes only width to 1100. No automated Narrator/UIA, High Contrast, 125/150/200% or long-string visual gates. `NETSTUCK_TEST_STATE_PATH` isolates principal state, while `src/NetStuck/NetStuck.cs:212-219`, `:230` show profile/vendor paths remain separate. |
| Current behavior | Strong logic/geometry/performance/cadence coverage at baseline; manual screenshots need harness cleanup to avoid reading host-local persisted data. |
| Problem | regressions in assistive tech, scaling/theme and privacy-safe capture can pass CI; test state override is incomplete. |
| User impact | release may look correct at 100% yet fail on target setup or accidentally expose local labels in artifacts. |
| Recommended change | Add isolated root for every persisted file/cache, DPI/theme/pseudo-locale screenshot matrix, UIA/keyboard assertions, deterministic modal/state capture and image-diff tolerances. |
| Why this control/pattern is appropriate | Test seams make visual evidence reproducible without probing real network or credentials. |
| Estimated effort | L |
| Dependencies | Dedicated test process/desktop, golden-image governance and sanitized fixtures. |
| Risk | pixel snapshots flaky across font/OS; use geometry/semantic assertions plus masked/tolerant diff. |
| Acceptance criteria | One command creates no files outside temp root; screenshots contain only allowlisted safe strings; CI actually sets and verifies 1100×700 plus 100/125/150/200%, High Contrast, long Thai/English, keyboard/UIA names and modal states; diffs require reviewed baseline update. |

### SHELL-01 — Fixed-width identity slots in StatusStrip

| Field | Detail |
| --- | --- |
| ID | `SHELL-01` |
| Priority | P3 |
| Confidence | High |
| Category | Shell / responsive layout |
| Affected menu/screen | MainForm StatusStrip |
| Evidence | `localIpStatus` and `publicIpStatus` use explicit 180/190px widths at `src/NetStuck/NetStuck.V103.cs:117-126`; `BuildShell` already places a spring item before time/identity at `src/NetStuck/NetStuck.cs:269-275`. Current 1100×900 screenshot fits short sanitized values. |
| Current behavior | Global operation status and time use native/default sizing around an existing spring; only local/public identity slots are fixed-width. |
| Problem | long IPv6/interface/public-provider text or scaled font can truncate/crowd those identity slots. |
| User impact | secondary diagnostics become incomplete; primary workflow remains usable. |
| Recommended change | retain the existing spring; make identity slots content-priority aware, ellipsize with accessible full text/tooltip, and hide/defer only low-priority identity detail at narrow width. |
| Why this control/pattern is appropriate | Existing native spring sizing already protects the left operation region; responsive identity items solve the evidenced fixed-width risk without rebuilding the strip. |
| Estimated effort | S |
| Dependencies | DPI/long-string fixture and priority rules. |
| Risk | hidden identity may surprise users; keep discoverable tooltip/details. |
| Acceptance criteria | 1100 logical width at 100–200% keeps operation/error fully visible; secondary text ellipsizes without overlap; full value accessible by keyboard/screen reader/tooltip; no horizontal form expansion. |

### UPDATE-01 — Monolithic Updates content

| Field | Detail |
| --- | --- |
| ID | `UPDATE-01` |
| Priority | P3 |
| Confidence | High |
| Category | Content design / support |
| Affected menu/screen | Updates |
| Evidence | One read-only multiline TextBox is built/populated at `src/NetStuck/NetStuck.cs:633-718`; screenshots show a long continuous release-note block. |
| Current behavior | Offline, safe, selectable text lists current version/features/notes; no network updater. |
| Problem | weak scan hierarchy, no quick jump/copy-by-section and TextBox visually resembles editable input. |
| User impact | ผู้ใช้ค้นหาสิ่งที่เปลี่ยน/ข้อจำกัดได้ช้า; low operational impact. |
| Recommended change | Keep offline behavior but render structured version header, highlights, fixes, known limits and copy/open-doc actions in a read-only document/list surface. |
| Why this control/pattern is appropriate | Structured content communicates read-only/support intent better than an input control while retaining selectable text. |
| Estimated effort | S–M |
| Dependencies | Content source/version ownership and accessible heading strategy. |
| Risk | RichTextBox custom formatting can add rendering/selection complexity; a simple panel + labels may suffice. |
| Acceptance criteria | Current version and major changes visible without scrolling at 1100×700; sections keyboard-navigable/read in logical order; all text selectable/copyable; no network request; version content stays generated from one reviewed source. |

## 9. Per-Menu and Per-Screen Audit

### Modernization summary

| Menu/Screen | Current issues | Proposed hierarchy | Control changes | Required states | Reusable components | Priority |
| ----------- | -------------- | ------------------ | --------------- | --------------- | ------------------- | -------- |
| Shell | status race, fixed identity slots, custom theme | identity → tabs → local content → global summary | retain spring; responsive identity + theme-aware tabs | idle/active/error/shutdown | operation/status presenter | P2 |
| Live Ping | blank states, dense actions, weak associations | setup → run metrics/actions → results → history | one primary, read-only grid, inline validation | initial/running/paused/partial/error/filtered empty | operation toolbar, result-grid shell | P1 |
| Traceroute | stale state, dense controls, ambiguous failure | session → target/policy → actions → route/events | retain deterministic fields; atomic state presenter | resolving/running/paused/cancelled/failed/complete | trace session shell, reducer | P1 |
| DNS Resolver | mode/action ambiguity, modal errors | inputs/options → selected mode → results | mutual-exclusive actions, inline errors | resolving/polling/stopping/partial/error | lookup/operation shell | P2 |
| MAC / WAN | MAC cache-miss and WAN can call external services without distinct source cue | independent tool → privacy/source → results | conditional external cue for both panes, shared lookup shell | local/cache/external/offline/no-match | lookup pane, result state | P2 |
| Calculators | unlabeled From/To, mixed result patterns | input → calculate → result/reference | labeled native selects, shared read-only result | initial/valid/invalid/boundary | field/result component | P2 |
| Config Collector | mutable run, weak preflight/cancel, performance risk | connection → credentials → commands/targets → run → results | snapshot + state machine + bounded terminal | preflight/collect/cancel/partial/fail/complete | credential row, run controller, terminal | P1 |
| Event Log | blank/filter state, Clear hierarchy | scope/filter → events → export/clear | read-only grid, safe destructive clear | receiving/filtered empty/export/cleared | table toolbar, state overlay | P2 |
| Updates | monolithic input-looking text | version → highlights → fixes/limits | structured offline content | available/missing | content page | P3 |
| Dialogs | inconsistent footer/focus/validation | instruction → content/error → footer | shared shell; retain native pickers | invalid/valid/dirty/confirm/cancel | dialog shell, footer | P2 |

### Shell

1. **Current purpose:** เป็นกรอบเดียวของแอปสำหรับ identity, 8 peer destinations และ status รวม.
2. **Current layout summary:** app header 72px ด้านบน, owner-drawn tab strip, content fill, StatusStrip ด้านล่าง; minimum 1100×700.
3. **Verified evidence:** `src/NetStuck/NetStuck.cs:188-283`; active status items `src/NetStuck/NetStuck.V103.cs:117-126`; safe screenshotsทุก tab ที่ 1100×900/1460×900; 1100×700 is source-declared, while 700-height behavior is Not verified.
4. **Main usability problems:** global status มี ownership race, fixed-width secondary fields และ theme/accessibility ไม่ adapt.
5. **Inconsistent controls:** owner-drawn tabs/status colors ไม่ใช้ platform state contract เดียวกับ native controls.
6. **Proposed information hierarchy:** app identity → primary destinations → screen content → local screen status; global stripแสดงเฉพาะ cross-screen summary.
7. **Proposed layout zones:** compact header, scroll-safe tab navigation, content host, priority-aware status strip.
8. **Controls to retain:** native Form, TabControl, StatusStrip, icon/title/version; คง 8 top-level tabs เป็น peers.
9. **Controls to replace:** เปลี่ยน fixed-width local/public identity slots ด้วย priority-aware sizing โดยคง spring item เดิม; custom rendering ใช้ theme-aware renderer.
10. **Controls to add:** operation-count/severity indicator, full-text tooltip/accessibility description และ optional overflow for tabs only if 1100/DPI requires.
11. **Controls to remove or consolidate:** ลดข้อความ status ซ้ำจากแต่ละ workflow; ไม่เพิ่ม side navigation หรือ dashboard ที่ซ้ำ tabs.
12. **Required UI states:** idle, one/many active, warning/error, offline/external unavailable, shutdown/cancelling.
13. **Reusable components:** semantic status presenter, tab renderer, page host with 12px padding, announcement helper.
14. **Keyboard behavior:** Ctrl+Tab/Ctrl+Shift+Tab เปลี่ยน tab, mnemonic/focus map, focus restore ต่อ tab, F6 optional วน major zones.
15. **Accessibility considerations:** system colors in High Contrast, named tabs/status, meaningful state text not color-only, full values for truncated status.
16. **Rendering/performance considerations:** invalidate only changed tab/status region; no per-sample full shell repaint; reuse brushes/fonts/owned resources.
17. **Acceptance criteria:** 8 tabs and status usable at 1100×700/100–200% DPI, no overlap, global state never says Ready while work active, keyboard/screen reader traversal passes.
18. **Estimated effort:** M.
19. **Dependencies and risks:** operation registry, DPI/theme decision; tab geometry is reflection/visual-test sensitive.

### Live Ping

```text
┌ Targets & run setup ────────────────┬ Session metrics ──────────────┐
│ Targets | Saved list | Count        │ Active | Up | Down | Avg RTT  │
│ Interval | Timeout | payload/ports  │ [Start] [Pause] [Stop]        │
├ Results toolbar: Search | Status | Columns | Copy | Export ────────┤
│ Realtime read-only grid (sort/resize/reorder; inline state overlay) │
├ Selected target history ────────────────────────────────────────────┤
│ History grid                                             [Export]  │
└─────────────────────────────────────────────────────────────────────┘
```

1. **Current purpose:** monitor one/many targets on fixed cadence and inspect current/history measurements.
2. **Current layout summary:** metrics + dense left setup/actions, right result table and lower selected-target history in split layout.
3. **Verified evidence:** active builder `src/NetStuck/NetStuck.V103.cs:221-374`; batch updater `src/NetStuck/NetStuck.Release1.cs:130-165`; 1100×900/1460×900 screenshots and 91/91 feature + cadence/performance tests.
4. **Main usability problems:** initial table blank, action hierarchy dense, field associations/keyboard order absent และ global/local state splitไม่ชัด.
5. **Inconsistent controls:** saved-list Delete อยู่ระดับเดียวกับ Load/Save; Start/Pause/Stop และ table actions weight คล้ายกัน; status colors contrast ต่ำ.
6. **Proposed information hierarchy:** targets/profile + probe policy → session controls/summary → filterable results → selected-target evidence.
7. **Proposed layout zones:** top responsive setup band, compact metrics/action band, full-width results toolbar/grid, collapsible history region.
8. **Controls to retain:** prominent multiline targets, visible ping count, numeric inputs, Start/Pause/Stop, filter/status filter, Columns/Copy/Export, sortable resizable reorderable grid and history.
9. **Controls to replace:** generic blank gridด้วย state overlay; destructive profile action with explicit destructive treatment; unlabeled/cue-only fields with labeled rows.
10. **Controls to add:** inline validation summary, result count/filter-empty message, clear-filter action, screen-local state/progress and Reset layout.
11. **Controls to remove or consolidate:** consolidate low-frequency table actions into one consistent secondary toolbar; do not remove filter/sort/column operations.
12. **Required UI states:** initial, validating, starting, running, paused, stopping, cancelled, complete, partial, error, no targets/results, filtered empty.
13. **Reusable components:** operation toolbar, labeled numeric field, result-grid shell, metrics tile, empty/error overlay, saved-list picker.
14. **Keyboard behavior:** Alt shortcuts for target/count/start; Space/Enter pause/resume when focused; Escape cancels only with explicit state; Ctrl+F filter; grid copy/sort/navigation preserved.
15. **Accessibility considerations:** labels include units/ranges, status has icon+text, read-only grid semantics, focus remains on selected row across refresh, completion announced once.
16. **Rendering/performance considerations:** preserve fixed cadence and batched UI flush; delta update rows; never reset selection/scroll/sort during sample arrival; virtualize only after profiling.
17. **Acceptance criteria:** existing cadence/tests pass; count always visible; all inputs remain non-overlapping at 1100/200%; filter/sort/copy/resize/reorder functional; 2,412-probe fixture stays within approved dispatch threshold.
18. **Estimated effort:** M.
19. **Dependencies and risks:** operation/status reducer and design tokens; layout/name changes may affect feature reflection tests; do not alter scheduler semantics.

### Traceroute

```text
┌ Session 1 ───────────────────────┬ Session 2 ───────────────────────┐
├ Target (46%) ─────┬ Network (18%)┬ Probe (18%) ──┬ Polling (18%) ──┤
│ target/editor     │ hops/timeout │ protocol 120  │ interval/mode    │
│                   │              │ port 120      │                  │
│                   │              │ packet 120    │                  │
├ [Start] [Pause] [Stop] [Hop descriptions] | session state ────────┤
│ Hop result grid                    │ Event filter + event grid      │
└────────────────────────────────────┴────────────────────────────────┘
```

1. **Current purpose:** run two independent route/service discovery sessions and correlate hop/event details.
2. **Current layout summary:** nested two-session tabs; deterministic three-row input grid, separate action row, hop grid left and event grid right.
3. **Verified evidence:** builder `src/NetStuck/NetStuck.V103.cs:377-503` defines 46/18/18/18 at `:426-429` and the three 120px fields at `:437-439`; protocol draw `:540-580`; run/apply `:869-1095`. `FeatureTests.cs:202-233` verifies frames, adjacency, equal widths and no overlap at 1100×900, not the numeric percentages/120px/700-height.
4. **Main usability problems:** stale-cycle mutation, failure state ambiguity, dense parameters, blank result regions and owner-drawn selector accessibility risk.
5. **Inconsistent controls:** protocol appears segmented/custom whereas adjacent fields are standard inputs; event filter/status treatments differ from Ping/Log.
6. **Proposed information hierarchy:** session identity/state → target → network/probe/polling policy → run actions → route → diagnostic events.
7. **Proposed layout zones:** retain exact deterministic input topology/action row; add clearly labeled groups and local state; balanced resizable result split below.
8. **Controls to retain:** exactly two sessions, editable target history, hops/timeouts/interval, protocol/port/packet inputs, Start/Pause/Stop, hop descriptions, hop/event grids.
9. **Controls to replace:** only replace custom protocol UI after explicit control-type decision; if kept, make it keyboard/UIA/theme complete. Replace blank grids with shared state overlay.
10. **Controls to add:** inline target error/retry, session generation/status text, filtered-empty state, Reset split/columns.
11. **Controls to remove or consolidate:** no functional parameter removal; avoid duplicating session state in global strip and action captions.
12. **Required UI states:** initial, resolving/discovering, running, paused, stopping, cancelled, complete, partial/timeout, failed, filtered empty.
13. **Reusable components:** trace-session shell, atomic state reducer, deterministic field-grid helper, result/event split, status presenter.
14. **Keyboard behavior:** Ctrl+1/Ctrl+2 or documented shortcut for sessions, logical target→network→probe→polling→actions order, arrow/Alt behavior for protocol, focus return on validation.
15. **Accessibility considerations:** every field persistent label/unit, session names/states explicit, protocol role/value predictable, grid events read-only, color-independent route state.
16. **Rendering/performance considerations:** preserve fixed cadence and two-session independence; apply immutable current-generation result atomically; row deltas/batching and no stale repaint.
17. **Acceptance criteria:** all existing layout/cadence tests pass; delayed N/N+1 test proves no stale topology; all terminal states correct; exact three 120px service columns remain non-overlapping at minimum logical width.
18. **Estimated effort:** M–L.
19. **Dependencies and risks:** ASYNC-01/STATE-03 first; protocol control decision; geometry is explicit v1.2.3 invariant.

### DNS Resolver

1. **Current purpose:** resolve forward/reverse DNS once or on polling interval with optional server.
2. **Current layout summary:** input/options/actions above a full result grid.
3. **Verified evidence:** builder `src/NetStuck/NetStuck.cs:507-545`; active logic `src/NetStuck/NetStuck.Release1.cs:228-354`; screenshot + automated resolving/polling tests.
4. **Main usability problems:** blank initial/filter states, modal validation, global status races and ambiguous one-shot vs polling ownership.
5. **Inconsistent controls:** Stop uses action styling different from shared operation pattern; custom server option/field association relies on proximity.
6. **Proposed information hierarchy:** names/addresses → resolution options/server → one-shot or polling command → result table/state.
7. **Proposed layout zones:** responsive input card with persistent labels, one operation toolbar, full-width grid with state overlay.
8. **Controls to retain:** multiline input, reverse option, custom server, interval, Resolve/Start polling/Stop, sortable/copyable result table.
9. **Controls to replace:** modal field errors with inline validation; blank grid with contextual empty state.
10. **Controls to add:** resolved/failed/total count, clear-filter/retry, explicit local polling state and timestamp.
11. **Controls to remove or consolidate:** present Resolve and Start polling as mutually exclusive modes/actions; avoid three equal primaries.
12. **Required UI states:** initial, validating, resolving once, polling, stopping, complete, partial, no results, error, cancelled.
13. **Reusable components:** operation toolbar, labeled option row, result grid, progress/status strip.
14. **Keyboard behavior:** mnemonic for input/resolve/poll/stop; Enter starts selected mode, Escape does not silently discard results; Ctrl+C grid copy.
15. **Accessibility considerations:** announce counts not every lookup; server checkbox controls enabled/disabled field with relation; errors reference line/input.
16. **Rendering/performance considerations:** batch result updates and preserve sort/selection/scroll; resolve concurrency bounded; cancellation propagated.
17. **Acceptance criteria:** one-shot/poll states mutually exclusive, stop bounded, invalid server focuses field, partial failures retain successful rows, no `Ready` overwrite while another operation active.
18. **Estimated effort:** S–M.
19. **Dependencies and risks:** shared operation registry/cancellation; DNS timing fixtures must be deterministic.

### MAC / WAN Lookup

1. **Current purpose:** classify multicast/locally administered MACs, resolve registered OUI from cache or external vendor API on cache miss, and explicitly query public-IP intelligence.
2. **Current layout summary:** equal left/right cards, each input/action/result region.
3. **Verified evidence:** `src/NetStuck/NetStuck.cs:547-570`; MAC local/multicast branches `:1111-1124`, cache `:1126-1132`, external miss `:1134-1140`; WAN `:1160-1195`. Screenshot used a locally administered safe fixture; neither external MAC miss nor WAN call was run.
4. **Main usability problems:** panes look equivalent although privacy/network behavior differs; blank grids and generic validation.
5. **Inconsistent controls:** MAC looks local/cache-oriented but can call an external API on registered cache miss; WAN is external, yet neither source transition is foregrounded consistently.
6. **Proposed information hierarchy:** two clearly named independent tools; both panes expose local/cache/external source, privacy note and last-success timestamp before/while relevant calls occur.
7. **Proposed layout zones:** responsive two-column ≥wide, stacked at constrained effective width/DPI; input/action/status then result.
8. **Controls to retain:** multiline MAC/IP input, explicit Lookup actions, result grids, vendor cache behavior.
9. **Controls to replace:** blank results with initial/no-match/error states; modal correctable input errors with inline feedback.
10. **Controls to add:** input examples, extracted/invalid counts, conditional external-request note for registered MAC cache misses plus WAN, retry/copy and source/cache timestamp.
11. **Controls to remove or consolidate:** no auto-WAN lookup on navigation/startup; consolidate repeated result toolbar patterns.
12. **Required UI states:** initial, validating, local lookup, external loading, complete, partial, cache hit/miss, no match, offline/error, cancelled.
13. **Reusable components:** lookup pane, privacy/network notice, result grid/state overlay.
14. **Keyboard behavior:** independent pane tab groups; mnemonic lookup; Ctrl+F/Ctrl+C in active grid; focus first invalid token.
15. **Accessibility considerations:** announce external action before activation, status text includes cache/source, stacked reading order follows visual order.
16. **Rendering/performance considerations:** parse/batch large paste off UI if profiling warrants; WAN single-flight/cancel; never run remote lookup while merely rendering.
17. **Acceptance criteria:** wide and stacked layouts have no overlap at 100–200%; multicast/local-admin/cache-hit MAC paths make no external call; registered cache miss and WAN call only after explicit Lookup and show external source/privacy state; per-token errors/counts visible.
18. **Estimated effort:** S.
19. **Dependencies and risks:** external response cannot be visually certified offline; privacy wording must match `PRIVACY.md`.

### Calculators

1. **Current purpose:** deterministic IPv4 subnet calculation and data/rate unit conversion.
2. **Current layout summary:** two equal cards; subnet input/result on left, value/from/to/result/reference on right.
3. **Verified evidence:** `src/NetStuck/NetStuck.cs:573-610`; safe screenshots and 16/16 core tests.
4. **Main usability problems:** Unit Converter combos lack persistent `From`/`To` labels, result hierarchy is weak and errors are detached.
5. **Inconsistent controls:** subnet result is read-only TextBox while converter result is Label/reference block.
6. **Proposed information hierarchy:** labeled input + example → primary Calculate/Convert → prominent copyable result → secondary explanation/reference.
7. **Proposed layout zones:** responsive two-column/stacked cards with common result component and minimum field widths.
8. **Controls to retain:** CIDR/value inputs, native ComboBoxes, Calculate/Convert, full subnet detail and quick reference.
9. **Controls to replace:** unlabeled combo sequence with visible From/To rows; editable-looking output with read-only result panel.
10. **Controls to add:** Copy result, swap units, inline range/format hint and accessible calculation summary.
11. **Controls to remove or consolidate:** consolidate duplicate result styling; do not add history unless user research supports it.
12. **Required UI states:** initial/example, valid result, invalid input, boundary/overflow.
13. **Reusable components:** labeled field, calculation result panel, copy affordance.
14. **Keyboard behavior:** Enter calculates active card, Alt shortcuts, swap shortcut optional, result selectable/copyable.
15. **Accessibility considerations:** From/To names and selected units announced; result announced once; invalid syntax includes example.
16. **Rendering/performance considerations:** synchronous calculations are trivial; avoid animation/spinner; recompute only on explicit action unless live mode is later tested.
17. **Acceptance criteria:** all core vectors unchanged; every field/unit labeled; long result wraps/copies; cards stack without overlap at high DPI; no network/file side effect.
18. **Estimated effort:** S.
19. **Dependencies and risks:** layout-only work should not alter parser/rounding; native ComboBox preferred.

### Config Collector

```text
┌ Connection & device ───────────────┬ Run summary / progress ───────┐
│ Transport | Port | Device type     │ State · completed/total       │
│ AUTH1 username | password | show   ├ Results (read-only grid) ─────┤
│ AUTH2 username | password | show   │ per-device status/output      │
├ Commands: Basic | Collect ─────────┤                               │
│ multiline commands                 ├ Terminal preview ─────────────┤
├ Devices ────────────┬ Output ──────┤ bounded, selectable, copyable  │
│ multiline targets  │ path Browse  │                               │
├ [Ping all] [Export] [Collect] [Cancel] ─────────────────────────────┤
└ inline validation / privacy note / next-run draft status ──────────┘
```

1. **Current purpose:** collect network-device configuration via SSH/Telnet with two credential stages, export files and show per-device/terminal results.
2. **Current layout summary:** dense fixed left setup with nested command tabs/devices/output/actions; right result grid over terminal split.
3. **Verified evidence:** builder `src/NetStuck/NetStuck.Features.cs:408-519`; operation `:565-687`; stream/batching `:1343-1385`; screenshot with synthetic data; mock/security/performance tests.
4. **Main usability problems:** no immutable run snapshot, Ping-all state/cancel gap, credential labels rely on cues, configuration remains mutable, finalize/preview performance risks, no clear progress/empty/error state.
5. **Inconsistent controls:** native `Show passwords` checkbox is appropriate but credential rows differ from labeled fields; five adjacent actions mix preflight/export/run/cancel.
6. **Proposed information hierarchy:** connection/device → credentials → commands → devices/output → preflight → primary collection → progress/results/terminal.
7. **Proposed layout zones:** retain two-pane splitter; left vertically scrollable setup with persistent sections; right compact run summary, result grid, terminal; action footer remains visible.
8. **Controls to retain:** transport/device/native ComboBoxes, numeric timeouts/port, AUTH1/AUTH2 password boxes and native Show-password checkbox, command tabs, targets/output path, Ping all/export/Collect/Cancel, results/terminal.
9. **Controls to replace:** cue-only credential identity with visible labels; live-control reads with snapshot-backed view; blank regions with state presenters; equal-weight action row with hierarchy.
10. **Controls to add:** completed/total progress, active snapshot/next-run-draft indicator, per-field validation, bounded-preview notice, retry failed, clear terminal and Reset layout where safe.
11. **Controls to remove or consolidate:** remove duplicate status prose from terminal when represented in structured result state; do not replace Show-password with cosmetic custom switch.
12. **Required UI states:** initial, invalid, preflighting, ready, collecting, cancelling, partial, complete, failed, retrying, output-path unavailable, terminal truncated; config locked/draft-for-next-run.
13. **Reusable components:** credential row, operation state machine, immutable run summary, device result grid, bounded terminal viewer, path picker row, progress presenter.
14. **Keyboard behavior:** logical section order; Alt mnemonics; Ctrl+Enter only if documented to Collect; Escape never clears credentials/results; Cancel action explicit; nested tab order predictable.
15. **Accessibility considerations:** password fields named without exposing value, Show state announced, validation associated, terminal update throttled/not announced line-by-line, progress announced at milestones.
16. **Rendering/performance considerations:** offload file post-processing, byte-bound terminal batches/queue, bounded preflight concurrency, marshal compact deltas, retain canonical output and redaction invariants.
17. **Acceptance criteria:** all devices in run use one snapshot; Ping all mutually exclusive/cancellable; 10×10MiB fixture remains responsive/bounded; secrets absent from argv/state/logs/screenshots; all controls reachable at 1100×700 and 100–200% via scroll/flex.
18. **Estimated effort:** L.
19. **Dependencies and risks:** highest-risk menu; requires state/cancellation/performance/test foundations first; no real device credential can enter automated evidence.

### Event Log

1. **Current purpose:** inspect, filter, export and clear realtime operational events across app workflows.
2. **Current layout summary:** search + level + Export/Clear toolbar over a full read-only-looking grid.
3. **Verified evidence:** `src/NetStuck/NetStuck.cs:613-631`; synthetic-row screenshots; batching/filter behavior covered by tests/source.
4. **Main usability problems:** blank initial/filter state, Clear consequence not distinguished, no visible result count/time range and grid semantics not explicit.
5. **Inconsistent controls:** Clear sits with Export despite destructive role; filter controls differ from Ping/Trace.
6. **Proposed information hierarchy:** current scope/count → search/level filters → chronological events → export/clear secondary actions.
7. **Proposed layout zones:** consistent table toolbar, full grid, compact state/count footer or header.
8. **Controls to retain:** search, level selector, sortable/copyable grid, Export and Clear.
9. **Controls to replace:** generic Clear treatment with destructive/confirm-or-undo pattern; blank filtered state with Clear filter action.
10. **Controls to add:** result/total count, clear-search button, time/source detail and optional Copy selected.
11. **Controls to remove or consolidate:** share table toolbar/filter behavior; no unnecessary log-detail modal.
12. **Required UI states:** initial/no events, receiving, filtered results, filtered empty, export success/failure, cleared.
13. **Reusable components:** table toolbar, result count, empty overlay, destructive clear confirmation.
14. **Keyboard behavior:** Ctrl+F search, Escape clears search only when focused/documented, grid navigation/copy, Alt shortcuts for Export/Clear.
15. **Accessibility considerations:** event severity text not color-only, timestamps/columns named, new-event announcements summarized not streamed noisily.
16. **Rendering/performance considerations:** maintain batched append/filter; cap history with visible policy; preserve current scroll unless user is at tail.
17. **Acceptance criteria:** search/level combine deterministically; Clear never happens accidentally; export matches visible/all documented scope; 10k-event fixture remains responsive and preserves selection/scroll policy.
18. **Estimated effort:** S.
19. **Dependencies and risks:** retention/export scope decision; status aggregation must avoid duplicate announcements.

### Updates

1. **Current purpose:** show offline release notes/current version; it is not an updater.
2. **Current layout summary:** one read-only multiline TextBox filling a page card.
3. **Verified evidence:** `src/NetStuck/NetStuck.cs:633-718`; 1100×900/1460×900 screenshots.
4. **Main usability problems:** monolithic text, weak headings and editable-control appearance.
5. **Inconsistent controls:** content page uses TextBox while other read-only information uses labels/grids.
6. **Proposed information hierarchy:** current version/date → highlights → fixes → known limits/security/privacy → local documentation links/actions.
7. **Proposed layout zones:** scrollable structured content region with compact version header and copy/open-doc footer.
8. **Controls to retain:** offline content, selectable/copyable text and exact release facts.
9. **Controls to replace:** monolithic input-looking TextBox with accessible read-only document/list rendering.
10. **Controls to add:** section headings, Copy release notes, open bundled README/release report if path exists.
11. **Controls to remove or consolidate:** no Check for updates/network button unless product scope explicitly changes.
12. **Required UI states:** content available, content missing/corrupt, current version; no loading state needed for embedded content.
13. **Reusable components:** read-only content page/section header.
14. **Keyboard behavior:** predictable reading order, PageUp/Down/Home/End, select/copy, heading navigation where feasible.
15. **Accessibility considerations:** semantic accessible names/sections, adequate contrast, no visual-only bullets.
16. **Rendering/performance considerations:** static content; create once, avoid rich formatting complexity/continuous repaint.
17. **Acceptance criteria:** current version/highlights visible at 1100×700, all content read/copyable by keyboard/screen reader, no external request and missing content fails gracefully.
18. **Estimated effort:** S–M.
19. **Dependencies and risks:** single-source version/release text; do not imply update capability.

### Custom and Native Dialogs

1. **Current purpose:** collect short names/mappings/column choices, confirm consequential actions and choose files/folders.
2. **Current layout summary:** three programmatic custom Forms plus MessageBox, SaveFileDialog and FolderBrowserDialog families.
3. **Verified evidence:** `src/NetStuck/NetStuck.cs:1808-1819`; `src/NetStuck/NetStuck.Release1.cs:168-202`; `src/NetStuck/NetStuck.Features.cs:1503-1518`; source-only this audit.
4. **Main usability problems:** inconsistent footer/focus/validation, custom forms not runtime-captured and routine errors overuse MessageBox.
5. **Inconsistent controls:** Save/Apply/Select all/Cancel placement and resize policy vary.
6. **Proposed information hierarchy:** title/instruction → task content → persistent error region → secondary + primary footer.
7. **Proposed layout zones:** 12/16px padded AutoSize content, 52px footer, owner-centered with sensible min/max.
8. **Controls to retain:** native SaveFileDialog/FolderBrowserDialog, native TextBox/CheckedListBox/buttons and confirmations for real consequences.
9. **Controls to replace:** routine validation MessageBoxes with inline errors; one-off Form metrics with shared dialog shell.
10. **Controls to add:** accessible description, initial/focus-return rules, select none where useful and visible item counts.
11. **Controls to remove or consolidate:** consolidate duplicated form construction; avoid custom replacement of native platform pickers.
12. **Required UI states:** initial, invalid, valid, dirty, confirming replace/delete, applied/cancelled.
13. **Reusable components:** dialog shell, footer, validation summary, confirmation wording template.
14. **Keyboard behavior:** AcceptButton/CancelButton, logical tab order, Esc cancels safely, mnemonic uniqueness, focus returns invoker.
15. **Accessibility considerations:** owner/title/instructions announced, every field/list labeled, error relation and selected/count state available.
16. **Rendering/performance considerations:** modal forms lightweight; layout auto-sizes/wraps at DPI; no network/long task on modal UI thread.
17. **Acceptance criteria:** modal screenshot/keyboard matrix passes 100–200%, long Thai/English, High Contrast; routine validation inline; native pickers remain native.
18. **Estimated effort:** M.
19. **Dependencies and risks:** modal automation harness; altering captions may affect tests/user muscle memory.

## 10. Control Usage Decision Matrix

| Scenario | Current control | Recommended control | Reason | Shared component candidate |
| -------- | --------------- | ------------------- | ------ | -------------------------- |
| Primary action | ordinary/custom-colored Button; often several peers | one native Button with primary semantic style per pane/dialog | makes next step clear; default action can be mapped safely | `PrimaryActionButton` factory/token |
| Secondary action | Button with similar weight to primary | native secondary Button, text verb, stable placement | actions remain visible without competing | `SecondaryActionButton` |
| Destructive action | danger-colored Delete; Event Clear near Export | separated destructive Button + confirmation/undo according to consequence | distinguishes data/list mutation from operation control | `DestructiveAction` + confirmation template |
| Boolean option | CheckBox, including reverse/custom server/show password | retain native CheckBox with persistent label/help | independent boolean is correct semantics and has platform keyboard/UIA behavior | `OptionCheckBox` / labeled option row |
| Multiple independent options | separate CheckBoxes | retain group of native CheckBoxes | users can choose any combination; radio/dropdown would falsely imply exclusivity | `OptionGroup` |
| Small mutually exclusive selection | ComboBox or custom protocol selector | radio buttons only for 2–4 stable choices when all should be visible; otherwise retain selector pending Trace decision | radio exposes all choices but may violate compact deterministic Trace layout | `ChoiceGroup` with radio/selector variants |
| Large single-selection list | ComboBox | retain `DropDownList` ComboBox | saves space and prevents invalid free text | labeled select field |
| Searchable/dynamic selection | Traceroute target is editable ComboBox; Live Ping targets are multiline TextBox and profile is `DropDownList` | retain editable ComboBox only where typing plus history is required, chiefly Trace target | supports typed host plus dynamic history; profile remains closed-list and multiline targets remain direct text entry | `SearchableComboField` |
| Immediate on/off state | no consistent switch; some CheckBoxes take effect in operation setup | use native CheckBox with present-tense state; no cosmetic custom switch unless platform-accessible implementation is proven | WinForms has no standard switch and native checkbox gives reliable role/state | `ImmediateOptionCheckBox` |
| Settings saved later | CheckBoxes/ComboBoxes in setup | retain form controls plus explicit Save/Apply only when a durable settings scope exists | switch implies immediate effect; setup values are applied at Start/Save | `SettingsFormSection` |
| Long-running operation | Start/Pause/Stop buttons + status text | operation toolbar: one primary Start, Pause/Resume secondary, Cancel/Stop operation action, local progress/status | clear state ownership and cancellation; Stop is not destructive | `OperationToolbar` + reducer |
| Validation error | modal MessageBox | inline field error + summary; focus first invalid | error remains near correction point and is screen-reader relatable | `ValidationPresenter` |
| Empty state | blank DataGridView/TextBox | in-place state overlay within data region with reason and next action | distinguishes initial/no result/filter empty/error without modal interruption | `DataRegionStatePresenter` |
| Warning | MessageBox or color/text | inline non-modal warning banner with icon+heading+action; dialog only before consequential decision | warning stays contextual and does not interrupt routine work | `StatusBanner` |
| Connection status | global text/color and row text | screen-local status chip/text + global aggregate; icon/text/color together | concurrent operations need local ownership and non-color cue | `OperationStatusPresenter` |
| Table/list actions | per-screen rows of text Buttons | consistent text-first toolbar: filter/search left, view/copy/export right, destructive separated | preserves discoverability and keyboard names; icon-only unnecessary | `DataViewToolbar` |
| Log actions | search/level/Export/Clear | standard log toolbar + read-only grid + safe Clear policy | aligns filters, result count and export scope; protects data mutation | `LogView` |

Additional decisions:

- **Menu button:** ใช้เฉพาะกลุ่มคำสั่ง low-frequency เช่น export variants; ไม่ใช้แทน data-selection ComboBox และไม่จำเป็นหากมีเพียง action เดียว.
- **Tabs:** top-level 8 tabs และ Trace Session 1/2 เป็น peer content ที่ถูก semantic; command tabsใน Collector ก็เป็น peer command sets ไม่ใช่ wizard. ห้ามใช้ tabs ซ่อน validation step ตามลำดับ.
- **Icon buttons:** ไม่จำเป็นสำหรับ core modernization; text-first actions ลด ambiguity. ถ้าเพิ่ม icon ต้อง embedded/local, 16px logical size, DPI-aware, มี visible tooltip และ `AccessibleName`.
- **Dialog:** ใช้กับ confirmation ที่มีผล, short focused editing หรือ platform file/folder selection; routine loading/error ไม่เปิด modal ซ้อน.
- **Password visibility:** คง native `Show passwords` CheckBox; state affects only rendering, must never copy secret into accessible description, argv, state, logs or screenshot fixture.

## 11. Proposed Layout and Interaction Patterns

### Layout primitives

| Pattern | Specification for NetStuck | Where |
| --- | --- | --- |
| App shell | app header 72 logical px (existing), peer tabs, content host, priority-aware status; 12px page inset | all screens |
| Page header | optional title + one-line context/local state; no decorative card when tab label already sufficient | dense/high-risk pages only, chiefly Collector |
| Section/group | heading + content separated by 8/12px whitespace or 1px divider; border only when containment matters | setup/credentials/commands |
| Form row | persistent label, control, unit/help/error; logical reading/tab order matches visual order | every input region |
| Action bar | secondary actions first/grouped, one safe primary, operation Cancel separate, destructive actions isolated | Ping/Trace/DNS/Collector/dialogs |
| Filter bar | search and data filters left; result count, view/copy/export right; wraps or overflows by priority | Ping/Trace/Log/grids |
| Data view | read-only table + stable selection/sort/scroll/column geometry + in-place state overlay | all result grids |
| Split view | resizable with minimum sizes; persist/clamp distance; support Reset layout | Ping, Trace, Collector |
| Status banner | icon + concise heading + explanation + optional action; non-modal and not color-only | validation, partial/error/offline |
| Dialog footer | 52px minimum logical height, 12/16px inset, right-aligned secondary then primary; Accept/Cancel semantics | custom dialogs |

### Interaction contract

1. **One operation owner:** every async operation has generation ID, state, CTS, local presenter and one global registry entry.
2. **One primary per context:** primary action changes with state (`Start` → disabled; `Pause/Resume`; explicit `Cancel/Stop`) but destructive action never inherits primary position/color.
3. **Validation before mutation:** validate all visible inputs, present summary + field errors, then snapshot immutable run options before scheduling work.
4. **Stable data surfaces:** incremental/batched updates cannot reset user sort, column width/order, row selection, focused cell or scroll unless the selected row no longer exists; then announce the fallback.
5. **State in place:** initial/loading/empty/filter-empty/partial/error replaces only the data body overlay, not the toolbar/headers/cancel action.
6. **Progress:** determinate `completed/total` when total is known (Collector/Ping-all); indeterminate text/spinner only for bounded unknown phases such as DNS resolution startup. Motion must be minimal and optional; Windows progress control is preferred.
7. **Completion:** show persistent local outcome/count and optionally brief global summary; copy/export success stays near the invoked action and does not use a toast requiring a new dependency.
8. **Focus:** successful action keeps focus predictable; validation focuses first error; modal close returns to invoker; refresh never steals focus from an active input/grid.
9. **Responsive layout:** use TableLayoutPanel percent/AutoSize + minimum column widths and scrollable setup areas. At effective narrow width, paired utility panes stack; deterministic Trace grid remains a documented exception.
10. **No card soup:** use whitespace and section headings first; cards only for meaningful independent panes/metrics or background containment.

### Shared data-screen wireframe

```text
[Inputs / scope]                                  [Local operation state]
[Primary action] [Pause/Cancel]       [Secondary settings/actions]

[Search________________] [Filter▼]  12 of 40   [Columns] [Copy] [Export]
┌ Column headers ────────────────────────────────────────────────────┐
│ rows, or one in-place state:                                     │
│ [icon] No matching results   Clear filter                        │
└───────────────────────────────────────────────────────────────────┘
[Last updated / partial warning]                    [Retry if relevant]
```

### Validation and consequential-action patterns

```text
[Error summary: 2 fields need attention]
Username  [________________]  Username is required
Port      [ 70000 ]           Enter 1–65535

                                      [Cancel] [Start collection]
```

- Confirmation title uses consequence (“Delete saved list?”), body names sanitized item and explains irreversibility, default focus is safe action.
- Unsaved changes applies only where a durable editable artifact exists (Hop descriptions/settings draft); transient operation inputs do not need a generic dirty dialog unless closing loses meaningful user-authored content.
- Permission denied/timeout/offline states retain inputs and valid partial rows, state the cause in plain language and offer one relevant retry/change-path action.

## 12. Proposed Design-System Baseline

This is a **small WinForms-native baseline**, not a new framework. Existing values are retained where they already form a tested contract; recommended values normalize repeated raw metrics and accessibility states.

### Geometry and typography

| Token/role | Existing verified value | Recommended normalized value | Basis/rationale |
| --- | --- | --- | --- |
| Spacing scale | common 8/12; raw values vary | 4, 8, 12, 16, 24 logical px | derives from existing 8/12 rhythm; 4 for tight internal gap, 16/24 for hierarchy |
| Page margin | `NewPage`/cards commonly 12 | 12 default; 16 for dialog/content edge where room permits | preserve minimum-width density; avoid nested padding accumulation |
| Section gap | mixed | 16 between sections; 8 label-to-control/help; 12 toolbar-to-data | distinguishes hierarchy without borders/cards |
| App header | 72px | retain 72 logical px | active `BuildShell` value at `NetStuck.cs:248`; change only with DPI manifest review |
| Section-header panel | 55px | retain where title + subtitle need two lines | existing `SectionHeader` helper at `NetStuck.cs:1679-1684` |
| Button height | 34px helper baseline | 34 dense desktop; 36 comfortable/dialog primary | existing consistency plus clearer click/focus target; width/padding remains content-driven |
| Text/select field height | native/mixed | platform-native preferred, target 30–32 when explicitly sized | native metrics scale better with DPI; do not force pixel height where AutoSize works |
| Grid header/row | 36/32px | retain 36/32 dense; allow 40/36 comfortable mode only as future opt-in | existing operational density and tested throughput |
| App title | Segoe UI ~17 Semibold | 17 Semibold | existing hierarchy |
| Section title | mixed bold | 11.5 Semibold | one step above 9.25 body without oversized cards |
| Body/control | Segoe UI 9.x | 9.25 Regular | Windows-native family/current density; scale through DPI, not manual coordinates |
| Caption/help | muted 8–9 | 8.5–9 Regular | readable secondary text; must still pass 4.5:1 |
| Metric/result | ~13/15 | metric 13 Semibold; primary result 15 Semibold | preserve existing visual emphasis, use sparingly |
| Terminal/mono | Consolas ~9–10 | Consolas 9.5/10 with user zoom | preserves alignment; terminal zoom scope labeled |

### Width, containment and rendering

| Element | Recommended baseline | Rationale/exception |
| --- | --- | --- |
| Standard text field | fluid; preferred 220–320px, minimum 160px | host/paths need room; flex before fixed values |
| Numeric field | 96–120px minimum plus visible unit | sufficient for values/spinner; do not stretch across card |
| ComboBox | 160–240px typical, based on longest expected option | avoid clipped choices; editable target can be fluid |
| Trace service fields | exactly three 120px columns | explicit v1.2.3 source invariant; feature tests protect equality/adjacency but should add numeric assertions |
| Multiline input | fluid; minimum 3–5 lines by workflow | targets/commands need scan/copy; setup container scrolls at DPI |
| Corner radius | 0/native square corners | WinForms/platform-native and avoids expensive/custom inconsistent chrome |
| Borders | native 1px control border; section divider 1px semantic neutral | whitespace first; border only for containment/focus/state |
| Cards | one background/border per independent pane at most | prevent nested boxes/card soup |
| Splitter | 8px logical gutter; minimum panels explicitly compatible with distance | existing visual rhythm and `SplitContainer` safety |
| Window minimum | retain 1100×700 logical contract pending DPI validation | explicit baseline; effective reachability may require scrolling at 200% |

### Color and state tokens

| Role | Current | Recommendation |
| --- | --- | --- |
| Canvas/surface | hard-coded light neutrals | semantic `Canvas`, `Surface`, `SurfaceSubtle`; High Contrast uses system colors |
| Text primary/secondary | palette text/muted | both ≥4.5:1 on every used surface; secondary is not “disabled” color |
| Accent/selection | custom blue | ≥3:1 boundary/focus and readable foreground; use system highlight in High Contrast |
| Success/warning/error/info | green/amber/red/blue fills/text | each has foreground/background pair meeting text target plus icon/label; color never sole signal |
| Focus | implicit/native mixed with custom draw | 2 logical px or native focus rectangle, visible on all adjacent colors at ≥3:1, not covered by hover |
| Disabled | custom pale/gray | native disabled rendering where possible; still legible, cursor/tooltip does not imply action |
| Hover/pressed | helper/custom draw | native state renderer; if custom, hover is subtle and pressed has boundary/fill change without layout shift |
| Selected | tab/grid custom colors | selection remains distinct with focus and High Contrast; inactive selection differentiable |

Exact replacement hex values should be chosen only after rendering on the supported Windows versions and automated contrast checks; the audit intentionally does not invent a brand palette without that evidence.

### Component policies

- **Icons:** reuse repository-owned packaged/icon assets; action icons optional 16px logical and local with multiple scale sources or vector-capable platform resource. Text label remains for ambiguous/frequent actions. Existing bitmap fidelity at packaged runtime must be separately verified because the screenshot host icon is not representative.
- **Density:** default dense operational mode uses existing 34px actions and 32px rows; do not add a density toggle until user need is demonstrated. High DPI is scaling, not “comfortable density.”
- **Dialogs:** owner-centered, AutoScale/DPI-aware, 12/16px content, 52px footer, minimum size derived from wrapped content, native Accept/Cancel.
- **Tables/lists/logs:** explicit read-only semantics, full-row selection where row is the unit, sortable columns where meaningful, keyboard copy, resize/reorder, persisted sanitized view state, stable refresh and empty/error overlay.
- **Empty state:** one concise title, cause/context and at most one primary next action plus optional secondary reset; no decorative illustration needed.
- **Loading/progress:** native ProgressBar for determinate progress, compact indeterminate indicator/text for bounded unknown phase, always preserve Cancel and avoid blocking modal progress dialogs.
- **Tooltips:** explain truncated/status/icon content, not replace persistent field labels; accessible name/description must not depend solely on hover.
- **Animation:** no current animation beyond live updates; keep transitions instant/minimal, so reduced-motion handling is currently **Not applicable** unless animation is introduced later.

## 13. Accessibility Audit

### Coverage matrix

| Requirement | Status | Evidence / observation | Required gate |
| --- | --- | --- | --- |
| Keyboard-only operation | Partially verified | Native controls/actions exist, but full traversal was not run; source has no defined screen maps | scripted traversal + manual task completion on all tabs/dialogs |
| Logical tab order | Not verified | 53 interactive controls reported `TabIndex == 0`; no explicit `TabIndex` declarations | unique logical order per container and dynamic-state tests |
| Visible focus | Partially verified | native controls provide focus; owner-drawn tabs/protocol selector may override visual cues | screenshot/manual check normal + High Contrast |
| Access keys/shortcuts | Verified gap | runtime buttons with `&` mnemonic: 0; no source mnemonic strategy | unique Alt keys and documented screen shortcuts |
| Accessible name | Verified gap for explicit metadata | 130 interactive controls; explicit `AccessibleName`: 0 | no unnamed actionable control except documented native exception |
| Role/state/value | Partially verified | native WinForms controls derive base roles; custom draw and read-only grids are not explicitly characterized | UI Automation tree assertions + Narrator |
| Accessible description/help | Verified gap | explicit `AccessibleDescription`: 0 | concise context/units/error relation where name alone is insufficient |
| Screen reader compatibility | Not verified | Narrator/NVDA/Accessibility Insights not run | manual Narrator task scripts + automation tree snapshot |
| Text contrast | Verified failures | measured custom pairs range ~3.07–4.43:1 for normal text; several below 4.5:1 | automated token matrix and rendered-state check |
| Non-text/focus contrast | Partially verified | trace border ~2.56:1; focus states not comprehensively rendered | ≥3:1 for focus/boundary/state indicators |
| Non-color status indicators | Partially verified | many states include text, but color styling carries additional meaning in metrics/tabs | icon/text/state shape for every semantic color |
| Error identification/instructions | Verified gap | modal messages not consistently related to field/focus | inline error text, summary and correction hint |
| Pointer/touch target | Partially verified | buttons commonly 34px high; desktop mouse target is usable, touch target matrix not run | maintain spacing; evaluate ≥40px comfortable actions if touch is in supported scope |
| High Contrast | Not verified | hard-coded colors + owner draw; no test run | system-color branch and manual matrix |
| 100/125/150/200% display scaling | 100% partially verified; others Not verified | 1100×900/1460×900 safe captures only; minimum 1100×700 visual not captured | four-scale screen/dialog matrix |
| Font/app zoom | Partially verified | `Features.cs:1595-1629` scales textbox/rich/grid subset | clarify scope; DPI/font test for all control types |
| Long Thai/English labels | Not verified | strings hard-coded English; no pseudo-locale fixture | 150–200% length fixture with wrapping/tooltip rules |
| Text truncation/full value | Partially verified | no overlap at 1100×900/100%; fixed status widths and minimum-height case remain | ellipsis + keyboard/screen-reader-accessible full text |
| RTL | Not applicable to current product | no localization resources/RTL support in repository | revisit only if product adds RTL locale |
| Icon clarity/DPI | Partially verified | repository icon assets exist; screenshot title bar and header logo derive from capture-host Form icon, not packaged EXE icon | packaged EXE multi-scale capture + accessible text |
| Reduced animation | Not applicable | no designed animation; live data updates only | respect system setting if animation is added |

### Detailed conclusions

- **A11Y-01 is not a claim that every native control is nameless.** WinForms can derive an accessible name from visible `Text`; the verified problem is zero explicit metadata and missing robust field association in programmatic, cue-dependent forms. The required test must inspect the actual UI Automation tree.
- Collector credential fields are the highest-risk naming case because cue text disappears on entry. A persistent `AUTH1 username`, `AUTH1 password`, `AUTH2 username`, `AUTH2 password` label is required; help text may explain use but may never echo the value.
- Result grids need explicit read-only behavior and stable column names. Selection, sort direction, row status, filter count and partial-error state should be exposed as text/state, not paint color alone.
- Owner-drawn top-level tabs and Traceroute protocol control must call/access native semantics or expose equivalent name/role/state/focus. Visual conformance alone is insufficient.
- Status announcements must be throttled: announce operation start, meaningful milestone, pause/cancel/failure/completion; do not announce every Ping sample, trace hop or terminal line.

### Accessibility acceptance suite

1. Complete one core task in every top-level tab using keyboard only, including safe synthetic validation paths and dialogs.
2. Capture and diff a normalized UI Automation tree: name, control type/role, enabled, focusable, selected/checked, value/read-only and error relation.
3. Narrator manual script covers launch, tab navigation, Collector credentials, Ping result/history, Trace session switch, filtered-empty state and modal focus return.
4. Render normal, focus, hover, selected, disabled, success, warning, error and High Contrast states; automated contrast calculations use final rendered token pairs.
5. No test artifact, automation property, screenshot or diagnostic includes plaintext credentials, local host identity or unauthorized active target; allowlisted built-in documentation/default examples may remain visible.

## 14. High-DPI, Resizing and Localization Audit

### Verified display behavior

| Area | 1100×900 @100% | 1460×900 @100% | 1100×700 minimum | 125/150/200% / source risk |
| --- | --- | --- | --- | --- |
| Shell/tabs/status | no overlap in safe capture | fits | source declaration only; 700-height Not verified | Not verified; fixed status widths/custom tab draw |
| Live Ping | inputs/actions/grid/history reachable | more result space | Not verified at 700 height | Not verified; dense left pane/raw metrics |
| Traceroute | deterministic inputs/action row do not overlap | fits | width 1100 tested only at height 900; 700-height Not verified | Not verified; fixed 120px trio/custom draw |
| DNS | fits | fits | Not verified at 700 height | Not verified; fixed/percent toolbar geometry |
| MAC/WAN | two panes fit | fits | Not verified at 700 height | Not verified; paired panes may become too narrow |
| Calculators | two panes fit | fits | Not verified at 700 height | Not verified; long labels/result wrapping |
| Collector | principal regions visible but dense | more usable terminal/grid | Not verified at 700 height | Not verified; fixed rows/columns/vertical reachability risk |
| Event Log | fits | fits | Not verified at 700 height | Not verified; toolbar long strings/filter count |
| Updates | fits/scrolls | fits | Not verified at 700 height | Not verified; long static text |
| Custom dialogs | source only | source only | Not verified | Not verified; fixed sizing/AutoScale variation |

### Findings

- `AutoScaleMode.Dpi` at `src/NetStuck/NetStuck.cs:211` is a useful starting point, but the compile path `scripts/Build-NetStuck.ps1:38-45` does not include a repository manifest/config that makes process DPI-awareness policy auditable. This is a platform configuration gap, not evidence that Windows necessarily renders the current binary incorrectly.
- `tests/FeatureTests.cs:39-42` disables scaling for deterministic fixtures, so its passing bounds do not cover DPI scale transitions. The exact v1.2.3 Trace grid contract still provides a valuable logical-geometry gate.
- App zoom at `src/NetStuck/NetStuck.Features.cs:1595-1629` changes TextBox/RichTextBox/grid fonts rather than the entire UI. It should be named “data/text zoom” unless expanded and resource ownership corrected.
- Split containers need compatible `Panel1MinSize`, `Panel2MinSize` and `SplitterDistance` after scaling. Restore persisted distances only after clamping to current client size/DPI.
- Paired MAC/WAN and Calculator panes should stack when their minimum content widths cannot coexist; Collector setup should become vertically scrollable while keeping run Cancel and current progress reachable.
- Repository UI strings are hard-coded English and no `.resx` localization layer exists. Thai/English long-text testing is therefore a resilience fixture, not a claim of current Thai localization. RTL is out of present scope.

### Proposed DPI/resizing sequence

1. Record packaged EXE behavior and actual DPI-awareness context before changing manifest policy.
2. Create screen/dialog fixtures at 100/125/150/200%, minimum logical size and one larger size on supported Windows 10/11.
3. Replace clipping-prone absolute metrics with AutoSize/percent/min-width/scroll patterns, while locking the documented Trace exception.
4. Add pseudo-long strings: 2× English, representative Thai without whitespace-heavy assumptions, long host/path/IPv6/status values and 4-digit numeric values.
5. Test movement between mixed-DPI monitors, maximize/restore and split/column-state restore; no offscreen modal or unreachable action.
6. Only then select/update manifest and golden geometry intentionally; do not treat regenerated snapshots as proof without review.

### Acceptance gate

At 100/125/150/200% and supported Windows versions: no control overlap/clipping, every action/input reachable, text wraps/ellipsizes according to documented priority, full critical values remain accessible, tab/focus order follows visible layout, grids keep usable minimum columns, and resize/restore does not throw or place content outside the client area.

## 15. Rendering and UI Performance Audit

### Fresh baseline

| Measure | Result | Interpretation |
| --- | ---: | --- |
| Warm startup | 926 ms | passed current performance test; not a cross-machine SLA |
| Double-buffered grids | 11 | mitigation is enabled on all detected grids; flicker itself was not directly measured |
| Live Ping `/24` | 254 rows / 2,412 probes / 19 ms worst dispatch | passed synthetic dispatch gate |
| Dual Traceroute | 46 ms worst dispatch | passed current synthetic dual-session gate |
| Working set | 73 MB | point measurement only |
| Accelerated soak | 11.45 s / 19 ms worst dispatch / +8 MB | short confidence gate, not long-term leak proof |

### Evidence classification

| Topic | Classification | Evidence | Audit conclusion / next check |
| --- | --- | --- | --- |
| Ping UI batching | Verified protective implementation | `NetStuck.Release1.cs:130-165`; performance tests | retain fixed cadence/batching; delta/selection contract must stay |
| Grid flicker | Verified mitigation enabled; flicker Not directly measured | double buffering `NetStuck.Release1.cs:54-64`; 11/11 detected | retain; add interactive resize/High-DPI observation gate |
| Traceroute stale update | Verified source defect | `NetStuck.V103.cs:1011-1022` before guards `:1040-1095` | fix generation gate before visual redesign |
| Collector finalization | Profiling risk, source-backed | `Features.cs:649-678`, `:1206-1285` after awaited work on captured context | profile large files; move CPU/file work off UI thread |
| Collector terminal budget | Verified code-contract mismatch / runtime scale not reproduced | 524,288-char item; budget check `Features.cs:1357-1365`; architecture says 128 KiB/pass | chunk by budget and bound preview queue |
| Ping all sequencing/reentry | Verified source issue | `Features.cs:485-496`, `:565-603` | bounded concurrency + state/cancel tests |
| Network/SSH process streaming | Partially verified protective design | async pumps `Features.cs:250-264`; tests/mocks pass | retain async streams; profile real-shape synthetic output only |
| Synchronous network on UI | No broad defect confirmed | principal flows use async; source audit found specific exceptions/lifecycle gaps only | do not claim rewrite; test cancellation/slow fake endpoints |
| Progress feedback | Verified inconsistency | screenshots + screen-specific status/button/row code | shared state/progress presenter |
| Long tables without virtualization | Preventive concern, not current defect | DataTable/DataGridView; `/24` fixture passes | profile 1k/10k rows before enabling virtual mode/paging |
| Unbounded UI collections | Source-backed risk | Trace caches `V103.cs:138-201`; terminal queue `Features.cs:403-405`, `:1343-1349` | byte/item caps and long soak |
| Font/GDI lifecycle | Source-backed risk | new fonts `Features.cs:1616-1628` | ownership/disposal + handle counter |
| Timer cadence | No excessive cadence finding | fixed cadence tests 3/3; clock timer source | preserve; explicitly dispose on close |
| Duplicate subscriptions | No defect confirmed | no reproducible duplicate event firing in tests | add one-time subscription assertion if builders consolidated |
| Layout thrashing/control churn | No defect confirmed | programmatic controls are principally created at page/form construction; static safe shots show no overlap at tested sizes, but resize/flicker was not measured | profile layout/paint counts during interactive resize at 100–200% before changing component topology |
| Filter debounce/throttle | Profiling risk, not reproduced | Ping filter `V103.cs:317-322` and Log search `NetStuck.cs:619-620` apply on `TextChanged` | profile rapid typing with populated 10k-row log; add 150–250 ms debounce only if measured |
| Per-row formatting/full redraw | Profiling risk, current `/24` gate passes | cell formatting `NetStuck.cs:1567-1580`; Ping grid invalidate `Release1.cs:161-165` | profile 1k+ rows/sort/filter; prefer changed-row invalidation only with evidence |
| Synchronous export/state/cache I/O | Profiling risk, not benchmarked at large scale | CSV construction/write `NetStuck.cs:1464-1474`; close/state work `:1835-1859` | profile large export/slow synthetic filesystem; offload only if UI delay is measured, with atomic/error semantics |
| Repeated image loading | No defect confirmed | embedded app assets instantiated at shell creation | packaged DPI asset check; avoid per-row image creation |
| Bitmap asset DPI | Not verified | compiler ICO and separately packaged PNG exist, but capture-host title bar/header logo are not packaged EXE evidence | inspect packaged asset at 100–200%; provide correct scale sources if blur is observed |
| Cross-thread UI access | Protective pattern present; lifecycle risk remains | queues/marshaling and current tests | enforce disposal/generation guard; no direct worker UI writes |
| Modal dialog over long operation | No defect confirmed; dialogs not runtime-captured | principal long operations use page controls/status; modal families are short input/confirmation/native pickers | keep progress/cancel in page; never add modal spinner that hides Cancel |
| Selection/scroll loss | Current feature protection exists in principal tables | feature tests/source behavior | make shared invariant and add populated visual/interaction test |
| Status layout jump/truncation | Source-backed responsive risk | fixed local/public identity widths `V103.cs:117-126`; spring already exists in `NetStuck.cs:269-275` | retain spring; priority/ellipsis for identity slots; benchmark not needed |

No percentage improvement is claimed. PERF-01/PERF-02 require synthetic worst-case profiling before and after future implementation, using identical workload, machine, build mode and capture method.

### Responsiveness targets for future implementation

- UI dispatch/pump ต้องผ่าน existing absolute gates และรายงาน p50/p95/max ภายใต้ approved large synthetic fixture; กำหนด tighter SLA หลังมี percentile telemetry/representative workload ไม่ตั้ง 50 ms โดยไม่มีฐาน.
- Cancel control paints and accepts input during every long-running phase; cancellation completes within the operation-specific timeout bound.
- Terminal preview enforces explicit bytes/chars per tick and total retained bytes; canonical capture remains lossless and redacted.
- Data refresh preserves selection/focused cell/sort/scroll; only changed rows invalidate when practical.
- Long soak tracks managed memory, private bytes, GDI/User handles, queue depth, cache count, timer/task count and unobserved exceptions.

## 16. Loading, Empty, Error and Progress-State Audit

### Operation-state coverage

| Workflow | Current observed/source states | Missing or ambiguous states | Recommended state contract | Evidence |
| --- | --- | --- | --- | --- |
| Live Ping | idle, running, paused, stop/complete, row up/down/error | validating, starting, cancelling, filtered-empty/partial summary | `Idle → Validating → Running ↔ Paused → Cancelling/Completed/Failed`; rows retain partial data | active builder/workflows; automated states |
| Traceroute | idle, discovering/running, paused, events/errors, stop | explicit failed terminal, stale-generation rejection, filtered empty | per-session reducer with `Resolving/Running/Paused/Cancelling/Cancelled/Complete/Partial/Failed` | `V103.cs:869-1095` |
| DNS one-shot | ready/resolving/results/errors | local progress, partial/no result, explicit cancel | `Validating/Resolving/Partial/Complete/Failed/Cancelled` | `Release1.cs:228-354` |
| DNS polling | running/stop/error | starting/cancelling/retry/offline ownership | `Starting/Running/Cancelling/Cancelled/Failed`, with retained last good rows | same |
| MAC lookup | local/multicast classification, cache hit, external cache-miss request, result/no match/error | extracted-invalid count, explicit source/offline/timeout/retry, filtered empty | `Parsing/Local/Cached/RequestingExternal/Complete/Partial/NoMatch/Offline/Timeout/Failed` | `NetStuck.cs:1089-1157` |
| WAN lookup | ready/loading/result/error | explicit offline/timeout/permission/rate limit/retry | `Requesting/Complete/Offline/Timeout/Denied/Failed/Cancelled` | `NetStuck.cs:1160-1195`; live external not run |
| Calculators | initial/result/modal error | inline invalid/boundary | `Initial/Valid/Invalid/Boundary` | `NetStuck.cs:573-610`; core tests |
| Collector Ping all | idle/rows | running ownership, cancel, reentry, determinate progress | `Preflighting(completed,total)/Cancelling/Ready/Partial/Failed` | `Features.cs:485-496`, `:565-585` |
| Collector Collect | validating/running/per-device status/cancel/complete/error | immutable snapshot, overall progress, next-run draft, output denied, terminal truncated | `Validating/Ready/Collecting/Cancelling/Partial/Complete/Failed`; structured per-device substate | `Features.cs:589-687`, `:1343-1385` |
| Event Log | empty/receiving/filtered/export/clear | filtered-empty, export completion scope/error | `NoEvents/Receiving/Filtered/FilteredEmpty/Exported/ExportFailed/Cleared` | `NetStuck.cs:613-631` |
| Updates | available static content | content missing/corrupt | `Available/Missing`; no network loading state | `NetStuck.cs:633-718` |

### Shared feedback pattern

| State | Presentation | Action/behavior | Accessibility |
| --- | --- | --- | --- |
| Idle/ready | short local instruction; do not fill grid with fake rows | primary Start/Resolve/Lookup enabled when valid | read once on focus, no repeated announcement |
| Dirty/modified | subtle `Changes apply next run` or dialog dirty marker only where durable | Save/Discard when meaningful | state included in section/dialog name |
| Validating | normally synchronous/local; reserve error area | disable duplicate submit for the validation transaction | announce summary then focus first error |
| Connecting/running | text + optional icon + elapsed/count; determinate bar if total known | Pause/Cancel reachable; config behavior explicit | announce start and milestone, not every update |
| Paused | persistent `Paused` text + Resume primary | Stop remains available; no new samples scheduled | selected/paused state announced |
| Cancelling | `Cancelling…` and bounded wait; inputs not prematurely re-enabled | no duplicate Cancel; close follows shutdown policy | announce once; no indefinite spinner |
| Success | local completion count/time, retain results | Copy/export/next run available | announce concise outcome once |
| Partial success/warning | warning banner above retained valid results with failed count | Retry failed / inspect errors | icon + heading + text, not color alone |
| Recoverable error | inline banner/body state with cause and correction | Retry/change input/path; preserve user input | programmatic relation and focus decision |
| Fatal error | scoped blocking dialog only when app cannot safely continue | Close/copy diagnostic, sanitized | clear title, safe default; no secret detail |
| Disconnected/offline | explicit source and last-good timestamp | Retry only on explicit action; local functions remain usable | status text distinguishes offline from empty |
| Empty/no data | in-place body message explains not started/no result | one next action if appropriate | named state; grid headers remain available if helpful |
| Filtered empty | `No results match current filters` | Clear filter | focusable clear action; total count announced |
| Permission denied | path/action-specific inline error | Change folder/retry; do not discard inputs/results | identify resource category without sensitive full path if screenshot/logged |
| Timeout | operation/target-specific state and duration | Retry/change timeout | row and summary expose timeout text |
| Copy/export completion | adjacent non-modal status with count/safe path treatment | Open folder only if already authorized and safe | announced once; never includes secret content |

### State invariants

- A state transition and action enablement update occur in one UI-thread transaction from a typed model, not independent text/color assignments.
- Late generation cannot change state, table, topology, selection or global status.
- `Cancel` means request cancellation; `Cancelled` is shown only after work stops. Timeout/partial/error are distinct from user cancellation.
- Loading/progress never destroys valid prior data unless the user explicitly clears/replaces it; stale data is labeled with last-updated time while refresh runs.
- Empty/filter-empty/error overlays do not alter column geometry, sort, split distance or selected row state.

## 17. Reusable Component Opportunities

These are incremental helpers/components inside the existing WinForms codebase. None requires a new UI framework, package manager or production dependency.

| Candidate | Responsibility | Initial consumers | Source motivation | Dependency/risk |
| --- | --- | --- | --- | --- |
| `UiTokens` | spacing, typography, semantic colors, logical sizes, density | all builders/custom renderers | repeated metrics/palette `NetStuck.cs:86-94`, `:1664-1805` | theme/High Contrast branch must not freeze hard-coded colors |
| `LabeledFieldRow` | persistent label, input, unit/help, inline error, accessibility association | Ping, Trace, DNS, calculators, Collector | generic field/cue gaps `NetStuck.cs:1724-1732`, `:1881-1884` | AutoSize/wrapping at DPI and long text |
| `CredentialFieldRow` | AUTH label, username/password, native visibility checkbox, secret-safe metadata | Collector | `Features.cs:451-461` | strict no-secret logging/state/UIA description |
| `OperationController<TState>` | generation, allowed transitions, CTS, enablement, local/global status registration | Trace first; Collector; then Ping/DNS/lookups | ASYNC-01, STATE-01–04 | preserve fixed cadence and partial-result semantics |
| `OperationToolbar` | one primary, pause/resume, cancel, secondary actions with state-driven enablement | Ping, Trace, DNS, Collector | action hierarchy drift | existing control names/reflection tests |
| `DataViewToolbar` | search/filter/result count/columns/copy/export and destructive separation | Ping, Trace events, DNS, Event Log | repeated toolbar patterns | action availability differs; configure, do not clone giant component |
| `ReadOnlyResultGrid` | explicit read-only/accessibility/sort/copy/resize/reorder/stable refresh defaults | all result/log/history grids | `NetStuck.cs:1740-1760`; `Release1.cs:66-74` | must preserve per-grid column types and selection behavior |
| `DataRegionStatePresenter` | initial/loading/empty/filter-empty/partial/error overlay without geometry reset | all data regions | blank screenshots/STATE-05 | overlay hit testing/focus and repaint throttling |
| `StatusBanner` | icon+heading+detail+action, semantic severity, accessible announcement | validation/offline/partial/error | inconsistent MessageBox/color feedback | reserve space or reflow without layout jump |
| `MetricsStrip` | compact labeled metrics with semantic state and responsive wrapping | Live Ping/Collector | existing metric panels | update only changed values; contrast |
| `DeterministicTraceInputGrid` | preserve source-defined 46/18/18/18 and exact 120px trio while applying labels/accessibility | Traceroute | active source contract `V103.cs:426-439`; feature tests currently cover equality/adjacency/no-overlap | deliberate exception to generic flexible form rows |
| `SplitViewState` | clamp/persist/reset splitter distances and grid view state | Ping, Trace, Collector | PERSIST-01 | version/DPI migration, no content data in state |
| `BoundedTerminalView` | byte-budget queue, chunking, trim indicator, stable selection/copy, batched append | Collector | `Features.cs:1343-1385` | canonical output must remain complete; GDI/text cost |
| `DialogShell` | owner, DPI layout, 12/16 content, 52 footer, Accept/Cancel, validation/focus-return | three custom dialogs | DLG-01 | native Save/Folder dialogs stay native |
| `SafeTestEnvironment` | one temporary root for state/profile/cache/output; synthetic identity/network fixture | all UI/runtime capture tests | current partial `NETSTUCK_TEST_STATE_PATH` isolation | fail closed if any path escapes root |

Recommended extraction order: tokens + labeled fields/read-only grid → state presenter/operation controller → screen-specific toolbars → dialog/split persistence/terminal. Do not begin by moving every builder; characterize active call paths first and delete/consolidate legacy code only after coverage proves it safe.

## 18. Test Coverage and Visual Regression Strategy

### Coverage plan

| Test area | Current status | Future automation | Manual requirement | Gate |
| --- | --- | --- | --- | --- |
| Existing logic/UI/integration | Exists: 16 Core + 91 Feature | retain every test; update only intentional geometry/text contracts | review behavior deltas | 107/107 minimum plus new tests |
| Performance/cadence/soak | Exists: 10 + 3 + 8 | retain fixed-cadence and dispatch suites; add large Collector/long soak | profile trace with tooling | no regression beyond approved absolute thresholds |
| Component-level UI | Partial helper/reflection coverage | instantiate tokens/fields/grids/state/banner/dialog shell; assert properties and bounds | spot-check render | all variants/states covered |
| Navigation | Top-level existence covered/visually seen | assert 8 tabs, two Trace sessions, focus restore and no duplicate entry | complete task route | exact topology retained |
| Keyboard | Gap | SendKeys/UIA task scripts, tab-order/mnemonic/default/cancel assertions | keyboard-only pass all tabs/dialogs | no unreachable action/focus trap |
| Validation | Partial branch tests | table-driven invalid/boundary/multiple-error/focus tests, including Collector out-of-range port and denied/uncreatable output path before mutation | wording and comprehension review | inline error + correction path; zero unhandled UI exception |
| Loading/error/empty/progress | Partial | deterministic state-model fixture and screenshots for every state | observe timing/announcement | state matrix in section 16 complete |
| Screenshot/visual regression | New safe baseline from this audit | packaged EXE fixture at states, widths, DPI/theme; semantic masks/tolerance | review every accepted golden update | no unreviewed clipping/overlap/state change |
| High DPI/mixed monitor | Gap | separate processes at 100/125/150/200; bounds/text-crop detector | move window between monitors | all section 14 criteria pass |
| Resize/minimum size | Existing 100% geometry tests | parameterized logical size/DPI + splitter/column restore | drag/maximize/restore | no overlap/unreachable action |
| Thai/English long text | Gap | pseudo-long resource/fixture injection | Thai line-break/readability check | no critical truncation; full text accessible |
| Accessibility automation | Gap | UIA tree snapshot, unnamed/focus/read-only/state assertions, contrast token tests | Accessibility Insights/Narrator task scripts | WCAG/platform-applicable AA gate |
| Modal dialogs | Source-only this audit | deterministic open/capture + Enter/Escape/focus return | platform picker checks | every custom dialog state rendered |
| UI responsiveness | Existing synthetic dispatch measures | ETW/Stopwatch pump latency, queue/cache/handle counters, slow fake endpoints | interact while loaded | target metrics in section 15 |
| Plink credential handling | Existing security tests; must retain | fake process captures argv/environment/stdin/log/state; search artifacts | code/security review | password absent from argv/log/state/screenshots |
| Privacy-safe capture | Ad hoc safe fixture used | allowlist OCR/text/UIA/property scan; all storage under temp root | visual identity scan | only approved synthetic identity/targets or documented built-in examples; no secrets |

### Visual regression matrix

| Dimension | Required values |
| --- | --- |
| Build host | packaged `NetStuck.exe`, supported Windows 10 and 11 where infrastructure allows |
| Width/height | 1100×700 logical minimum; 1460×900 reference; one constrained effective-DPI case |
| DPI | 100%, 125%, 150%, 200%; mixed-monitor transition |
| Theme | normal light + Windows High Contrast; future themes only if product adds them |
| Content | initial, populated, long values, filtered empty, partial, error, running/paused/cancelling/completed, disabled/focus |
| Dialogs | save list invalid/valid; hop editor long text/dirty; columns chooser; representative confirmation |
| Language fixture | baseline English, 2× English, representative Thai labels/help/status; RTL excluded |
| Sensitive-data policy | synthetic host/user/paths and RFC 5737 active targets; allowlisted built-in DNS/private-range examples; explicitly synthetic username permitted, but no password/enable-secret rendered; no host-local cache/profile |

Golden-image policy:

1. Capture in a fresh process with fixed font/theme/time/fixture and a fully isolated temporary app-data root.
2. Prefer semantic bounds/text/UIA assertions for controls; use pixel diff for alignment, clipping, focus, state colors and unexpected repaint artifacts.
3. Mask only intentionally variable non-product chrome (clock/window shadow), never mask content/control geometry or error/status text.
4. Use small anti-alias tolerance and region-level thresholds; a passing diff does not replace manual review at golden update.
5. Store fixture metadata (commit, OS build, DPI, window size, scenario) next to—not embedded with sensitive machine identity in—the artifact.

### Security regression specifics

- A fake Plink executable records process arguments and controlled streams; assert password is absent from command line, state, log, event text, terminal preview, output filenames, exception strings and screenshots.
- Verify credential values are cleared/disposed according to the chosen lifetime and never included in `AccessibleName`, `AccessibleDescription`, clipboard helpers or immutable snapshot diagnostics.
- Preserve the current password-free argv design even if UI fields/components are reorganized; a UI modernization is not authority to change transport/security behavior.

## 19. Prioritized Implementation Roadmap

| Phase | Scope | Findings addressed | Dependencies | Risk | Verification |
| ----- | ----- | ------------------ | ------------ | ---- | ------------ |
| A — Foundations | tokens, labeled fields, explicit read-only grid, operation/state model, inline/state presenters, safe test root, UIA/DPI/visual harness | A11Y-01/02/03/04, VAL-01, STATE-04/05, TEST-01, part of CTRL-01/DPI-01 | active-builder map; packaged test host; approved contrast/DPI policy | broad shared-style changes can move geometry or break reflection tests | existing 128; component states; UIA tree; contrast; safe 100–200% smoke |
| B1 — Config Collector | immutable run snapshot, Ping-all/Collect mutual exclusion+cancellation, credential rows, progress/results/terminal bounds, off-UI finalize | PERF-01, STATE-01/02, ASYNC-02, part PERF-02/CTRL-01 | Phase A operation controller, slow/large fake device, security fixture | highest security/state/performance risk; secret leakage or output loss unacceptable | argv/log/state/screenshot secret scan; 10×10MiB profile; cancel/reentry; mock device regression |
| B2 — Live Ping + Traceroute | preserve prominent Ping configuration/table capabilities; atomic Trace generation/state; local operation bars/empty/error states | ASYNC-01, STATE-03, CTRL-01, PERSIST-01, part A11Y/DPI/STATE | Phase A; delayed-probe fixtures; geometry invariants | cadence, dual-session independence, selection/scroll and exact Trace layout | cadence 3/3; Ping/Trace performance; stale-cycle tests; 1100–200% screenshots; keyboard/UIA |
| C — Remaining screens/dialogs | DNS, MAC/WAN, calculators, Event Log, Updates, custom dialog shell, view-state persistence | DLG-01, SHELL-01, UPDATE-01, remainder VAL/STATE/PERSIST/CTRL | proven Phase A patterns; privacy/offline fixtures | UI drift if each screen customizes shared patterns; external MAC cache-miss/WAN behavior cannot gate offline | per-screen state/validation/keyboard screenshots; core/DNS/cache/export tests; no implicit startup/navigation network |
| D — Hardening and cleanup | mixed-DPI/High Contrast/Narrator, long Thai/English, long soak/handles, cache bounds, consolidate characterized legacy builders, documentation | DPI-01, PERF-02, ARCH-01, TEST-01 and residual findings | Phases A–C stable; Windows test matrix; profiling thresholds | manifest/legacy deletion can cause system-wide changes | full expanded suite; packaged startup; repository-default 8-hour soak; accessibility manual sign-off; checksum/release rehearsal |

Phase boundaries are releaseable: finish and verify one screen family before moving forward. Do not execute a repository-wide visual rewrite or delete legacy builders in Phase A. Any geometry/security/cadence contract change requires explicit review and an intentionally updated test, not silent snapshot replacement.

## 20. Quick Wins

These are candidates for the first implementation PRs, not changes made by this audit:

1. Fix Traceroute failure exit so state cannot remain `Discovering`, with branch tests (`STATE-03`, S).
2. Make every result DataGridView explicitly read-only while preserving select/copy/sort/resize/reorder (`A11Y-04`, S).
3. Add persistent Collector AUTH1/AUTH2 field labels and secret-safe accessible names before moving layout (`A11Y-01`, S slice of M).
4. Define and test semantic contrast pairs; replace only failing text/border pairs first, including focus (`A11Y-02`, S slice of M).
5. Add clear initial and filtered-empty text/actions to Ping, Trace, DNS and Event Log using one non-blocking presenter (`STATE-05`, M shared win).
6. Separate Stop/Cancel styling from Delete/Clear and ensure one primary per operation pane (`CTRL-01`, S–M).
7. Focus the invalid field after current validation dialogs as an interim bridge, then migrate to inline feedback (`VAL-01`, S interim).
8. Retain the existing StatusStrip spring and make fixed local/public identity slots priority-aware while exposing full truncated values accessibly (`SHELL-01`, S).
9. Add a single `NETSTUCK_TEST_ROOT`-style test seam covering state/profile/cache/output before expanding screenshots (`TEST-01`, M).
10. Add deterministic stale Trace cycle and Collector post-start-control-mutation tests before implementation (`ASYNC-01`, `STATE-02`, S–M test-first).

Quick wins still pass the complete baseline and privacy/security gates. “Small” does not permit changing the 120px Trace fields, fixed cadence or credential handling contract without review.

## 21. Risks and Backward-Compatibility Concerns

| Risk | Existing contract at stake | Mitigation / release gate |
| --- | --- | --- |
| Trace layout modernization | two sessions; 46/18/18/18 top row; exact 120px Protocol/Port/Packet; action row | encode as named invariant tests at all DPIs before styling; preserve control names where reflection-tested |
| Ping layout/table changes | prominent targets/count; filter/sort/copy; resizable/reorderable columns; stable selection/scroll | task-based regression with populated grid and persisted view state |
| Scheduling refactor | fixed cadence and bounded slots | keep scheduler untouched where possible; cadence suite required on every state/controller change |
| Collector snapshot/credential UI | password-free Plink argv/state/logs and two-stage AUTH behavior | secret-taint fake process/artifact scan; security review blocks release |
| Terminal backpressure | canonical output completeness/redaction | only preview may truncate/drop under documented policy; canonical byte comparison required |
| Moving work off UI thread | exception/cancellation/order semantics | immutable result + one UI apply; deterministic failure/cancel tests |
| DPI manifest change | global WinForms geometry and bitmap scaling | measure packaged baseline first; staged manifest branch with 100–200% golden review |
| High Contrast/system colors | current brand appearance/custom tabs | semantic renderer with normal-theme snapshots plus system-color branch; no color-only cue |
| Persistence expansion | corrupt/stale state, offscreen splits, sensitive data | version, clamp, atomic write, safe reset; schema allowlist contains geometry only |
| Cache bounds | Trace lookup hit rate and saved-cache behavior | deterministic LRU policy, hit/eviction tests, document cap |
| Legacy-builder cleanup | hidden/reflection/fallback call paths | call graph + runtime coverage + staged removal; no bulk deletion |
| Dialog consolidation | caption/default/focus muscle memory | preserve verbs/control names where valuable; keyboard/task tests and release notes |
| Grid virtualization | sorting/copy/data-binding behavior | adopt only after row-count profiling; characterize all commands before switch |
| Screenshot automation | host identity/profile/cache leakage | isolated root, synthetic allowlist, image/text/property scan and human review |
| New UI dependency/framework | build portability, csc-only pipeline, release size | not recommended; use existing WinForms/native controls |

Versioning note: even a visual-only change can be behaviorally observable through focus order, default button, enabled state, saved view schema and screen-reader tree. Treat those as compatibility surfaces and document intentional changes.

## 22. Open Decisions

| Decision | Conservative default | Alternatives / decision evidence needed |
| --- | --- | --- |
| Traceroute Protocol control | retain current compact selector footprint, fix keyboard/UIA/theme semantics | radio buttons if stable 2–4 choices fit without violating exact 120px trio; native DropDownList if owner draw cannot meet accessibility |
| Process DPI-awareness mode/manifest | measure packaged v1.2.3 context first; add only the mode supported by target .NET Framework/Windows matrix | per-monitor mode after mixed-monitor proof; system-aware if compatibility dictates |
| Minimum size semantics at 200% | keep 1100×700 logical contract and make setup scroll/reflow | raise minimum only with product approval; never hide actions beyond viewport |
| Localization scope | English UI with long English/Thai resilience fixtures | full `.resx` Thai localization in separate product scope; RTL only with supported locale |
| Touch target/density | retain dense desktop 34px/32px baseline with spacing/focus fixes | 36–40px comfortable mode if touch/low-vision research justifies; no untested density toggle |
| View-state persistence | column width/order/visibility and splitter distance only | also sort/filter if users expect session restore; never persist credentials/results incidentally |
| Event Log Clear | confirmation with safe default; clarify whether current-session rows only | undo/soft clear or remove Clear if retention policy says log is authoritative |
| Table scaling strategy | current binding/batching until profiling threshold fails | virtual mode/paging only with characterized sorting/copy/export and 10k+ row evidence |
| Collector terminal overload | preserve canonical output; chunk/bound preview and show `Preview truncated` | disk-backed preview/paging if users require all terminal content interactively |
| Collector configuration edits during run | lock active snapshot; edits become explicit next-run draft if supported | fully disable section for simpler first release |
| Global status announcement cadence | start/error/completion + coarse milestones | user-configurable verbosity only if screen-reader research requires |
| Semantic palette | choose tested accessible light tokens close to current identity | additional dark theme is out of this modernization unless explicitly scoped |
| Updates rendering | simple native scroll panel/labels with selectable copy path | RichTextBox only if section navigation/selection requirements outweigh complexity |
| Icons | text-first; reuse repository-owned packaged/icon assets and platform/system symbols where available | curated local 16/20/24px set if repeated user testing shows value |
| Performance SLAs | establish from repeatable representative fixtures and current baseline | do not approve percentage claims or arbitrary row counts without measurement |

No open decision blocks this audit report. Each should be resolved just before its implementation phase with a small prototype/test, not by redesigning every screen at once.

## 23. Standards and Sources

Primary sources were checked on 2026-08-26. Guidance is applied where it maps to Windows desktop/WinForms; web-specific WCAG mechanics are not copied blindly into native controls.

| Source title | Organization | URL | Criterion used | NetStuck application |
| --- | --- | --- | --- | --- |
| Check boxes | Microsoft | [Windows checkbox guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/checkbox) | independent boolean choices, clear labels, standard interaction | reverse lookup, custom DNS, Show passwords and Collector options |
| Radio buttons | Microsoft | [Windows radio-button guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/radio-button) | small mutually exclusive choices visible together | open decision for stable compact protocol/mode choices |
| Combo boxes | Microsoft | [Windows combo-box guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/combo-box) | single selection when space/list size favors a popup; editable variant only when typing is meaningful | targets/history, device type, filters, units, server choices |
| Forms | Microsoft | [Windows forms design guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/forms) | labels, grouping, validation and efficient form flow | persistent field labels, units/help/error rows and Collector setup |
| Dialogs and flyouts | Microsoft | [Windows dialog guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs) | use modal interruption sparingly; clear primary/secondary action | custom dialogs, confirmations and replacement of routine validation MessageBoxes |
| Progress controls | Microsoft | [Windows progress-control guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/progress-controls) | determinate when total is known, indeterminate for unknown bounded phase, avoid false precision | Collector/Ping-all counts and DNS/lookup unknown phases |
| Content basics | Microsoft | [Windows content/layout basics](https://learn.microsoft.com/en-us/windows/apps/design/basics/content-basics) | spacing/alignment/hierarchy and content prioritization | 4/8/12/16/24 rhythm, form/action/data zones, no card soup |
| Guidelines for targeting | Microsoft | [Windows targeting guidance](https://learn.microsoft.com/en-us/windows/apps/develop/input/guidelines-for-targeting) | target size/spacing should reflect input modality and accessibility | desktop button density, focus and possible touch-scope decision |
| Walkthrough: Create an accessible Windows-based application | Microsoft | [WinForms accessibility walkthrough](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/walkthrough-creating-an-accessible-windows-based-application) | WinForms accessible names/descriptions and control properties | explicit metadata, labeled fields, UI Automation test plan |
| Accessible text requirements | Microsoft | [Windows accessible text requirements](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements) | programmatic names, meaningful reading order and text alternatives | controls, statuses, icons, custom draw and result grids |
| Accessibility overview | Microsoft | [Windows accessibility overview](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview) | keyboard, screen readers, color/contrast, High Contrast and inclusive state communication | section 13 matrix and manual/automation gates |
| High DPI support in Windows Forms | Microsoft | [WinForms high-DPI support](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms?view=netframeworkdesktop-4.8) | framework/configuration/scaling behavior must be considered together | `AutoScaleMode.Dpi`, manifest open decision and 100–200% matrix |
| How to make thread-safe calls to Windows Forms controls | Microsoft | [WinForms thread-safe calls](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls) | UI controls are thread-affine; marshal compact updates and manage lifecycle | Ping/Collector batching, atomic trace apply and shutdown guards |
| Web Content Accessibility Guidelines (WCAG) 2.2 | W3C | [WCAG 2.2](https://www.w3.org/TR/WCAG22/) | applicable AA principles: keyboard, focus, contrast, non-color cues, error identification, target size | contrast/focus/error/keyboard acceptance criteria; interpreted for native desktop |
| Understanding Success Criterion 3.3.1: Error Identification | W3C WAI | [Error Identification](https://www.w3.org/WAI/WCAG22/Understanding/error-identification.html) | identify invalid item and describe error in text | inline validation summary, field error and focus behavior |

Source hierarchy for implementation: supported WinForms/.NET Framework behavior and Windows platform guidance first, WCAG 2.2 AA as the accessibility outcome baseline where applicable, then NetStuck's tested security/cadence/layout invariants. No random design blog or unverified trend is used as authority.

## 24. Appendix: Commands, Evidence and Screenshots

### Commands executed

```powershell
Set-Location C:\Codex\_Project\NetStuck
git status --short
git rev-parse HEAD
git describe --tags --always
rg --files
.\scripts\Test-NetStuck.ps1 -SoakSeconds 10
git diff --name-only -- src tests scripts
git ls-files --others --exclude-standard
git diff --no-index --check -- /dev/null docs/UI_AUDIT_REPORT.md
```

Build is invoked by the repository test script using the repository's `csc.exe` path. Source/document inspection used read-only `Get-Content`, `rg` and Git commands. A temporary local capture harness outside the repository instantiated `MainForm` from the built UI assembly in a fresh process; the helper was deleted after capture and is not a deliverable.

### Command results

| Command/check | Result |
| --- | --- |
| Initial `git status --short` | empty; clean baseline |
| `git rev-parse HEAD` | `2f35f72988fac0a44292a6bd69196e0842cbfc73` |
| `git describe --tags --always` | `v1.2.3` |
| Repository test command | 128/128 passed; detailed split/measurements in section 7 |
| Repository-specific Markdown checker | none found in documented scripts; completion uses local structural/link checks |
| Production-source diff | empty for `src`, `tests` and `scripts` |
| Final allowed-file status | `?? docs/UI_AUDIT_REPORT.md` and `?? docs/ui-audit/` only |

### Ad-hoc audit checks

The request permits only the report and screenshots, so temporary harness source was not retained as another repository file. These methods/results make the additional evidence explicit; future implementation should create a reviewed, sanitized reusable harness within its separately approved scope.

| Check | Method | Result | Limitation |
| --- | --- | --- | --- |
| Safe runtime capture | temporary PowerShell/C# host loaded the built UI assembly, instantiated `MainForm`, injected safe display fixtures and used `DrawToBitmap` at fixed sizes | 18 PNGs: nine states/screens × 1100×900 and 1460×900 | not packaged EXE; native ComboBox popup and packaged icon fidelity unavailable |
| Accessibility property inventory | temporary recursive WinForms control-tree reflection over one isolated form instance | 130 interactive; explicit `AccessibleName` 0; `AccessibleDescription` 0; `TabIndex==0` 53; mnemonic buttons 0; `TabStop==false` 0 | not a Narrator/UI Automation compatibility test |
| Contrast calculation | WCAG relative-luminance formula over source RGB tokens and used backgrounds/borders | success/pale 3.15:1; danger/pale 4.41:1; warning/pale 3.07:1; muted/canvas 4.43:1; trace border/white 2.56:1 | token calculation, not a complete rendered-state scan |
| Image integrity/metadata | `System.Drawing` dimensions plus PNG chunk parser | 18/18 exact 1100×900 or 1460×900; only `IHDR,sRGB,gAMA,pHYs,IDAT,IEND`; no text/EXIF/private chunks | OCR unavailable; manual visual inspection remains required |
| Markdown structure/local links | PowerShell regex/section/table/field checks and relative-file existence | 24 ordered headings, 5 required tables, 30 inventory rows, 22×15 finding fields, 10 screens×19 items; 34 references resolve to 18 PNGs | no repository-specific Markdown renderer/linter exists |
| Privacy text check | compare report with current machine name/local IPv4 and scan assignment-shaped secret patterns/sensitive filenames | no current machine name, local IPv4 or secret assignment; screenshot names generic | heuristic, plus manual visual inspection; not OCR |
| Git scope | HEAD/tag, tracked diff and untracked allowlist | baseline unchanged; 0 production/test/script diff; 1 report + 18 PNGs only | untracked because audit must not commit |

### Principal evidence index

| Evidence | What it establishes |
| --- | --- |
| `AGENTS.md`; `.codex/skills/netstuck-maintainer/SKILL.md` | repository workflow, security/layout/cadence invariants |
| `README-TH.md`; `docs/ARCHITECTURE.md`; `docs/DEVELOPMENT.md`; `docs/TESTING.md` | product intent, architecture, supported environment and test commands |
| `PRIVACY.md`; `SECURITY.md` | privacy and password-free process/log/state constraints |
| `src/NetStuck/NetStuck.cs` | shell, legacy/simple pages, shared helpers, palette, persistence paths, validation/dialogs |
| `src/NetStuck/NetStuck.V103.cs` | active v1.2.3 Ping/Trace/status layout and Trace state/cache behavior |
| `src/NetStuck/NetStuck.Release1.cs` | grid rendering/batching, activity status, dialogs and DNS workflow |
| `src/NetStuck/NetStuck.Features.cs` | Collector, persistence, column chooser, zoom and terminal batching |
| `scripts/Build-NetStuck.ps1`; `scripts/Test-NetStuck.ps1` | compiler/build shape and fresh verification entry point |
| `tests/*.cs` | functional, geometry, integration, security, performance, cadence and soak coverage |
| `docs/releases/v1.2.3/*` | prior release baseline; not substituted for fresh results |

### Inventory shared-component and uncertainty cross-reference

This supplements the exact inventory table in section 6 without changing its required columns.

| Surface from inventory | Shared component/helper used | Known limitation or uncertainty |
| --- | --- | --- |
| MainForm header/status | app palette, `NewPage`, top tabs, status items | packaged EXE icon and >100% DPI not verified |
| Live Ping configuration/metrics | field/button/card/metric helpers; active deterministic builder | active state screenshot not captured; tests/source used |
| Live Ping results/actions | shared grid, filter/table actions, batching | keyboard/UIA and populated visual state incomplete |
| Live Ping history | shared grid/split/export | only partially runtime inspected |
| Live Ping saved profiles | ComboBox/buttons + custom prompt | host-local profiles intentionally not exercised |
| Trace session navigation | nested native TabControl | Session 2 not separately screenshot; automated coverage exists |
| Trace Session 1 | deterministic Trace builder/custom selector/shared grid | popup overlay not captured; >100% DPI unverified |
| Trace Session 2 | same Trace session factory/state model | visual based on identical factory + tests, not separate screenshot |
| Trace event log | shared grid/filter/event model | populated/filtered visual not captured |
| DNS Resolver | shared fields/buttons/grid + Release1 workflow | real DNS failure/long label not captured |
| MAC Vendor | shared pane/grid + local classification/cache/external vendor workflow | safe locally administered fixture only; host cache and external registered-OUI miss were not exercised |
| WAN IP Intelligence | shared pane/grid + external lookup workflow | live external call intentionally not performed |
| IPv4 Subnet Calculator | card/field/action + core parser | high-DPI/long result not verified |
| Unit Converter | card/ComboBox/result + core converter | From/To label gap; high-DPI not verified |
| Collector transport/credentials | form rows/buttons/native controls | real credentials/devices prohibited; metadata/DPI gaps |
| Collector Basic commands | nested command tab/TextBox | alternate long content visual not captured |
| Collector Collect commands | same nested tab/TextBox | existence/source verified; tab content not separately shot |
| Collector devices/output/actions | shared fields/path/native picker/actions | native folder picker and operation-active visual not captured |
| Collector result grid | shared grid/DataTable | safe empty/mock only; large real-shape output needs profiling |
| Collector terminal | RichTextBox + queue/timer batching | oversized/bounded preview risk is source-based |
| Event Log | shared grid/filter/export/button | synthetic row only; long-running tail behavior not manually exercised |
| Updates | page/card/read-only TextBox | static content only; packaged version path not independently rendered |
| Save target list dialog | custom prompt form | source only; modal keyboard/DPI not verified |
| Hop descriptions dialog | custom resizable form | source only; long text/dirty/focus not verified |
| Realtime columns dialog | custom form/CheckedListBox | source + action existence; modal render not captured |
| CSV export dialog | native SaveFileDialog | source only; platform rendering left native |
| Collector output folder dialog | native FolderBrowserDialog | source only; platform rendering left native |
| Replace/Delete confirmation | native MessageBox | source only; default focus/wording not manually verified |
| Large-target confirmation | native MessageBox | source/branch only; no network run |
| Validation/error dialog family | native MessageBox call sites | representative modals not captured; migration scope needs inventory-by-callsite |

### Screenshot procedure and privacy

- Delivered image dimensions are exactly 1100×900 or 1460×900. They are width comparisons, not 1100×700 minimum-height evidence; only the 1100 width is automated, while 700-height geometry/runtime visual is Not verified.
- Every delivered screenshot uses sanitized identity and RFC 5737 addresses for active target fixtures. Built-in public-DNS/private-range examples remain visible as documentation/default text; they were not probed and are not host-local data. Collector shows an explicitly synthetic username only; no real credential, password or enable-secret value is rendered.
- Startup network identity calls were suppressed through isolated test state; no Ping, Trace, WAN, SSH/Telnet or real-device operation was triggered for capture.
- Images were visually inspected at original size. Host-local profiles/cache/results are not displayed.
- Capture API cannot include the native ComboBox popup; therefore the `protocol-open` pair is evidence of focused control layout only.
- The capture host's Form icon appears in both the title bar and in-app header logo; neither is used as evidence for packaged `NetStuck.exe` icon fidelity.

| Screen/state | 1100px | 1460px |
| --- | --- | --- |
| Live Ping | [image](ui-audit/screenshots/runtime-live-ping-1100.png) | [image](ui-audit/screenshots/runtime-live-ping-1460.png) |
| Traceroute | [image](ui-audit/screenshots/runtime-traceroute-1100.png) | [image](ui-audit/screenshots/runtime-traceroute-1460.png) |
| Traceroute protocol focused/open attempt | [image](ui-audit/screenshots/runtime-traceroute-protocol-open-1100.png) | [image](ui-audit/screenshots/runtime-traceroute-protocol-open-1460.png) |
| DNS Resolver | [image](ui-audit/screenshots/runtime-dns-resolver-1100.png) | [image](ui-audit/screenshots/runtime-dns-resolver-1460.png) |
| MAC / WAN Lookup | [image](ui-audit/screenshots/runtime-mac-wan-lookup-1100.png) | [image](ui-audit/screenshots/runtime-mac-wan-lookup-1460.png) |
| Calculators | [image](ui-audit/screenshots/runtime-calculators-1100.png) | [image](ui-audit/screenshots/runtime-calculators-1460.png) |
| Config Collector | [image](ui-audit/screenshots/runtime-config-collector-1100.png) | [image](ui-audit/screenshots/runtime-config-collector-1460.png) |
| Event Log | [image](ui-audit/screenshots/runtime-event-log-1100.png) | [image](ui-audit/screenshots/runtime-event-log-1460.png) |
| Updates | [image](ui-audit/screenshots/runtime-updates-1100.png) | [image](ui-audit/screenshots/runtime-updates-1460.png) |

### Completion validation

| Gate | Final result |
| --- | --- |
| Required structure | Passed: 24 headings in exact order; all 5 required table headers present |
| Inventory/per-screen coverage | Passed: 30 UI surface groups; 10 shell/menu/dialog families each contain items 1–19 |
| Finding integrity | Passed: 22 findings, every finding has all 15 required fields; P0 0 / P1 7 / P2 13 / P3 2 |
| Screenshot links/files | Passed: 34 Markdown references resolve to 18 unique PNGs; exact dimensions/chunks verified |
| Markdown whitespace | Passed: `git diff --no-index --check` reports no whitespace error; no repository-specific checker was found |
| Secret/privacy safety | Passed heuristic text/filename and PNG-chunk checks plus manual original-size review; OCR/screen-reader inspection Not available |
| Baseline/scope | Passed: HEAD `2f35f72988fac0a44292a6bd69196e0842cbfc73`, tag `v1.2.3`, no tracked diff and no `src`/`tests`/`scripts` change |
| Allowed files | Passed: only `docs/UI_AUDIT_REPORT.md` and 18 files under `docs/ui-audit/screenshots/` |
| Implementation boundary | **Stopped before implementation; production code, dependency, behavior, commit, tag and remote state remain unchanged** |
