NetStuck v.1.2.3
=================

Portable Windows network toolbox. Run NetStuck.exe directly; Python is not
required. Keep NetStuck.exe and the tools folder together.

What changed in v.1.2.3
-----------------------

- Replaced fixed-width Traceroute flow rows with deterministic adjacent grid
  columns, so input boxes remain ordered and cannot overlap.
- Target, Max Hops, Timeout and Interval now share the first aligned row.
- Protocol, Port and Packet Size now share the second aligned row; opening the
  Protocol list can no longer cover a timing input.

Changes retained from v.1.2.2
-----------------------------

- Moved Start, Pause and Stop to a dedicated row inside the bordered Traceroute
  panel, so timing inputs and actions cannot overlap at narrow widths/high DPI.
- Corrected the main Traceroute splitter at the 1100-pixel minimum window width;
  the result and event panes now retain their declared minimum sizes.
- Standardized ComboBox and NumericUpDown backgrounds for enabled and disabled
  Traceroute states.
- Renamed the port label to Port (TCP/UDP); it remains editable while stopped
  and is ignored by ICMP probes.

Changes retained from v.1.2.1
-----------------------------

- Rebuilt the Traceroute settings area as one bordered, compact two-row panel.
- Target, Protocol, Port and Packet Size stay aligned on the first row.
- Max Hops, Timeout and Interval remain grouped on the lower-left; equal-width
  Start, Pause and Stop actions now align on the lower-right.
- Reduced the settings area by 38 pixels to give the result grid more room.
- Polling, adaptive TTL logic and Collector behavior are unchanged from v.1.2.0.

Changes retained from v.1.2.0
----------------------------

- Config Collector exports only failed results to CSV with the columns IP,
  Status, Protocol, Username and Detail.
- Collector terminal messages are drained in UI batches, which reduces redraw
  work when 16-32 devices are collected concurrently.
- SSH and Telnet captures stream to temporary files as data arrives. Only a
  bounded preview remains in memory; TXT and optional JSON are finalized after
  a successful collection.
- Traceroute updates only changed hop rows instead of resetting the complete
  binding. Manual vertical/horizontal scroll and the current cell stay intact.
- Stable routes use adaptive TTL polling. The destination remains frequent,
  intermediate stable hops rotate, full discovery runs periodically, and a
  route change forces three full cycles.
- ISP/ASN and reverse-DNS results persist in a bounded cache. Provider entries
  expire after 24 hours (30 minutes for unavailable results); DNS expires after
  15 minutes.
- Protocol, Port, Packet Size, Max Hops, Timeout and Interval use balanced input
  widths and complete borders.
- Dormant Log Sanitizer fields and implementation were removed from source after
  the compatibility release.
- An overnight soak harness covers packet loss, route and DNS changes, TACACS
  rejection, VTY/session limits, terminal load, UI dispatch and memory growth.

Config Collector notes
----------------------

- DOMAIN\username is passed with one literal backslash. Pasted doubled
  separators are normalized to one separator.
- AUTH2 follows AUTH1 only after a confirmed authentication rejection.
- Passwords and enable secrets are not stored in state, CSV or process argv.
- Full captured output is in the saved TXT file. The terminal intentionally
  shows only the most recent bounded preview for very large captures.

Soak test
---------

The source package contains OvernightSoakTests.exe/test source. Its default run
is eight hours. For a short validation run:

  OvernightSoakTests.exe --seconds 60

Distribution
------------

Copy the complete NetStuck-v.1.2.3 folder to another Windows computer. Keep
NetStuck.exe, tools, licenses and the other packaged files together. User state
and caches are stored under:

  %LOCALAPPDATA%\NetStuck

See TEST-REPORT.txt and PERFORMANCE-REPORT-AND-NEXT-PLAN.txt for the measured
release validation and recommendations for a later version.
