# NetStuck Phase A P2 Closure Remediation Report

## 1. Executive Result

ผลการ remediation ใน canonical checkout คือ **READY_FOR_INDEPENDENT_REVIEW** เท่านั้น ไม่ใช่การรับรอง commit readiness ด้วยตนเอง หลักฐานที่ทำซ้ำในงานนี้ปิด `UIF-RR-001` ถึง `UIF-RR-007` (P2) และ `UIF-RR-008` (P3) ครบทั้งแปดรายการ โดยไม่เปิด Phase B, ไม่เพิ่ม feature/dependency, ไม่เปลี่ยน version และไม่ commit/tag/push

Automated closure gates ที่ผ่านประกอบด้วย corrected compiler argv provenance บน PowerShell สองตระกูล, capture transaction negative matrix `39/39`, canonical capture `9/9` ที่ hash ตรงกันครบห้ารอบ, Traceroute lifecycle `31/31` พร้อม targeted `50/50` ต่อ host, full suite PS5.1 `5/5` และ pwsh `3/3` ที่ `292/292`, development build, exact portable package, packaged native-window smoke และ residue/privacy scan ศูนย์

## 2. Scope

งานจำกัดอยู่ที่ reliability/tooling/documentation corrections ของ Phase A:

- ทำให้ provenance แทน actual compiler argv แบบหนึ่งต่อหนึ่ง
- ทำ capture publication เป็น transaction และกำจัด uncontrolled viewport state
- ทำ Traceroute Stop เป็น explicit run-owned task-quiescence boundary พร้อม actual-path lifecycle oracle
- ย้าย smoke/test state ไป unique OS-temporary root และลบเฉพาะ residue ที่พิสูจน์ ownership แล้ว
- เพิ่ม authoritative per-suite test floors และแก้ current maintainer documentation

ไม่มี Collector async redesign, Ping/Traceroute UI redesign, protocol/credential/schema change, new framework, new dependency, dark mode, version bump หรือ Phase B work

## 3. Initial Repository State

| Item | Verified initial value |
| --- | --- |
| Canonical repository | `C:\Projects\NetStuck` |
| Branch | `ui/phase-a-foundations` |
| HEAD | `2f35f72988fac0a44292a6bd69196e0842cbfc73` |
| Baseline | `v1.2.3` |
| Application/file version | `1.2.3.0` |
| Staged changes | `0` |
| `PRE_P2_CLOSURE_REMEDIATION_FINGERPRINT` | `d3b52451c898544650e3019e29829022f28ae07c` |
| Independent final re-review SHA-256 | `908fbb05ab22425a269e36accc6d32723f7237f55952e8743a0e671fe568d16c` |

`docs/UI_FOUNDATIONS_FINAL_CLOSURE_REREVIEW.md` ไม่ถูกแก้ไข

## 4. Finding Matrix

