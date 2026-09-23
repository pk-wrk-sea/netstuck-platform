# Versioning

NetStuck uses semantic versions for repository tags and releases. The current update-trial target is:

- Git tag/release: `v1.3.2`
- Legacy UI display: `v.1.3.2`
- Assembly/File version: `1.3.2.0`

v1.3.2 is an explicitly requested owner update-trial release. Tests and CI are skipped for this release only; normal release gates remain unchanged. It is published as normal Latest because the 1.3.1 updater ignores prereleases.

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
