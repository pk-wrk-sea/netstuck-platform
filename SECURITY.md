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

## Reporting

This is a private repository. Report a suspected vulnerability privately to the repository owner rather than opening a public issue containing device information or credentials.