| Finding | Severity | Commit / merge / release gate | Evidence and root cause | Remediation | Verification | Post-status |
| --- | --- | --- | --- | --- | --- | --- |
| `UIF-RR-001` | P2 | Yes / Yes / Yes | Actual argv มี 26 items แต่ normalized model เดิมมี 27 เพราะ `/win32icon:` กับ path ถูกแยก และ actual/normalized lists สร้างอิสระจากกัน | ใช้ ordered argument specification เดียวสร้าง actual/normalized vectors และ binary canonical serialization | Provenance `19/19` บน PS5.1/pwsh; 26/26/spec 26; special-character, order, content, display และ relocation negatives PASS | `RESOLVED` |
| `UIF-RR-002` | P2 | Yes / Yes / Yes | catch เดิม restore backup เฉพาะเมื่อ target ไม่มี จึงปล่อย B และ backup หลัง post-publish failure | run-owned promotion/backup/failed quarantine พร้อม exact pre-hash restore และ combined rollback diagnostics | Actual publish cases pre/post/interrupted/rollback-failure ใน capture matrix `39/39`; final residue 0 | `RESOLVED` |
| `UIF-RR-003` | P2 | Yes / Yes / Yes | first-render WinForms scrollbar/focus/selection/scroll state ไม่ถูก normalize ในสอง scenario | กำหนด viewport invariant ต่อ scenario และรอ stable observable signature ก่อน/หลัง warm render | fresh isolated capture 5 รอบ: semantic/dimensions/PNG/privacy และ hash ตรง `9/9` ทุกครั้ง | `RESOLVED` |
| `UIF-RR-004` | P2 | Yes / Yes / Yes | cancellation ออกจาก scheduler ได้ก่อน non-cancellable ICMP tasks สิ้นสุด และไม่มี run-owned registry | track cycle/hop/service tasks, observe terminals/exceptions, derived async drain, block restart จน terminal | default lifecycle `31/31`; delayed/fault/timeout/restart/dispose/close; pending/callbacks 0 เมื่อ Stop สำเร็จ | `RESOLVED` |
| `UIF-RR-005` | P2 | Yes / Yes / Yes | harness เดิมใช้ narrow shared state path, suppress cleanup และไม่ครอบคลุม route table/provider/slow obsolete/active close | actual `MainForm` harness, controllable probe gate, unique `NETSTUCK_TEST_ROOT`, both bound tables, fail-closed cleanup | targeted PS5.1 `50/50`, pwsh `50/50`; default `31/31`; ไม่มี retry/residue | `RESOLVED` |
| `UIF-RR-006` | P2 | Yes / Yes / Yes | packaged-smoke outputs สี่ไฟล์อยู่ใต้ repo และเก็บ absolute operator-profile `CollectorFolder` | ยืนยันว่า test-owned, ลบ exact files/empty dirs, เพิ่ม OS-temp smoke + privacy/cleanup assertions และ runner residue scan | repo `state.json=0`; operator-profile/credential/test residue 0; smoke ผ่านทั้งสอง hosts | `RESOLVED` |
| `UIF-RR-007` | P2 | Yes / Yes / Yes | current docs นำ historical 247/capture/provenance/cache/process claims มาใช้เป็น current truth | แก้ architecture/development/testing/releasing และเพิ่ม current addendum โดยคง historical reports | docs เทียบกับ current implementation, suite, package และ manual-boundary evidence แล้ว | `RESOLVED` |
| `UIF-RR-008` | P3 | No / No / No | runner เช็ค positive results แต่ไม่มี per-suite discovered-count floor | authoritative ordered floor map เดียว, schema-3 JSON, reconciliation/exit failure และ negative fixture | runner infrastructure assertion จำลอง `1 < 2` แล้ว overall nonzero; current floors ทั้งสิบ suite PASS | `RESOLVED` |

## 5. Compiler Argument Provenance Root Cause

Build จริงส่ง `/win32icon:<absolute path>` เป็น argument เดียว แต่ normalized provenance เดิมประกอบ expression ในบริบท PowerShell array จน prefix และ path เป็นสอง array entries จึงได้ actual `26` เทียบ normalized `27` แม้ compiler ทำงานถูกต้อง การมี builder คนละชุดสำหรับ invocation และ provenance ทำให้ defect นี้ไม่ถูกตรวจพบด้วย suite เดิม `13/13`

การแก้ไม่ special-case เฉพาะ icon แต่ยก compiler invocation ทั้งหมดเป็น ordered vector จาก specification เดียว แต่ละ record มี `Role`, `Actual` และ `Normalized` แล้ว materialize vectors ทั้งคู่ด้วย cardinality assertion

## 6. Canonical argv Model

Canonical serialization คือ:

1. ASCII header `NetStuck.csc.argv.v2` ตามด้วย NUL
2. argument count เป็น little-endian signed Int32
3. ต่อ argument ตามลำดับ: zero-based index Int32, UTF-8 byte length Int32 และ exact UTF-8 bytes
4. ปฏิเสธ null argument และ embedded NUL

ไม่มี newline, culture conversion หรือ display quoting ใน identity Human-readable command line สร้างแยกเพื่อ diagnostics เท่านั้น Tests ครอบคลุม space, tab, quote, backslash, colon, equals, parentheses, underscore, Unicode, `/define:ONE;TWO`, output/icon/reference paths และพิสูจน์ว่า reorder/content mutation เปลี่ยน hash แต่ display style ไม่เปลี่ยน canonical identity

