# NetStuck Phase A Independent Final Closure Re-review

Review date: 2026-08-27 (Asia/Bangkok)  
Review mode: independent, read-only implementation review  
Verdict: **NOT_READY**

## 1. Executive Verdict

Phase A ยังไม่พร้อมสร้าง commit checkpoint แม้ผล runtime ส่วนใหญ่แข็งแรงมาก: targeted Traceroute stress ผ่าน 25/25 invocation ต่อ host, full suite ผ่านครบ 5 รอบบน Windows PowerShell 5.1 และ 3 รอบบน PowerShell 7 ที่ `247/247`, development/package build สำเร็จ, ZIP content ถูกต้อง และ packaged EXE smoke ผ่าน

เหตุที่ verdict เป็น **NOT_READY** คือ gate ระดับ commit ยังล้มเหลวจริง:

- historical `UIF-FR-001` ปิด P1 off-UI crash ได้ แต่ยังไม่ปิดความหมายของ task quiescence/drain ที่ finding เดิมกำหนด จึงเป็น `PARTIALLY_RESOLVED`;
- compiler invocation fingerprint ไม่แทน actual argv แบบหนึ่งต่อหนึ่ง;
- capture publish ไม่ rollback เมื่อ failure เกิดหลัง target ถูกแทนที่;
- fresh capture Run A/Run B ต่างกัน 2/9 ไฟล์จาก scrollbar state;
- Traceroute สามารถ re-enable Start ก่อน pending ICMP tasks หมด timeout และ canonical lifecycle harness ไม่พิสูจน์ slow/obsolete/active-close paths;
- ignored smoke state เดิม 4 ไฟล์ยังเก็บ absolute operator-profile path;
- current authoritative documentation มี claim ที่การตรวจครั้งนี้ disproved.

Finding count: `P0=0`, `P1=0`, `P2=7`, `P3=1`. ไม่มี production source, tests, scripts, existing screenshots, Git index, branch, tag, config หรือ remote ถูกเปลี่ยนโดย reviewer

## 2. Scope

ตรวจเฉพาะ canonical checkout, source/tests/scripts ที่ต่างจาก `v1.2.3`, current/historical reports, runtime evidence ที่สร้างใหม่ใน isolated mirror, package bytes และ Git state งานนี้ไม่แก้ implementation และไม่ติดต่อ production device/credential

Phase A visual scope ที่ตรวจคือ application shell, Calculators และ Event Log. การเปลี่ยน `NetStuck.V103.cs` ถูกประเมินเป็น narrow Traceroute reliability correction ไม่ใช่ visual adoption

## 3. Canonical Repository

| Property | Observed | Result |
| --- | --- | --- |
| Repository | `C:\Projects\NetStuck` | PASS |
| Branch | `ui/phase-a-foundations` | PASS |
| HEAD | `2f35f72988fac0a44292a6bd69196e0842cbfc73` | PASS |
| Baseline/tag at HEAD | `v1.2.3` | PASS |
| App/file version | `1.2.3` / `1.2.3.0` | PASS |
| Historical final-review SHA-256 | `82809d17e1245a0f03fd340e9435165082087578653f635a40b3a3122f52b409` | PASS |

Repository authority และ path ถูกตรวจสด ไม่ได้อนุมานจากรายงานเก่า

## 4. Initial Git State

เก็บ state นี้ก่อนสร้างรายงาน:

- modified tracked paths: 8;
- untracked paths: 43;
- staged paths: 0;
- tracked diff: 1,070 insertions, 192 deletions;
- `git diff --check`: exit 0; มีเพียง LF-to-CRLF warnings จาก working-tree policy;
- ignored pre-existing roots/items: `artifacts/` และ `fake-plink-auth-count.txt`;
- prohibited Git operations: ไม่ได้รัน.

Modified tracked paths คือ `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT.md`, `docs/TESTING.md`, `scripts/Build-NetStuck.ps1`, `scripts/Package-NetStuck.ps1`, `scripts/Test-NetStuck.ps1`, `src/NetStuck/NetStuck.V103.cs`, `src/NetStuck/NetStuck.cs`

## 5. Initial Implementation Fingerprint

`REREVIEW_INITIAL_IMPLEMENTATION_FINGERPRINT = d3b52451c898544650e3019e29829022f28ae07c`

ค่านี้มาจาก binary-safe native pipeline `git diff --binary --no-ext-diff | git hash-object --stdin` ก่อนสร้างรายงาน และตรงกับ expected post-remediation fingerprint

## 6. Historical Finding Reconciliation

| Finding | Previous status | Claimed remediation | Independent evidence | Final status |
| --- | --- | --- | --- | --- |
| UIF-CR-009 | PARTIALLY_RESOLVED | restore dead legacy delta; remove dead foundation contracts | `BuildPingPageLegacyV102` current/baseline normalized blocks equal, SHA-256 `f428731a…`; production deferred-contract hits 0; all remaining primitives have consumers | RESOLVED |
| UIF-R2-004 | PARTIALLY_RESOLVED | enforce PNG order/singletons/contiguous IDAT | state machine checks exact order/CRC/EOF; malformed matrix 27/27 on both hosts | RESOLVED |
| UIF-R2-005 | PARTIALLY_RESOLVED | inventory every build input | ten repository inputs, six production sources, toolchain/references/package layers are enumerated; untracked foundation source changes source identity | RESOLVED |
| UIF-FR-001 | RESOLVED (claimed) | UI context/dispatch, obsolete-run guards, lifecycle stress | P1 bound-table crash path is fixed and 50 focused invocations pass; however cancellation can bypass `Task.WhenAll(pending)` and harness does not cover slow provider/active-close/drain | PARTIALLY_RESOLVED |
| UIF-FR-002 | RESOLVED (claimed) | structural PNG parser and negatives | actual malformed ordering/truncation/critical-chunk cases all reject under both hosts | RESOLVED |
| UIF-FR-003 | RESOLVED (claimed) | restore dead legacy visual line | legacy method is baseline-equivalent with zero Phase A delta | RESOLVED |
| UIF-FR-004 | RESOLVED (claimed) | binary-safe Git diff fingerprint | PS5.1, pwsh and native value all equal `d3b52451…` | RESOLVED |
| UIF-FR-005 | RESOLVED (claimed) | `/noconfig`, `/nostdlib+`, explicit references/toolchain | original implicit-input gap is closed; new argument-boundary defect is tracked separately as `UIF-RR-001` | RESOLVED |
| UIF-FR-006 | RESOLVED (claimed) | current architecture/testing inventory and manual matrix | original omissions/counts/manual matrix are corrected; separate current-doc inaccuracies are `UIF-RR-007` | RESOLVED |

