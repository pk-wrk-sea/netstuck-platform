# Privacy and data handling

NetStuck is a desktop tool. Most data stays on the operator's Windows computer, but several features contact external services and local state can reveal network topology.

## Local data

`%LOCALAPPDATA%\NetStuck` can contain:

- Saved Ping profiles and targets
- Window/menu/input state
- Hop descriptions and Traceroute target history
- DNS, MAC and WAN lookup inputs
- Config Collector device lists, commands, usernames and output-folder path
- MAC vendor, ISP/ASN, reverse-DNS and public-IP caches

Passwords and enable secrets are not persisted. Other saved values may still be sensitive.

Config Collector output defaults to `%USERPROFILE%\Documents\NetStuck Configs` and may contain full network-device configuration, hostname, IP, username metadata and command output. Protect it according to the organization's security policy.

## External services

Features may send queries to:

| Service | Purpose | Data sent |
| --- | --- | --- |
| `api.ipify.org` | Determine public IP | Request source/public IP |
| `ipwho.is` | WAN owner/location and public-hop provider | Queried public IP |
| `api.maclookup.app` | MAC OUI vendor | OUI/MAC prefix |
| Cloudflare, Google and `pool.ntp.org` NTP endpoints | Time synchronization | Standard NTP request metadata |

DNS forward/reverse queries use the Windows-configured system resolver. SSH/Telnet/Ping/Traceroute traffic goes to targets entered by the operator.

## Repository rule

Do not copy runtime state, screenshots showing real network identity, lookup exports or collector output into issues, commits or releases. Sanitize diagnostic logs before sharing them with an AI system or third party.
