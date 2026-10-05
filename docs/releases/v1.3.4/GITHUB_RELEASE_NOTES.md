NetStuck v1.3.4 improves the UI layout and Light/Dark readability.

- Ping and Config Collector actions stay visible while settings scroll.
- Clearer search/credential labels, wrapped headers, adaptive dialogs and dropdown sizing.
- Neutral dark surfaces and stronger text/status contrast in both themes.
- Correct restored text zoom and table-row sizing.

Download **NetStuck-v.1.3.4.zip**, extract the complete folder, and keep `NetStuck.exe`, `tools` and the PuTTY license together. The ZIP SHA256 is attached; `SHA256SUMS.txt` inside covers the portable files.

Validation: fresh regression on PowerShell 5.1 and 7, deterministic screenshots, Windows CI and packaged startup checks. Exact totals and measurements are in [the release report](https://github.com/pk-wrk-sea/netstuck-platform/blob/v1.3.4/docs/releases/v1.3.4/RELEASE_REPORT.md).

Normal and 1100×700 layouts were reviewed on a 96-DPI desktop. Real Windows Scale 125/150/200%, mixed-DPI monitor transitions, High Contrast and screen-reader acceptance remain **unverified**. This release preserves system-DPI awareness; monitor transitions can still use Windows bitmap scaling.