เพราะ `UIF-FR-001` ยังไม่เป็น `RESOLVED` ตามขอบเขต finding เดิม commit-ready verdict จึงถูกห้ามโดย policy ของงานนี้

## 7. Final Blocker Remediation Review

Verified claims:

- off-UI `DataTable`/bound `DataGridView` crash mechanismได้รับ narrow source correction;
- pause check occurs before pending-completion application;
- baseline tests remain byte-identical;
- test runner reconciles native exit/stdout/stderr, suite inventory and JSON verdict;
- PNG parser fixes historical ordering false-green;
- dead legacy/foundation deltasถูกลบ;
- source/toolchain/reference/package identities are separated;
- full-suite repeatability and smoke claims reproduced.

Claims not accepted:

- full worker quiescence on Stop/Close;
- byte-deterministic fresh screenshot set;
- failed post-promotion capture cannot replace canonical evidence;
- `BUILD_INVOCATION_FINGERPRINT` faithfully models actual argv;
- no sensitive smoke-state residue remains.

## 8. Traceroute Root-cause Review

Historical attribution is supported. The button event at `NetStuck.V103.cs:459` calls the actual `RunTraceSessionAsync`. The old continuation could resume without a WinForms synchronization context, allowing `AddTraceEvent` to mutate `EventTable` off the UI thread and synchronously notify a bound grid

Current correction establishes `WindowsFormsSynchronizationContext` before the first await (`869-978`), applies completed cycles on that context (`910-918`), carries run CTS identity through DNS/provider callbacks (`1180-1308`), and synchronously dispatches `AddTraceEvent` when `InvokeRequired` (`1412-1436`). Close/dispose/run-identity guards stop obsolete results from mutating bound UI. This is a real root-cause correction, not merely a probability reduction or catch-and-ignore mask

## 9. Traceroute Lifecycle Correction

Acceptable parts:

- pause is checked before draining completed results;
- new 80 ms delay is cancellation-aware pause polling, not a retry;
- only disposal-specific exceptions around `Control.Invoke` are conditionally suppressed;
- all 15 V103 diff blocks are narrow lifecycle/UI-dispatch corrections; unrelated visual redesign blocks = 0.

Residual boundary:

- `ProbeHopV103Async` has no cancellation token and `Ping.SendPingAsync` can run until timeout (`1162-1177`);
- Stop cancellation thrown from `Task.Delay`/`WaitForTraceWakeV105` reaches the outer catch (`906`, `949`, `956`) and bypasses `Task.WhenAll(pending)` (`952-954`);
- `finally` clears/disposes CTS and re-enables Start (`961-968`) while abandoned old-generation probes may still be live;
- run-local overlap is capped 2–8 (`651-656`), but rapid slow-target stop/restart can accumulate old-generation work beyond that bound.

Old results cannot mutate UI because run/disposal guards are sound. Therefore the historical P1 crash is corrected, but literal worker/task quiescence is not

## 10. Targeted Traceroute Stress

ทุก invocation รัน serial, ไม่มี retry, ใช้ isolated `%TEMP%` wrapper และตรวจ residue หลัง host batch

| Host | Cycles | Assertions | Passed | Failed | Exceptions | Exit/residue |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Windows PowerShell 5.1.26100.3624 | 25 | 150 | 150 | 0 | 0 | 25 exits 0; residue 0 |
| PowerShell 7.6.4 | 25 | 150 | 150 | 0 | 0 | 25 exits 0; residue 0 |

This proves loopback UI-event mutation, pause, observable stop/restart and post-close event stability. It does not prove slow public-provider callbacks, route-table mutation thread, active-run close, or pending-probe drain

## 11. Full-suite Repeatability

Runs were serial in an isolated repository mirror. No capture ran concurrently

| Run | Host | Suites | Passed/Total | Failed | Skipped | Infra | Exit |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| PS5.1-1 | Desktop 5.1.26100.3624 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| PS5.1-2 | Desktop 5.1.26100.3624 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| PS5.1-3 | Desktop 5.1.26100.3624 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| PS5.1-4 | Desktop 5.1.26100.3624 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| PS5.1-5 | Desktop 5.1.26100.3624 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| pwsh-1 | Core 7.6.4 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| pwsh-2 | Core 7.6.4 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |
| pwsh-3 | Core 7.6.4 | 10/10 | 247/247 | 0 | 0 | 0 | 0 |

Representative measurements: PS5.1 warm startup 969 ms, `/24` worst UI dispatch 18 ms, dual trace 40 ms, working set 76 MB, soak 11.232 s / 19 ms / 8 MB; pwsh warm startup 931 ms, `/24` 21 ms, dual trace 34 ms, working set 75 MB, soak 11.479 s / 14 ms / 8 MB. Cadence was 11 vs 3 Live Ping completions and 14 vs 4 Traceroute samples for 250 ms vs 1000 ms

## 12. Cross-host Reconciliation

