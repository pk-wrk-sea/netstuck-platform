# NetStuck — Phase A UI Foundations Implementation Report

> **Post-Closure-Review remediation addendum (2026-08-26):** The numbered report below is retained as the Round 1 implementation record. It is not rewritten to imply that its first evidence set was correct. `docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md` found that the three Calculator success images were false-green (`Idle`), the clock made seven images non-reproducible, the reported branch was wrong, and the 179-check/package evidence was incomplete. Those defects were corrected in the current working tree; the detailed disposition is in `docs/UI_FOUNDATIONS_REMEDIATION_REPORT.md`.

> **Round 2 blocker-remediation addendum (2026-08-27):** `docs/UI_FOUNDATIONS_CLOSURE_REVIEW_R2.md` then found a Windows PowerShell 5.1 native-`stderr` failure, runner false-green paths, incomplete stale-promotion proof, an unused progress contract, a PNG-validator gap and incomplete package provenance. The current working tree resolves those tooling/code findings; detailed evidence is in `docs/UI_FOUNDATIONS_R2_REMEDIATION_REPORT.md`. Both closure-review reports remain immutable historical evidence.

> **Final-blocker remediation addendum (2026-08-27):** `docs/UI_FOUNDATIONS_FINAL_CLOSURE_REVIEW.md` retained one intermittent Traceroute event-grid crash and found a PNG ordering false-green, one dead legacy visual delta, host-dependent binary-diff hashing, incomplete compiler provenance and stale maintainer documentation. The current working tree applies narrow corrections and adds focused evidence; the complete diagnosis, finding disposition and final Git state are in `docs/UI_FOUNDATIONS_FINAL_BLOCKER_REMEDIATION_REPORT.md`. All three closure-review reports remain immutable historical evidence, and this addendum does not constitute independent approval to commit.

> **P2 closure-remediation addendum (2026-08-28):** `docs/UI_FOUNDATIONS_FINAL_CLOSURE_REREVIEW.md` retained `NOT_READY` with seven P2 findings (`UIF-RR-001`–`UIF-RR-007`) and one P3 finding (`UIF-RR-008`). The current working tree applies the narrowly scoped remediation and records its reproduced evidence in `docs/UI_FOUNDATIONS_P2_CLOSURE_REMEDIATION_REPORT.md`. The independent final re-review remains immutable and only another independent closure review may decide commit readiness.

Current repository truth after P2 closure remediation:

- Branch: `ui/phase-a-foundations`; HEAD/baseline: `2f35f72988fac0a44292a6bd69196e0842cbfc73` / `v1.2.3`; application/file version remains `1.2.3.0`.
- The authoritative unchanged-behavior corpus remains `130/130`. The complete runner now contains `292` checks in ten mandatory suites and two mandatory stages. Windows PowerShell 5.1.26100.3624 passed five consecutive full runs and pwsh 7.6.4 passed three; every counted run was `292/292`, with zero failed, skipped or infrastructure checks, exact `10/10` inventory, every per-suite floor satisfied and exit `0`.
- Compiler invocation now comes from one ordered 26-entry argument specification. Actual and path-normalized argv preserve one-to-one atomic boundaries, including the icon and reference paths with spaces. The normalized invocation identity uses binary `NetStuck.csc.argv.v2` count/index/UTF-8-length/UTF-8-byte serialization; human-readable quoting is diagnostic only. Current source/toolchain/build-invocation/reference fingerprints are `0379f44bcf92a966b393631cf0296bf266ba09ce921774ea5606c94c33e27739`, `449f6846e8ad6a4734945765ff9e0a8503f4e2b952faf6362f34d011ce66680b`, `134e486a3bb5df9ccce341ee52a68cfa046c036af4b6e7e24d8083b419791e94` and `199091183009849210a8da3c47db8b312d4e5e07f21c72269460e1efb22c4223`. The corrected 19-check provenance suite passes on both PowerShell hosts with the same canonical fingerprints.
- Capture publication now keeps a run-owned rollback set through post-publish validation. Pre-publish, post-publish, interrupted-promotion and forced-rollback-failure paths exercise the actual publish function; ordinary failures restore exact prior hashes and remove owned staging/backup state, while rollback failure remains an explicit combined failure and retains recoverable original evidence. The capture infrastructure matrix is `39/39`.
- Capture scenarios independently normalize selected page, focus, scroll offsets, grid selection/current row and client/layout state, then wait for a bounded stable observable viewport before and after a warm render. Five fresh isolated full runs were identical for all `9/9` semantic, dimensional, PNG-structural and privacy-valid scenarios. The authoritative nine hashes in the table below were unchanged, so canonical screenshots were retained.
- Each Traceroute run now owns every cycle and hop/service probe task until terminal observation. Stop cancels scheduling and drains asynchronously; a derived timeout produces explicit incomplete quiescence and leaves START disabled while observation continues. The actual-MainForm lifecycle harness is `31/31`, covers zero/one/multiple delayed probes, fault, timeout, obsolete/restart, page disposal and active close, and leaves zero pending tasks/callbacks/temp roots. Its isolated targeted mode passed `50/50` cycles on both hosts with no retry.
- The four ignored `artifacts/r2-verification/**/state.json` files were confirmed as packaged-smoke test outputs, contained no credential value, and were removed without touching user-owned runtime state. New packaged smoke uses a unique OS-temporary `NETSTUCK_TEST_ROOT`, rejects operator-profile/credential content, treats cleanup failure as failure and passed under both PowerShell hosts with responsive native window, graceful exit `0`, no process leak and no owned-root residue.
- Fresh package input identity is `448555f8f669cc020987ae09c23662490c1f9634a4a6ddbc0c90dbb5a9d3ac1e`; exact nine-file decompressed content identity is `ec6c0d372c8eabe20c7ebd840cae16c13205edfbb3c64598c11aa9e74a991cf2`; the separately recorded ZIP SHA-256 is `ade3a775d95f28f00916950c9ebf275ccafddf8fe02e2a2faae98ccffa7e5744`.
- Physical pointer, native dropdown, modal dialog, 125%/150%/200% DPI, High Contrast and screen-reader checks remain **NOT VERIFIED**. Automated metadata, `DrawToBitmap`, synthetic event routing and packaged startup smoke are not substitutes.

The following final-blocker snapshot is retained as historical truth and is superseded for current status by the P2 closure-remediation addendum above.

Historical repository truth after final-blocker remediation:

- Branch: `ui/phase-a-foundations`; HEAD/baseline: `2f35f72988fac0a44292a6bd69196e0842cbfc73` / `v1.2.3`; application/file version remains `1.2.3.0`.
- The authoritative unchanged-behavior corpus remains `130/130`. The complete runner now contains `247` checks in ten mandatory suites and two mandatory stages. Windows PowerShell 5.1.26100.3624 passed five consecutive full runs and pwsh 7.6.4 passed three; every run was `247/247`, with zero failed, skipped or infrastructure checks, exact `10/10` inventory and exit `0`.
- The recovered Traceroute crash was an unhandled cross-thread `DataTable.Rows.Add` notification into a bound `DataGridView`. A focused test using the real MainForm/Traceroute page showed baseline and pre-fix mutations on worker threads; the correction establishes the WinForms synchronization context before the async boundary, enforces pause/stop/close quiescence and rejects callbacks from obsolete runs. The focused test now observes event mutations only on UI thread `1` and passed 25 serial cycles on each PowerShell host.
- `BuildPingPageLegacyV102` is again block-equivalent to `v1.2.3`. Consumer-aware UI-foundation tests confirm that no deferred token, state, progress/action field, non-pilot visual adoption or test-only production API remains.
- Capture validation now enforces raw CRC/bounds, exactly one leading `IHDR`, terminal `IEND`, contiguous `IDAT`, the supported critical/ancillary allowlist, singleton metadata, and `sRGB`/`gAMA`/`pHYs` before the first `IDAT`. Its focused infrastructure matrix is `27/27` on both hosts.
- Two fresh complete screenshot runs passed all nine semantic/privacy scenarios and were byte-identical to each other and to the canonical nine-file set; canonical screenshots were not replaced.
- Build provenance uses an explicit six-source allowlist, `/noconfig`, `/nostdlib+`, nine explicit hashed framework references, normalized compiler arguments, raw-byte SHA-256 inventories and separate source/toolchain/invocation/reference/package identities. The current source, toolchain and invocation fingerprints are `9ca605aeec681dfe07af807141d6bfc2bcf6ae2f01a8f8d08b1ac8e74c8aa8cb`, `449f6846e8ad6a4734945765ff9e0a8503f4e2b952faf6362f34d011ce66680b` and `cde7a5f52ac9f88d2be8482d829ac76a30e31f8d8ca2c319d99616b2ef2551ab`.
- The verified portable package has exact nine-file content, package-input fingerprint `40a8934c6e78702a624c1d30823d617a5bad1b63e72028a5b0277ef8b86b028b`, decompressed-content fingerprint `bf54a09ba23adf5fdaee3d9f16eab05ac6e790a2ef1d618016aa51309b8bbeaf` and ZIP SHA-256 `7cf4c54bd0f8a3cd799a323f694456d89f79a840372e906807be9fd099906cff`. The packaged executable opened a responsive native window, closed gracefully with exit `0` and left no process residue.
- Manual screen-reader, High Contrast, 125%/150%/200% DPI, physical pointer, native dropdown and modal-dialog checks remain **NOT VERIFIED** and must not be inferred from automation.

The following Round 2 snapshot is retained as historical truth and is superseded for current status by the final-blocker addendum above.

Historical repository truth after Round 2 remediation:

- Branch: `ui/phase-a-foundations`; HEAD/baseline: `2f35f72988fac0a44292a6bd69196e0842cbfc73` / `v1.2.3`.
- Pre-Round-2-remediation tracked-diff fingerprint: `5c335b1d132b2b953f00f242ddad6ff6aef4ab5d`; the post-remediation fingerprint is recorded in `docs/UI_FOUNDATIONS_R2_REMEDIATION_REPORT.md` after final Git-integrity checks.
- Current cross-host automated result: `217/217` passed, `0` failed, `0` skipped and `0` infrastructure failures on both Windows PowerShell 5.1.26100.3624 and pwsh 7.6.4; mandatory inventory is `8/8` and the reconciled exit is `0`.
- The current authoritative unchanged-behavior corpus is `130/130`. The historical report's `128/128` was accurate for its recorded output; two unchanged Feature assertions were omitted from that older output arithmetic and history is not rewritten.
- Visual adoption is accurately limited to the application shell, Calculators and Event Log. Non-pilot shared button/grid/palette styling and the Traceroute visual edits were restored to baseline; no app-wide visual migration is claimed.
- Capture now selects the target tab before acting, validates exact semantic pre/postconditions, rejects `Idle` for Calculator success, stages a complete per-run set, and promotes only after all nine scenarios pass exact inventory, dimensions, PNG decode/signature/chunk order/CRC/EOF allowlist and privacy validation. The combined stale-set negative proves a failed current run cannot reuse or replace prior authoritative PNGs.
- Capture-only time is fixed at `2000-01-02 03:04:05 ICT`; two independent complete runs and the promoted evidence set matched byte-for-byte for all nine files.
- Native processes in the Round 2 runner/capture paths now return separate stdout, stderr, exit code and invocation-failure fields. The final CLI verdict, JSON verdict and process exit derive from the same reconciliation result, so a partial stage summary cannot become a full-run PASS.
- The unused `Running`/determinate-progress state metadata, presenter control and public setter were deleted rather than wired to a fake consumer. Progress is deferred; the remaining state contract is consumed by the shell, Calculators or Event Log.
- Package verification now requires schema-2 reconciled test evidence and fingerprints every compiler input (including untracked production source), build recipe and compiler identity before/after verification. The historical `docs/releases/v1.2.3/TEST-REPORT.txt` remains unchanged.
- Manual screen-reader, High Contrast, 125%/150%/200% DPI, physical pointer, native dropdown and modal-dialog checks remain **NOT VERIFIED**.

Current authoritative screenshot SHA-256 values:

| File | SHA-256 |
| ---- | ------- |
| `main-shell-1100x900.png` | `8f4380229712b3c36217730d56967e9b0167440b9bb60adc63fbbe134bf00fe1` |
| `main-shell-1460x900.png` | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` |
| `pilot-calculators-1100x700.png` | `d4fcb3963863b9184c1940040e6d080c3fa04389fd6deceffe1cc0c43a7f0734` |
| `pilot-calculators-1100x900.png` | `df064670144084a4d67aaf3ff4f7884bd17f0ecb450f066323b6cb37b987f553` |
| `pilot-calculators-1460x900.png` | `68067d3f8c0b70f3b8e8429acadcde8eee13c015afff7201387214c18c08fcc3` |
| `pilot-calculators-validation-1100x900.png` | `4ddbafc4b5afa2d848c81065dddb01ae182cc1c0f070e0f14d4591b4290ef074` |
| `pilot-event-log-empty-1100x700.png` | `0e46a07c4018833add76017adcfd237ba00c7f13b83b7909310209b5f279c26c` |
| `pilot-event-log-empty-1100x900.png` | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` |
| `pilot-event-log-filtered-empty-1460x900.png` | `a571c39d0cf17700b5dbb9d075c0bf6a9741667a1569a7d2af84719d65e9f2b3` |

The remainder of this file is the historical Round 1 record and must be read with the addendum above.

## 1. Executive Summary

Phase A สร้าง shared WinForms UI foundation และนำไปใช้จริงกับ application shell, `Calculators` และ `Event Log` โดยไม่เปลี่ยน network/process/credential flow เดิม ตัว foundation อยู่ที่ `src/NetStuck/NetStuck.UiFoundation.cs:8-383`; pilot wiring อยู่ที่ `src/NetStuck/NetStuck.cs:264-355`, `src/NetStuck/NetStuck.cs:637-735` และ `src/NetStuck/NetStuck.cs:1394-1482`

ผล automated regression สุดท้ายคือ `179/179` ผ่าน: ชุดเดิม `128/128` ไม่ regression และเพิ่ม `UiFoundationTests` 51 checks คำสั่ง `./scripts/Test-NetStuck.ps1 -SoakSeconds 10` จบด้วย exit code `0` และข้อความ `All NetStuck test suites passed.`

สถานะส่งมอบโดยรวมเป็น **PARTIAL** เฉพาะด้าน runtime verification: environment นี้ตรวจได้ที่ 96 DPI, `HighContrast=False` และไม่มี screen reader session จึงไม่อ้างว่า 125%/150%/200%, High Contrast หรือ screen reader ผ่าน ส่วน implementation, build, test, sanitized capture และ pilot layout เสร็จครบตาม scope ที่อนุญาต

