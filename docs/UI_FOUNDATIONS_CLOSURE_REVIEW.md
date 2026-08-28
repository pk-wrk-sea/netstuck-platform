# NetStuck — Phase A UI Foundations Closure Review

วันที่ตรวจ: 2026-08-26
บทบาทผู้ตรวจ: Principal Software Engineer / Independent Closure Reviewer
Repository ที่ตรวจจริง: `C:\Codex\_Project\NetStuck`
Baseline: tag `v1.2.3`, commit `2f35f72988fac0a44292a6bd69196e0842cbfc73`
Working branch: `ui/phase-a-foundations`
Final verdict: **NOT_READY**
Commit blocker: **YES**

รายงานนี้เป็น read-only closure review ของ Phase A เท่านั้น ผู้ตรวจไม่ได้แก้ production code, tests, scripts, implementation report, audit report หรือ screenshot ใด และไม่ได้ stage, commit, tag หรือ push การเปลี่ยนแปลงใด ไฟล์นี้เป็น repository file ใหม่เพียงไฟล์เดียวที่ผู้ตรวจสร้างตามขอบเขตที่ได้รับอนุญาต

## 1. Executive Verdict

Phase A ยัง **ไม่พร้อม commit / merge / release** ในสถานะที่ตรวจ แม้ source จะ build ได้, test runner ปัจจุบันผ่าน `179/179`, baseline tag ผ่าน `128/128`, package สร้างได้, manifest ตรง `8/8` และ packaged EXE เปิดหน้าต่างที่ responsive แล้วปิดด้วย exit `0`

เหตุผลหลักไม่ใช่ production regression ที่ตรวจพบ แต่เป็น closure evidence และ test contract ที่ให้ผลเขียวเกินจริง:

1. screenshot ที่รายงานว่าเป็น Calculator success ทั้ง 3 ภาพยังแสดง `Ready to calculate`, `Ready to convert`, subnet result ว่าง และ `Result: —`; capture host เรียก `PerformClick()` ตอนแท็บ `Calculators` ถูกซ่อน จึงไม่เรียก action จริง แต่ script ตรวจเพียงชื่อและ dimensions แล้วรายงาน `9/9` ผ่าน
2. screenshot rerun ไม่ hash-reproducible: rerun ใน temporary mirror ผ่าน 9/9 แต่ตรงกับหลักฐานเดิมเพียง 2/9 hashes เพราะ footer clock ยังเปลี่ยนตามเวลาจริง
3. `UiStatePresenter.SetDeterminateProgress(12, 10)` clamp visual value เป็น `10/10` แต่ accessibility metadata ประกาศ `12 of 10 complete`; test กลับ assert ข้อความที่ผิดนี้ให้ผ่าน
4. test และ snapshot harness กลืน cleanup exception ด้วย `catch { }` ทำให้ cleanup failure สามารถผ่านหลอกได้
5. shared palette/action/grid changes มีผลกว้างกว่าสอง pilot screens แต่ post-change visual evidence ครอบคลุมเพียง shell, Calculators และ Event Log

สรุป gate:

| Gate | Result | เหตุผล |
| --- | --- | --- |
| Development build | PASS | `NetStuck.exe` version `1.2.3.0` สร้างได้ |
| Automated regression | PASS | current `179/179`; clean baseline `128/128` |
| Package creation/integrity | PASS with release gap | ZIP/manifest/smoke ผ่าน แต่ bundled test report ยังเป็น `128/128` |
| Behavior preservation | PASS for automated/static scope | network/process/credential engines ไม่ถูกแก้; cadence/performance/soak ผ่าน |
| Screenshot evidence integrity | FAIL | Calculator success false-green และ hashes ไม่ reproducible |
| Test quality/isolation | FAIL | semantic capture assertion ขาด, invalid progress expectation, cleanup failure ถูกกลืน |
| Manual DPI/HC/screen reader | NOT VERIFIED | environment ปัจจุบัน 96 DPI, High Contrast off |
| Ready for commit | NO | UIF-CR-001 ถึง UIF-CR-004 เป็น blockers |
| Ready for merge | NO | ต้องปิด blocker และ resolve global-surface evidence |
| Ready for release | NO | ต้องปิด blocker, refresh packaged test report และทำ manual release matrix |

## 2. Scope

ขอบเขตที่ตรวจคือ Phase A UI Foundations เท่านั้น:

- shared WinForms UI foundation ใน `src/NetStuck/NetStuck.UiFoundation.cs`
- shell wiring และ shared factories ใน `src/NetStuck/NetStuck.cs`
- Traceroute UI token/action changes ใน `src/NetStuck/NetStuck.V103.cs`
- build/test/capture integration ใน `scripts/Build-NetStuck.ps1`, `scripts/Test-NetStuck.ps1`, `scripts/Capture-UiFoundations.ps1`
- `tests/UiFoundationTests.cs` และ `tests/UiFoundationSnapshot.cs`
- Phase A implementation report และ `docs/ui-foundations/screenshots/*.png`
- traceability กลับไปยัง audit findings ทั้ง 22 รายการใน `docs/UI_AUDIT_REPORT.md`
- baseline relationship, package contents, security/privacy, accessibility, state model, rendering/performance และ diff hygiene

อ่านครบก่อนลง verdict:

- `AGENTS.md`
- `.codex/skills/netstuck-maintainer/SKILL.md`
- `README-TH.md`
- `docs/ARCHITECTURE.md`
- `docs/DEVELOPMENT.md`
- `docs/TESTING.md`
- `docs/UI_AUDIT_REPORT.md`
- `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md`
- `PRIVACY.md`
- `SECURITY.md`
- `docs/VERSIONING.md`
- `docs/RELEASING.md`

สิ่งที่ไม่ได้ทำ:

- ไม่ redesign app-wide และไม่ implement remediation
- ไม่แก้ Phase A artifacts เดิม
- ไม่ทำ live network mutation, SSH/Telnet login หรือใช้ credential จริง
- ไม่ทดสอบ mixed-DPI, High Contrast, Narrator/screen reader, physical pointer หรือ multi-monitor จริง
- ไม่เปลี่ยน version เพราะไม่มี intentional release/versioning change
- ไม่ stage/commit/tag/push

`C:\Codex_Project\NetStuck` จาก environment context ไม่ใช่ checkout ที่มีเนื้อหา ผู้ตรวจจึง resolve repository ที่มี `AGENTS.md`, source และ `.git` เป็น `C:\Codex\_Project\NetStuck` ก่อนเริ่ม preflight

## 3. Repository Snapshot

สถานะก่อนสร้าง closure report:

| Item | Value |
| --- | --- |
| Branch | `ui/phase-a-foundations` |
| HEAD | `2f35f72988fac0a44292a6bd69196e0842cbfc73` |
| `git describe` | `v1.2.3` |
| Tag commit | `2f35f72988fac0a44292a6bd69196e0842cbfc73` |
| Ahead/behind `v1.2.3...HEAD` | `0 / 0` |
| Staged paths | `0` |
| Modified tracked paths | `4` |
| Untracked paths expanded to files | `33` |
| Tracked diff stat | `250 insertions, 88 deletions` |

Initial `git status --short`:

```text
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

`artifacts/` และ `fake-plink-auth-count.txt` เป็น ignored paths และไม่อยู่ใน Git change inventory. การเรียก build/package script ใช้ output tree ที่ script กำหนดไว้ใน `artifacts/`; การตรวจ capture ซ้ำที่ใช้ตัดสิน reproducibility ทำใน temporary mirror นอก repository และลบออกสำเร็จ

## 4. Baseline Relationship

Phase A เป็น working-tree change บน commit เดียวกับ tag `v1.2.3`; ไม่มี commit ใหม่เหนือ baseline:

- `git rev-parse HEAD` และ `git rev-parse v1.2.3^{commit}` ให้ commit เดียวกัน
- `git rev-list --left-right --count v1.2.3...HEAD` ให้ `0 0`
- `git merge-base --is-ancestor v1.2.3 HEAD` exit `0`
- clean local clone ที่ checkout commit นี้มี empty `git status --short`

Baseline test suite ที่ clean tag ผ่าน `128/128`:

| Suite | Baseline | Phase A current |
| --- | ---: | ---: |
| Core | 16/16 | 16/16 |
| Feature/UI/integration | 91/91 | 91/91 |
| UI Foundation | not present | 51/51 |
| Performance | 10/10 | 10/10 |
| Fixed cadence | 3/3 | 3/3 |
| Accelerated soak | 8/8 | 8/8 |
| Total | 128/128 | 179/179 |

Baseline invariants ที่ใช้เป็น closure constraints และยังผ่าน automated/static review ได้แก่ fixed monotonic cadence, bounded overlap, stale-result protection, dual independent Trace sessions, exact Traceroute input geometry, bounded UI updates, secret-free process arguments/state/logs, minimum window `1100x700`, portable packaging และ no new dependency

ข้อผิดพลาดใน documentation: implementation report ระบุ branch เป็น `main` ที่ `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md:372` แต่ repository จริงอยู่ที่ `ui/phase-a-foundations`

## 5. Initial Diff Fingerprint

Fingerprint ของ tracked implementation diff ก่อนสร้างรายงาน:

```text
Command: git diff --binary --no-ext-diff | git hash-object --stdin
Result:  e40ca16a2d07e693bafdedac02413f0134c150f3
Exit:    0
```

Fingerprint นี้ครอบคลุม tracked diff เท่านั้น จึงใช้ SHA-256 รายไฟล์ใน Appendix สำหรับ untracked Phase A artifacts. หลังสร้าง closure report fingerprint ของ tracked implementation diff ต้องยังเท่าเดิม และ hashes ของ Phase A source/test/evidence เดิมต้องไม่เปลี่ยน

## 6. Authoritative Change Inventory

### Modified tracked files

| File | Purpose observed | Production impact |
| --- | --- | --- |
| `scripts/Build-NetStuck.ps1` | เพิ่ม `NetStuck.UiFoundation.cs` ใน production compile list | Foundation ถูกรวมใน EXE |
| `scripts/Test-NetStuck.ps1` | เพิ่ม foundation source/test executable และ run order | เพิ่ม 51 assertions; nonzero exit ยัง propagate |
| `src/NetStuck/NetStuck.cs` | shell, global tokens/factories, test root, Calculators, Event Log | UI-visible และ global style reach |
| `src/NetStuck/NetStuck.V103.cs` | tokenized Trace controls/focus/action roles | UI-visible; polling engine ไม่เปลี่ยน |

### New Phase A files

| Category | Files | Count |
| --- | --- | ---: |
| Foundation production source | `src/NetStuck/NetStuck.UiFoundation.cs` | 1 |
| Test source | `tests/UiFoundationTests.cs`, `tests/UiFoundationSnapshot.cs` | 2 |
| Capture script | `scripts/Capture-UiFoundations.ps1` | 1 |
| Implementation report | `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md` | 1 |
| Phase A screenshots | `docs/ui-foundations/screenshots/*.png` | 9 |
| Total Phase A untracked files |  | 14 |

### Pre-existing audit evidence in the same working tree

| Category | Files | Count |
| --- | --- | ---: |
| Audit report | `docs/UI_AUDIT_REPORT.md` | 1 |
| Audit screenshots | `docs/ui-audit/screenshots/*.png` | 18 |
| Total pre-existing audit files |  | 19 |

`NetStuck.UiFoundation.cs` มี 384 lines / 21,249 bytes, `UiFoundationTests.cs` 487 lines / 27,677 bytes, `UiFoundationSnapshot.cs` 132 lines / 6,570 bytes และ capture script 76 lines. ไม่พบ project file, package manifest, lockfile หรือ third-party dependency ใหม่

Build source-list coverage:

- production build: includes foundation
- test UI library: includes foundation
- capture UI library: includes foundation
- package: เรียก full test/build script และ bundle production EXE
- packaged assembly: มี `NetStuck.UiTokens` และ `NetStuck.UiStatePresenter`; ไม่มี `NetStuck.Tests.UiFoundationTests` หรือ `NetStuck.Tests.UiFoundationSnapshot`

## 7. Audit Traceability

สถานะด้านล่างประเมิน closure ของ original app-wide finding ไม่ใช่เพียงว่ามี API หรือ test synthetic แล้ว `NOT IMPLEMENTED` ที่ตรงกับ declared phase boundary ไม่ถือเป็น Phase A regression โดยอัตโนมัติ

| Audit ID | Sev | Implementation claim | Independent status | Independent evidence / gap |
| --- | --- | --- | --- | --- |
| A11Y-01 | P1 | Partially implemented | PARTIAL | shell/pilots/actions มี metadata แต่ runtime probe ยังพบ 83/127 interactive controls ไม่มี explicit name |
| A11Y-02 | P1 | Partially implemented | PARTIAL | normal palette contrast assertions ผ่าน; High Contrast branch มีใน code แต่ runtime ไม่ได้เปิด HC |
| ASYNC-01 | P1 | Out of scope | NOT IMPLEMENTED | Trace stale-cycle reducer ไม่ได้แก้ใน Phase A; regression suite เดิมผ่าน |
| PERF-01 | P1 | Out of scope | NOT IMPLEMENTED | Collector finalize/terminal engine ไม่ได้แก้; absolute performance gates ผ่าน |
| STATE-01 | P1 | Foundation available, not adopted | NOT IMPLEMENTED | generic state catalog ไม่มี Collector operation controller/cancel/re-entry consumer |
| STATE-02 | P1 | Out of scope | NOT IMPLEMENTED | immutable Collector run snapshot ยังไม่มี |
| STATE-03 | P1 | Foundation available, not adopted | NOT IMPLEMENTED | generic Error state ไม่ได้แก้ Trace reducer/failure transition |
| A11Y-03 | P2 | Partially implemented | PARTIAL | pilot tab order/focus/mnemonics ผ่าน; physical keyboard/app-wide coverage ไม่มี |
| A11Y-04 | P2 | Partially implemented | PARTIAL | Event Log ใช้ read-only primitive; runtime มี 11 grids แต่ read-only 4 และ explicit accessible name เพียง 1 |
| DPI-01 | P2 | Partially implemented | PARTIAL | 1100x700, 1100x900, 1460x900 ผ่านที่ 96 DPI; 125/150/200% ไม่ได้ทดสอบ |
| VAL-01 | P2 | Partially implemented | PARTIAL | Calculator invalid/success runtime tests ผ่าน; app-wide modal validation ยังเดิม |
| STATE-04 | P2 | Foundation available, not adopted | NOT IMPLEMENTED | action flags อยู่ใน definition แต่ไม่มี global operation registry/status ownership |
| ASYNC-02 | P2 | Deferred | NOT IMPLEMENTED | cancellation/close lifecycle ไม่ได้ refactor |
| PERF-02 | P2 | Deferred | NOT IMPLEMENTED | resource/cache/font lifecycle ยังไม่ถูกปิดและไม่มี before/after handle profile |
| CTRL-01 | P2 | Implemented for Phase A | PARTIAL | shared factory แยก four roles ได้ แต่ test spot-check เพียง Calculate/Event Clear/Ping Stop และ legacy Clear actions บางแห่งยัง secondary |
| ARCH-01 | P2 | Partially implemented | PARTIAL | แยก foundation partial seam โดยไม่ rewrite engine; MainForm monolith/duplicate builders ยังอยู่ |
| DLG-01 | P2 | Foundation available, not adopted | NOT IMPLEMENTED | dialog tokens ไม่มี concrete dialog consumer; native/custom dialogs ไม่เปลี่ยน |
| PERSIST-01 | P2 | Deferred | NOT IMPLEMENTED | view geometry/state persistence ไม่ได้เพิ่ม |
| STATE-05 | P2 | Partially implemented | PARTIAL | Calculators/Event Log ใช้ presenter; loading/running/error/unavailable ไม่มี production consumer |
| TEST-01 | P2 | Implemented for Phase A | PARTIAL | safe root และ 51 checks มีจริง แต่ capture false-green, cleanup swallow และ DPI/UIA/manual gaps ยังอยู่ |
| SHELL-01 | P3 | Partially implemented | PARTIAL | shell header/status responsive ที่ target sizes; broader priority/truncation policy และ assistive runtime ยังไม่ปิด |
| UPDATE-01 | P3 | Deferred | NOT IMPLEMENTED | Updates content ไม่ได้ redesign ตาม scope |

ไม่มี original audit finding ใดที่ independent review จัดเป็น fully `VERIFIED` app-wide ใน Phase A; สิ่งนี้สอดคล้องกับ incremental phase boundary แต่ต้องไม่สื่อว่า API catalog เท่ากับ finding closure

## 8. Foundation Architecture Review

ข้อดีที่ยืนยันได้:

- foundation อยู่ใน source file เดียวภายใต้ namespace/assembly เดิม ไม่มี project/dependency ใหม่
- `MainForm` ยังคง partial class และ network/process engines ไม่ถูกย้ายหรือ rewrite
- production/test/capture source lists รวม foundation อย่างสอดคล้องกัน
- helpers มี purpose ชัดสำหรับ accessibility association, action role, read-only grid และ state presenter
- test root ใช้ environment seam เดียวเพื่อแยก state/profile/MAC/Trace cache paths และ suppress startup external continuations
- packaged EXE มี foundation types แต่ไม่มี test/capture host types

ความเสี่ยงทางสถาปัตยกรรม:

- global aliases `Canvas/Surface/Border/Text/Accent/...` ที่ `NetStuck.cs:86-94`, global `ActionButton` factory ที่ `NetStuck.cs:1846-1877` และ grid styling ที่ `NetStuck.cs:1909-1911` ทำให้ visual impact กว้างกว่าสอง pilot screens
- foundation มี token/state/action contracts ที่ยังไม่มี concrete consumer; tests บังคับ catalog shape มากกว่าพิสูจน์ real workflow
- progress contract มี inconsistency ระหว่าง visual value กับ accessibility metadata ซึ่งเป็นตัวอย่างของ speculative API ที่ยังไม่ผ่าน consumer-driven validation
- global state ownership, structured cancellation และ dialog shell ยังไม่มี แม้มี vocabulary รองรับบางส่วน

Architecture verdict: direction ใช้ได้และหลีกเลี่ยง framework rewrite แต่ยังไม่ minimal/consumer-proven พอสำหรับ closure จนกว่าจะตัดหรือแก้ unconsumed contracts และเพิ่ม evidence ให้ global reach

## 9. Design Token Review

ตรวจพบ token declarations 34 รายการ:

- spacing scale เรียงลำดับและ positive; component test ผ่าน
- normal-theme semantic text pairs ผ่าน threshold `4.5:1` และ focus/required borders ผ่าน `3:1` ตาม test implementation
- High Contrast branch ใช้ `SystemInformation.HighContrast` และ `SystemColors`; code path มีแต่ไม่ได้ runtime-test
- dimensions ที่ pilot ใช้จริง เช่น `SpaceSm/Md/Lg`, control heights, splitter width, grid row/header heights มี concrete consumers
- 13 tokens ไม่มี production reference นอก foundation; ในจำนวนนี้ 11 รายการมีเพียง declaration แม้ภายใน foundation ได้แก่ `CornerRadius`, `DialogControlHeight`, `DialogFooterHeight`, `DialogMargin`, `IconMedium`, `IconSmall`, `NumericFieldMinWidth`, `SectionGap`, `SpaceXl`, `StandardFieldPreferredWidth`, `WideFieldPreferredWidth`
- `BorderWidth` และ `CaptionFontSize` ถูกใช้ภายใน foundation แต่ยังไม่มี direct consumer นอก foundation

การมี future token ไม่ใช่ production defect โดยลำพัง แต่ขัดกับ requirement ที่ให้ foundation มีเฉพาะของที่จำเป็นต่อ Phase A. ควร trim/defer declaration-only values หรือระบุ concrete Phase A consumer และ test ที่พิสูจน์ behavior ไม่ใช่เพียง presence

## 10. Pilot Surface Review

### Shell

- header/navigation/status ใช้ layout containers และ metadata ใหม่
- screenshots 1100x900 และ 1460x900 ไม่มี overlap/clipping ที่สังเกตได้
- `MinimumSize` `1100x700` และ navigation behavior เดิมยังผ่าน tests
- current environment 96 DPI; ไม่ใช่หลักฐาน 125–200%
- shell screenshots สองภาพ rerun hash ตรงกับ repository evidence

### Calculators

- persistent labels `Address / prefix`, `Value`, `From`, `To` มี mnemonic/metadata
- tab order, focus return, invalid-input inline state และ success runtime actions ผ่าน `UiFoundationTests`
- non-finite unit values ถูก reject เพิ่มเติม เป็น intentional validation hardening
- runtime probe อิสระยืนยันว่า visible-tab clicks เปลี่ยนทั้งสอง presenter เป็น `Success`, subnet output ไม่ว่าง และ unit output เป็น `Result: 1 Gbit`
- screenshot success evidence 3 ภาพไม่ใช่ success state เพราะ clicks เกิดขณะแท็บถูกซ่อน; ดู UIF-CR-001
- validation screenshot เป็น validation state จริง เพราะแท็บถูก select หลัง capture success ชุดแรกแล้ว
- physical dropdown opening, physical mouse click และ tooltip behavior ยังไม่ verified

### Event Log

- Search/Level มี persistent labels; Export/Clear มี action metadata/roles
- grid เป็น explicit read-only, ยัง sort/select/copy/resize/reorder ได้ตาม component tests
- empty และ filtered-empty states แสดงชัดใน screenshots และ runtime tests
- 1100x700 ยังใช้งานได้ แม้ grid มี horizontal scrollbar จาก fixed columns
- filter/list-change wiring อาจประกาศ filtered-empty ซ้ำ; ดู UIF-CR-006

## 11. Behavior-Preservation Review

ไม่พบ diff ใน `NetStuck.Features.cs`, `NetStuck.Release1.cs`, `NetOpsCore.cs` หรือ operation/test protocol engines. Diff ใน `NetStuck.V103.cs` จำกัดที่ visual tokens/action roles/focus border. Static diff ไม่พบการเปลี่ยน:

- fixed cadence หรือ probe scheduling
- overlapping-operation bounds
- stale result/generation checks
- two Trace session lifecycle
- password/process argument construction
- Collector streaming/finalization logic
- state schema/version
- export data contracts

Intentional behavior changesที่ตรวจพบ:

- Event Log grid ถูกกำหนด read-only ชัดเจน
- Calculator validation แสดง inline และ focus กลับ field
- NaN/Infinity ถูก reject ใน unit converter
- action buttons ได้ semantic role/style จาก shared factory
- `NETSTUCK_TEST_ROOT` redirect test-owned state/cache paths และ suppress startup network work เมื่อ set

Evidence:

- original Core/Feature/Performance/Cadence/Soak suites ผ่านทั้งหมดใน current tree
- clean baseline และ current suite ต่างผ่าน absolute gates
- no new dependencies
- packaged smoke ผ่าน

ข้อจำกัด: automated tests ยืนยัน functional invariants ได้ดี แต่ไม่ได้พิสูจน์ visual preservation ของทุก non-pilot surface ที่รับ global palette/button/grid changes

## 12. Build and Package Review

| Check | Result | Evidence |
| --- | --- | --- |
| `scripts/Build-NetStuck.ps1` | PASS, exit 0 | optimized WinExe created; file version `1.2.3.0` |
| `scripts/Test-NetStuck.ps1 -SoakSeconds 10` | PASS, exit 0 | `179/179` |
| `scripts/Package-NetStuck.ps1 -Version 1.2.3 -PlinkPath ...` | PASS, exit 0 | tests rerun, stage/ZIP created |
| ZIP SHA-256 | PASS | `af45cbec053b9945794d35e651bfe73f47d562c90c9e9c9c763aaa4c3fc27935` |
| Manifest | PASS | 8 entries, 0 missing, 0 hash mismatches |
| Stage/ZIP file count | PASS | 9 files including manifest |
| Source/test/script leakage | PASS | 0 `.cs`, `.ps1`, `.pdb`, test EXEs or capture hosts in ZIP |
| Production type inclusion | PASS | `UiTokens`/`UiStatePresenter` present |
| Test type exclusion | PASS | `UiFoundationTests`/`UiFoundationSnapshot` absent |
| Packaged EXE smoke | PASS, exit 0 | responsive `NetStuck` window, nonzero handle, graceful close |
| Application signing | INFO | `NetStuck.exe` not signed; unchanged release model |
| Plink integrity | PASS | PuTTY 0.80 hash expected; Authenticode `Valid`, signer Simon Tatham |

Package contents:

```text
CHANGELOG.md
NetStuck-Icon.png
NetStuck.exe
README-TH.md
README.md
SHA256SUMS.txt
TEST-REPORT.txt
tools/plink.exe
tools/PuTTY-LICENCE.txt
```

Release evidence gap: package command reruns current `179/179`, แต่ `scripts/Package-NetStuck.ps1:52` copies static `docs/releases/v1.2.3/TEST-REPORT.txt`; packaged fileยังระบุ `128/128` และไม่มี UI Foundation `51/51`. นี่ไม่ block source commit โดยลำพัง แต่ block release claim ของ package นี้

ไม่มี version bump ซึ่งเหมาะกับ review/working-tree Phase A ที่ยังไม่ใช่ intentional release; ห้ามนำ package นี้ออก release จนผ่าน release gates

## 13. Test Runner Review

`scripts/Test-NetStuck.ps1`:

- compile production UI library จาก `NetOpsCore.cs`, `NetStuck.UiFoundation.cs`, `NetStuck.cs`, `NetStuck.Features.cs`, `NetStuck.Release1.cs`, `NetStuck.V103.cs`
- compile test executables เป็น separate outputs
- run order: `NetOpsCoreTests`, `FeatureTests`, `UiFoundationTests`, `PerformanceTests`, `PollingCadenceTests`, `OvernightSoakTests`
- `Invoke-TestExecutable` throw เมื่อ child exit code ไม่ใช่ 0; failure propagation มีจริง
- ไม่พบ skip condition, allow-failure, ignored exit code หรือ conditional suppression สำหรับ UI Foundation tests
- package script เรียก runner ก่อน package และตรวจ file version
- PowerShell AST parse scripts ทั้ง 4 ไฟล์ได้ 0 errors

ข้อสรุป: runner orchestration ไม่ได้ทำให้ test ผ่านหลอก ปัญหา false-green อยู่ใน semantic weakness ของ snapshot host/test assertions ไม่ใช่ exit propagation

## 14. Test-Quality Review

| Test area | Strength | Limitation |
| --- | --- | --- |
| Token/color catalog | ตรวจ presence/order/contrast เป็น deterministic | catalog completeness อาจล็อก speculative tokens |
| State catalog | render ทุก enum, marker/text/colors | six states ไม่มี production consumer; action flagsไม่ wired |
| Progress | visual clamp ถูกตรวจ | accessibility assertion ยอมรับ impossible `12 of 10` |
| Test root | ตรวจ resolved paths และ state write อยู่ใน temp | cleanup exception ถูกกลืน |
| Shell | construction/layout/accessibility smoke | ไม่ใช่ real UIA tree/screen reader |
| Calculator | real visible-tab `PerformClick`, focus, validation, success | screenshot hostใช้ลำดับต่างและพลาด success; mouse test synthetic |
| Event Log | read-only operations, empty/filtered/populated state | ไม่ตรวจ announcement count/debounce |
| Layout | bounds/non-overlap ที่ 3 sizes | forced `AutoScaleMode.None`; ไม่ครอบคลุม actual DPI scaling |
| Long English/Thai | metadata คงข้อความและ `DrawToBitmap` ไม่ล้ม | ไม่ assert wrapping/clipping/readability ของ visible pixels |
| Credential metadata | sentinel scan ทั้ง controls/toolstrip | ไม่แทน full screen-reader/export/process inspection |
| Screenshots | names/dimensions/PNG valid | ไม่มี semantic assertions, exact-count check หรือ frozen dynamic state |

Mouse coverage ที่ `tests/UiFoundationTests.cs:251-255` เรียก protected `OnMouseClick` ผ่าน reflection และ assert เฉพาะ local `MouseClick` handler count; ไม่ใช่ physical hit-test และไม่ยืนยัน app action handler. ควรเรียกสิ่งนี้ว่า synthetic routing smoke เท่านั้น

## 15. Test Isolation Review

สิ่งที่ผ่าน:

- `NETSTUCK_TEST_ROOT` สร้างจาก OS temp + GUID และ resolve เป็น full path
- current tests ยืนยัน state/profile/MAC/Trace cache path อยู่ใต้ root และ state write อยู่ใต้ root
- startup network continuation ถูก suppress เมื่อ test root set
- independent packaged smoke, hidden-tab probe, accessibility/grid probes สร้าง temp roots นอก repoและลบสำเร็จ
- หลังทุก run ไม่พบ leftover `NetStuck` process
- clean baseline clone มี working tree สะอาดก่อน test

ปัญหา:

- `tests/UiFoundationTests.cs:67-76` และ `tests/UiFoundationSnapshot.cs:68-76` ใช้ `catch { }` รอบ recursive cleanup
- snapshot host return `0` หลัง finally โดยไม่มี post-cleanup assertion
- ถ้า handle/file lock หลุด cleanup อาจล้มเหลวแต่ suite/capture ยังผ่านและทิ้งข้อมูลชั่วคราว

ผลตรวจครั้งนี้ไม่พบ residue จริง แต่ test contract ต้องทำ cleanup failure observable จึงจะปิดข้อกำหนดเรื่อง false-green isolation ได้

## 16. Automated Test Reconciliation

Fresh current run:

| Metric | Result |
| --- | --- |
| Core | 16/16 |
| Feature/UI/integration | 91/91 |
| UI Foundation | 51/51 |
| Performance | 10/10 |
| Fixed cadence | 3/3 |
| Accelerated soak | 8/8 |
| Total | 179/179 |
| Skipped reported/detected | 0 |
| Exit | 0 |

Current fresh performance snapshot:

- warm UI startup: 1,374 ms
- Live Ping /24: 2,388 probes; worst UI dispatch 89 ms
- dual Traceroute worst UI dispatch: 62 ms
- working set: 79 MB
- cadence: Ping 10 samples at 250 ms vs 3 at 1000 ms; Trace 13 vs 4
- accelerated soak: 11.484 s; worst dispatch 26 ms; memory growth 9 MB

Clean baseline snapshot:

- total `128/128`, exit 0
- warm UI startup 1,218 ms
- Live Ping /24: 2,421 probes; worst dispatch 50 ms
- dual Trace worst dispatch 89 ms
- working set 78 MB
- cadence: Ping 11 vs 3; Trace 12 vs 4
- soak 11.368 s; worst dispatch 63 ms; memory growth 8 MB

Package-triggered current rerun ผ่าน `179/179` เช่นกัน. ตัวเลขทั้งหมดผ่าน absolute thresholds แต่เป็น single-run noisy measurements คนละช่วงเวลา จึง **ไม่ใช้กล่าวว่า Phase A เร็วขึ้นหรือช้าลง** และไม่แทน before/after profiling ที่ควบคุม environment

## 17. Accessibility Review

ผล runtime probe ของ current packaged form ที่ 96 DPI:

| Metric | Value |
| --- | ---: |
| Interactive controls | 127 |
| Explicit AccessibleName | 44 |
| Explicit AccessibleDescription | 44 |
| Missing explicit name | 83 |
| ToolStrip items | 9 |
| ToolStrip items with explicit name | 6 |
| DataGridViews | 11 |
| Explicit read-only grids | 4 |
| Grids with explicit accessible name | 1 |

สิ่งที่ยืนยัน:

- shell, Calculators, Event Log และ shared action buttonsมี metadata เพิ่มขึ้นจริง
- Calculator labels เป็น persistent/mnemonic และ focus returns หลัง validation
- state presenter ใช้ text markerร่วมกับ color
- normal palette contrast assertions ผ่าน
- credential sentinel ไม่ปรากฏใน accessibility names/descriptions
- Event Log read-only semantics และ selectable/copyable operations ผ่าน component tests

สิ่งที่ยังไม่ยืนยัน:

- UI Automation tree จริง, accessible relationships ที่ Narrator/other screen readers expose
- announcement order/frequency
- High Contrast runtime
- visible focus ในทุก action/screen
- app-wide label/name coverage
- physical keyboard/dropdown/mouse behavior

Accessibility blocker เฉพาะ foundation: determinate progress metadata สามารถประกาศค่าที่เป็นไปไม่ได้ และ Event Log อาจประกาศ filtered-empty ซ้ำ

## 18. State Model Review

`UiSemanticState` มี 11 states: `Idle`, `Loading`, `Running`, `Cancelling`, `Success`, `Warning`, `Error`, `Empty`, `FilteredEmpty`, `Unavailable`, `ValidationFailure`

Production references นอก foundation:

| State | References |
| --- | ---: |
| Idle | 2 |
| Success | 2 |
| Empty | 2 |
| FilteredEmpty | 1 |
| ValidationFailure | 2 |
| Loading | 0 |
| Running | 0 |
| Cancelling | 0 |
| Warning | 0 |
| Error | 0 |
| Unavailable | 0 |

Current consumersเหมาะกับ pilot states: Calculatorsใช้ Idle/Success/ValidationFailure และ Event Logใช้ Empty/FilteredEmpty. อย่างไรก็ตาม:

- `PrimaryActionEnabled`, `CancelActionEnabled` และ progress flags ถูก assertใน component test แต่ไม่ control action ใดจริง
- ไม่มี operation generation/state reducer/global registry
- generic catalog จึงไม่ปิด STATE-01/03/04
- `SetDeterminateProgress` clamp control value แต่ไม่ clamp announced value
- `SetState(... announce: true)` ไม่มี deduplication guard

คำตัดสิน: pilot state renderingใช้งานได้ แต่ operation-state foundation ยัง speculative และไม่ควรถูกอ้างว่า ready for adoption จนแก้ contract/tests

## 19. Rendering and Performance Review

Rendering checks ที่ผ่าน:

- bounds/non-overlap tests ที่ 1100x700, 1100x900, 1460x900
- screenshots ทั้ง 9 decode เป็น PNG และ exact dimensions ตรงชื่อ
- shell/Calculator/Event Logไม่มี overlapที่เห็นจาก evidence 96 DPI
- focus cue เห็นบน Calculate ใน 1100x900/1460x900 แม้ state screenshot ยัง idle
- app icon/logo resources present
- no missing-resource exception จาก build/test/smoke

Environment ที่ตรวจ:

```text
HighContrast=False
DpiX=96
DpiY=96
PrimaryScreen=1920x1200
WorkingArea=1920x1152
```

Performance suite และ 10-second soak ผ่าน; ไม่พบ leftover process. อย่างไรก็ตาม Phase Aเพิ่ม `Font` allocations ใน foundation/action/grid paths และ PERF-02 ถูก defer จึงไม่มี handle/GDI object before-after evidence. ไม่มี regression ที่พิสูจน์ได้จาก single runs แต่ยังห้ามสรุปว่า resource lifecycle ปิดแล้ว

## 20. Security and Privacy Review

Static scan ครอบคลุม added tracked lines และ Phase A untracked text:

- credential-value patterns: 0
- user-profile paths: 0
- email addresses: 0
- credential-term references: 12 ซึ่งเป็น identifiers/test/report claims ไม่ใช่ค่า secret
- IPv4 matches ที่เห็นใน capture/test fixturesใช้ RFC 5737 (`192.0.2.0/24`, `203.0.113.0/24`); `ipv4-other` lexical matches 4 จุดเป็น version-like dotted numbers ไม่ใช่ live address

Screenshot privacy:

- visible local/public valuesถูก sanitize เป็น `192.0.2.10` และ `203.0.113.10`
- fixture user/path เป็น synthetic; password/secret fields ว่าง
- PNG ทั้ง 9 มี chunk types `IHDR,sRGB,gAMA,pHYs,IDAT,IEND`
- text metadata chunks `tEXt/zTXt/iTXt`: 0
- ไม่เห็น username, home path, hostname, email, token หรือ credential จริงในการตรวจภาพ

Behavior/security:

- diff ไม่เปลี่ยน process argument/SSH/Telnet credential flow
- existing password-free argv/state/log testsผ่านใน 91/91 suite
- packaged Plink hash `06861c...fd6cc3`, Authenticode valid, signer Simon Tatham
- ZIP ไม่มี source/tests/capture utility

Dynamic date/time ใน footer ไม่ใช่ secret แต่ทำให้ screenshot hash ไม่ deterministic

## 21. Screenshot Evidence Review

Inventory:

| Group | Count | Sizes |
| --- | ---: | --- |
| Main shell | 2 | 1100x900, 1460x900 |
| Calculator normal-labelled-as-success | 3 | 1100x700, 1100x900, 1460x900 |
| Calculator validation | 1 | 1100x900 |
| Event Log empty | 2 | 1100x700, 1100x900 |
| Event Log filtered-empty | 1 | 1460x900 |
| Total | 9 | all exact |

Integrity checks:

- exactly 9 repository PNGs in Phase A directory
- all signatures/decode/dimensions valid
- no duplicate repository hashes
- all 9 referenced by implementation report
- no PNG text metadata chunks
- sanitized visible fixtures

Semantic failure:

- `tests/UiFoundationSnapshot.cs:40-41` จบที่ selected tab `Updates`
- lines 43-46 set Calculator values และ call `PerformClick()` ก่อน select Calculator
- `CapturePage` เพิ่ง select Calculator ที่ lines 47/99-104
- independent runtime probeยืนยัน hidden-tab clickคง `Idle`, subnet output blank, unit output `Result: —`; click หลัง select tabจึงได้ `Success`, subnet output nonblank, `Result: 1 Gbit`
- repository images 3 ภาพแสดง idle stringsตรงกับ probe ไม่ใช่ success
- implementation report lines 190-192 และ 275-277 เรียกภาพเหล่านี้ว่า success
- capture script lines 50-76 ตรวจเพียง expected filename/dimensions จึงรายงาน `9/9` โดยไม่เห็น semantic failure

Reproducibility failure:

- rerun capture ใน temporary mirror exit 0 และได้ 9/9 files
- only 2/9 fresh hashes match repository; 7 pilot images differ
- visual comparisonพบ state/layoutเหมือนเดิมแต่ footer clockเปลี่ยน เช่น repository `20:03:10` กับ fresh `21:33:02`
- `Sanitize()` ไม่ freeze `clockStatus`; `CapturePage()` pump events 120 ms ทำให้ timer repaint dynamic text

สรุป: image integrity ทางไฟล์ผ่าน แต่ evidence correctness และ deterministic reproducibility ไม่ผ่าน

## 22. Documentation Consistency Review

สอดคล้อง:

- architecture/testing/release docs ตรงกับ csc/.NET Framework build และ package model
- implementation reportเปิดเผยว่า non-96 DPI, High Contrast, screen reader และ full app rolloutยังไม่ verified
- audit traceability phase boundaryส่วนใหญ่ตรงกับ source

ไม่สอดคล้อง:

1. implementation report line 372 ระบุ branch `main`; actual branchคือ `ui/phase-a-foundations`
2. lines 190-192 และ 275-277 ระบุ Calculator screenshotsเป็น success แต่ภาพ/independent probeเป็น idle
3. line 185 เรียก captureว่า deterministic ทั้งที่ rerun hashesต่าง 7/9 เพราะ clock
4. line 79/283 ใช้ `9/9 captures PASS` โดยไม่ระบุว่า scriptตรวจเพียงชื่อและ dimensions
5. `docs/DEVELOPMENT.md:46` ยังบอกเพียง `NETSTUCK_TEST_STATE_PATH` และกล่าวว่า cachesอื่นไม่ redirect; ไม่ document seamใหม่ `NETSTUCK_TEST_ROOT`
6. packaged `TEST-REPORT.txt` รายงาน 128/128 แม้ package runปัจจุบันผ่าน 179/179

Documentation ต้องแก้หลัง implementation/evidence remediation; closure reviewนี้ไม่แก้เอกสารเหล่านั้นตามข้อห้าม

## 23. Diff Hygiene

ผลตรวจ:

- `git diff --check`: PASS, exit 0
- staged files: 0
- no new dependency/project/lock files
- no unrelated tracked filesนอก build/test/UI source scope
- no production test typesใน packaged assembly
- no source/test utilitiesใน package
- Phase A screenshotsอยู่แยกจาก `docs/ui-audit/`; captureไม่ overwrite audit evidence
- tracked implementation fingerprintคงที่ระหว่าง review

ข้อควรระวัง:

- `NetStuck.cs` มีทั้ง pilot changesและ global factory/palette changesในไฟล์เดียว จึงต้อง stageเป็น logical hunksหากแบ่ง commits
- working treeมี audit artifacts 19 filesที่มาก่อน Phase A; อย่ารวมกับ implementation commitโดยไม่ตั้งใจ
- closure reportนี้เป็น untracked fileใหม่หนึ่งไฟล์หลัง review
- package scriptใช้ ignored `artifacts/` เป็น output; pathsเหล่านี้ไม่ควร stage

## 24. Findings

### UIF-CR-001 — Calculator success screenshots are false-green

- **ID:** UIF-CR-001
- **Severity:** P1
- **Confidence:** HIGH
- **Category:** Test quality / Screenshot evidence
- **Affected files/surfaces:** `tests/UiFoundationSnapshot.cs`, `scripts/Capture-UiFoundations.ps1`, `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md`, three `pilot-calculators-*.png` normal-state images
- **Evidence:** snapshot captures Updates at lines 40-41, clicks hidden Calculator controls at 45-46, selects Calculators only at 47; capture script lines 50-76 checks only names/dimensions; independent probe shows hidden click=`Idle`/blank/`Result: —`, visible click=`Success`/nonblank/`Result: 1 Gbit`; images and report claims disagree
- **Observed behavior:** script exits 0 and prints `9/9` while all three claimed success screenshots are idle
- **Expected behavior:** success screenshots must be captured after the tab is visible and must fail generation unless presenter states/results equal expected success semantics
- **Impact:** closure report gives false confidence; screenshot review cannot prove Phase A success state or focus-after-success behavior
- **Root cause / likely cause:** WinForms `PerformClick()` on a hidden control does not execute the intended click path; no semantic precondition/postcondition is asserted before saving
- **Recommended remediation:** select Calculators, pump messages, set deterministic inputs, click, assert both `Success` states and exact sanitized outputs, then capture; make capture host exit nonzero on mismatch; regenerate 3 images and update hashes/report
- **Verification after fix:** fresh temp-mirror capture; hidden/visible probe; visual inspection; semantic assertion log; exact inventory/dimension/privacy scan
- **Commit blocker:** YES

### UIF-CR-002 — Screenshot evidence is not hash-reproducible

- **ID:** UIF-CR-002
- **Severity:** P2
- **Confidence:** HIGH
- **Category:** Evidence reproducibility
- **Affected files/surfaces:** `tests/UiFoundationSnapshot.cs`, seven pilot PNGs, implementation report hash/evidence claims
- **Evidence:** independent rerun produced 9/9 but only 2/9 SHA-256 matches; visual comparison shows clock changes; `Sanitize()` lines 81-97 does not freeze clock and `CapturePage()` pumps UI events
- **Observed behavior:** otherwise equivalent captures receive different bytes/hashes because current time is rendered in footer
- **Expected behavior:** sanitized evidence used for hash verification must freeze or mask all dynamic values
- **Impact:** another reviewer cannot reproduce recorded hashes or distinguish expected clock drift from unreviewed pixel drift
- **Root cause / likely cause:** production clock timer remains active during capture and the fixture has no injectable deterministic clock/status seam
- **Recommended remediation:** freeze timer and set a fixed synthetic timestamp/time-source text inside capture-only test seam; assert exactly 9 files and optionally compare deterministic hashes in CI
- **Verification after fix:** two consecutive clean temp-mirror captures must produce identical names, dimensions and hashes
- **Commit blocker:** YES

### UIF-CR-003 — Progress accessibility metadata contradicts the visual value

- **ID:** UIF-CR-003
- **Severity:** P2
- **Confidence:** HIGH
- **Category:** Accessibility / State model / Test correctness
- **Affected files/surfaces:** `src/NetStuck/NetStuck.UiFoundation.cs:367-375`, `tests/UiFoundationTests.cs:178-182`
- **Evidence:** maximum is sanitized to at least 1 and visual value clamps to `[0,safeTotal]`, but description uses raw `completed` and raw `total`; test passes `(12,10)` and explicitly requires `12 of 10`
- **Observed behavior:** progress bar shows 10/10 while assistive technology is told 12/10
- **Expected behavior:** visible and announced progress must use the same validated values and define zero/negative total semantics
- **Impact:** impossible progress can be announced to screen-reader users; passing test encodes the defect as contract
- **Root cause / likely cause:** clamp applied only to `ProgressBar.Value`, not to accessibility string; no real production consumer challenged the API
- **Recommended remediation:** calculate `safeCompleted` once and use it for both control and metadata; decide total<=0 behavior; add negative, zero, overflow and normal cases; remove API if not needed in Phase A
- **Verification after fix:** unit assertions for value/maximum/description equality and Narrator smoke when first production consumer is added
- **Commit blocker:** YES

### UIF-CR-004 — Cleanup failures are silently swallowed

- **ID:** UIF-CR-004
- **Severity:** P2
- **Confidence:** HIGH
- **Category:** Test isolation / False-green risk
- **Affected files/surfaces:** `tests/UiFoundationTests.cs:67-76`, `tests/UiFoundationSnapshot.cs:68-76`
- **Evidence:** both finally blocks wrap recursive delete in empty `catch { }`; snapshot returns 0 afterward
- **Observed behavior:** current runs happened to remove temp roots, but any future lock/handle/path cleanup failure is invisible and does not fail tests
- **Expected behavior:** owned temporary roots must be removed or cleanup failure must be reported and produce nonzero exit, with safe absolute-target validation retained
- **Impact:** CI/local suite may pass while leaking state, preserving sensitive fixtures or contaminating subsequent runs
- **Root cause / likely cause:** best-effort teardown implemented without verification/error propagation
- **Recommended remediation:** capture cleanup exception, report sanitized path/error class, increment failure/return nonzero; dispose all owned handles first; assert root absence
- **Verification after fix:** normal cleanup pass plus deliberate locked-file fixture that must fail visibly and leave a diagnostic
- **Commit blocker:** YES

### UIF-CR-005 — Global visual reach exceeds post-change evidence coverage

- **ID:** UIF-CR-005
- **Severity:** P2
- **Confidence:** HIGH
- **Category:** Scope / Behavior-preservation evidence
- **Affected files/surfaces:** `src/NetStuck/NetStuck.cs:86-94`, `:1846-1911`; all screens using shared ActionButton/Grid/palette helpers; `src/NetStuck/NetStuck.V103.cs`
- **Evidence:** palette aliases are app-wide, action factory styles Ping/Trace/DNS/MAC/Collector/dialog actions, grid helper styles multiple result grids; Phase A post-change screenshots cover only shell/Calculators/Event Log
- **Observed behavior:** functional regression suite passes, but visual/layout proof does not cover every surface changed by shared helpers
- **Expected behavior:** either adoption remains limited to named pilots or all globally affected surfaces receive proportional post-change bounds/interaction/visual checks
- **Impact:** non-pilot clipping, focus, role styling or grid rendering regressions may escape closure
- **Root cause / likely cause:** shared factory adoption was used as a convenient foundation seam without expanding evidence matrix to match actual blast radius
- **Recommended remediation:** scope global style wiring down to intended pilots, or add deterministic post-change evidence/tests for Live Ping, Trace, DNS, MAC/WAN, Collector and affected dialogs; preserve exact Trace geometry
- **Verification after fix:** compare all affected surfaces at minimum/standard/wide 96 DPI, then release DPI matrix; rerun 179+ checks
- **Commit blocker:** CONDITIONAL — resolve scope or evidence before merge; given current closure claim treat as blocker

### UIF-CR-006 — Event Log state announcements can be duplicated/noisy

- **ID:** UIF-CR-006
- **Severity:** P2
- **Confidence:** MEDIUM
- **Category:** Accessibility / Event wiring
- **Affected files/surfaces:** `src/NetStuck/NetStuck.cs:712-713`, `:733`, `:1455-1477`; Event Log filters/state presenter
- **Evidence:** Search/Level handlers call `ApplyLogFilter()`, BindingSource `ListChanged` also calls `UpdateLogStatePresenter()`, and `ApplyLogFilter()` calls it explicitly; filtered-empty always calls `SetState(..., announce:true)`
- **Observed behavior:** code has more than one path to announce the same state and no last-announced-state/text guard; automated tests check state visibility only
- **Expected behavior:** one meaningful announcement per state transition, not every redundant filter/list update or nonmatching row
- **Impact:** screen-reader users may receive repeated alerts while typing/filtering or while log rows change
- **Root cause / likely cause:** visual update and accessibility announcement are coupled without dedupe/debounce
- **Recommended remediation:** centralize update path, compare previous semantic state/message, announce only meaningful transitions; test notification count with an injectable notifier if practical
- **Verification after fix:** event sequence test plus manual Narrator/Windows screen-reader check
- **Commit blocker:** CONDITIONAL — must be resolved or manually disproved before accessibility sign-off

### UIF-CR-007 — Packaged test report is stale relative to the packaged build

- **ID:** UIF-CR-007
- **Severity:** P2
- **Confidence:** HIGH
- **Category:** Package / Release evidence
- **Affected files/surfaces:** `scripts/Package-NetStuck.ps1:26,52`, `docs/releases/v1.2.3/TEST-REPORT.txt`, packaged `TEST-REPORT.txt`
- **Evidence:** package run executes 179 checks, then copies a static report claiming 128/128 and omitting 51 UI Foundation assertions
- **Observed behavior:** package integrity hashes are internally correct, but human-readable verification record describes an older suite
- **Expected behavior:** packaged report must identify exact source revision, command, current suite counts and relevant environment/gaps
- **Impact:** release recipients/auditors receive misleading verification metadata
- **Root cause / likely cause:** static release document copied without regeneration or deliberate refresh gate
- **Recommended remediation:** before intentional release, refresh/generate report from verified run and include revision/fingerprint; do not version-bump merely for this review
- **Verification after fix:** unpack ZIP, reconcile report counts with runner log, verify manifest and packaged smoke
- **Commit blocker:** NO — release blocker YES

### UIF-CR-008 — Repository documentation does not match the actual branch/test seam

- **ID:** UIF-CR-008
- **Severity:** P3
- **Confidence:** HIGH
- **Category:** Documentation consistency
- **Affected files/surfaces:** `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md:372`, `docs/DEVELOPMENT.md:46`
- **Evidence:** implementation report says `main` while actual branch is `ui/phase-a-foundations`; development guide documents only `NETSTUCK_TEST_STATE_PATH` and says other caches are not redirected, while current code adds broader `NETSTUCK_TEST_ROOT`
- **Observed behavior:** a maintainer following docs receives stale repository/test-isolation information
- **Expected behavior:** reports identify actual branch and canonical development docs describe safe root precedence/scope
- **Impact:** reproducibility and future test isolation are harder to audit
- **Root cause / likely cause:** implementation report captured stale branch output and foundational seam documentation was left local to the report/tests
- **Recommended remediation:** correct branch statement and document `NETSTUCK_TEST_ROOT` without removing legacy override behavior unless intentionally changed
- **Verification after fix:** documentation grep/link review plus isolated path test
- **Commit blocker:** NO — correct before merge/documentation closure

### UIF-CR-009 — Foundation exposes declaration-only and unconsumed contracts

- **ID:** UIF-CR-009
- **Severity:** P2
- **Confidence:** HIGH
- **Category:** Architecture / Over-abstraction
- **Affected files/surfaces:** `src/NetStuck/NetStuck.UiFoundation.cs`, catalog portions of `tests/UiFoundationTests.cs`
- **Evidence:** 11 tokens are declaration-only; six of 11 semantic states have zero production references outside foundation; action enablement/progress flags have no production action consumer
- **Observed behavior:** tests enforce presence/completeness of APIs that Phase A does not use, including one contract with an accessibility defect
- **Expected behavior:** shared primitives should be driven by concrete Phase A consumers or intentionally deferred until a workflow needs them
- **Impact:** larger maintenance/API surface, premature compatibility burden and false confidence from catalog-only tests
- **Root cause / likely cause:** audit roadmap concepts were implemented as a broad foundation catalog before consumer-driven adoption
- **Recommended remediation:** retain only tokens/states needed by shell/Calculators/Event Log and shared action semantics, or document/implement a concrete immediate consumer; avoid new framework/dependency
- **Verification after fix:** usage scan, focused component tests and no regression in 179-check suite
- **Commit blocker:** CONDITIONAL — trim or justify before final architecture approval

## 25. Verification Commands

Substantive commands/probes และผล:

| Command / probe | Exit | Result |
| --- | ---: | --- |
| `git status --short` | 0 | initial inventory captured |
| `git branch --show-current; git rev-parse HEAD; git describe --tags --always` | 0 | branch/head/tag captured |
| `git rev-parse v1.2.3^{commit}; git rev-list --left-right --count v1.2.3...HEAD` | 0 | same commit, 0/0 |
| `git merge-base --is-ancestor v1.2.3 HEAD` | 0 | baseline ancestor confirmed |
| `git diff --stat; git diff --check` | 0 | 250+/88-, whitespace clean |
| `git diff --binary --no-ext-diff \| git hash-object --stdin` | 0 | `e40ca16a...` |
| `scripts/Test-NetStuck.ps1 -SoakSeconds 10` | 0 | current 179/179 |
| same test command in clean baseline clone | 0 | baseline 128/128 |
| `scripts/Build-NetStuck.ps1` | 0 | EXE 1.2.3.0 |
| `scripts/Package-NetStuck.ps1 -Version 1.2.3 -PlinkPath <verified>` | 0 | ZIP created; tests 179/179 |
| package manifest/ZIP inspection | 0 | 8/8 manifest; 9 files; no test/source leakage |
| Windows PowerShell assembly reflection | 0 | foundation types present, test types absent |
| packaged EXE smoke under temp test root | 0 | responsive window, graceful close, temp removed |
| `Capture-UiFoundations.ps1` in external temporary mirror | 0 | 9/9 names/dimensions; only 2/9 hashes reproduced |
| hidden-tab vs visible-tab `PerformClick` probe | 0 | Idle vs Success discrepancy confirmed |
| PowerShell AST parse for 4 scripts | 0 | 0 parse errors |
| UI environment probe | 0 | 96 DPI, High Contrast false |
| runtime accessibility census | 0 | 44/127 named; 83 missing |
| runtime DataGridView census | 0 | 4/11 read-only; 1/11 explicitly named |
| added-line/untracked privacy scan | 0 | 0 credential value shapes/profile paths/emails |
| PNG chunk scan | 0 | 9 valid; 0 text chunks |
| process cleanup check | 0 | 0 leftover NetStuck processes |
| SHA-256 inventory | 0 | source/test/evidence hashes recorded |

Exploratory failuresและ correction ที่ไม่ถูกนับเป็น verification pass:

- PowerShell 7 `ReflectionOnlyLoadFrom` ไม่รองรับและให้ `PlatformNotSupportedException`; ผลนั้นถูก discard และ rerunด้วย Windows PowerShell `Assembly.LoadFrom`, exit 0
- packaged-smoke payloadแรกถูก execution policy rejectก่อน process start จึงไม่มี process/exit code; revised safe .NET cleanup methodผ่าน exit 0
- exploratory PowerShell `foreach ... | Format-*` สองคำสั่งและ path-existence probeหนึ่งคำสั่งมี parser error exit 1; rerunด้วย intermediate `$rows` ผ่าน exit 0

การมี command exit 0 ไม่ได้ override semantic finding: capture script exit 0 แต่ screenshot success claim ยังผิด

## 26. Manual Verification Gaps

| Gap | Current status | Commit gate | Merge gate | Release gate |
| --- | --- | --- | --- | --- |
| 125%, 150%, 200% DPI | NOT VERIFIED | ไม่จำเป็นถ้า Phase A ระบุ gapตรงไปตรงมา | recommended | required |
| Mixed-DPI/multi-monitor transition | NOT VERIFIED | no | no | required |
| High Contrast live theme | NOT VERIFIED | no | recommended | required |
| Narrator/screen reader names/order/announcements | NOT VERIFIED | progress bugต้องแก้ก่อน | recommended for pilots | required |
| Physical mouse hit-testing/clicks | NOT VERIFIED | no after semantic automated fix | recommended | required |
| Native ComboBox dropdown keyboard/mouse | NOT VERIFIED | no | recommended | required |
| Tooltip behavior | NOT APPLICABLE to new pilot contract; no new tooltip | no | no | inspect if release checklist includes |
| Modal behavior | unchanged in Phase A | no | no | regression smoke for affected global button style |
| Long Thai/English visible wrapping | PARTIAL synthetic only | no | recommended | required for localization/readability claim |
| Non-pilot post-change visual regression | NOT VERIFIED | conditional due global reach | required unless scope reduced | required |
| Full default 8-hour soak | NOT RUN; 10-second accelerated run only | no | no | required by release policy when scheduling allows |

Dedicated DPI/High Contrast/screen-reader/physical interaction workสามารถเป็น follow-upได้ตาม phase rule แต่ต้องระบุเป็น release gates และห้ามใช้ automated 96-DPI smokeอ้างว่า verifiedแล้ว

## 27. Commit / Merge / Release Gates

| Gate | Status | Blocking IDs / evidence | Required timing |
| --- | --- | --- | --- |
| Correct success screenshot semantics | FAIL | UIF-CR-001 | before commit |
| Deterministic screenshot hashes | FAIL | UIF-CR-002 | before commit |
| Correct progress accessibility contract | FAIL | UIF-CR-003 | before commit or remove unconsumed API |
| Observable cleanup failures | FAIL | UIF-CR-004 | before commit |
| Global surface scope/evidence | OPEN | UIF-CR-005 | resolve before merge; treat current closure as blocked |
| Event Log announcement dedupe | OPEN | UIF-CR-006 | before accessibility sign-off/merge |
| Package test report current | FAIL for release | UIF-CR-007 | before release |
| Documentation consistency | OPEN | UIF-CR-008 | before merge |
| Minimal consumer-driven foundation | OPEN | UIF-CR-009 | architecture approval before merge |
| Automated current suite | PASS | 179/179 | rerun after remediation |
| Baseline comparison | PASS | 128/128 baseline | retain evidence |
| Build/package/smoke | PASS | exit 0, manifest 8/8 | rerun after remediation |
| Security/privacy | PASS for reviewed scope | zero secret value shapes; sanitized PNGs | rerun after recapture |
| 96-DPI target layouts | PASS | 3 target sizes | rerun after recapture |
| DPI/HC/screen-reader/manual | NOT VERIFIED | manual matrix | before release |

Overall:

- **Ready for commit: NO**
- **Ready for merge: NO**
- **Ready for release: NO**
- **Commit Blocker: YES**

## 28. Required Remediation

ลำดับขั้นต่ำก่อนขอ closure review ใหม่:

1. แก้ capture sequence ให้ select Calculator ก่อน click และเพิ่ม semantic state/output assertions
2. freeze clock/dynamic footer แล้ว regenerate 9 screenshots; ทดสอบสอง clean runsให้ hashesเท่ากัน
3. แก้/ตัด `SetDeterminateProgress` ให้ visualและ accessible valuesสอดคล้อง พร้อม boundary tests
4. ทำ cleanup failure observable/nonzero ใน testและsnapshot harness
5. ตัดสินใจ scope global palette/button/grid adoption: ลด scopeหรือเพิ่ม evidenceทุก surfaceที่ได้รับผล
6. dedupe Event Log announcement pathและเพิ่ม event-sequence test;ทำ manual screen-reader smoke
7. ลดหรือ justify declaration-only tokens/unconsumed statesด้วย concrete consumer
8. แก้ implementation report branch/screenshot/determinism claimsและ document `NETSTUCK_TEST_ROOT`
9. rerun full test/build/package/smoke/privacy/hash/link/diff checks
10. ก่อน release ให้ refresh packaged `TEST-REPORT.txt`, ทำ DPI/HC/Narrator/physical interaction matrixและ policy-required soak

Remediation ต้องไม่เปลี่ยน cadence, cancellation, stale-result, dual Trace, credential safety, exact Trace layout, package shape หรือ versionโดยไม่มี intentional requirement

## 29. Recommended Commit Structure

ยังไม่ควร commit จน blockerปิด หลังแก้แล้วแนะนำ logical commits:

1. `ui: add minimal shared foundation and safe test root`
   - foundation primitivesที่มี concrete consumers
   - build inclusion
   - test-root seam
   - focused component/isolation tests
2. `ui: apply shell and shared action semantics`
   - shell/accessibility/action roles
   - proportional evidenceสำหรับทุก global surfaceที่รับผล
3. `ui: migrate calculator pilot`
   - labels/layout/inline states/validation
   - corrected deterministic success/validation screenshots
4. `ui: migrate event log pilot`
   - labels/read-only grid/empty states/announcement dedupe
   - deterministic empty/filtered evidence
5. `docs: record Phase A implementation and verification`
   - corrected implementation report
   - screenshots/hashes
   - development/testing seam docs
   - closure reportตาม policyของ repository

เพราะหลาย logical areasอยู่ใน `NetStuck.cs` เดียวกัน ต้อง stage hunksอย่างระมัดระวังและ rerun suiteหลังแต่ละ commit. อย่ารวม `docs/ui-audit/` หรือ ignored `artifacts/` โดยไม่ได้ตั้งใจ และไม่ต้อง version-bumpจนมี intentional release decision

## 30. Final Repository State

Expected/verified stateหลังสร้าง closure reportและก่อนส่งมอบ:

- branchยัง `ui/phase-a-foundations`
- HEADยัง `2f35f72988fac0a44292a6bd69196e0842cbfc73`
- tag relationshipยัง `v1.2.3`, ahead/behind `0/0`
- staged path count `0`
- tracked implementation diff fingerprintยัง `e40ca16a2d07e693bafdedac02413f0134c150f3`
- Phase A source/test/evidence hashesเดิมไม่เปลี่ยน
- Git-visible deltaจาก closure reviewมีเพียง `?? docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md`
- no commit, tag หรือ push
- no leftover `NetStuck` process
- temporary capture/smoke/probe directoriesของ reviewerถูกลบ

Final short statusควรเท่ากับ initial statusบวกบรรทัด:

```text
?? docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md
```

## 31. Appendix: Hashes and Evidence

### Phase A source, test, script and report SHA-256

```text
a47ac834f92076bfe31ad89fe73ec6bd3408b858b25e4930f98aa9d0a18eff11  docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md
928c70202cc0ff76ad6d2a61b852f59c7d2ed2c192f77610183a060a21d10f20  scripts/Capture-UiFoundations.ps1
a2bdf824bb35a7c411c465f8d95aa0d456f0a60b3b8801bb0556e5723d6d3216  src/NetStuck/NetStuck.UiFoundation.cs
4f61ce873396b5738197c9820a145303596521460801be6afd082ea768c953cd  tests/UiFoundationSnapshot.cs
3966e0c2a949f9f768c1e4e615ae1c810ce3999ab593a934bcad84f3270e4048  tests/UiFoundationTests.cs
73b5afd209954c70220f26e95906e393da4da121e7d4186bd373e990f58fef27  scripts/Build-NetStuck.ps1
5f368186f68b0f326ac9d8f39027ae72fed3386717d2a13548c7c8fd907b49cf  scripts/Test-NetStuck.ps1
a287e3794ecb7573da25078e746cb612781b4b6287ae83b60183f045199350c4  src/NetStuck/NetStuck.cs
08f5c86d9662dd6e2ad97b79fe08cafe8d2c664d4238b92148abafc5540bbfe5  src/NetStuck/NetStuck.V103.cs
```

### Phase A screenshot SHA-256

```text
f68f0e4abf8b47249c37346f2c883e97e99b133d3460473f7960e3649057bb3a  docs/ui-foundations/screenshots/main-shell-1100x900.png
c7ec147a881362a353232c645985766ebda490be294f273d8791d0b1207e660e  docs/ui-foundations/screenshots/main-shell-1460x900.png
f2e79f7767eb6424253e947c2a2828692797bc7ef393801a1c365a2a3bcde458  docs/ui-foundations/screenshots/pilot-calculators-1100x700.png
ddcfc5c89eef98267aba2866f928d2936127d8148551f85f953339e693001381  docs/ui-foundations/screenshots/pilot-calculators-1100x900.png
3d4bbcc3777c0ff17db517a1f30a9ac275792fba2df6203d97d85bbd9bffbf7d  docs/ui-foundations/screenshots/pilot-calculators-1460x900.png
a3211fd9fa6d248462bb35394064910b46915d8dbc4adb08236069ed3de315d0  docs/ui-foundations/screenshots/pilot-calculators-validation-1100x900.png
c62fd6b1934dc8cd80e3ed2b8bb58a826dc11e9c00ddbc50cf6214534b76b0e0  docs/ui-foundations/screenshots/pilot-event-log-empty-1100x700.png
cf95ad1fb4927c4098d3c73fcadda72a2d0f5f66300a37ed454bc8553899e411  docs/ui-foundations/screenshots/pilot-event-log-empty-1100x900.png
be7611ce94439a6104386a9a9e8dceea3567f735abb8d14e09940dfee3781c4d  docs/ui-foundations/screenshots/pilot-event-log-filtered-empty-1460x900.png
```

### Audit evidence identity

```text
12f3f23fe24973e7d7f244091d597bf68023a5ef67e07d9c4b7cff48ea6eeeff  docs/UI_AUDIT_REPORT.md
Audit screenshot count: 18
Audit screenshot directory: docs/ui-audit/screenshots/
```

Audit screenshotsถูกอ่านเป็น pre-existing evidence ไม่ใช่ Phase A after-state และไม่ถูกแก้/overwrite

### Package identity

```text
546d6a0f984d6baa158aa3098a51bcfaba7ab09d4ad9dae7517142334c81ea5d  packaged NetStuck.exe
06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3  packaged tools/plink.exe
af45cbec053b9945794d35e651bfe73f47d562c90c9e9c9c763aaa4c3fc27935  NetStuck-v.1.2.3.zip
Manifest entries: 8/8 verified
Packaged file entries: 9
Unexpected test/source entries: 0
```

Closure report ไม่ใส่ self-hashไว้ภายในไฟล์เพื่อหลีกเลี่ยง self-referential digest; hashของรายงานควรคำนวณหลัง final integrity check