| Property | PS5.1 | pwsh | Match |
| --- | ---: | ---: | --- |
| Mandatory suites | 10/10 | 10/10 | YES |
| Baseline checks | 130/130 | 130/130 | YES |
| Total | 247 | 247 | YES |
| Passed/failed/skipped | 247/0/0 | 247/0/0 | YES |
| Runner infrastructure | 8/8 | 8/8 | YES |
| Traceroute lifecycle | 6/6 | 6/6 | YES |
| UI foundation | 63/63 | 63/63 | YES |
| Capture infrastructure | 27/27 | 27/27 | YES |
| Provenance infrastructure | 13/13 | 13/13 | YES |
| Overall | PASS | PASS | YES |

All eight runs have one identical per-suite count/status signature

## 13. Baseline Preservation

The baseline corpus is `130/130`: NetOpsCore 16, Feature 93, Performance 10, PollingCadence 3 and OvernightSoak 8. Five core test blobs plus `FakePlink.cs` and `UiV103ActiveSnapshot.cs` match `v1.2.3` exactly; tracked test diff vs baseline is empty. No skip/ignore/preprocessor weakening was found

Historical `128/128` remains accurately historical: two unchanged Feature assertions were omitted from the older output arithmetic, not deleted from current behavior

## 14. Dead Legacy/Foundation Review

`BuildPingPageLegacyV102` is unreachable and baseline-equivalent: current/baseline normalized length 13,090 characters, SHA-256 `f428731aaa585582ee59e1444db88e961df3ea112bb20ad3833f0fce9fc6576d`, Phase A delta 0

Production contains no deferred `Running/Loading/Cancelling/Warning/Error/Unavailable` state contract, determinate-progress API, unused action-enablement flags, speculative dialog tokens or test-only public foundation API. Remaining tokens/helpers have production consumers; `UiFoundationTests` also asserts removed-contract absence. `UIF-CR-009` is resolved

## 15. PNG Structural Validator

The parser validates signature, bounds, CRC, allowed critical/ancillary chunks, `IHDR` first/single/length 13, `IEND` terminal/single/length 0, at least one contiguous `IDAT`, singleton `sRGB/gAMA/pHYs` before first IDAT, and exact EOF. Canonical, Run A and Run B all decode at expected dimensions and use `IHDR,sRGB,gAMA,pHYs,IDAT,IEND`

Result: **PASS** for structural state machine and historical `UIF-FR-002`/`UIF-R2-004`

## 16. PNG Negative Matrix

Standalone execution under both hosts returned 27/27, exit 0. Every requested malformed fixture was rejected:

| Case | PS5.1 | pwsh |
| --- | --- | --- |
| pHYs/gAMA/sRGB after IDAT | PASS | PASS |
| duplicate IHDR | PASS | PASS |
| duplicate singleton metadata | PASS | PASS |
| non-contiguous IDAT | PASS | PASS |
| bad CRC | PASS | PASS |
| truncated chunk | PASS | PASS |
| trailing bytes/chunk after IEND | PASS | PASS |
| early IEND/missing image data | PASS | PASS |
| unexpected ancillary/critical chunk | PASS | PASS |
| stale semantic candidate/current-run isolation | PASS | PASS |

The existing stale negative stops before promotion; it must not be interpreted as a post-promotion rollback test

## 17. Canonical Screenshot Validation

All nine files are direct children, decode as PNG, pass structural validation, have expected dimensions/scenario associations, and were visually inspected. Visible values are documentation-range addresses, fixed synthetic time and synthetic UI content; no credential or operator identity is visible

| File | Dimensions | SHA-256 |
| --- | --- | --- |
| `main-shell-1100x900.png` | 1100x900 | `8f4380229712b3c36217730d56967e9b0167440b9bb60adc63fbbe134bf00fe1` |
| `main-shell-1460x900.png` | 1460x900 | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` |
| `pilot-calculators-1100x700.png` | 1100x700 | `d4fcb3963863b9184c1940040e6d080c3fa04389fd6deceffe1cc0c43a7f0734` |
| `pilot-calculators-1100x900.png` | 1100x900 | `df064670144084a4d67aaf3ff4f7884bd17f0ecb450f066323b6cb37b987f553` |
| `pilot-calculators-1460x900.png` | 1460x900 | `68067d3f8c0b70f3b8e8429acadcde8eee13c015afff7201387214c18c08fcc3` |
| `pilot-calculators-validation-1100x900.png` | 1100x900 | `4ddbafc4b5afa2d848c81065dddb01ae182cc1c0f070e0f14d4591b4290ef074` |
| `pilot-event-log-empty-1100x700.png` | 1100x700 | `0e46a07c4018833add76017adcfd237ba00c7f13b83b7909310209b5f279c26c` |
| `pilot-event-log-empty-1100x900.png` | 1100x900 | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` |
| `pilot-event-log-filtered-empty-1460x900.png` | 1460x900 | `a571c39d0cf17700b5dbb9d075c0bf6a9741667a1569a7d2af84719d65e9f2b3` |

Canonical screenshots were not regenerated or overwritten

## 18. Capture Determinism/Stale Protection

Both isolated runs reported 9 semantic PASS, 9 PNG, exit 0, exact inventory/dimensions/structure. Hash comparison nevertheless failed:

| File | Run A | Run B/canonical | Pixel delta |
| --- | --- | --- | --- |
| `main-shell-1460x900.png` | `7d2d5163b4278afab163a85675a156f88308ff8ff3fec474f97e19e3d4047be0` | `fa9a29b0f58d0c46767579941c255e279bfd2214ef7f78965ac971077db1ccd5` | 9,904 pixels; right vertical scrollbar region |
| `pilot-event-log-empty-1100x900.png` | `90b0aae7b519be1fe1687e210bf71858a1f9718560ae84d22b93dce6637b16e6` | `7456ec416473b3d119203ca08aaa92cbdec748edf5c684b597111c15a768e72f` | 16,416 pixels; bottom horizontal scrollbar region |

