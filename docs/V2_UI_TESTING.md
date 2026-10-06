# V2 testing profiles and acceptance

| Profile | Command / scope |
| --- | --- |
| QUICK | `scripts/Test-UiV2.ps1 -Profile Quick`: local metadata/transition/package/recovery fixtures, no application UI |
| UI | `scripts/Test-UiV2.ps1 -Profile UI`: one isolated form, navigation, native roles, layout, filters, theme/zoom and disposal |
| FUNCTIONAL | `scripts/Test-UiV2.ps1 -Profile Functional`: existing 93-check integration suite, including local Collector transports |
| CLOSURE | `scripts/Test-NetStuck.ps1 -SoakSeconds 10`: all mandatory stages/suites; run under Windows PowerShell 5.1 and PowerShell 7 |
| RELEASE | CLOSURE + package integrity/provenance, staged/draft download startup smoke, screenshots and separately recorded manual display/accessibility gates |

Normal iteration uses QUICK or UI; popup-heavy closure is a deliberate command. The new mandatory suites contain 460 V2 UI checks and 27 version-recovery checks. They extend the existing corpus rather than replacing networking or security checks. Test-only `LegacyVersionStub.exe` has assembly/file version 1.3.5.0 and is a sandbox payload fixture, never a production package input.

`scripts/Capture-UiV2.ps1` produces sanitized structural page screenshots for both themes at requested 1366×768, 1440×900, 1920×1080 and 1100×700. It uses the production manifest/icon and real process DPI; the clock/network labels and inputs are deterministic fixtures. `DrawToBitmap` is structural evidence and does not capture native popups or prove physical interaction. With `-NativePopups`, the host opens a real native combo list, verifies its HWND/visibility/bounds, renders that owned HWND with `PrintWindow` and composites it over the sanitized form render. It never reads the desktop. These captures verify native list text/bounds at 96 DPI; they do not prove physical pointer interaction or modal acceptance.

V2 geometry tests cover requested 1024×768, 1100×700, 1280×720, 1366×768, 1440×900, 1920×1080 and 2560×1440 in both themes. Actual observed bounds are emitted. A request below the nominal minimum or above the current working area is clamped; this is not a claim of having a physical monitor at that requested resolution. The existing maintenance suite also exercises a constrained synthetic 1024×600 canvas with its own explicit minimum override.

Required checks include page/nav identity, positive usable grid/action geometry, complete action text, accessibility names/native roles, pinned operation actions, scroll-reachable settings, dropdown construction, result filter/selection/column persistence, zoom wiring and disposal. Existing UI layout checks measure 4.5:1 body/muted/header/selection/enabled-action contrast in both palettes and exercise Pause/Stop.

The downgrade corpus covers stable versus draft/prerelease metadata, numeric ordering, archive location, checksum/host restrictions, duplicate/current/future/incompatible versions, explicit mode, changed installed identity, older executable validation, full application-file backup, unchanged state/unrelated files, injected copy failure and exact-hash recovery. Live download validation fetches historical GitHub ZIPs into owned temporary storage without installing them.

Windows Scale 125/150/175/200%, monitor transitions, text-size/fallback-font variance, longer localized primary labels, High Contrast, screen reader, physical pointer selection and modal recovery acceptance remain manual unless independently recorded. Never convert these to PASS from a 96-DPI image or an automated accessibility-role query. Keep source/CI/package checks and local observations distinct from those gates.