## 2. Scope and Explicit Non-Scope

### Implemented

- Centralized spacing, typography, sizing, width, focus, semantic color และ state resources (`src/NetStuck/NetStuck.UiFoundation.cs:59-150`)
- Shared action roles, accessibility helpers, read-only result-grid convention และ state presenter (`src/NetStuck/NetStuck.UiFoundation.cs:153-383`)
- Responsive application header/root navigation (`src/NetStuck/NetStuck.cs:264-355`)
- `Calculators` pilot: persistent labels, logical tab order, primary actions, read-only results และ inline validation/success state (`src/NetStuck/NetStuck.cs:637-700`, `src/NetStuck/NetStuck.cs:1394-1440`)
- `Event Log` pilot: persistent Search/Level labels, filter/action bar, secondary/destructive separation, explicit read-only grid และ empty/filtered-empty state (`src/NetStuck/NetStuck.cs:702-735`, `src/NetStuck/NetStuck.cs:1466-1482`)
- Isolated test root, UI component/layout tests และ sanitized screenshot harness (`src/NetStuck/NetStuck.cs:219-258`, `tests/UiFoundationTests.cs:20-75`, `tests/UiFoundationSnapshot.cs:15-116`)

### Explicit Non-Scope

- ไม่ redesign UI ทั้ง 30 groups
- ไม่ refactor Config Collector, Live Ping หรือ Traceroute business state machine
- ไม่แก้ SSH/PuTTY/Plink arguments, authentication, retry, timeout, file format หรือ credential storage
- ไม่เปลี่ยน network logic, schema, public API, telemetry, branding หรือ framework
- ไม่เพิ่ม dependency, package หรือ theme engine
- ไม่สร้าง release artifact, commit, tag หรือ push

ข้อเปลี่ยนพฤติกรรมที่ตั้งใจและจำกัดอยู่ใน pilot มีสองรายการ: `Event Log` result grid ถูกประกาศเป็น read-only ตาม semantics และ non-finite unit input (`NaN`/infinity) แสดง inline validation แทนผลลัพธ์ที่ใช้ไม่ได้ (`src/NetStuck/NetStuck.cs:730`, `src/NetStuck/NetStuck.cs:1424-1440`) ไม่มี data-flow หรือ external-operation behavior เปลี่ยน

## 3. Repository State and Baseline

- Repository: `C:\Codex\_Project\NetStuck`
- Branch statement recorded in Round 1: `main` (**incorrect at review time**). Verified actual branch before and after remediation: `ui/phase-a-foundations`.
- HEAD ก่อนและหลังงาน: `2f35f72988fac0a44292a6bd69196e0842cbfc73`
- Baseline tag relationship: `git describe --tags --always` ให้ `v1.2.3`; build file version ยังคง `1.2.3.0`
- ไม่มี tracked diff ก่อนเริ่ม
- Pre-existing untracked ที่แยกจาก Phase A: `docs/UI_AUDIT_REPORT.md`, `docs/ui-audit/`
- Pre-existing ignored ที่แยกจาก Phase A: `artifacts/`, `fake-plink-auth-count.txt`

Baseline command `./scripts/Test-NetStuck.ps1 -SoakSeconds 10` ผ่าน `128/128`, exit code `0`: Core 16, Feature 91, Performance 10, PollingCadence 3 และ OvernightSoak 8 ไม่มี skipped test ที่ suite รายงาน Baseline measurements ที่บันทึกก่อนแก้คือ warm startup 1108 ms, `/24` worst dispatch 21 ms, dual-trace worst dispatch 35 ms, working set 79 MB และ soak 11.4735 s / worst dispatch 20 ms / growth 9 MB

## 4. UI Framework and Existing Architecture

- Framework จริงคือ Windows Forms บน .NET Framework 4.x; entry point เรียก `Application.Run(new MainForm())` ที่ `src/NetStuck/NetStuck.cs:2050-2055`
- Build ใช้ `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` ผ่าน `scripts/Build-NetStuck.ps1`; ไม่มี SDK/framework migration
- `MainForm` เป็น monolithic partial class แยกใน `NetStuck.cs`, `NetStuck.Features.cs`, `NetStuck.Release1.cs` และ `NetStuck.V103.cs` (`src/NetStuck/NetStuck.cs:80`, `src/NetStuck/NetStuck.Features.cs:365`, `src/NetStuck/NetStuck.Release1.cs:40`, `src/NetStuck/NetStuck.V103.cs:115`)
- Existing conventions ใช้ `TableLayoutPanel`, `FlowLayoutPanel`, `SplitContainer`, Dock/Anchor และ helper เช่น `Card`, `ActionButton`, `LabeledField`, `DataGrid`; Phase A ทำให้ helper เหล่านี้ consume shared tokens (`src/NetStuck/NetStuck.cs:1806-1920`)
- Existing minimum window คือ 1100×700 และ `AutoScaleMode.Dpi` (`src/NetStuck/NetStuck.cs:203`, `src/NetStuck/NetStuck.cs:218`)
- ไม่มี localization resource mechanism; visible strings ยังคง embedded English ตาม architecture เดิม Phase A จึงทดสอบ resilience ของ long English/Thai text โดยไม่สร้าง localization system ใหม่ (`tests/UiFoundationTests.cs:167-176`)

## 5. Audit Finding Traceability

สถานะในตารางหมายถึงผลต่อ finding ทั้ง application ไม่ใช่เพียงว่ามี API แล้ว