Seven files were byte-identical across A/B/canonical. Run B matched canonical 9/9. This is a genuine repeatability failure and no third run was used to hide it

Post-move rollback was probed in an isolated target by injecting failure on the second validator call: failure observed, validation calls 2, old target restored `false`, new target left installed `true`, backup residue count 1. Probe residue was then removed. Thus `Publish-ScreenshotSet` is not transactional after target replacement; existing stale test does not exercise this branch

Result: semantic/current-candidate stale rejection PASS; determinism FAIL; post-promotion stale/mixed-set protection FAIL

## 19. Source Provenance

Independent recalculation:

- source-input fingerprint: `9ca605aeec681dfe07af807141d6bfc2bcf6ae2f01a8f8d08b1ac8e74c8aa8cb` (matches reported);
- repository input count: 10 (six production `.cs`, three build/package/provenance recipes, one icon);
- raw SHA-256 per file, normalized relative path, size, role, ordinal sort, UTF-8/LF manifest;
- manifest contains no canonical/mirror absolute root;
- canonical checkout and a second isolated path produce the same fingerprint;
- missing/unexpected/test source negatives reject.

Raw working-tree byte identity is intentional. Mixed LF/CRLF means the value describes this checkout's exact compiler-input bytes; it is not claimed invariant across a checkout that applies different EOL conversion

## 20. Toolchain Provenance

Actual compiler: .NET Framework `csc.exe` 4.8.9032.0, SHA-256 `a725546bde53f1ad533e74abb01dd5ed5f07b171f5c284738da88f6ce478cf5f`

Toolchain fingerprint recalculates to `449f6846e8ad6a4734945765ff9e0a8503f4e2b952faf6362f34d011ce66680b`, covering compiler, compiler config, `cvtres.exe` and framework `clr.dll`. Result: PASS

## 21. Build Invocation Provenance

The actual compiler command is operationally correct and includes `/noconfig`, `/nostdlib+`, winexe, AnyCPU, optimize/debug/checked/unsafe choices, output, icon, nine references and six sources. The normalized provenance model is not correct:

- actual argv count: 26;
- normalized count: 27;
- actual atomic `/win32icon:<absolute-path>` tokens: 1;
- normalized atomic `/win32icon:src/NetStuck/assets/netstuck-bright.ico` tokens: 0;
- normalized model has separate `/win32icon:` and detached path tokens;
- reproduced identically on PS5.1 and pwsh; strict gate exit 1 on both.

Reported `cde7a5f52ac9f88d2be8482d829ac76a30e31f8d8ca2c319d99616b2ef2551ab` is deterministic for the wrong 27-token model. A one-token diagnostic correction hashes to `17a6640efce5c9ccd92ad92e6d774042b93361f1356efbb9e3be41c0d143c986`; this diagnostic value is not canonical until implementation/tests are fixed. Result: FAIL

## 22. Compiler Reference Provenance

Nine explicit raw-hashed references resolve beside the selected framework compiler: `mscorlib`, `System`, `System.Core`, `System.Data`, `System.Data.DataSetExtensions`, `System.Drawing`, `System.Windows.Forms`, `System.Web.Extensions`, `System.Xml`. Reference fingerprint is `199091183009849210a8da3c47db8b312d4e5e07f21c72269460e1efb22c4223`

`/nostdlib+` disables implicit standard-library inclusion and `/noconfig` disables `csc.rsp`; no test source is in actual production argv. Result: PASS within the fingerprinted Windows/.NET Framework toolchain boundary

## 23. Provenance Negative Tests

Existing production implementation suite ran under both hosts: `13/13`, failures 0, exit 0. It detects exact allowlist, missing/unexpected/test source, compiler flag drift, raw binary byte drift, checkout relocation, package input drift, toolchain-only drift, binary-safe Git diff and canonical manifest encoding

The suite does not assert actual/normalized argv cardinality or atomic icon-token correspondence; therefore its 13/13 is false-green for `UIF-RR-001`. Result: PARTIAL

## 24. Package Input Provenance

Fresh isolated package inputs are exactly 8 and every relative path/size/raw hash was independently recomputed. Fresh authoritative fingerprint:

`33838ab7720a8d98e35d71f28846eab710c6114b1bb32df4d48306a21552fd9f`

It matches the fresh external sidecar. It differs from historical `40a8934…` because fresh `NetStuck.exe` and generated `TEST-REPORT.txt` differ; six other pre-manifest inputs are byte-identical. This is an explained fresh-output difference, not reuse of historical evidence. Per-file/package-input identity: PASS; inherited build-invocation provenance: FAIL

## 25. Package Content Identity

Fresh decompressed content identity:

`d5b363adbbc5ad2cf421ff9d023ac6e517fe1eac53ac2ac0c9c2cf03dc883a52`

Independent stage and extracted-ZIP calculations match the sidecar and each other. Exact content is 9 files; all per-file hashes match. It differs from historical `bf54a09b…` because `NetStuck.exe`, `TEST-REPORT.txt` and consequent `SHA256SUMS.txt` differ. ZIP container identity is separately `7596dff7520ad77c9ce8bb43dac39e7d2ffbd0a8492807f4c9470c81ab4cefce`. Result: PASS

## 26. Development Build

Standalone development build: exit 0, size 442,880 bytes, file/product version `1.2.3.0`, SHA-256 `9da0a7d408c3c224440055be9e2b4caa99d20892a2067fabde891cce9df56937`. Production source inventory contains six files and no test source

Binary build/version result is PASS. The complete development-build gate is FAIL because its generated provenance record contains the invalid invocation model from section 21

## 27. Portable Package

Package command: exit 0; internal current suite 247/247; manifest 8/8; stage/ZIP inventory 9/9; exact decompressed hashes verified; Plink/license present; no tests, source tests, screenshots, closure/remediation reports or credential residue in the ZIP