## 7. Corrected Provenance Results

| Identity | Final re-review value | Corrected current value | Note |
| --- | --- | --- | --- |
| Source input | `9ca605aeec681dfe07af807141d6bfc2bcf6ae2f01a8f8d08b1ac8e74c8aa8cb` | `0379f44bcf92a966b393631cf0296bf266ba09ce921774ea5606c94c33e27739` | Changed source/recipe bytes |
| Toolchain | `449f6846e8ad6a4734945765ff9e0a8503f4e2b952faf6362f34d011ce66680b` | same | Same compiler/runtime inputs |
| Build invocation | invalid 27-token `cde7a5f52ac9f88d2be8482d829ac76a30e31f8d8ca2c319d99616b2ef2551ab` | `134e486a3bb5df9ccce341ee52a68cfa046c036af4b6e7e24d8083b419791e94` | Portable normalized 26-item argv identity |
| Actual argv | not authoritative | `60826a9939832df86d44e7aa1c46068cdac16919e69aee5a94626adc97ac3585` | Exact absolute 26-item vector used by `csc.exe` |
| Canonical argv fixture | absent | `d8995cca43e11933ea457c13a7da1cb493369d30581e32a48413890afab08eaf` | Same on both hosts |
| Reference input | `199091183009849210a8da3c47db8b312d4e5e07f21c72269460e1efb22c4223` | same | Nine explicit framework references |
| Package input | `33838ab7720a8d98e35d71f28846eab710c6114b1bb32df4d48306a21552fd9f` | `448555f8f669cc020987ae09c23662490c1f9634a4a6ddbc0c90dbb5a9d3ac1e` | Fresh eight-file input |
| Package content | `d5b363adbbc5ad2cf421ff9d023ac6e517fe1eac53ac2ac0c9c2cf03dc883a52` | `ec6c0d372c8eabe20c7ebd840cae16c13205edfbb3c64598c11aa9e74a991cf2` | Fresh exact nine-file decompressed identity |
| ZIP container | `7596dff7520ad77c9ce8bb43dac39e7d2ffbd0a8492807f4c9470c81ab4cefce` | `ade3a775d95f28f00916950c9ebf275ccafddf8fe02e2a2faae98ccffa7e5744` | Separate non-content identity |

The review's one-token diagnostic `17a6640e...` remains historical and is not treated as canonical

## 8. Capture Publication Transaction

`Publish-ScreenshotSet` now validates complete candidate A/B-independent evidence before mutation, snapshots canonical pre-hashes, creates run-owned promotion/backup paths, moves the prior canonical whole set to backup, promotes the complete candidate, validates the published target and removes backup only after success

On any ordinary failure it removes/quarantines the newly promoted target, restores the whole prior set, validates exact pre-hashes and removes owned staging/backup state Failed publication is therefore observationally equivalent to no publication: no partial B, no mixed A/B และ no orphan transaction residue

## 9. Rollback Failure Tests

Actual publish logic is exercised with deterministic fault points:

- `BeforeCanonicalReplacement`: failure/nonzero, canonical unchanged, no backup/staging
- `AfterBackupBeforePromotion`: interrupted replacement restores the complete prior set and leaves no mixed set
- `PostPublishValidation`: exact pre-hashes restored and backup/promotion removed
- `RollbackRestore`: overall failure retains both publish-validation and rollback diagnostics, never returns PASS, and preserves recoverable original evidence until the fixture owner cleans it
- success path installs and revalidates one complete set

The broader `39/39` matrix also rejects stale candidate reuse, missing/unexpected scenario, privacy failure, semantic failure, bad CRC, trailing/truncated data, invalid critical/metadata chunks and invalid chunk order Final transaction residue scan returned `0`

## 10. Screenshot Nondeterminism Root Cause

The independent review localized differences to `main-shell-1460x900.png` (right vertical scrollbar, 9,904 pixels) and `pilot-event-log-empty-1100x900.png` (bottom horizontal scrollbar, 16,416 pixels). The screenshot harness had selected semantic state correctly but did not fully normalize first-render WinForms viewport state: focus, `AutoScrollPosition`, multiline textbox caret/scroll, grid current selection/first displayed row/horizontal offset and the final native scrollbar layout could depend on the preceding message/layout pass