| Finding ID | Priority | Evidence | Phase A treatment | Status | Verification |
| ---------- | -------- | -------- | ----------------- | ------ | ------------ |
| A11Y-01 | P1 | `docs/UI_AUDIT_REPORT.md:261` | เพิ่ม naming/description/role helpers และ adopt บน shell/สอง pilots | Partially implemented | `tests/UiFoundationTests.cs:196-302`; app-wide rollout deferred |
| A11Y-02 | P1 | `docs/UI_AUDIT_REPORT.md:281` | semantic palette, non-color markers และ `SystemColors` High Contrast branch | Partially implemented | contrast/resource tests PASS; High Contrast runtime NOT VERIFIED |
| ASYNC-01 | P1 | `docs/UI_AUDIT_REPORT.md:301` | ไม่เปลี่ยน UI-thread operation flow | Out of scope | Deferred Phase B1 |
| PERF-01 | P1 | `docs/UI_AUDIT_REPORT.md:321` | ไม่เปลี่ยน Collector rendering/operation engine | Out of scope | Existing performance assertions PASS |
| STATE-01 | P1 | `docs/UI_AUDIT_REPORT.md:341` | มี shared state vocabulary แต่ยังไม่ adopt ใน Collector | Foundation available but not adopted | catalog test PASS; Phase B1 adoption |
| STATE-02 | P1 | `docs/UI_AUDIT_REPORT.md:361` | ไม่เพิ่ม immutable run snapshot | Out of scope | Deferred Phase B1 |
| STATE-03 | P1 | `docs/UI_AUDIT_REPORT.md:381` | มี state vocabulary; ไม่แก้ Traceroute reducer/cycle | Foundation available but not adopted | existing Traceroute tests PASS |
| A11Y-03 | P2 | `docs/UI_AUDIT_REPORT.md:401` | logical tab order, keyboard focus smoke และ visible action focus บน pilots | Partially implemented | pilot tests/screenshots PASS; app-wide deferred |
| A11Y-04 | P2 | `docs/UI_AUDIT_REPORT.md:421` | เพิ่ม read-only result-grid primitive; adopt ที่ Event Log | Partially implemented | `tests/UiFoundationTests.cs:281-285` PASS |
| DPI-01 | P2 | `docs/UI_AUDIT_REPORT.md:441` | ใช้ layout containers/tokens และทดสอบ target sizes | Partially implemented | 1100×700/900 และ 1460×900 PASS; non-96 DPI NOT VERIFIED |
| VAL-01 | P2 | `docs/UI_AUDIT_REPORT.md:461` | inline validation ใกล้ Calculator input พร้อม focus return | Partially implemented | safe invalid-input tests + screenshot PASS |
| STATE-04 | P2 | `docs/UI_AUDIT_REPORT.md:481` | state/action availability อยู่ใน shared definitions; ไม่มี global registry | Foundation available but not adopted | `tests/UiFoundationTests.cs:126-181` PASS |
| ASYNC-02 | P2 | `docs/UI_AUDIT_REPORT.md:501` | ไม่ refactor cancellation/lifecycle | Deferred | Phase B/C |
| PERF-02 | P2 | `docs/UI_AUDIT_REPORT.md:521` | ไม่ refactor cache/font lifecycle | Deferred | Phase D |
| CTRL-01 | P2 | `docs/UI_AUDIT_REPORT.md:541` | Primary/Secondary/OperationCancel/Destructive roles และ pilot adoption | Implemented for Phase A | role tests PASS; `src/NetStuck/NetStuck.UiFoundation.cs:8-14`, `src/NetStuck/NetStuck.cs:1846-1879` |
| ARCH-01 | P2 | `docs/UI_AUDIT_REPORT.md:561` | แยก smallest foundation seam; ไม่ rewrite partial MainForm | Partially implemented | build/test PASS; monolith remains |
| DLG-01 | P2 | `docs/UI_AUDIT_REPORT.md:581` | token/migration target เท่านั้น; ไม่ migrate custom dialogs | Foundation available but not adopted | Deferred Phase C |
| PERSIST-01 | P2 | `docs/UI_AUDIT_REPORT.md:601` | ไม่เปลี่ยน persistence schema/view settings | Deferred | existing state-schema test PASS |
| STATE-05 | P2 | `docs/UI_AUDIT_REPORT.md:621` | state presenter adopt ใน Calculators/Event Log | Partially implemented | idle/success/validation/empty/filtered-empty PASS |
| TEST-01 | P2 | `docs/UI_AUDIT_REPORT.md:641` | เพิ่ม isolated component/layout/a11y/privacy/snapshot harness | Implemented for Phase A | 51/51 new checks + 9/9 captures PASS |
| SHELL-01 | P3 | `docs/UI_AUDIT_REPORT.md:661` | responsive shell header, navigation/status accessibility | Partially implemented | shell smoke/screenshots PASS; broader identity policy deferred |
| UPDATE-01 | P3 | `docs/UI_AUDIT_REPORT.md:681` | ไม่ redesign Updates | Deferred | existing Updates tests PASS |

## 6. Design Decisions

1. ใช้ project-local static resources/helper แทน theme engine เพราะ WinForms ไม่มี resource-style system แบบ WPF และ repo build ตรงด้วย `csc.exe` (`src/NetStuck/NetStuck.UiFoundation.cs:59-295`)
2. ใช้ `SystemInformation.HighContrast` + `SystemColors` เป็น framework-native branch; normal palette ใช้ค่าที่ผ่าน automated contrast assertions แต่ไม่อ้าง High Contrast runtime pass (`src/NetStuck/NetStuck.UiFoundation.cs:91-118`, `tests/UiFoundationTests.cs:83-123`)
3. ใช้ rectangular native controls (`CornerRadius=0`) ไม่เพิ่ม custom rounded-control rendering ซึ่งลด DPI/focus risk (`src/NetStuck/NetStuck.UiFoundation.cs:82`)
4. แยก `OperationCancel` จาก `Destructive`: Stop/Cancel ไม่สื่อว่า delete data ส่วน profile Delete/Event Log Clear ใช้ Destructive (`src/NetStuck/NetStuck.cs:1867-1879`, `src/NetStuck/NetStuck.V103.cs:258`)
5. เก็บ state presenter เป็น view-only component; ไม่ผูกกับ Collector/Ping/Traceroute operation state machine (`src/NetStuck/NetStuck.UiFoundation.cs:297-383`)
6. ใช้ `NETSTUCK_TEST_ROOT` เฉพาะเมื่อ environment variable ถูกกำหนดเพื่อ isolate state/profile/cache และ suppress startup network refresh; normal production paths ไม่เปลี่ยน (`src/NetStuck/NetStuck.cs:219-258`)
7. ใช้ sanitized component capture ไม่ใช้ production host/device เพื่อรักษา privacy (`tests/UiFoundationSnapshot.cs:20-96`)

## 7. Design Tokens and Shared Resources

| Token/Resource | Value or native source | Purpose | Consumers | Rationale |
| -------------- | ---------------------- | ------- | --------- | --------- |
| `SpaceXs/Sm/Md/Lg/Xl` | 4/8/12/16/24 px | spacing scale | shell, panels, state/action helpers, Trace frame | scale สั้นและใช้งานจริง |
| `PageMargin` | 12 px | page padding | `NewPage` | รักษา existing density |
| `DialogMargin` | 16 px | dialog migration target | DLG-01 Phase C | กำหนดค่าโดยไม่ migrate dialog ใน Phase A |
| `SectionGap` | 16 px | section migration target | Phase B page adoption | สอดคล้อง spacing scale |
| `AppHeaderHeight/SectionHeaderHeight` | 72/55 px | shell/section hierarchy | `BuildShell`, `SectionHeader` | ป้องกัน arbitrary repetition |
| `DenseControlHeight/DialogControlHeight` | 34/36 px | standard control heights | action buttons; dialog target | รักษา compact desktop density |
| `GridHeaderHeight/GridRowHeight` | 36/32 px | grid rhythm | all grids through `DataGrid` | centralizes repeated values |
| `DialogFooterHeight` | 52 px | dialog-footer migration target | DLG-01 Phase C | no dialog behavior change now |
| `SplitterWidth` | 8 px | resize affordance | Calculators split | easier pointer target without broad redesign |
| `IconSmall/Medium/Large` | 16/20/40 px | icon categories | shell logo; resource checks; future icon migration | small bounded set |
| `BorderWidth/FocusWidth/CornerRadius` | 1/2/0 px | border/focus/native shape | buttons, focus cue, Trace input frame | native WinForms geometry |
| `Numeric/Standard/WideField*` | 96/160/240/320 px | input width categories | Calculator and migration tests | replaces per-screen guessed widths |
| Typography roles | Segoe UI 8.5–17; Consolas for data | body/caption/action/title/result/data | shell, actions, state, calculators | system-available Windows fonts |
| `Canvas/Surface/Text/MutedText/Border/Focus` | `SystemColors` in HC; centralized normal palette | base semantics | shared helpers and modified surfaces | no scattered palette in modified paths |
| `Info/Success/Warning/Error/Destructive` | semantic palette + HC branch | status/action meaning | state presenter, buttons, status | status also has text marker |
| interaction surfaces | hover/pressed/selection/disabled resources | interaction feedback | buttons, grids, inputs | explicit states rather than color literals per surface |
| `UiActionRole` | 4 roles | action semantics | `ActionButton`, `DangerButton`, `DestructiveButton` | cancel and delete differ semantically |
| `UiSemanticState` | 11 states | view-state vocabulary | `UiStatePresenter`, pilots, tests | includes `FilteredEmpty` in addition to required states |