Fresh ZIP size 839,934 bytes; SHA-256 `7596dff7520ad77c9ce8bb43dac39e7d2ffbd0a8492807f4c9470c81ab4cefce`. Package byte/content truth is PASS; overall portable gate is PARTIAL because build-invocation provenance is not truthful

## 28. Packaged EXE

Package build output and staged executable are byte-identical: SHA-256 `86ba92a9d05a0441f28a3221fac9a012b47a6d84fa986277c1b199094f395ba1`, version `1.2.3.0`

Standalone development and package compilation have equal length/version/source/toolchain/reference identities but differ at 44 byte positions, consistent with separate legacy compiler outputs; the package correctly stages its own verified build, so this is not a stage/ZIP integrity failure

Safe smoke: process started, input idle reached, native window created, responsive, `CloseMainWindow` accepted, graceful exit 0, forced termination false, remaining NetStuck processes 0. Owned smoke root had two files, contained no operator-profile path, and cleanup succeeded

## 29. Behavior Preservation

| Surface | Evidence | Result |
| --- | --- | --- |
| Shell | 63-check foundation suite, 1100/1460 screenshots | PASS |
| Calculators | real buttons, validation/success states, focus/tab order, 3 sizes | PASS automated; physical/dropdown manual open |
| Event Log | read-only/sort/copy contracts, empty/filtered/populated transitions | PASS automated |
| Ping | core/feature/performance/cadence blobs/tests unchanged; 11 vs 3 cadence | PASS |
| Traceroute | P1 UI race fixed; 50 stress invocations | PARTIAL due pending-probe drain/coverage |
| Collector | `NetStuck.Features.cs` baseline blob match; Feature integration tests pass | PASS |

No broad Ping/Traceroute/Collector visual modernization entered. Version and portable-folder model are preserved

## 30. Security/Privacy

Fresh package Plink 0.80 SHA-256 is `06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3`; Authenticode status `Valid`, signer Simon Tatham; PuTTY license exists, SHA-256 `47fce8b…`

Collector source remains baseline-equivalent: argv contains user/host/options but not password/enable secret; password and enable secret are written only through redirected stdin and redacted from captured terminal text. Package text scan found zero profile-path and credential-pattern hits; screenshots visually contain no credentials

Privacy gate nevertheless fails: four ignored `artifacts/r2-verification/**/state.json` smoke residues have a non-empty `CollectorFolder` under the operator profile. No password, enable secret or private key was detected. They are ignored and were not modified/deleted by this read-only review, but contradict the explicit no-sensitive-temp-residue gate and the final remediation report's cleanup claim

## 31. Current Documentation Review

Accurate current statements include pilot scope, 130 baseline, historical 128, current 247/10 suites, both PowerShell hosts, removed progress contract and manual limitations

Material inaccuracies:

- `UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md` says two fresh captures are byte-identical; independent Run A/B failed 2/9;
- it and the remediation report present `cde7a5f5…` as valid normalized invocation identity;
- `DEVELOPMENT.md` says failed capture cannot leave mixed authoritative evidence, disproved by the post-move probe;
- `DEVELOPMENT.md` says `NETSTUCK_TEST_STATE_PATH` leaves caches in LocalAppData, but `trace-lookups.json` follows the state path's directory;
- `ARCHITECTURE.md` says single-process although nslookup/Plink/explorer child processes are launched;
- final remediation report says no sensitive smoke-state residue remains, contradicted by four ignored state files.

Historical closure reviews remain immutable and their SHA/state are preserved. Current documentation gate: FAIL

## 32. Manual Verification Gates

| Gate | Status | Lifecycle disposition |
| --- | --- | --- |
| Physical pointer | NOT VERIFIED | required before merge for changed pilot actions |
| Native dropdown | NOT VERIFIED | required before merge for changed Unit Converter controls |
| Modal dialog | NOT VERIFIED | no Phase A dialog surface changed; not a merge blocker |
| DPI 125% | NOT VERIFIED | release/acceptance blocker |
| DPI 150% | NOT VERIFIED | release/acceptance blocker |
| DPI 200% | NOT VERIFIED | release/acceptance blocker |
| High Contrast | NOT VERIFIED | release/acceptance blocker |
| Screen reader | NOT VERIFIED | release/acceptance blocker |

`DrawToBitmap`, synthetic mouse routing, metadata tests and native smoke are not promoted to manual PASS

## 33. New Findings

### UIF-RR-001

ID: UIF-RR-001  
Severity: P2  
Confidence: HIGH  
Category: Build invocation provenance  
Related prior finding: UIF-FR-005 / UIF-R2-005  
Affected paths: `scripts/NetStuck.BuildProvenance.ps1`, `scripts/Test-BuildProvenance.ps1`, generated build/package sidecars  
Evidence: lines 224-253; cross-host 26 actual vs 27 normalized arguments; atomic normalized icon token absent  
Observed: reported fingerprint hashes a synthetic two-token icon option, not actual compiler argv  
Expected: normalized argv preserves one-to-one argument boundaries while replacing only host paths  
Impact: package/build provenance can misidentify compiler invocation even when binary compilation succeeds  
Recommended remediation: group/interpolate the icon expression, derive normalized/actual vectors from one specification, add cardinality/token tests on both hosts, regenerate all fingerprints  
Commit blocker: YES  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-002

ID: UIF-RR-002  
Severity: P2  
Confidence: HIGH  
Category: Capture transaction integrity  
Related prior finding: UIF-R2-003  
Affected path: `scripts/Capture-UiFoundations.ps1`  
Evidence: lines 540-599; injected second-validation failure left new target installed and one backup  
Observed: catch restores backup only when target is absent  
Expected: any failed run leaves the prior canonical target byte-for-byte intact and no promotion/backup residue  
Impact: command can return failure after replacing authoritative evidence  
Recommended remediation: treat promotion/validation/backup deletion as a transaction; on any post-move failure remove/quarantine new target and restore backup; add deterministic post-move and cleanup-failure negatives  
Commit blocker: YES  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-003

