# Versioning

NetStuck uses semantic versions for repository tags and releases. The current UI/layout release target is:

- Git tag/release: `v2.0.0`
- Legacy UI display: `v.2.0.0`
- Assembly/File version: `2.0.0.0`

v2.0.0 introduces the sidebar workspace, Ping redesign, Home/Settings and GitHub downgrade recovery on the functional v1.3.5 baseline. Local regression, Windows CI, exact package integrity and startup verification are required. Real Windows Scale 125/150/200%, mixed-DPI and accessibility acceptance remain separately documented manual boundaries. The owner requested GitHub publication in the current task; this does not convert an unperformed check into PASS.

When a version upgrade is explicitly requested, update all of these locations together:

1. `AssemblyVersion` in `src/NetStuck/NetStuck.cs`
2. `AssemblyFileVersion` in `src/NetStuck/NetStuck.cs`
3. `AppVersion` in `src/NetStuck/NetStuck.cs`
4. The Updates-page current entry in `src/NetStuck/NetStuck.cs`
5. Version assertions/names in `tests/FeatureTests.cs`
6. Default version in `scripts/Package-NetStuck.ps1`
7. CI artifact name in `.github/workflows/windows-ci.yml`
8. `README.md`, `README-TH.md`, `CHANGELOG.md` and release reports
9. `src/NetStuck/app.manifest`, updater User-Agent and `.github/workflows/release.yml` defaults

Do not bump the version merely because repository documentation, CI or non-functional maintenance metadata changed. Record baseline maintenance in the changelog until a user requests a product release.