Token definitions และ consumers อยู่ที่ `src/NetStuck/NetStuck.UiFoundation.cs:59-150`, `src/NetStuck/NetStuck.cs:1806-2010` และ `src/NetStuck/NetStuck.V103.cs:405-597`

## 8. Layout Patterns

- **Application shell:** `Panel` + responsive `TableLayoutPanel`; identity column expands while version remains right-aligned (`src/NetStuck/NetStuck.cs:264-323`)
- **Page/section container:** `NewPage`, `Card`, `SectionHeader` consume central margins/colors/heights (`src/NetStuck/NetStuck.cs:1806-1844`)
- **Label/control pairing:** `UiAccessibility.AssociateLabel` gives persistent mnemonic label and accessibility metadata (`src/NetStuck/NetStuck.UiFoundation.cs:172-193`)
- **Action pattern:** shared factory applies role, native focus rectangle, hover/down/disabled semantics (`src/NetStuck/NetStuck.UiFoundation.cs:200-283`)
- **Data-view toolbar:** Event Log uses a two-row `TableLayoutPanel` with visible labels and fixed action columns (`src/NetStuck/NetStuck.cs:708-724`)
- **Data-view container:** Event Log grid fills remaining space and the state presenter docks above it (`src/NetStuck/NetStuck.cs:725-735`)
- **Split pilot:** Calculators uses `SplitContainer` with equal-split recalculation and 360 px minimum panes (`src/NetStuck/NetStuck.cs:639-700`)
- **Status/validation/empty:** one presenter component covers text marker, title, detail, optional progress และ accessible announcement (`src/NetStuck/NetStuck.UiFoundation.cs:297-383`)

## 9. Control Semantics

- `Primary`: Calculate/Convert และ existing primary actions; filled accent treatment
- `Secondary`: Export/Load/Pause-like non-destructive action; neutral treatment
- `OperationCancel`: Stop/Cancel; warning treatment and description that saved data is not deleted
- `Destructive`: profile Delete และ Event Log Clear; red text/border, not competing with primary action
- `ComboBoxStyle.DropDownList` remains the correct single-selection control for unit/severity choices (`src/NetStuck/NetStuck.cs:671-672`, `src/NetStuck/NetStuck.cs:712`)
- Event Log output is explicitly `ReadOnly`, but column resize/order, sorting, selection and copy remain available (`src/NetStuck/NetStuck.UiFoundation.cs:286-295`, `tests/UiFoundationTests.cs:281-285`)
- ไม่มี control type เปลี่ยนเพื่อ cosmetic reason; checkbox/radio/toggle behaviors เดิมไม่ถูกแตะ

## 10. Accessibility Foundation

- `UiAccessibility.Configure` กำหนด stable `Name`, `AccessibleName`, `AccessibleDescription` และ `TabIndex`; overload รองรับ `ToolStripItem` (`src/NetStuck/NetStuck.UiFoundation.cs:153-170`)
- `AssociateLabel` สร้าง persistent label พร้อม mnemonic และ metadata; Calculators ใช้ Address/Value/From/To, Event Log ใช้ Search/Level (`src/NetStuck/NetStuck.cs:650-681`, `src/NetStuck/NetStuck.cs:714-717`)
- Shell navigation, page tabs และ status items มี accessible names/descriptions (`src/NetStuck/NetStuck.cs:326-350`)
- Pilot tests ตรวจ unnamed/duplicate accessible names, visual/task tab order และ keyboard focus (`tests/UiFoundationTests.cs:196-302`)
- Action focus cue ใช้ native `ControlPaint.DrawFocusRectangle`; screenshot `pilot-calculators-1460x900.png` แสดง focus บน Calculate (`src/NetStuck/NetStuck.UiFoundation.cs:220-229`)
- State ไม่สื่อด้วยสีอย่างเดียว: ทุก state มี marker เช่น `[i]`, `[OK]`, `[!]`, `[X]`, `[-]` พร้อม text/role (`src/NetStuck/NetStuck.UiFoundation.cs:121-150`)
- Screen-reader discoverability ถูกเตรียมด้วย role/name/description และ `AccessibilityNotifyClients`; การใช้ screen reader จริงยัง NOT VERIFIED (`src/NetStuck/NetStuck.UiFoundation.cs:347-364`)

## 11. State Presentation Foundation

`UiSemanticState` มี `Idle`, `Loading`, `Running`, `Cancelling`, `Success`, `Warning`, `Error`, `Empty`, `FilteredEmpty`, `Unavailable` และ `ValidationFailure` (`src/NetStuck/NetStuck.UiFoundation.cs:16-29`)

แต่ละ definition ระบุ marker, default title, foreground/background, progress style, primary/cancel availability และ accessible role (`src/NetStuck/NetStuck.UiFoundation.cs:31-57`, `src/NetStuck/NetStuck.UiFoundation.cs:121-150`) `UiStatePresenter` แสดง title/detail/progress, clamp determinate values และ reapply system colors (`src/NetStuck/NetStuck.UiFoundation.cs:297-383`)

Adoption ที่ verified:

- Calculators: Idle → Success หรือ ValidationFailure; error คืน focus/select ไป field (`src/NetStuck/NetStuck.cs:1394-1440`)
- Event Log: Empty → FilteredEmpty → hidden เมื่อมี matching data (`src/NetStuck/NetStuck.cs:1466-1482`)
- Loading/Running/Cancelling/Warning/Error/Unavailable ถูก component-test แต่ยังไม่เชื่อมกับ production operation engine ตาม explicit non-scope (`tests/UiFoundationTests.cs:126-181`)

## 12. Pilot Surfaces

### Application shell

Responsive header ใช้ table layout, version right alignment, accessible logo/navigation/status และรักษา tab/status order เดิม ภาพหลังแก้ที่ 1100×900 และ 1460×900 ไม่มี overlap/clipping ที่ตรวจด้วยตา

### Calculators

เป็น low-risk local computation surface: ไม่มี external device, credential หรือ network dependency ใช้ responsive 50/50 split, persistent labels, two primary contexts, selectable read-only results, inline status/validation และ logical tab order ภาพ 1100×700 แสดง quick-reference scrolling เฉพาะพื้นที่ data โดย inputs/actions/status ยังมองเห็น

### Event Log

เป็น local in-memory data-view surface ใช้ persistent Search/Level labels, secondary Export, destructive Clear, read-only grid และ explicit Empty/FilteredEmpty states ภาพ 1100×700/900 และ 1460×900 ไม่พบ overlap/clipping

## 13. Files Changed