ID: UIF-RR-003  
Severity: P2  
Confidence: HIGH  
Category: Screenshot reproducibility  
Related prior finding: UIF-CR-001  
Affected paths: `tests/UiFoundationSnapshot.cs`, `scripts/Capture-UiFoundations.ps1`, canonical screenshot claims  
Evidence: Run A/B hash comparison differs for two scrollbar regions; all other bytes/scenarios validated  
Observed: A/B/canonical equality is 7/9, while Run B alone matches canonical 9/9  
Expected: two fresh serial isolated captures are byte-identical for all nine scenarios  
Impact: canonical capture identity remains timing/layout-state dependent and current report claim is false  
Recommended remediation: deterministically settle/normalize scrollbars before capture and add A/B hash repeatability as a required failing gate  
Commit blocker: YES  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-004

ID: UIF-RR-004  
Severity: P2  
Confidence: HIGH  
Category: Traceroute lifecycle/resource bound  
Related prior finding: UIF-FR-001  
Affected path: `src/NetStuck/NetStuck.V103.cs`  
Evidence: non-cancellable hop probes at 1012-1016/1162-1177; cancellation bypasses 952-954; CTS clears at 961-968  
Observed: Stop reaches UI quiescence but can re-enable Start while old-generation ICMP tasks remain live  
Expected: bounded overlap applies across stop/restart lifecycle and worker quiescence is real before restart  
Impact: rapid restart against slow targets can accumulate probe work beyond nominal 2–8 per-run bound  
Recommended remediation: drain pending cycles before clearing CTS/re-enabling Start, or make hop probes cancellable while preserving cadence; add a slow deterministic restart test  
Commit blocker: YES under repository bounded-overlap rule  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-005

ID: UIF-RR-005  
Severity: P2  
Confidence: HIGH  
Category: Traceroute regression-oracle isolation/coverage  
Related prior finding: UIF-FR-001  
Affected paths: `tests/TracerouteLifecycleTests.cs`, `src/NetStuck/NetStuck.V103.cs`, `docs/DEVELOPMENT.md`  
Evidence: harness sets one temp state file only, shares parent `trace-lookups.json`, suppresses cleanup errors, observes only EventTable/session 1/loopback/stopped close and 300/450 ms windows  
Observed: canonical test can pass without exercising route-table thread, provider callback, obsolete slow run, active close or actual drain  
Expected: unique owned root, fail-closed cleanup and deterministic coverage of the historical lifecycle boundaries  
Impact: regression in stale provider/drain paths may remain green; shared cache can affect or be deleted by another test  
Recommended remediation: use per-process `NETSTUCK_TEST_ROOT`, surface cleanup failures, instrument both tables, add slow provider/active-close/obsolete-run cases  
Commit blocker: YES under this closure harness-quality gate  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-006

ID: UIF-RR-006  
Severity: P2  
Confidence: HIGH  
Category: Privacy/evidence hygiene  
Related prior finding: final remediation cleanup claim  
Affected paths: four ignored `artifacts/r2-verification/**/state.json` smoke outputs  
Evidence: parsed `CollectorFolder` is absolute and under operator profile in all four; credential-like values absent  
Observed: sensitive username/profile-derived runtime state remains after prior smoke verification  
Expected: no sensitive temp residue after success or failure  
Impact: ignored status prevents ordinary staging but does not make evidence safe to retain/share  
Recommended remediation: remove exact owned residues, make smoke cleanup fail-closed, add redacted absolute-profile scan  
Commit blocker: YES under explicit security/privacy gate  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-007

ID: UIF-RR-007  
Severity: P2  
Confidence: HIGH  
Category: Current documentation/evidence accuracy  
Related prior finding: UIF-FR-006  
Affected paths: `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT.md`, `docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md`, `docs/UI_FOUNDATIONS_FINAL_BLOCKER_REMEDIATION_REPORT.md`  
Evidence: section 31 discrepancies  
Observed: current authoritative docs claim deterministic/transactional capture, valid invocation provenance, cache behavior and residue cleanup that current code/evidence does not support  
Expected: current maintainer docs distinguish fact, limitation and historical result accurately  
Impact: committing these docs would create a false closure record and unsafe future verification guidance  
Recommended remediation: correct current docs/reports after implementation fixes; preserve historical closure reviews unchanged  
Commit blocker: YES  
Merge blocker: YES  
Release blocker: YES

### UIF-RR-008

ID: UIF-RR-008  
Severity: P3  
Confidence: HIGH  
Category: Test-count regression guard  
Related prior finding: UIF-R2-002  
Affected path: `scripts/Test-NetStuck.ps1`  
Evidence: suite PASS requires positive PASS count and reconciliation identity/status, but no expected per-suite/aggregate count floor  
Observed: deleting assertions could still yield overall PASS if remaining output is positive; current baseline blobs/counts are intact  
Expected: canonical inventory pins expected per-suite counts or another explicit contract that fails on count loss  
Impact: future test deletion may become a false-green  
Recommended remediation: pin the ten per-suite discovered counts with an intentional update mechanism  
Commit blocker: NO for this run because independent evidence enforced exact 247/per-suite counts  
Merge blocker: NO  
Release blocker: NO

## 34. Commit/Merge/Release Matrix

