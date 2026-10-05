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

   `06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3`

7. Package:

   ```powershell
   .\scripts\Package-NetStuck.ps1 -Version 1.3.4 -PlinkPath C:\path\to\plink.exe
   ```

8. Re-verify `SHA256SUMS.txt` and run the fail-closed packaged smoke from the staged portable folder:

   ```powershell
   .\scripts\Test-PackagedSmoke.ps1 -ExecutablePath .\artifacts\release\NetStuck-v.1.3.4\NetStuck.exe
   ```
9. Commit, push and wait for Windows CI to pass.
10. After release acceptance, create the annotated tag `v1.3.4`. The tag workflow tests/packages the tag, runs packaged startup and stages a draft GitHub Release with the ZIP and ZIP SHA256. It refuses to replace an already published release.
11. Download the draft assets into a clean directory, compare the asset/ZIP hashes, verify the exact portable inventory and per-file manifest, and repeat startup smoke before publishing as Latest. Keep unperformed manual acceptance checks explicit in the report and release notes; publication authorization is not an observed test PASS.

## Package requirements

- `NetStuck.exe`
- `NetStuck-Icon.png`
- `tools/plink.exe`
- `tools/PuTTY-LICENCE.txt`
- README/CHANGELOG/test report
- `SHA256SUMS.txt`

Do not package LocalAppData state, collector output, test artifacts or credentials.