| File | Change type | Purpose | Behavior impact | Test coverage |
| ---- | ----------- | ------- | --------------- | ------------- |
| `src/NetStuck/NetStuck.UiFoundation.cs` | New production | tokens, action/a11y/state primitives | presentation foundation only | token/state/action tests |
| `src/NetStuck/NetStuck.cs` | Modified production | shell, pilots, shared helpers, isolated test root | Event Log read-only; pilot validation/state feedback | existing 128 + new pilot/layout tests |
| `src/NetStuck/NetStuck.V103.cs` | Modified production | destructive profile role; tokenized Trace styling without geometry change | visual semantics only | existing Traceroute/profile tests |
| `scripts/Build-NetStuck.ps1` | Modified build | compile new foundation source | build input only | build in every full run |
| `scripts/Test-NetStuck.ps1` | Modified test runner | compile/run `UiFoundationTests` | test infrastructure only | final suite output |
| `tests/UiFoundationTests.cs` | New test | 51 token/state/a11y/input/layout/privacy/resource checks | none | 51/51 PASS |
| `tests/UiFoundationSnapshot.cs` | New test utility | sanitized deterministic UI capture | none | capture 9/9 PASS |
| `scripts/Capture-UiFoundations.ps1` | New script | compile capture host and verify dimensions | writes Phase A evidence only | exit 0 |
| `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md` | New documentation | required evidence/report | none | Markdown/link checks |
| `docs/ui-foundations/screenshots/main-shell-1100x900.png` | New evidence | shell after, 1100×900 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/main-shell-1460x900.png` | New evidence | shell after, 1460×900 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-calculators-1100x700.png` | New evidence | Calculators success after, 1100×700 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-calculators-1100x900.png` | New evidence | Calculators success/focus after, 1100×900 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-calculators-1460x900.png` | New evidence | Calculators success/focus after, 1460×900 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-calculators-validation-1100x900.png` | New evidence | safe validation error after, 1100×900 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-event-log-empty-1100x700.png` | New evidence | Event Log empty after, 1100×700 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-event-log-empty-1100x900.png` | New evidence | Event Log empty after, 1100×900 | none | visual + dimension check |
| `docs/ui-foundations/screenshots/pilot-event-log-filtered-empty-1460x900.png` | New evidence | Event Log filtered-empty after, 1460×900 | none | visual + dimension check |

`docs/UI_AUDIT_REPORT.md` และ `docs/ui-audit/` เป็น pre-existing untracked evidence ไม่ใช่ Phase A changes และ capture script เขียนเฉพาะ `docs/ui-foundations/screenshots` (`scripts/Capture-UiFoundations.ps1:9-13`)

## 14. Behavior-Preservation Review

- Action event handlers เดิมยัง map ไป functions เดิม; shared helper เปลี่ยน presentation/metadata ไม่เปลี่ยน command execution (`src/NetStuck/NetStuck.cs:1846-1879`)
- Collector/Ping/Traceroute engine, retry, cadence, timers, connection and file output ไม่ถูก refactor
- Traceroute 46/18/18/18 responsive geometry, 120 px service fields และ action row ยังคงเดิม; existing related FeatureTests ผ่านทั้งหมด
- Event Log Clear ยังคง clear session table และเพิ่มเพียง state refresh (`src/NetStuck/NetStuck.cs:720`)
- Finite unit conversion ใช้ `NetOpsCore.ConvertUnit` เดิม; เพิ่ม guard เฉพาะ non-finite input (`src/NetStuck/NetStuck.cs:1424-1440`)
- Normal user profile/state/cache paths เหมือนเดิมเมื่อไม่มี `NETSTUCK_TEST_ROOT`; override ใช้เฉพาะ explicit test environment (`src/NetStuck/NetStuck.cs:219-240`)
- Existing 128 assertions ผ่านหลังแก้; raw performance numbers แสดงใน section 16 โดยไม่ตีความเป็นเปอร์เซ็นต์ improvement

## 15. Security and Credential Review

- ไม่มีการแก้ `NetStuck.Features.cs` SSH/PuTTY/Plink transport code
- Existing tests ที่เกี่ยวกับ password-not-in-argv, single authentication attempt, masking/persistence และ credential fallback ผ่านใน final suite
- New test ตรวจว่า current credential control values ไม่ปรากฏใน `AccessibleName`/`AccessibleDescription` (`tests/UiFoundationTests.cs:328-337`)
- Snapshot harness ใส่ RFC 5737 fixture (`192.0.2.0/24`, `203.0.113.0/24`), `example.test`, synthetic device/user/path และล้าง password/secret fields (`tests/UiFoundationSnapshot.cs:81-96`)
- `NETSTUCK_TEST_ROOT` ป้องกัน state/profile/cache เขียนไป user profile และ suppress startup NTP/HTTP ใน UI tests/capture (`src/NetStuck/NetStuck.cs:219-258`)
- ตรวจภาพด้วยตา: ไม่พบ password, token, private hostname/IP จริง, username จริง, personal path หรือ external-device data
- Static secret/privacy scan และ PNG metadata/string scan แสดงผลใน section 23; ไม่มี secret candidate ใน Phase A files

## 16. Automated Test Results

| Suite | Baseline | Final | Delta | Failed | Skipped reported |
| ----- | -------- | ----- | ----- | ------ | ---------------- |
| `NetOpsCoreTests` | 16 | 16 | 0 | 0 | 0 |
| `FeatureTests` | 91 | 91 | 0 | 0 | 0 |
| `UiFoundationTests` | 0 | 51 | +51 | 0 | 0 |
| `PerformanceTests` | 10 | 10 | 0 | 0 | 0 |
| `PollingCadenceTests` | 3 | 3 | 0 | 0 | 0 |
| `OvernightSoakTests` | 8 | 8 | 0 | 0 | 0 |
| **Total** | **128** | **179** | **+51** | **0** | **0** |

Final run measurements: warm UI startup 1157 ms; 254 `/24` rows and 2402 probes; `/24` worst UI dispatch 23 ms; dual Traceroute worst dispatch 60 ms; working set 81 MB; soak duration 11.3872806 s, worst dispatch 21 ms, growth 8 MB ทุก performance assertion ผ่าน ค่าเหล่านี้เป็น single-run evidence ไม่ใช่ claim ว่าประสิทธิภาพดีขึ้น

ระหว่างพัฒนาเคยมี compile/test failures ที่แก้ตรงสาเหตุ: protected `ShowFocusCues`, invalid `ToolStripItem.Spring` access, ambiguous reflection overload, focus test tab selection และ pointer-injection check ที่ environment ไม่ส่ง input ให้ window ไม่มีการลด existing assertion หรือเปลี่ยน expected business behavior Final run หลังแก้ทั้งหมด exit `0`

## 17. Runtime Verification Matrix

| Test/Scenario | Command or method | Result | Evidence | Limitation |
| ------------- | ----------------- | ------ | -------- | ---------- |
| Existing automated suite | `./scripts/Test-NetStuck.ps1 -SoakSeconds 10` | PASS | 179/179; exit 0 | none |
| Existing 128/128 baseline | before/after comparison | PASS | 128 existing checks remain PASS | none |
| Application launch | hidden packaged EXE smoke under isolated root | PASS | process responding and closed cleanly; exit 0 | no external network work |
| Pilot surfaces open | snapshot host selects tabs | PASS | 9 captures generated | `DrawToBitmap` host, not human session |
| Mouse interaction | action activation + WinForms `MouseClick` routing | PARTIAL | `tests/UiFoundationTests.cs:244-255` PASS | OS pointer injection blocked; no physical mouse session |
| Keyboard-only navigation | `SelectNextControl`/focus smoke | PASS | Calculator focus check PASS | pilot only |
| Visible focus | render test + focused screenshot | PASS | `pilot-calculators-1460x900.png` | current theme only |
| Tab order | reflection/control checks | PASS | Calculator and Event Log checks PASS | pilot only |
| Accessible names | control-tree checks | PASS | no unnamed/duplicate pilot controls | no screen reader runtime |
| Default button | scope review | NOT APPLICABLE | two independent Calculator contexts; no dialog modified | existing dialogs unchanged |
| Escape/Cancel | scope review | NOT APPLICABLE | no dialog/cancel lifecycle modified | existing dialogs unchanged |
| Resize 1100×900 | layout tests + screenshots | PASS | shell/Calculators/Event Log | 96 DPI |
| Resize 1460×900 | layout tests + screenshots | PASS | shell/Calculators/Event Log | 96 DPI |
| Height near 700 px | layout tests + screenshots | PASS | Calculators/Event Log 1100×700 | 96 DPI |
| DPI 125% | environment inspection | NOT VERIFIED | current capture DPI is 96×96 | no safe 120-DPI session |
| DPI 150% | environment inspection | NOT VERIFIED | current capture DPI is 96×96 | no safe 144-DPI session |
| DPI 200% | environment inspection | NOT VERIFIED | current capture DPI is 96×96 | no safe 192-DPI session |
| High Contrast | environment inspection | NOT VERIFIED | `HighContrast=False` | code branch/inspection is not runtime proof |
| Screen reader | environment inspection | NOT VERIFIED | no screen reader session | metadata tests only |
| Thai long text | component render/accessibility test | PASS | `tests/UiFoundationTests.cs:172-176` | no Thai production localization or screen reader |
| English long text | component render/accessibility test | PASS | `tests/UiFoundationTests.cs:167-170` | component-level |
| Empty state | Event Log safe fixture | PASS | empty screenshots/tests | pilot only |
| Error state | invalid local Calculator input | PASS | validation screenshot/test | safe local failure only |
| Loading/progress state | state catalog/determinate test | PASS | presenter/progress checks PASS | not adopted into operation engine |
| Credential masking | existing + new metadata tests | PASS | collector tests and accessibility scan | no production device used |
| Screenshot privacy | sanitization + manual/static review | PASS | 9 sanitized PNGs | pixels inspected at current theme |

## 18. Screenshots and Visual Evidence

ภาพทั้งหมดเป็น **after** evidence และสร้างจาก sanitized local fixture:

| Screenshot | Resolution | Runtime state | Visual result |
| ---------- | ---------- | ------------- | ------------- |
| `docs/ui-foundations/screenshots/main-shell-1100x900.png` | 1100×900 | Updates/shell | no overlap/clipping observed |
| `docs/ui-foundations/screenshots/main-shell-1460x900.png` | 1460×900 | Updates/shell | header/version/navigation align |
| `docs/ui-foundations/screenshots/pilot-calculators-1100x700.png` | 1100×700 | successful local values | controls/status visible; reference scrolls |
| `docs/ui-foundations/screenshots/pilot-calculators-1100x900.png` | 1100×900 | successful local values | no overlap/clipping observed |
| `docs/ui-foundations/screenshots/pilot-calculators-1460x900.png` | 1460×900 | success + focus | visible focus cue |
| `docs/ui-foundations/screenshots/pilot-calculators-validation-1100x900.png` | 1100×900 | safe invalid input | inline validation and field focus visible |
| `docs/ui-foundations/screenshots/pilot-event-log-empty-1100x700.png` | 1100×700 | empty | persistent labels/state/grid visible |
| `docs/ui-foundations/screenshots/pilot-event-log-empty-1100x900.png` | 1100×900 | empty | no overlap/clipping observed |
| `docs/ui-foundations/screenshots/pilot-event-log-filtered-empty-1460x900.png` | 1460×900 | one sanitized row filtered out | explicit filtered-empty state |

`./scripts/Capture-UiFoundations.ps1` ตรวจชื่อไฟล์และ exact dimensions ได้ `9/9`, exit `0` (`scripts/Capture-UiFoundations.ps1:50-76`) ไม่มีการแก้ pixel หลัง capture และไม่ได้ overwrite `docs/ui-audit/`

## 19. Known Limitations

- DPI runtime verified เฉพาะ 96×96; 125%/150%/200% ยัง NOT VERIFIED
- High Contrast runtime และ screen reader ยัง NOT VERIFIED
- Physical mouse click ไม่สามารถ inject ผ่าน desktop host; มีเพียง action activation และ synthetic WinForms mouse-event routing จึงเป็น PARTIAL
- Screenshot host compile เป็น separate executable ทำให้ title-bar icon มาจาก capture host ไม่ใช่ packaged `NetStuck.exe`; content/layout evidence ยัง valid
- `DrawToBitmap` ไม่พิสูจน์ popup, tooltip, native dropdown, modal dialog หรือ animation fidelity
- Accessibility adoption จำกัด shell/Calculators/Event Log และ shared action factory; finding app-wide ยังไม่ปิด
- `MainForm` monolithic partial architecture ยังอยู่; Phase A แยกเฉพาะ smallest foundation seam
- High Contrast branch ของ colors มี code/component coverage แต่ไม่มี live theme-switch evidence
- Dialog tokens มี migration target แต่ยังไม่มี dialog adoption ตาม DLG-01

## 20. Deferred Findings

| Finding | Reason deferred | Intended phase | Dependency | Risk if delayed |
| ------- | --------------- | -------------- | ---------- | --------------- |
| ASYNC-01, PERF-01 | Collector UI-thread/performance change ถูกห้ามใน Phase A | Phase B1 | operation profiling + state contract | long operation ยังอาจกระทบ responsiveness |
| STATE-01, STATE-02 | Collector state/run snapshot ต้องเปลี่ยน operation model | Phase B1 | immutable run context | stale/mixed run presentation risk |
| STATE-03 | Traceroute reducer/cycle เป็น business state | Phase B2 | state transition tests | stale-cycle confusion remains |
| A11Y-01, A11Y-03, A11Y-04 | Phase A adopt เฉพาะ pilots | Phase B/C | per-surface inventory | unnamed/order/editability defects remain elsewhere |
| DPI-01 | ไม่มี multi-DPI runtime session | Phase B verification | 120/144/192 DPI test hosts | scaling defects outside tested sizes may remain |
| VAL-01 | inline validation adopt เฉพาะ Calculators | Phase B/C | field/error mapping | modal/distant errors remain elsewhere |
| STATE-04, STATE-05 | presenter ยังไม่เชื่อม operation engine | Phase B | state adapters | inconsistent operation feedback remains |
| ASYNC-02 | cancellation lifecycle out of scope | Phase B/C | operation ownership | cancel/close races remain possible |
| PERF-02 | cache/font lifecycle refactor ไม่จำเป็นต่อ pilot | Phase D | profiling | resource churn may remain |
| ARCH-01 | broad decomposition เสี่ยง regression | incremental phases | stable seams/tests | monolith slows future migration |
| DLG-01 | no dialog migration authorized | Phase C | dialog inventory/conventions | inconsistent dialog actions remain |
| PERSIST-01 | schema/view persistence change prohibited | Phase B2/C | compatibility plan | view state inconsistency remains |
| SHELL-01 | adopted layout/a11y slice only | Phase C | information architecture decision | identity/hierarchy issues partly remain |
| UPDATE-01 | Updates redesign is P3/non-pilot | Phase C | content policy | low-priority inconsistency remains |

## 21. Recommended Phase B Scope

1. **Phase B1 — Collector correctness/responsiveness:** address P1 `ASYNC-01`, `PERF-01`, `STATE-01`, `STATE-02` together with immutable run context and explicit state adapter; preserve SSH/PuTTY/Plink security tests
2. **Phase B2 — Traceroute/Ping state adoption:** map existing state transitions into `UiStatePresenter` without changing cadence/probe behavior; resolve `STATE-03/04/05`
3. **Accessibility expansion:** inventory interactive controls per page, add names/descriptions/labels/tab order และ explicit read-only semantics; prioritize P1/P2 surfaces
4. **Real environment matrix:** run 120/144/192 DPI, High Contrast, keyboard, physical mouse and at least one Windows screen reader; store sanitized evidence separately
5. **Do not begin Phase C dialog/persistence redesign** until Phase B operation/state contracts and compatibility tests are stable

## 22. Exact Git Status and Diff Summary

Final snapshot หลัง implementation/report/capture:

```text
git status --short
 M scripts/Build-NetStuck.ps1
 M scripts/Test-NetStuck.ps1
 M src/NetStuck/NetStuck.V103.cs
 M src/NetStuck/NetStuck.cs