The correction addresses this uncontrolled state directly; it does not crop/mask pixels, loosen hashes, reuse run 1, vote by majority or retry until matching

## 11. Deterministic Viewport Correction

Every scenario now owns initialization of selected page, requested window/client size, focus target, scrollable-control origin, multiline selection/caret, `DataGridView` selection/current cell/first row/horizontal offset, content and layout A bounded loop pumps posted messages/layout and requires the observable viewport signature to remain identical for four passes A warm `DrawToBitmap` is followed by the same state establishment and stability gate before the authoritative render Timeout is `2,500 ms` with explicit scenario diagnostic; no blind multi-second sleep determines acceptance

## 12. Five-run Capture Evidence

Command: `scripts/Capture-UiFoundations.ps1 -DeterminismRuns 5`

Result: five fresh isolated complete candidates, serial, `9/9` semantic PASS per run, expected dimensions, PNG structure/CRC/order PASS, privacy PASS and exact SHA-256 equality for every scenario Canonical images did not change

| Scenario | SHA-256 across runs 1–5 |
| --- | --- |
| `main-shell-1100x900.png` | `8f4380229712b3c36217730d56967e9b0167440b9bb60adc63fbbe134bf00fe1` |
| `main-shell-1460x900.png` | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` |
| `pilot-calculators-1100x700.png` | `d4fcb3963863b9184c1940040e6d080c3fa04389fd6deceffe1cc0c43a7f0734` |
| `pilot-calculators-1100x900.png` | `df064670144084a4d67aaf3ff4f7884bd17f0ecb450f066323b6cb37b987f553` |
| `pilot-calculators-1460x900.png` | `68067d3f8c0b70f3b8e8429acadcde8eee13c015afff7201387214c18c08fcc3` |
| `pilot-calculators-validation-1100x900.png` | `4ddbafc4b5afa2d848c81065dddb01ae182cc1c0f070e0f14d4591b4290ef074` |
| `pilot-event-log-empty-1100x700.png` | `0e46a07c4018833add76017adcfd237ba00c7f13b83b7909310209b5f279c26c` |
| `pilot-event-log-empty-1100x900.png` | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` |
| `pilot-event-log-filtered-empty-1460x900.png` | `a571c39d0cf17700b5dbb9d075c0bf6a9741667a1569a7d2af84719d65e9f2b3` |

## 13. Traceroute Probe-quiescence Contract

For a successful `StopTraceSessionAsync(R)` completion:

- scheduling for R has stopped and `StartedAfterStop=0`
- every already-started cycle/hop/service task is terminal and observed
- task exceptions/cancellations are observed
- no run callback remains and obsolete output cannot mutate bound tables/model
- run-owned task references are released before `ActiveRun` clears and START re-enables

Stop is asynchronous on the WinForms UI thread A timeout returns `false`, reports pending count, keeps the old run active/START disabled and continues terminal observation; it is never reported as a successful stop

## 14. In-flight Task Ownership

`TraceRunV123` owns a synchronized `HashSet<Task>`, `Quiesced`/`Completed` completion sources, cancellation source, configured probe timeout/max outstanding cycles and counters for started/observed/faulted/cancelled tasks, active callbacks, starts-after-stop and timeout state Every cycle task and every hop/service task is registered before use A synchronous continuation observes `Exception`, removes the task, updates counters and signals quiescence only when both task and callback counts reach zero

Non-cancellable ICMP work is not abandoned Cancellation stops new scheduling; already-started probes remain registered until terminal completion and their results are quarantined by run/session identity

## 15. Lifecycle Harness Expansion

The canonical harness now uses the actual `MainForm`, Traceroute page, route `DataTable`, event `DataTable`, UI synchronization context and private test seam `traceProbeGateV123` Each case owns one form, controlled gate, events, task registry and GUID-named `NETSTUCK_TEST_ROOT`; it asserts `state.json`, profiles, MAC cache and trace cache stay within that root Cleanup failures are assertions, not suppressed