| Gate | Result | Commit | Merge | Release | Evidence |
| --- | --- | --- | --- | --- | --- |
| Canonical repository/fingerprint | PASS | No | No | No | expected path/branch/HEAD/tag and `d3b52451…` |
| Historical blockers | FAIL | Block | Block | Block | UIF-FR-001 partial |
| Traceroute root cause | PASS | No | No | No | UI context/dispatch/run guards |
| Traceroute targeted stress | PASS | No | No | No | 25/25 each host, 300/300 assertions |
| PS5.1 5-run repeatability | PASS | No | No | No | 5 × 247/247 |
| pwsh 3-run repeatability | PASS | No | No | No | 3 × 247/247 |
| Cross-host inventory | PASS | No | No | No | identical 10-suite signature |
| Baseline 130 | PASS | No | No | No | blobs unchanged; 130/130 |
| Current full suite | PASS | No | No | No | 247/247, 0 failed/skipped/infra |
| PNG structural ordering | PASS | No | No | No | exact state machine |
| PNG malformed negatives | PASS | No | No | No | 27/27 both hosts |
| Canonical screenshot set | PASS | No | No | No | 9 valid/privacy-safe canonical files |
| Capture determinism | FAIL | Block | Block | Block | Run A/B 7/9 hashes |
| Stale evidence prevention | FAIL | Block | Block | Block | post-move rollback probe |
| Dead legacy delta | PASS | No | No | No | delta 0 |
| Dead foundation contracts | PASS | No | No | No | no material dead contract |
| Source provenance | PASS | No | No | No | `9ca605ae…`, path-independent |
| Toolchain provenance | PASS | No | No | No | `449f6846…` |
| Build invocation provenance | FAIL | Block | Block | Block | 26 actual vs 27 normalized |
| Compiler references | PASS | No | No | No | 9 explicit; `19909118…` |
| Package input provenance | PARTIAL | Block | Block | Block | per-file/fingerprint pass; invocation inherited fail |
| Package content identity | PASS | No | No | No | stage/extract `d5b363ad…` |
| Development build | PARTIAL | Block | Block | Block | binary/version pass; provenance fail |
| Portable build | PARTIAL | Block | Block | Block | package bytes pass; provenance fail |
| Packaged EXE | PASS | No | No | No | native responsive window/exit 0/no leak |
| Behavior preservation | PARTIAL | Block | Block | Block | Traceroute worker drain residual |
| Plink/password/privacy | FAIL | Block | Block | Block | Plink/CLI pass; ignored state residue fails |
| Current documentation | FAIL | Block | Block | Block | current false claims |
| Physical pointer | NOT VERIFIED | No | Acceptance | Acceptance | changed pilot actions |
| Native dropdown | NOT VERIFIED | No | Acceptance | Acceptance | changed Unit Converter controls |
| Modal dialog | NOT VERIFIED | No | No | No | no Phase A dialog change |
| DPI 125% | NOT VERIFIED | No | No | Acceptance | real scaled session required |
| DPI 150% | NOT VERIFIED | No | No | Acceptance | real scaled session required |
| DPI 200% | NOT VERIFIED | No | No | Acceptance | real scaled session required |
| High Contrast | NOT VERIFIED | No | No | Acceptance | real theme session required |
| Screen reader | NOT VERIFIED | No | No | Acceptance | Narrator/NVDA required |
| Git diff hygiene | PASS | No | No | No | staged 0, diff-check 0, fingerprint preserved |

Ready to commit: **NO**. Ready to merge: **NO**. Ready to release: **NO**

## 35. Recommended Commit Inventory

Verdict does not permit staging or commit. The following is a **provisional exact inventory only after all blockers are remediated and re-reviewed**; do not run `git add .`

Provisional Commit 1 — `feat(ui): add shared UI foundations and pilot adoption`:

```text
scripts/Build-NetStuck.ps1
scripts/Capture-UiFoundations.ps1
scripts/NetStuck.BuildProvenance.ps1
scripts/Package-NetStuck.ps1
scripts/Test-BuildProvenance.ps1
scripts/Test-NetStuck.ps1
src/NetStuck/NetStuck.UiFoundation.cs
src/NetStuck/NetStuck.V103.cs
src/NetStuck/NetStuck.cs
tests/TracerouteLifecycleTests.cs
tests/UiFoundationSnapshot.cs
tests/UiFoundationTests.cs
```

Provisional Commit 2 — `docs(ui): add phase A implementation and verification evidence`:

```text
docs/ARCHITECTURE.md
docs/DEVELOPMENT.md
docs/TESTING.md
docs/REPOSITORY_AUTHORITY_CONFIRMATION.md
docs/UI_AUDIT_REPORT.md
docs/UI_FOUNDATIONS_CLOSURE_REVIEW.md
docs/UI_FOUNDATIONS_CLOSURE_REVIEW_R2.md
docs/UI_FOUNDATIONS_FINAL_BLOCKER_REMEDIATION_REPORT.md
docs/UI_FOUNDATIONS_FINAL_CLOSURE_REVIEW.md
docs/UI_FOUNDATIONS_FINAL_CLOSURE_REREVIEW.md
docs/UI_FOUNDATIONS_IMPLEMENTATION_REPORT.md
docs/UI_FOUNDATIONS_R2_REMEDIATION_REPORT.md
docs/UI_FOUNDATIONS_REMEDIATION_REPORT.md
docs/ui-audit/screenshots/runtime-calculators-1100.png
docs/ui-audit/screenshots/runtime-calculators-1460.png
docs/ui-audit/screenshots/runtime-config-collector-1100.png
docs/ui-audit/screenshots/runtime-config-collector-1460.png
docs/ui-audit/screenshots/runtime-dns-resolver-1100.png
docs/ui-audit/screenshots/runtime-dns-resolver-1460.png
docs/ui-audit/screenshots/runtime-event-log-1100.png
docs/ui-audit/screenshots/runtime-event-log-1460.png
docs/ui-audit/screenshots/runtime-live-ping-1100.png
docs/ui-audit/screenshots/runtime-live-ping-1460.png
docs/ui-audit/screenshots/runtime-mac-wan-lookup-1100.png
docs/ui-audit/screenshots/runtime-mac-wan-lookup-1460.png
docs/ui-audit/screenshots/runtime-traceroute-1100.png
docs/ui-audit/screenshots/runtime-traceroute-1460.png
docs/ui-audit/screenshots/runtime-traceroute-protocol-open-1100.png
docs/ui-audit/screenshots/runtime-traceroute-protocol-open-1460.png
docs/ui-audit/screenshots/runtime-updates-1100.png
docs/ui-audit/screenshots/runtime-updates-1460.png
docs/ui-foundations/screenshots/main-shell-1100x900.png
docs/ui-foundations/screenshots/main-shell-1460x900.png
docs/ui-foundations/screenshots/pilot-calculators-1100x700.png
docs/ui-foundations/screenshots/pilot-calculators-1100x900.png
docs/ui-foundations/screenshots/pilot-calculators-1460x900.png
docs/ui-foundations/screenshots/pilot-calculators-validation-1100x900.png
docs/ui-foundations/screenshots/pilot-event-log-empty-1100x700.png
docs/ui-foundations/screenshots/pilot-event-log-empty-1100x900.png
docs/ui-foundations/screenshots/pilot-event-log-filtered-empty-1460x900.png
```