?? docs/UI_AUDIT_REPORT.md
?? docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md
?? docs/ui-audit/
?? docs/ui-foundations/
?? scripts/Capture-UiFoundations.ps1
?? src/NetStuck/NetStuck.UiFoundation.cs
?? tests/UiFoundationSnapshot.cs
?? tests/UiFoundationTests.cs
```

`docs/UI_AUDIT_REPORT.md` และไฟล์ 18 ภาพใน `docs/ui-audit/screenshots/` เป็น pre-existing untracked; Phase A เพิ่ม 14 untracked files ได้แก่ report, foundation source, capture script, two test sources และ 9 screenshots

```text
git status --ignored --short
[same modified/untracked entries as above]
!! artifacts/
!! fake-plink-auth-count.txt

git diff --stat
 scripts/Build-NetStuck.ps1    |   1 +
 scripts/Test-NetStuck.ps1     |   4 +-
 src/NetStuck/NetStuck.V103.cs |  20 +--
 src/NetStuck/NetStuck.cs      | 313 +++++++++++++++++++++++++++++++-----------
 4 files changed, 250 insertions(+), 88 deletions(-)

git diff --name-status
M scripts/Build-NetStuck.ps1
M scripts/Test-NetStuck.ps1
M src/NetStuck/NetStuck.V103.cs
M src/NetStuck/NetStuck.cs
```

`git diff --stat/name-status` ไม่รวม untracked files ตาม behavior ของ Git; รายการ Phase A new files อยู่ใน section 13 `git diff --check` exit `0` ไม่มี whitespace error หลัง EOL audit Existing tracked files ถูกคืนเป็น CRLF เหมือน baseline และ Phase A text files ทั้ง 9 มี `bareLF=0`; ไม่มี EOL-only sweep, dependency/project/lockfile change, unexpected cache/debug dump หรือ unreviewed binary นอก 9 PNG evidence

## 23. Commands and Exit Codes

| Command/check | Exit | Result |
| ------------- | ---- | ------ |
| `git branch --show-current; git rev-parse HEAD; git describe --tags --always` | 0 | `main`; `2f35f72988fac0a44292a6bd69196e0842cbfc73`; `v1.2.3` |
| Baseline `./scripts/Test-NetStuck.ps1 -SoakSeconds 10` | 0 | 128/128 PASS |
| Final `./scripts/Test-NetStuck.ps1 -SoakSeconds 10` | 0 | 179/179 PASS; 51 new checks |
| `./scripts/Capture-UiFoundations.ps1` | 0 | 9/9 PNG names/dimensions verified |
| `./scripts/Build-NetStuck.ps1` after CRLF normalization | 0 | `NetStuck.exe` built; file version 1.2.3.0 |
| Packaged EXE hidden launch under `NETSTUCK_TEST_ROOT` | 0 | process responding; closed cleanly |
| Current DPI/High Contrast inspection | 0 | 96×96 DPI; `HighContrast=False`; 1920×1200 screen |
| PowerShell AST parse | 0 | 3/3 changed/new scripts parse |
| Required report heading validation | 0 | 23/23 in required order |
| Markdown local-link validation | 0 | 0 links; 0 broken |
| Expanded secret/personal-path scan | 0 | 9 Phase A text files; 0 candidates |
| PNG signature/dimension/hash scan | 0 | 9/9 valid PNGs |
| PNG metadata/ASCII sensitive-string scan | 0 | 45 standard property items; 0 sensitive candidates |
| Dependency/project/lockfile status scan | 0 | no changes |
| `git status --short`, `git status --ignored --short`, `git diff --stat`, `git diff --name-status` | 0 | exact snapshot in section 22 |
| `git diff --check` | 0 | PASS after CRLF normalization |

Intermediate engineering runs ที่ exit `1` ถูกเก็บเป็น evidence ไม่ได้ซ่อน: compile-time API mistakes ถูกแก้, reflection/focus tests ถูกแก้ และ OS pointer-injection assertion ที่ไม่เสถียรถูกแทนด้วย deterministic WinForms mouse-event routing พร้อมลด runtime matrix เป็น PARTIAL ไม่มี existing assertion ถูก skip/ลดระดับ Final full run และ build หลังแก้ทั้งหมด exit `0`

Pre-remediation Round 1 screenshot SHA256 (retained as historical evidence; the three Calculator success images below were false-green and these hashes are not authoritative current evidence):

| File | SHA256 |
| ---- | ------ |
| `main-shell-1100x900.png` | `f68f0e4abf8b47249c37346f2c883e97e99b133d3460473f7960e3649057bb3a` |
| `main-shell-1460x900.png` | `c7ec147a881362a353232c645985766ebda490be294f273d8791d0b1207e660e` |
| `pilot-calculators-1100x700.png` | `f2e79f7767eb6424253e947c2a2828692797bc7ef393801a1c365a2a3bcde458` |
| `pilot-calculators-1100x900.png` | `ddcfc5c89eef98267aba2866f928d2936127d8148551f85f953339e693001381` |
| `pilot-calculators-1460x900.png` | `3d4bbcc3777c0ff17db517a1f30a9ac275792fba2df6203d97d85bbd9bffbf7d` |
| `pilot-calculators-validation-1100x900.png` | `a3211fd9fa6d248462bb35394064910b46915d8dbc4adb08236069ed3de315d0` |
| `pilot-event-log-empty-1100x700.png` | `c62fd6b1934dc8cd80e3ed2b8bb58a826dc11e9c00ddbc50cf6214534b76b0e0` |
| `pilot-event-log-empty-1100x900.png` | `cf95ad1fb4927c4098d3c73fcadda72a2d0f5f66300a37ed454bc8553899e411` |
| `pilot-event-log-filtered-empty-1460x900.png` | `be7611ce94439a6104386a9a9e8dceea3567f735abb8d14e09940dfee3781c4d` |
