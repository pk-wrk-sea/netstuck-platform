# V2 feature migration

All original destinations remain reachable. No functional feature is removed or deferred. Home, Settings, target-file loading, optional Ping history detail and GitHub downgrade recovery are additions.

| Feature group | New entry | Preservation evidence |
| --- | --- | --- |
| ICMP/TCP Live Ping and target lists | Network Monitor → Ping Live | Existing Feature, Core, Cadence, Performance and Soak suites; V2 filtering/selection/column checks |
| Results, CSV/copy, summary, selected-target history and log export | Ping results toolbar / More; history header | Existing named controls and handlers retained; minimum-width geometry and responsive action checks |
| Two route sessions and hop/service settings | Network Monitor → Traceroute | Existing Feature and 32 Traceroute lifecycle checks; V2 layout matrix |
| Route/DNS events, hop descriptions and columns | Traceroute session tools and event pane | Existing three-row input/frame contract and session-owned tables/handlers retained |
| Forward/reverse DNS and continuous polling | Network Monitor → DNS Resolver | Existing Feature/Core checks and V2 construction/layout matrix |
| MAC and public-IP lookups | Network Tools → MAC / WAN Lookup | Existing cache/cancellation and Feature checks; V2 construction/layout matrix |
| Subnet and unit calculators | Calculators | Phase A field order, validation states and keyboard/announcement checks retained |
| SSH/Telnet collection and credentials | Config Collector | Existing prompt-aware transport, literal domain separator, fallback, streaming, redaction and password-free argv/state tests |
| Command tabs, output folder, TXT/JSON, error export and terminal | Config Collector input scroller and review pane | Existing handlers; pinned Collect/Stop and terminal batching checks |
| Event filtering, level, export and clear | Event Log | Existing read-only grid operations, empty-state/announcement and accessibility tests |
| Daily/manual latest update and recovery backup | Updates / recovery | Existing 40 maintenance checks plus explicit transition and sandbox downgrade/rollback tests |
| Theme, zoom, saved inputs and window geometry | Header; Settings | Schema 6 and original eight indices retained; existing restore/zoom tests plus V2 state/control checks |

The old FeatureTests assertion requiring a 690-pixel Traceroute first pane was replaced by direct usability assertions on target/timing widths and non-overlap. That fixed presentation width cannot coexist with the new sidebar at the original minimum window size. The functional test count and three-row input separation contract are retained. MaintenanceTests now identifies primary navigation by its stable name, because Home/Settings add two destinations.

Physical non-96-DPI, mixed-monitor, High Contrast and screen-reader acceptance require real sessions. Construction, geometry, rendered images and native popup captures establish only the cases actually recorded; see `V2_UI_TESTING.md` and the implementation report.