Coverage includes normal completion, zero pending Stop, one delayed probe, multiple delayed probes, fault during Stop, explicit drain timeout/no false stopped state/eventual observation, obsolete run, new run after Stop, rapid Stop→Start blocking, page disposal, active form close, both bound tables on UI thread, no late mutation, exact task/callback observation, disposed window and removed state root

## 16. Traceroute Stress Evidence

| Host | Targeted cycles | Cycle-level assertions | Retry | Result |
| --- | ---: | ---: | --- | --- |
| Windows PowerShell 5.1.26100.3624 | 50/50 | 50/50 | none | PASS, exit 0 |
| pwsh 7.6.4 | 50/50 | 50/50 | none | PASS, exit 0 |

Each targeted cycle creates a fresh form/gate/state root, delays one controlled probe across Stop, drains it, verifies UI-only/no-late mutation, asserts `started=observed`, `pending=0`, `callbacks=0`, `StartedAfterStop=0` and cleans ownership The default full-suite lifecycle inventory is separately `31/31` per run

## 17. Sensitive-state Residue Analysis

The four findings were:

- `artifacts/r2-verification/packaged-smoke-final/state.json`
- `artifacts/r2-verification/packaged-smoke-state/state.json`
- `artifacts/r2-verification/packaged-smoke-state-final/state.json`
- `artifacts/r2-verification/packaged-smoke-state-ps5-final/state.json`

Each was produced by a prior packaged-smoke verification, existed only to isolate startup state, should have lived for one test invocation, contained a non-empty absolute operator-profile `CollectorFolder`, contained no password/enable-secret/private-key value, was test-owned, did not belong in repository scope and was safe to remove after ownership/hash/content classification Exact files and their now-empty owned directories were removed; no user-owned LocalAppData state was touched

## 18. State/Cache Isolation

New lifecycle/capture/smoke work uses a unique OS-temporary `NETSTUCK_TEST_ROOT`, which redirects `state.json`, `profiles.json`, `mac-vendors.json` and `trace-lookups.json` together Test owners validate containment and remove only the exact GUID/prefix-owned root; cleanup failure makes the test fail

Legacy `NETSTUCK_TEST_STATE_PATH` still redirects `state.json`; `trace-lookups.json` follows the effective state file directory, while profiles and MAC cache remain under LocalAppData New tests do not use this narrower legacy boundary Final scan after canonical suites, capture, package and both smoke hosts found repository `state.json=0`, operator-profile state residue `0`, credential residue `0`, temp smoke roots `0` and running NetStuck processes `0`

## 19. Test-count Floor Model

The only authoritative floor map is `$requiredSuiteMinimums` in `scripts/Test-NetStuck.ps1`:

| Mandatory suite | Minimum |
| --- | ---: |
| Test runner infrastructure | 10 |
| NetOpsCoreTests.exe | 16 |
| FeatureTests.exe | 93 |
| TracerouteLifecycleTests.exe | 31 |
| UiFoundationTests.exe | 63 |
| PerformanceTests.exe | 10 |
| PollingCadenceTests.exe | 3 |
| OvernightSoakTests.exe | 8 |
| Capture-UiFoundations infrastructure | 39 |
| Build provenance infrastructure | 19 |

More tests remain valid; a suite below its floor makes suite status, schema-3 JSON `Verdict`, reconciliation exit and process exit fail Diagnostic names expected/actual counts Negative runner fixture proves a simulated `1 < 2` suite cannot be green

## 20. Documentation Corrections

- `docs/ARCHITECTURE.md`: correct one-GUI-process/child-process model; Traceroute ownership/drain contract; test root/cache behavior; canonical argv boundary
- `docs/DEVELOPMENT.md`: one argument specification, binary identity, state isolation, floors/current 292, five-run capture transaction and packaged smoke usage
- `docs/TESTING.md`: schema-3 runner, exact suite coverage/floors/counts, targeted lifecycle/capture commands and manual-evidence limits
- `docs/RELEASING.md`: fail-closed packaged smoke command replaces an informal smoke instruction
- `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md`: new current P2 addendum supersedes, but does not rewrite, the historical 128/179/207/217/247 snapshots

