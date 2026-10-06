# Security policy

## Sensitive data

Never commit or attach:

- Passwords, enable secrets, OTP/MFA responses or private keys
- `%LOCALAPPDATA%\NetStuck` state/cache files
- Collector TXT/JSON captures or temporary capture files
- Error exports from real devices
- Real device inventories, internal DNS mappings or operator usernames

Use documentation-only IP ranges and synthetic credentials in tests and examples.

## Config Collector requirements

- Passwords and enable secrets must not enter process arguments, state, CSV, saved config or diagnostic logs.
- Preserve prompt-aware redirected-input authentication for Plink.
- Preserve strict-host-key behavior and do not silently weaken it.
- AUTH2 fallback is allowed only after an authentication-class failure.
- Redact command output before finalizing saved files where the current collector requires it.

## Dependency handling

The release includes PuTTY Plink but Git history does not. Verify its pinned SHA256 and Authenticode signature before packaging, and distribute `PuTTY-LICENCE.txt` beside it.

NetStuck executables are currently unsigned. SHA256 manifests provide integrity checking but not publisher identity.

## Updater trust boundary

Updater accepts only stable release metadata from `pk-wrk-sea/netstuck-platform`, HTTPS GitHub/CDN hosts, the exact portable package inventory and matching SHA256/version. It executes the installed application copied as an updater, never a downloaded shell script. It runs as the current user and does not elevate. State and collector files are outside the package allowlist. Checksums detect corruption; compromise of the publishing account can also replace checksums and remains a risk while releases are unsigned.

Version recovery requires an explicitly selected earlier version and confirmation. The helper revalidates downgrade direction and the expected installed version before replacement, retains the current application-file backup and shares the existing integrity/rollback checks. Latest-release checks never trigger a downgrade.

## Reporting

The repository is currently public. Report a suspected vulnerability privately to the repository owner rather than opening a public issue containing device information or credentials.