Do not stage: `artifacts/`, `fake-plink-auth-count.txt`, runtime state/profile/cache/log/export/capture data, temporary mirrors, `.exe`, `.dll`, `.pdb`, `.zip`, real credentials, operator paths, or unrelated workspace files

## 36. Final Git Integrity

After report creation:

- modified tracked paths remain 8;
- untracked paths are 44, with exactly one reviewer-created path: this report;
- staged paths remain 0;
- `git diff --check` exit 0;
- implementation-only fingerprint excluding the untracked report remains `d3b52451c898544650e3019e29829022f28ae07c`;
- historical final-review SHA-256 remains `82809d17e1245a0f03fd340e9435165082087578653f635a40b3a3122f52b409`;
- production changed by reviewer: NO;
- tests/scripts changed by reviewer: NO;
- screenshots changed by reviewer: NO;
- Git index/config/branch/tag/remote changed: NO;
- commit/tag/push performed: NO.
- isolated review mirror and its generated build/test/capture/package/state/log evidence were removed after evidence reconciliation: YES.

## 37. Final Recommendation

Required before commit:

1. correct atomic compiler-argument normalization and add cross-host one-to-one tests;
2. make capture promotion fully rollback-safe and add post-move/cleanup negatives;
3. remove scrollbar timing nondeterminism and prove two fresh 9/9 hash-identical captures;
4. make Traceroute Stop await/cancel pending probes before restart and strengthen the lifecycle oracle with unique fail-closed state/cache cleanup plus slow/obsolete/active-close cases;
5. remove exact sensitive smoke residues and verify no profile-derived state remains;
6. correct current documentation claims while preserving historical closure reviews;
7. rerun 25+25 targeted stress, 5+3 full suites, 27-case PNG matrix, A/B capture, provenance, development build, package, smoke and final Git integrity without retry.

Required before merge after commit blockers close: physical-pointer and native-dropdown acceptance on the changed pilot controls

Required before release: DPI 125/150/200, High Contrast and screen-reader acceptance, plus all commit/merge blockers closed

Final recommendation: **NOT_READY**

## 38. Appendix — Commands, Versions, Exit Codes, Hashes

Key commands/evidence:

```text
git diff --binary --no-ext-diff | git hash-object --stdin
  exit 0; d3b52451c898544650e3019e29829022f28ae07c

powershell.exe ... Test-NetStuck.ps1 -SoakSeconds 10
  5/5 exits 0; each 247/247; 10/10

pwsh.exe ... Test-NetStuck.ps1 -SoakSeconds 10
  3/3 exits 0; each 247/247; 10/10

TracerouteLifecycleTests.exe
  PS5.1 25/25 exits 0, 150/150; pwsh 25/25 exits 0, 150/150

Capture-UiFoundations.ps1 -RunInfrastructureTests
  PS5.1 27/27 exit 0; pwsh 27/27 exit 0

Capture Run A / Run B
  each 9 semantic PASS, exit 0; hashes equal 7/9 => FAIL

Build-NetStuck.ps1
  exit 0; version 1.2.3.0; provenance gate FAIL

Package-NetStuck.ps1 -Version 1.2.3 -PlinkPath <PINNED_PLINK>
  exit 0; internal 247/247; 8/8 manifest; 9/9 content

Packaged EXE smoke
  native window/responsive/graceful close/exit 0/no process leak
```

Fingerprint summary:

```text
Initial/final implementation  d3b52451c898544650e3019e29829022f28ae07c
Source input                  9ca605aeec681dfe07af807141d6bfc2bcf6ae2f01a8f8d08b1ac8e74c8aa8cb
Toolchain                     449f6846e8ad6a4734945765ff9e0a8503f4e2b952faf6362f34d011ce66680b
Reference inputs              199091183009849210a8da3c47db8b312d4e5e07f21c72269460e1efb22c4223
Reported invocation           cde7a5f52ac9f88d2be8482d829ac76a30e31f8d8ca2c319d99616b2ef2551ab (INVALID MODEL)
Diagnostic corrected argv     17a6640efce5c9ccd92ad92e6d774042b93361f1356efbb9e3be41c0d143c986
Fresh package input           33838ab7720a8d98e35d71f28846eab710c6114b1bb32df4d48306a21552fd9f
Fresh package content         d5b363adbbc5ad2cf421ff9d023ac6e517fe1eac53ac2ac0c9c2cf03dc883a52
Fresh ZIP                     7596dff7520ad77c9ce8bb43dac39e7d2ffbd0a8492807f4c9470c81ab4cefce
Standalone dev EXE            9da0a7d408c3c224440055be9e2b4caa99d20892a2067fabde891cce9df56937
Package/staged EXE             86ba92a9d05a0441f28a3221fac9a012b47a6d84fa986277c1b199094f395ba1
```

Review output/report: `docs/UI_FOUNDATIONS_FINAL_CLOSURE_REREVIEW.md`

PHASE_A_REREVIEW_NOT_READY