All closure/re-review/remediation reports other than the explicitly requested implementation-report addendum remain unchanged In particular `docs/UI_FOUNDATIONS_FINAL_CLOSURE_REREVIEW.md` still hashes to `908fbb05ab22425a269e36accc6d32723f7237f55952e8743a0e671fe568d16c`

## 21. Cross-shell Verification

`scripts/Test-BuildProvenance.ps1` passed `19/19` under Windows PowerShell 5.1.26100.3624 and pwsh 7.6.4 Both computed the same source, toolchain, normalized build-invocation, canonical argv fixture and reference fingerprints The path-with-spaces/Unicode relocation kept icon/reference arguments atomic, checkout relocation preserved portable identity, binary mutation and compiler-argument mutation were detected, and display formatting had no effect on canonical hash

## 22. Full-suite Repeatability

| Host | Consecutive required runs | Result per run | Suites | Failed | Skipped | Infrastructure | Exit |
| --- | ---: | --- | --- | ---: | ---: | ---: | ---: |
| Windows PowerShell 5.1.26100.3624 | 5/5 | `292/292` | `10/10` | 0 | 0 | 0 | 0 |
| pwsh 7.6.4 | 3/3 | `292/292` | `10/10` | 0 | 0 | 0 | 0 |

ไม่มี concurrent capture และไม่มี retry Package verification ran one additional passing pwsh `292/292` suite Representative fresh package-run measurements: warm UI startup `1,025 ms`, `/24` worst UI dispatch `25 ms`, dual Traceroute worst dispatch `52 ms`, working set `82 MB`, overlap `6/3` slots, Live Ping cadence `11 vs 3`, Traceroute cadence `14 vs 4`, and 10-second soak duration `11.4135 s` / worst dispatch `13 ms` / memory growth `9 MB`

## 23. Build/Package Results

Development build: PASS, final build/package `NetStuck.exe` file version `1.2.3.0`, SHA-256 `d87a8d41840f0caceb840b8250fc6e7a373c8df3718f654c56a05fec8e49024f`

Portable package: PASS

- package inputs `8/8`; manifest lines `8/8`, mismatches `0`
- staged and extracted ZIP inventory `9/9`; decompressed hashes/content identity match
- package input fingerprint `448555f8f669cc020987ae09c23662490c1f9634a4a6ddbc0c90dbb5a9d3ac1e`
- package content identity `ec6c0d372c8eabe20c7ebd840cae16c13205edfbb3c64598c11aa9e74a991cf2`
- fresh ZIP size `842,610` bytes; SHA-256 `ade3a775d95f28f00916950c9ebf275ccafddf8fe02e2a2faae98ccffa7e5744`
- no source tests, screenshots, closure reports, state or credentials in the package inventory

Packaged EXE smoke passed under both PowerShell hosts: responsive native window, `CloseMainWindow` accepted, graceful exit `0`, no forced success, no process leak, no operator-profile/credential state and exact owned temp-root cleanup

## 24. Behavior Preservation

| Surface | Evidence | Result |
| --- | --- | --- |
| Shell | 63 foundation assertions, deterministic 1100/1460 captures | PASS automated |
| Calculators | real actions, inline validation/success, focus/tab/layout at three sizes | PASS automated |
| Event Log | empty/filtered/populated semantics, read-only/sort/copy contracts, deterministic captures | PASS automated |
| Ping | unchanged core/feature/performance/cadence contracts; `11 vs 3` representative cadence | PASS |
| Traceroute | two sessions, reached-target stop, scroll/selection, bounded cadence plus new lifecycle ownership only | PASS automated |
| Collector | AUTH1→AUTH2, literal `DOMAIN\username`, transport/export/queue tests unchanged | PASS automated |
| Portable folder | exact nine-file inventory, Plink/license and packaged startup | PASS |

The Traceroute change is lifecycle/reliability-only No new protocol, control, schema or feature semantics entered

## 25. Security/Privacy

