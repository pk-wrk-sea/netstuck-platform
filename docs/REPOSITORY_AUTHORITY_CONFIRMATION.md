# NetStuck — Repository Authority Confirmation & Path Portability Audit

วันที่ตรวจ: 2026-08-26 (Asia/Bangkok)
ลักษณะงาน: read-only provenance/path audit
ไฟล์ที่ได้รับอนุญาตให้สร้าง: `docs/REPOSITORY_AUTHORITY_CONFIRMATION.md` เท่านั้น

## 1. Executive Verdict

**Verdict: `AUTHORITATIVE_CONFIRMED`**

`C:\Projects\NetStuck` คือ authoritative continuation ของ NetStuck Phase A repository ด้วยหลักฐานอิสระที่ตรงกันครบ: Git identity, post-remediation tracked-diff fingerprint, immutable Round 1 report hash, screenshot hashes 9/9, working-tree inventory, V103 baseline blob และ package evidence

ไม่มี candidate repository อื่นที่ขัดแย้งกัน: path เดิม `C:\Codex\_Project\NetStuck` ไม่พบ และ `C:\Codex_Project\NetStuck` เป็น directory ว่างที่ไม่มี `.git`

ไม่พบ active runtime/build/test/script dependency ต่อ path เดิมหรือ checkout destination ปัจจุบัน จึงไม่ต้อง move, copy, merge หรือ repair directory ใด

**Ready for Closure Review Round 2: `YES`**
**Path remediation required before Round 2: `NO`**

## 2. Candidate Repository

| Item | Actual | Assessment |
| --- | --- | --- |
| Candidate | `C:\Projects\NetStuck` | authoritative candidate |
| Git top level | `C:/Projects/NetStuck` | exact normalized form of candidate |
| Git directory | `.git` | present |
| Old expected checkout | `C:\Codex\_Project\NetStuck` | not found |
| Non-Git workspace | `C:\Codex_Project\NetStuck` | exists, empty, no `.git` |

Repository content, `AGENTS.md` และ repository skill ถูกใช้เป็น authority; ไม่มีการใช้ non-Git workspace เพื่อ repair หรือเติมไฟล์

## 3. Git Identity

| Check | Expected | Actual | Status |
| --- | --- | --- | --- |
| Top level | `C:\Projects\NetStuck` | `C:/Projects/NetStuck` | MATCH |
| Branch | `ui/phase-a-foundations` | `ui/phase-a-foundations` | MATCH |
| HEAD | `2f35f72988fac0a44292a6bd69196e0842cbfc73` | `2f35f72988fac0a44292a6bd69196e0842cbfc73` | MATCH |
| Describe | `v1.2.3` | `v1.2.3` | MATCH |
| Tag at HEAD | `v1.2.3` | `v1.2.3` | MATCH |
| Last commit | expected baseline commit | `2f35f72 test: bypass legacy CLR shutdown recursion` | MATCH |
| Staged changes | none | none | MATCH |

Identity commands ทุกคำสั่งจบด้วย exit code `0`

## 4. Known Historical Anchors

หลักฐานที่ repository reports บันทึกไว้ก่อน relocation audit:

- post-remediation tracked-diff fingerprint: `5c335b1d132b2b953f00f242ddad6ff6aef4ab5d`
- Round 1 review SHA-256: `ae51dc56dc13101e86bd389ab6fcda6cdcb13ba4a1def560e3040491da9e5ca7`
- final screenshot SHA-256 set: 9 ค่า
- remediation package SHA-256: `a44442ce45fe32249a5effb081c13cbb9565dcf9300aeb5a60d6283e63194221`
- V103 final blob claimed equal to immutable baseline
- final Phase A state: branch `ui/phase-a-foundations`, HEAD `2f35f729...`, tag `v1.2.3`, no staged change

หลักฐานเหล่านี้ถูกคำนวณใหม่จาก candidate โดยไม่เชื่อ report เพียงอย่างเดียว

## 5. Implementation Fingerprint Reconciliation

