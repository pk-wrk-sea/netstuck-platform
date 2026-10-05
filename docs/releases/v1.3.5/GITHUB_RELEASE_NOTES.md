NetStuck v1.3.5 improves UI layout, Light/Dark readability and Traceroute Stop reliability.

- Ping and Config Collector actions stay visible while settings scroll.
- Clearer search/credential labels, wrapped headers, adaptive dialogs and dropdown sizing.
- Neutral dark surfaces and stronger text/status contrast in both themes.
- Correct restored text zoom and table-row sizing.
- Keep Traceroute Stop timeout messages on the UI thread and guard disposed/replaced sessions.

Download **NetStuck-v.1.3.5.zip**, extract the complete folder, and keep `NetStuck.exe`, `tools` and the PuTTY license together. The ZIP SHA256 is attached; `SHA256SUMS.txt` inside covers the portable files.

Exact fresh test totals, performance and acceptance boundaries are in [the release report](https://github.com/pk-wrk-sea/netstuck-platform/blob/v1.3.5/docs/releases/v1.3.5/RELEASE_REPORT.md). The unpublished v1.3.4 candidate failed release packaging; v1.3.5 includes its UI improvements and the deterministic Stop repair.

Normal and 1100×700 layouts were reviewed on a 96-DPI desktop. Real Windows Scale 125/150/200%, mixed-DPI monitor transitions, High Contrast and screen-reader acceptance remain **unverified**. This release preserves system-DPI awareness; monitor transitions can still use Windows bitmap scaling.