- Collector password/enable secret remain absent from argv, persisted state, logs, reports and screenshots; existing stdin-only/redaction contract passes Feature tests
- packaged Plink 0.80 SHA-256 is `06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3`; Authenticode status `Valid`
- PuTTY license SHA-256 is `47fce8b739b17c3bed25e5356857a9848ee79f3eb17afc48c163ae74edace5dc`
- package manifest mismatch count `0`; repository test-state/operator-profile/credential residues `0`
- canonical screenshots remain privacy-safe; capture temp/backup/staging residue `0`

## 26. Manual Gates

| Gate | Status |
| --- | --- |
| Physical pointer | `NOT VERIFIED` |
| Native dropdown | `NOT VERIFIED` |
| Modal dialog | `NOT VERIFIED` |
| DPI 125% | `NOT VERIFIED` |
| DPI 150% | `NOT VERIFIED` |
| DPI 200% | `NOT VERIFIED` |
| High Contrast | `NOT VERIFIED` |
| Screen reader | `NOT VERIFIED` |

`DrawToBitmap`, metadata assertions, synthetic event routing and native-window startup smoke do not convert these gates to PASS

## 27. Files Changed

P2 remediation changed only these paths relative to the pre-remediation working tree:

- `scripts/NetStuck.BuildProvenance.ps1` — canonical argv specification/serialization and actual/normalized identities
- `scripts/Test-BuildProvenance.ps1` — 19-check special-character, mutation, display and relocation matrix
- `scripts/Build-NetStuck.ps1` — schema-2 argv provenance record
- `scripts/Package-NetStuck.ps1` — schema-3 suite-floor enforcement and argv/package provenance
- `scripts/Capture-UiFoundations.ps1` — transactional publication, rollback faults and N-run determinism gate
- `scripts/Test-NetStuck.ps1` — authoritative floors, schema-3 reconciliation and state-residue regression
- `scripts/Test-PackagedSmoke.ps1` — new fail-closed OS-temp native-window smoke
- `src/NetStuck/NetStuck.V103.cs` — run-owned Traceroute task registry and async quiescence
- `tests/TracerouteLifecycleTests.cs` — isolated actual-path lifecycle/stress oracle
- `tests/UiFoundationSnapshot.cs` — deterministic per-scenario viewport settlement
- `docs/ARCHITECTURE.md` — current process/lifecycle/provenance architecture
- `docs/DEVELOPMENT.md` — current build/test/capture/state guidance
- `docs/TESTING.md` — current inventory, floors, stress and manual boundaries
- `docs/RELEASING.md` — packaged smoke gate
- `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md` — current P2 addendum with historical preservation
- `docs/UI_FOUNDATIONS_P2_CLOSURE_REMEDIATION_REPORT.md` — this evidence report

Canonical PNG files were not changed No dependency/version file changed

## 28. Final Git State

| Item | Final value |
| --- | --- |
| Repository / branch / HEAD | `C:\Projects\NetStuck` / `ui/phase-a-foundations` / `2f35f72988fac0a44292a6bd69196e0842cbfc73` |
| Baseline / version | `v1.2.3` / `1.2.3.0` |
| `POST_P2_CLOSURE_REMEDIATION_FINGERPRINT` | `7e19299281c20775f0c7f3380862a689fc65dc1a` |
| Diff check | PASS, exit `0` |
| Staged changes | `0` |
| Commit/tag/push | none / none / none |
| Repository `state.json` | `0` |
| Capture/package transaction residue | `0` |
| P2 diagnostic residue | `0` |
| Temporary smoke roots / running NetStuck processes | `0` / `0` |

The post fingerprint was calculated with the required native binary-safe pipeline and differs from pre-remediation `d3b52451c898544650e3019e29829022f28ae07c`

## 29. Recommendation for Independent Re-review

Recommendation: **READY_FOR_INDEPENDENT_CLOSURE_REREVIEW**

All seven reported P2 findings and the P3 suite-floor finding have local reproduced `RESOLVED` evidence No known automated remediation blocker remains This report does not authorize a commit, merge, tag, push or version change An independent reviewer must re-run/inspect the closure evidence and decide commit readiness Manual pointer/dropdown/modal/DPI/High Contrast/screen-reader acceptance remains explicitly unresolved for the independent merge/release decision
