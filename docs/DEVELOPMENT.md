# Development

## Requirements

- Windows 10 or 11
- .NET Framework 4.x compiler at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
- PowerShell 5.1 or later
- PuTTY `plink.exe` only when exercising or packaging Config Collector SSH

Python and the modern .NET SDK are not required.

## Build

```powershell
.\scripts\Build-NetStuck.ps1
```

Output: `artifacts\build\NetStuck.exe`.

`build_windows.bat` is a convenience wrapper around the same command.

## Development workflow

1. Confirm `git status` and preserve unrelated user changes.
2. Read the feature owner and invariants in `ARCHITECTURE.md` and `AGENTS.md`.
3. Make the smallest coherent change. Do not bump the version for an ordinary fix unless requested.
4. Run focused checks while iterating.
5. Run the complete baseline command before handoff:

   ```powershell
   .\scripts\Test-NetStuck.ps1 -SoakSeconds 10
   ```

6. Visually inspect relevant screens when layout, grid behavior, colors, zoom or activity indicators change.

## UI guidance

- Keep the interface minimal and preserve prominent inputs and status visibility.
- Result grids must remain filterable/copyable and columns resizable/reorderable where currently supported.
- Avoid fixed-width combinations that exceed their parent. Use explicit table columns, minimum sizes and validated splitter constraints.
- Preserve user scroll and selection during polling refresh.
- Use UI batching for high-frequency results. Increasing resource use does not justify per-result redraw.

## Runtime diagnostics

For isolated tests, set `NETSTUCK_TEST_STATE_PATH` to a temporary JSON path. Be aware that this override does not currently redirect every cache (`profiles.json`, `mac-vendors.json` and public-IP cache still use LocalAppData).

Never diagnose against or copy files from a production collector-output folder into this repository.