Command:

```powershell
git diff --binary --no-ext-diff | git hash-object --stdin
```

| Item | Value |
| --- | --- |
| Known post-remediation fingerprint | `5c335b1d132b2b953f00f242ddad6ff6aef4ab5d` |
| Current pre-report fingerprint | `5c335b1d132b2b953f00f242ddad6ff6aef4ab5d` |
| Match | YES |
| Exit code | `0` |

Fingerprint นี้ครอบคลุม tracked working-tree diff ตาม Git semantics และไม่รวม untracked reports/screenshots รวมถึง authority report ใหม่นี้ การคำนวณซ้ำหลังสร้าง report ต้องและได้ค่าเดิม

## 6. Working-tree Inventory

### Initial inventory captured before authority-report creation

| Metric | Actual |
| --- | ---: |
| Tracked files | 42 |
| Untracked files | 35 |
| Modified tracked files | 5 |
| Staged files | 0 |
| Unexpected files | 0 |

ตัวเลข `42 tracked / 35 untracked` ตรงกับ relocation validation report; การ reconcile ด้านล่างยืนยันเนื้อหา ไม่ได้อาศัย count อย่างเดียว

### Modified tracked files

| Path | Status | Reconciliation |
| --- | --- | --- |
| `docs/DEVELOPMENT.md` | tracked modified | expected remediation documentation |
| `scripts/Build-NetStuck.ps1` | tracked modified | expected original Phase A build inclusion |
| `scripts/Package-NetStuck.ps1` | tracked modified | expected package-evidence remediation |
| `scripts/Test-NetStuck.ps1` | tracked modified | expected test-summary/capture integration remediation |
| `src/NetStuck/NetStuck.cs` | tracked modified | expected Phase A/remediation production change |

Tracked diff stat:

```text
docs/DEVELOPMENT.md          |  20 ++-
scripts/Build-NetStuck.ps1   |   1 +
scripts/Package-NetStuck.ps1 |  51 +++++++-
scripts/Test-NetStuck.ps1    | 122 +++++++++++++++---
src/NetStuck/NetStuck.cs     | 293 ++++++++++++++++++++++++++++++++++---------
5 files changed, 411 insertions(+), 76 deletions(-)
```

`git diff --check` passed. Cached diff stat/name-status are empty.

### Known Phase A/remediation paths

| Path | Actual status | Expected relationship |
| --- | --- | --- |
| `src/NetStuck/NetStuck.UiFoundation.cs` | untracked | expected new production source |
| `src/NetStuck/NetStuck.cs` | tracked modified | expected |
| `src/NetStuck/NetStuck.V103.cs` | tracked unchanged | expected restored-to-baseline result |
| `scripts/Build-NetStuck.ps1` | tracked modified | expected |
| `scripts/Test-NetStuck.ps1` | tracked modified | expected |
| `scripts/Capture-UiFoundations.ps1` | untracked | expected new capture script |
| `scripts/Package-NetStuck.ps1` | tracked modified | expected |
| `tests/UiFoundationTests.cs` | untracked | expected new test |
| `tests/UiFoundationSnapshot.cs` | untracked | expected new test/capture host |
| `docs/DEVELOPMENT.md` | tracked modified | expected |
| `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md` | untracked | expected |
| `docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md` | untracked | expected immutable review evidence |
| `docs/UI_FOUNDATIONS_REMEDIATION_REPORT.md` | untracked | expected remediation evidence |
| `docs/ui-foundations/screenshots/` | 9 untracked files | expected exact screenshot set |

Semantic grouping of the 35 untracked files:

- pre-existing audit evidence: 19 files (`UI_AUDIT_REPORT.md` + 18 screenshots)
- Phase A/remediation/review evidence and implementation: 16 files
- unexpected/temporary/generated untracked files: 0

## 7. Untracked-file Inventory

Inventory นี้เก็บก่อนสร้าง authority report; paths เรียงลำดับ, sizes เป็น bytes และ hashes เป็น SHA-256

