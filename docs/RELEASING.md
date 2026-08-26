# Releasing

## Release policy

- Git history contains source, tests, documentation and license notices.
- Compiled NetStuck/PuTTY binaries belong in a GitHub Release ZIP, not in Git history.
- The complete portable folder is the distribution unit.
- NetStuck binaries are currently unsigned; publish SHA256 checksums and expect Windows reputation warnings on a new build.

## Pre-release checklist

1. Confirm the working tree contains only intended changes.
2. Update all version locations listed in `VERSIONING.md`.
3. Update `CHANGELOG.md`, the Updates tab and release documentation.
4. Run:

   ```powershell
   .\scripts\Test-NetStuck.ps1 -SoakSeconds 10
   ```

5. Complete relevant UI acceptance gates from `TESTING.md`.
6. Obtain the verified PuTTY 0.80 `plink.exe` locally. The packaging script requires SHA256:

   `e7d461204302d4ed4f47079a70070da9f6fe10b074c37cf8463f91f4709cdfb8`

7. Package:

   ```powershell
   .\scripts\Package-NetStuck.ps1 -Version 1.2.3 -PlinkPath C:\path\to\plink.exe
   ```

8. Re-verify `SHA256SUMS.txt` and smoke-start `NetStuck.exe` from the staged portable folder.
9. Commit, push and wait for Windows CI to pass.
10. Create an annotated tag such as `v1.2.3` and a GitHub Release with the ZIP and ZIP SHA256.
11. Clone/download into a clean directory and repeat the startup smoke test before marking the release stable.

## Package requirements

- `NetStuck.exe`
- `NetStuck-Icon.png`
- `tools/plink.exe`
- `tools/PuTTY-LICENCE.txt`
- README/CHANGELOG/test report
- `SHA256SUMS.txt`

Do not package LocalAppData state, collector output, test artifacts or credentials.
