# Portable updates (1.3.1)

The Updates page provides **Check for updates**, the last successful check time, a stable-release notification and **Update now**. Optional background checks run after startup when the last successful check is at least 24 hours old. Disabling automatic checks still allows manual checks. A failed check is not reported as up to date. Existing 1.3.0 users must install 1.3.1 manually once.

The source is the public `pk-wrk-sea/netstuck-platform` GitHub Releases API. Drafts, prereleases, malformed versions, unexpected hosts and missing/duplicate assets are rejected. Version comparison is numeric. Metadata is bounded to 1 MiB with a 15-second deadline. Downloads are bounded to 100 MiB with a ten-minute total deadline. Cancellation aborts the request, including on application close. GitHub unauthenticated quotas are shared by clients using the same public IP; a failed/rate-limited check leaves the app usable.

## Installing

1. Stop active Ping, both Traceroute sessions, DNS polling, MAC/WAN lookups and Collector work.
2. Select Update now and confirm replacement/restart.
3. The app downloads the full versioned ZIP and companion SHA256. It validates the exact nine-file inventory, internal SHA256 manifest and executable version in an isolated staging folder. ZIP traversal, duplicates and extra/missing files are rejected.
4. Settings must save successfully and the installation folder must be writable before the app exits. There is no automatic privilege elevation.
5. A copy of the **installed** NetStuck executable runs as the helper and waits for the original process identity to exit. It checks package integrity again, rejects an equal/newer installed version, backs up the original files and writes a durable recovery record before replacing any file.
6. Only declared package files are replaced. LocalAppData state/profiles and unrelated portable-folder files are preserved. The helper verifies installed hashes and starts NetStuck from the same folder. It requires the new GUI process to reach input-idle within 15 seconds before confirming restart.

The program does not resume live network jobs automatically after restart. Settings and input lists remain available. Installation failure attempts rollback and reports any rollback failure explicitly. If the app restarts but fails later, automatic health monitoring is not provided.

## Recovery

Update folders live under `%LOCALAPPDATA%\NetStuck\updates\<operation-id>`. Each operation retains `job.json`, the copied `NetStuck.UpdateHelper.exe`, validated `payload`, and `backup` with `recovery.json`. Backups contain application files, not operator state. Keep these until the new version is accepted; remove only a known completed operation folder when it is no longer needed.

If power loss or termination interrupts installation, close NetStuck and run the helper from that operation folder with `--recover-update`. It uses the recorded original file inventory, restores the backup and starts the original application. Do not run recovery while any NetStuck instance from that installation is open. Never use a recovery record supplied by someone else.

## Displays and columns

Normal minimum window size remains 1100×700. On a smaller working area the window fits the monitor; individual pages retain a minimum usable canvas with scrollbars. Splitter bounds are recalculated as the window changes. Collector's taller input surface can be scrolled to reach commands and actions without collapsing the result area. Traceroute Columns controls hide/show existing named columns independently for each session and persist in state schema 6 as optional fields. At least one column must remain visible. Column dragging/resizing shows a blue dashed guideline.

The executable declares **system DPI awareness**. Windows scales it when moved to a monitor with different DPI; native per-monitor DPI rerendering is not implemented. Physical 125%, 150%, 200%, multi-monitor moves, High Contrast and screen reader checks must be recorded independently; virtual canvas geometry tests do not establish those results.

## Publishing contract

Publish the complete `NetStuck-v.X.Y.Z.zip` and `NetStuck-v.X.Y.Z.zip.sha256.txt`, with one top-level `NetStuck-v.X.Y.Z` folder. The checksum line is lowercase SHA256, two spaces, then the exact ZIP name. The nine-file package allowlist is shared with the release policy. Keep future changes to package paths synchronized with updater compatibility before publishing them. Checksums provide integrity, not publisher identity; NetStuck remains unsigned.