| Relative path | Size | SHA-256 | Category | Reconciliation |
| --- | ---: | --- | --- | --- |
| `docs/UI_AUDIT_REPORT.md` | 189433 | `12f3f23fe24973e7d7f244091d597bf68023a5ef67e07d9c4b7cff48ea6eeeff` | Documentation | expected pre-existing UI audit artifact |
| `docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md` | 68739 | `ae51dc56dc13101e86bd389ab6fcda6cdcb13ba4a1def560e3040491da9e5ca7` | Review evidence | expected immutable Round 1 review artifact |
| `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md` | 46706 | `2e95dce33dcc6e0c9950fa95f1b614a40937f4b91de56f45bca68b6c1b385592` | Documentation | expected Phase A implementation artifact |
| `docs/UI_FOUNDATIONS_REMEDIATION_REPORT.md` | 22804 | `293e843896754ad892032f3bf198c8b0e68f13abf1c7be8377a799cfc35e1e73` | Review evidence | expected remediation artifact |
| `docs/ui-audit/screenshots/runtime-calculators-1100.png` | 50013 | `26d63bd79e43869e6b751fa756f7171b7456ca019962eaa9774c1a4aa1cf04a9` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-calculators-1460.png` | 51967 | `94d2e994570e453a50322a5f02b248ba4d71acd5e92c9933d62d607d5dd90de3` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-config-collector-1100.png` | 59205 | `e79033f238062efda2a0a43f81f8e06ebdf27ec221007c3f5dcaeb8f3c4ef482` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-config-collector-1460.png` | 64874 | `7d2edd25c9c6480318834d265b4b452e2cd503c90db80382445b39d7131002c2` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-dns-resolver-1100.png` | 42253 | `6d864b25edfd9aff4aa61f002c260abf4cba7ea1dd7b99fc1f6ea25e89d9f656` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-dns-resolver-1460.png` | 44622 | `86ba9c7deb588da68568f3cf5936204f51f7319b4ef333269a106e4c30a2de4b` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-event-log-1100.png` | 33917 | `249fb3ffc07b7bc53c503e96fea36530bf55288ba2b863784508a4e5883952e9` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-event-log-1460.png` | 35533 | `8bccfc275bb975050cb07feb840c359ab527679d00b5d67e880845b466bb3ec4` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-live-ping-1100.png` | 63190 | `47cf88bb962505ea5345cb804502f9fd58da78bfe8c4a8698f61d35bb056147a` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-live-ping-1460.png` | 66599 | `d6c7aee89d5b5553c92a25f81cb03ee204ed42443f15980900e64dfb528bd8bb` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-mac-wan-lookup-1100.png` | 41521 | `5aaaa6bd30a680e324460a6bc46f64e3ba7a55394b21d6c3855e6e9215352751` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-mac-wan-lookup-1460.png` | 43170 | `b29bf2e5700b81c0a67cd11e09b39ce7d73e305fb649e8c5d57fae6ed9cdc846` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-traceroute-1100.png` | 50543 | `b619187f995f0ff02c0cc98c6118af910269f63c2c233622d537b58a2a87cff2` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-traceroute-1460.png` | 53119 | `b7d7f748a2f769fbaf28de1d3424a4027d81b5b5a294b342c26765ab69e04d42` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-traceroute-protocol-open-1100.png` | 50553 | `fe39271003e73afb3393384003d1faa2b3c38082a9b025d698c43cdd6ea7eeb7` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-traceroute-protocol-open-1460.png` | 53126 | `c0d2551fc9b9128e61e766069f4ae1e9c0b3db3b00de8a82d667eaa43ba2bdbf` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-updates-1100.png` | 63327 | `ba703dcb2ca42c8ba3217d85ef0c196683b94488b9be81ddcdfb7a8ae5e8e36b` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-audit/screenshots/runtime-updates-1460.png` | 63707 | `f32c7ce2690c80ba29bc3ee66ca28115067f013183d21641277568148619e34b` | Screenshot evidence | expected pre-existing UI audit artifact |
| `docs/ui-foundations/screenshots/main-shell-1100x900.png` | 60622 | `8f4380229712b3c36217730d56967e9b0167440b9bb60adc63fbbe134bf00fe1` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/main-shell-1460x900.png` | 62876 | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-calculators-1100x700.png` | 47006 | `d4fcb3963863b9184c1940040e6d080c3fa04389fd6deceffe1cc0c43a7f0734` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-calculators-1100x900.png` | 58199 | `df064670144084a4d67aaf3ff4f7884bd17f0ecb450f066323b6cb37b987f553` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-calculators-1460x900.png` | 60268 | `68067d3f8c0b70f3b8e8429acadcde8eee13c015afff7201387214c18c08fcc3` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-calculators-validation-1100x900.png` | 55000 | `acd8f939637140eba614cd733fec4d669838cf6ac762d77eeb455adf7fba94a8` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-event-log-empty-1100x700.png` | 28565 | `0e46a07c4018833add76017adcfd237ba00c7f13b83b7909310209b5f279c26c` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-event-log-empty-1100x900.png` | 30540 | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` | Screenshot evidence | expected remediation artifact |
| `docs/ui-foundations/screenshots/pilot-event-log-filtered-empty-1460x900.png` | 32452 | `a571c39d0cf17700b5dbb9d075c0bf6a9741667a1569a7d2af84719d65e9f2b3` | Screenshot evidence | expected remediation artifact |
| `scripts/Capture-UiFoundations.ps1` | 18502 | `31c685c9097b34c99ada84f702ad3d6f446c752b0c9f022cb28da75d4c59e587` | Script | expected Phase A/remediation artifact |
| `src/NetStuck/NetStuck.UiFoundation.cs` | 19829 | `606233e0c39c4da7fc7231e959dc5ba7780f79dc19e3551f974072e03ae4a46c` | Production source | expected Phase A/remediation artifact |
| `tests/UiFoundationSnapshot.cs` | 20340 | `122544c43c74852f1d585f1335f1083a5749d8dc8a75949a3b97f812f202dd1c` | Test | expected Phase A/remediation artifact |
| `tests/UiFoundationTests.cs` | 37295 | `e5053e3d3cb75f85eb52de920d4eb8d511c39cdadd5c5c294b1be08cc5f996ed` | Test | expected Phase A/remediation artifact |

Category totals: Production source 1, Test 2, Script 1, Documentation 2, Review evidence 2, Screenshot evidence 27, Temporary/generated 0, Unexpected 0

## 8. Round 1 Report Hash Reconciliation

| Item | SHA-256 |
| --- | --- |
| Reported | `ae51dc56dc13101e86bd389ab6fcda6cdcb13ba4a1def560e3040491da9e5ca7` |
| Actual | `ae51dc56dc13101e86bd389ab6fcda6cdcb13ba4a1def560e3040491da9e5ca7` |
| Status | MATCH |

`docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md` ยังคง immutable ตาม remediation claim

## 9. Screenshot Hash Reconciliation

Current inventory มี exactly 9 direct PNG files และชื่อครบตาม expected set

| File | Expected SHA-256 | Actual SHA-256 | Status |
| --- | --- | --- | --- |
| `main-shell-1100x900.png` | `8f4380229712b3c36217730d56967e9b0167440b9bb60adc63fbbe134bf00fe1` | `8f4380229712b3c36217730d56967e9b0167440b9bb60adc63fbbe134bf00fe1` | MATCH |
| `main-shell-1460x900.png` | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` | MATCH |
| `pilot-calculators-1100x700.png` | `d4fcb3963863b9184c1940040e6d080c3fa04389fd6deceffe1cc0c43a7f0734` | `d4fcb3963863b9184c1940040e6d080c3fa04389fd6deceffe1cc0c43a7f0734` | MATCH |
| `pilot-calculators-1100x900.png` | `df064670144084a4d67aaf3ff4f7884bd17f0ecb450f066323b6cb37b987f553` | `df064670144084a4d67aaf3ff4f7884bd17f0ecb450f066323b6cb37b987f553` | MATCH |
| `pilot-calculators-1460x900.png` | `68067d3f8c0b70f3b8e8429acadcde8eee13c015afff7201387214c18c08fcc3` | `68067d3f8c0b70f3b8e8429acadcde8eee13c015afff7201387214c18c08fcc3` | MATCH |
| `pilot-calculators-validation-1100x900.png` | `acd8f939637140eba614cd733fec4d669838cf6ac762d77eeb455adf7fba94a8` | `acd8f939637140eba614cd733fec4d669838cf6ac762d77eeb455adf7fba94a8` | MATCH |
| `pilot-event-log-empty-1100x700.png` | `0e46a07c4018833add76017adcfd237ba00c7f13b83b7909310209b5f279c26c` | `0e46a07c4018833add76017adcfd237ba00c7f13b83b7909310209b5f279c26c` | MATCH |
| `pilot-event-log-empty-1100x900.png` | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` | MATCH |
| `pilot-event-log-filtered-empty-1460x900.png` | `a571c39d0cf17700b5dbb9d075c0bf6a9741667a1569a7d2af84719d65e9f2b3` | `a571c39d0cf17700b5dbb9d075c0bf6a9741667a1569a7d2af84719d65e9f2b3` | MATCH |

Result: **9/9 exact match**

## 10. Package Evidence

Exact remediation artifact ยังอยู่และระบุได้อย่างปลอดภัย:

| Artifact | Actual SHA-256 | Expected | Status |
| --- | --- | --- | --- |
| `artifacts/release/NetStuck-v.1.2.3.zip` | `a44442ce45fe32249a5effb081c13cbb9565dcf9300aeb5a60d6283e63194221` | same | MATCH |

`artifacts/remote-release/NetStuck-v.1.2.3.zip` มี hash `dd3c74e0f3a4cba2850239dd3abc0b9f3845a19098d78bb904b10129b575cca4` ซึ่งเป็นคนละ ignored historical release artifact ตาม path และไม่ขัดแย้งกับ remediation package. ZIPs ใต้ `artifacts/action-logs/` ไม่ใช่ package candidates

ไม่มีการ rebuild หรือ regenerate package ใน audit นี้

## 11. V103 Baseline Comparison

| Item | Git blob ID |
| --- | --- |
| Working `src/NetStuck/NetStuck.V103.cs` | `19b26409b0dc921ffe40428e2b0100f3e59a6c8c` |
| `v1.2.3:src/NetStuck/NetStuck.V103.cs` | `19b26409b0dc921ffe40428e2b0100f3e59a6c8c` |
| Status | MATCH |

`git diff --exit-code v1.2.3 -- src/NetStuck/NetStuck.V103.cs` returned `0` with no output

## 12. Old-path Search

Search scope: tracked and untracked text content; ignored artifacts, `.git` internals และ binary PNG content ไม่ถูกนำมาปนกับ active dependency scan

Unique matches:

| File:line | Variant(s) | Category | Assessment |
| --- | --- | --- | --- |
| `docs/UI_FOUNDATIONS_REMEDIATION_REPORT.md:23` | `C:\Codex\_Project\NetStuck` | HISTORICAL_EVIDENCE | recorded remediation location only |
| `docs/UI_AUDIT_REPORT.md:38` | both old/non-Git checkout variants | HISTORICAL_EVIDENCE | explains path resolution during audit |
| `docs/UI_AUDIT_REPORT.md:59` | `C:\Codex\_Project\NetStuck` | HISTORICAL_EVIDENCE | audit repository identity |
| `docs/UI_AUDIT_REPORT.md:1483` | `C:\Codex\_Project\NetStuck` | HISTORICAL_EVIDENCE | historical command transcript |
| `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md:65` | `C:\Codex\_Project\NetStuck` | HISTORICAL_EVIDENCE | original Phase A location record |
| `docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md:5` | `C:\Codex\_Project\NetStuck` | HISTORICAL_EVIDENCE | Round 1 repository record |
| `docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md:77` | both old/non-Git checkout variants | HISTORICAL_EVIDENCE | explains Round 1 path resolution |

Forward-slash variants returned zero matches. Broad parent-path patterns produced no additional unique lines. Classification totals:

- ACTIVE_RUNTIME: 0
- ACTIVE_BUILD: 0
- ACTIVE_TEST: 0
- ACTIVE_SCRIPT: 0
- CURRENT_DOCUMENTATION: 0 for pre-existing corpus
- HISTORICAL_EVIDENCE: 7 unique lines
- COMMENT_ONLY: 0
- UNEXPECTED: 0

Historical reports should remain unchanged; their path records are provenance evidence, not active hard-coding

## 13. Destination-path Search

Pre-report search for both forms returned zero matches:

```text
C:\Projects\NetStuck
C:/Projects/NetStuck
```

ดังนั้นไม่มี active build/test/runtime dependency ต่อ destination checkout. References in this authority report itself are `CURRENT_DOCUMENTATION` and are explicitly acceptable under the audit policy

## 14. Active Path Dependencies

### Repository-root resolution

Active entry points resolve repository content independently of checkout name/location:

| Path:line | Mechanism | Assessment |
| --- | --- | --- |
| `scripts/Build-NetStuck.ps1:7` | `Split-Path -Parent $PSScriptRoot` | location-independent repo root |
| `scripts/Test-NetStuck.ps1:9` | `Split-Path -Parent $PSScriptRoot` | location-independent repo root |
| `scripts/Capture-UiFoundations.ps1:8` | `Split-Path -Parent $PSScriptRoot` | location-independent repo root |
| `scripts/Package-NetStuck.ps1:8` | `Split-Path -Parent $PSScriptRoot` | location-independent repo root |
| `build_windows.bat:3` | `%~dp0scripts\Build-NetStuck.ps1` | location-independent batch entry point |
| `.github/workflows/release.yml:44,65` | `$env:RUNNER_TEMP`, `$PWD` + `Join-Path` | runner/repository-relative |
| `src/NetStuck/NetStuck.Features.cs:803` | `AppDomain.CurrentDomain.BaseDirectory` + `Path.Combine` | portable bundled Plink lookup |
| tests/capture | `Path.GetTempPath`, GUID-owned directories | checkout-independent temporary roots |

### Specific-drive references

| ID | File:line | Category | Impact | Recommended location-independent fix |
| --- | --- | --- | --- | --- |
| PORT-001 | `scripts/Build-NetStuck.ps1:9` | ACTIVE_BUILD | compiler assumes Windows is on drive `C:` | derive from `$env:WINDIR`; optionally probe Framework64/Framework |
| PORT-001 | `scripts/Test-NetStuck.ps1:13` | ACTIVE_TEST | same compiler assumption | same shared resolver |
| PORT-001 | `scripts/Capture-UiFoundations.ps1:12` | ACTIVE_SCRIPT | same compiler assumption | same shared resolver |
| PORT-002 | `src/NetStuck/NetStuck.Features.cs:804` | ACTIVE_RUNTIME fallback | installed Plink fallback assumes `C:\Program Files`; bundled portable path is checked first | derive fallback from `Environment.SpecialFolder.ProgramFiles` or `%ProgramFiles%` |
| INFO-001 | `tests/UiFoundationSnapshot.cs:344` | TEST_FIXTURE | display-only synthetic path; no filesystem dependency | optional use a drive-neutral synthetic label; no functional fix required |
| DOC-001 | `docs/DEVELOPMENT.md:6` | CURRENT_DOCUMENTATION | documents PORT-001 as current requirement | update only with a future PORT-001 implementation |

PORT-001/002 are real system-location portability follow-ups but are unrelated to repository relocation and do not invalidate Round 2 in the current environment. No code depends on `Codex`, `_Project`, `Projects`, developer username or absolute checkout location

## 15. Underscore Root-cause Assessment

**Conclusion: `NO_EVIDENCE`**

Evidence:

- no `_Project`, `Codex`, `Projects` or developer-username match exists in active `scripts/`, `tests/`, `src/`, build wrapper or CI workflows
- PowerShell scripts derive the repository root with `Split-Path -Parent $PSScriptRoot`; they do not split on `_`
- no repo-path regex, underscore tokenization, wildcard transformation, URI conversion or escaping rule treats underscore specially
- `-match` expressions in scripts parse test/status output; C# `Regex`/`Split` sites parse user input, host tokens, prompts or line endings, not checkout paths
- old `_Project` strings occur only as literal historical documentation evidence

ไม่มีหลักฐานเชิง source/test/script ว่า `_` ทำให้ relocation หรือ execution failure; การอ้าง underscore เป็นสาเหตุจะเกินหลักฐาน

## 16. Non-Git Workspace Assessment

`C:\Codex_Project\NetStuck`:

| Check | Actual |
| --- | --- |
| Exists | YES |
| Directory | YES |
| `.git` present | NO |
| Top-level entries | 0 |
| Overlapping NetStuck files | none |
| Classification | empty non-Git workspace placeholder |

Directory นี้ไม่ใช่ stale build output, incomplete copy หรือ competing repository ตาม evidence ปัจจุบัน และไม่ต้อง merge/copy/delete

`C:\Codex\_Project\NetStuck` ไม่พบ จึงไม่มี filesystem candidate ให้เปรียบเทียบหรือเคลื่อนย้าย

## 17. Provenance Anchor Matrix

| Anchor | Expected | Actual | Status | Significance |
| --- | --- | --- | --- | --- |
| Branch | `ui/phase-a-foundations` | `ui/phase-a-foundations` | MATCH | Git identity |
| HEAD | `2f35f72988fac0a44292a6bd69196e0842cbfc73` | same | MATCH | immutable baseline identity |
| `v1.2.3` | points at HEAD | points at HEAD; `git describe`=`v1.2.3` | MATCH | release baseline |
| Implementation fingerprint | `5c335b1d132b2b953f00f242ddad6ff6aef4ab5d` | same | MATCH | strongest working-state continuity anchor |
| Round 1 review SHA-256 | `ae51dc56dc13101e86bd389ab6fcda6cdcb13ba4a1def560e3040491da9e5ca7` | same | MATCH | immutable independent review evidence |
| `main-shell-1100x900.png` | `8f438022...00fe1` | same full hash in section 9 | MATCH | deterministic visual evidence |
| `main-shell-1460x900.png` | `fa9a29b0...ccd5` | same full hash in section 9 | MATCH | deterministic visual evidence |
| `pilot-calculators-1100x700.png` | `d4fcb396...0734` | same full hash in section 9 | MATCH | semantic success evidence |
| `pilot-calculators-1100x900.png` | `df064670...f553` | same full hash in section 9 | MATCH | semantic success evidence |
| `pilot-calculators-1460x900.png` | `68067d3f...fcc3` | same full hash in section 9 | MATCH | semantic success evidence |
| `pilot-calculators-validation-1100x900.png` | `acd8f939...4a8` | same full hash in section 9 | MATCH | validation evidence |
| `pilot-event-log-empty-1100x700.png` | `0e46a07c...26c` | same full hash in section 9 | MATCH | empty-state evidence |
| `pilot-event-log-empty-1100x900.png` | `7456ec41...e72f` | same full hash in section 9 | MATCH | empty-state evidence |
| `pilot-event-log-filtered-empty-1460x900.png` | `a571c39d...f2b3` | same full hash in section 9 | MATCH | filtered-empty evidence |
| V103 baseline blob | working blob equals baseline | `19b26409...a6c8c` equals `19b26409...a6c8c` | MATCH | independent restoration anchor |
| Working-tree inventory | 42 tracked / 35 untracked / 0 staged | 42 / 35 / 0; semantic set reconciled | MATCH | relocation inventory continuity |
| Remediation ZIP | `a44442ce...4221` | same full hash in section 10 | MATCH | supplemental package anchor |

Rubric summary:

- Anchor A — Git identity: MATCH
- Anchor B — Phase A implementation fingerprint: MATCH
- Anchor C — Round 1 report hash: MATCH
- Anchor D — Screenshot hashes: MATCH (9/9)
- Anchor E — Expected Phase A/remediation inventory: MATCH
- Anchor F — V103 baseline blob: MATCH

## 18. Limitations

- relocation task ไม่ได้บันทึก pre-move fingerprint ณ เวลาย้าย แต่ independent historical/current anchors ทุกตัวที่ตรวจได้ตรงกัน จึงไม่ลด verdict ตาม policy
- audit นี้ไม่ rerun build/tests/capture เพราะเป็น provenance task และการรันจะเขียน ignored artifacts; ใช้ hashes, Git objects และ current repository content แทน
- manual DPI/High Contrast/screen-reader gaps ที่ remediation report ระบุยังเป็น Round 2/release review scope; ไม่ใช่ repository-authority conflict
- ignored artifacts ไม่ถูกนับเป็น working-tree authority ยกเว้น exact remediation ZIP ที่ตรวจเป็น supplemental anchor
- filesystem inspection จำกัดตามสาม paths ที่ผู้ใช้กำหนด; ไม่มี broad disk-wide repository discovery ซึ่งไม่จำเป็นเมื่อ specified alternatives ไม่ขัดแย้ง

ข้อจำกัดเหล่านี้ไม่ก่อให้เกิด conflicting evidence

## 19. Round 2 Readiness

| Gate | Result | Basis |
| --- | --- | --- |
| Authority | `AUTHORITATIVE_CONFIRMED` | all six independent anchors MATCH |
| Active old-path dependency | none | only historical report references |
| Active destination hard-coding | none | pre-report search 0 |
| Unexplained working-tree mutation | none | 42/35 inventory semantically reconciled |
| Merge/copy required | no | old path absent; non-Git workspace empty |
| Portability issue invalidates review execution | no | PORT-001/002 are system fallback issues, not checkout dependencies |

**Ready for Closure Review Round 2: `YES`**

## 20. Required Follow-up

### Required before Round 2

None

### Non-blocking portability follow-up

1. PORT-001: centralize .NET Framework compiler resolution under `$env:WINDIR` with Framework64/Framework probing
2. PORT-002: derive installed Plink fallback from the Windows Program Files location while retaining bundled `tools\plink.exe` precedence
3. Preserve historical path references in prior reports; do not rewrite evidence solely to modernize paths

ดำเนิน follow-up เหล่านี้เป็น separate remediation task เท่านั้น เพราะ audit นี้ห้ามเปลี่ยน source/scripts/tests

## 21. Final Git State

Final integrity expectations/verification after report creation:

| Item | Final value |
| --- | --- |
| Branch | `ui/phase-a-foundations` |
| HEAD | `2f35f72988fac0a44292a6bd69196e0842cbfc73` |
| Describe | `v1.2.3` |
| Modified tracked files | same 5 paths captured before report |
| Initial untracked files | 35 |
| Final untracked files | 36, adding only `docs/REPOSITORY_AUTHORITY_CONFIRMATION.md` |
| Staged changes | none |
| Cached diff | empty |
| Tracked diff fingerprint excluding authority report | `5c335b1d132b2b953f00f242ddad6ff6aef4ab5d` |
| Production/source files changed by this task | none |
| Tests/scripts changed by this task | none |
| Git index/config/remotes changed | none |
| Commit/tag/push | none |
| Filesystem move/copy/merge/delete | none |

Final authority result: **`AUTHORITATIVE_CONFIRMED`**
Round 2 readiness: **`YES`**
