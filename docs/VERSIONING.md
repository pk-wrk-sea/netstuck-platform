# Versioning

NetStuck uses semantic versions for repository tags and releases. The current candidate target is:

- Planned Git tag/release: `v1.3.0` (not created during candidate preparation)
- Legacy UI display: `v.1.3.0`
- Assembly/File version: `1.3.0.0`

The last published stable baseline remains `v1.2.3` until release acceptance, tagging and publication are complete.

When a version upgrade is explicitly requested, update all of these locations together:

1. `AssemblyVersion` in `src/NetStuck/NetStuck.cs`
2. `AssemblyFileVersion` in `src/NetStuck/NetStuck.cs`
3. `AppVersion` in `src/NetStuck/NetStuck.cs`
4. The Updates-page current entry in `src/NetStuck/NetStuck.cs`
5. Version assertions/names in `tests/FeatureTests.cs`
6. Default version in `scripts/Package-NetStuck.ps1`
7. CI artifact name in `.github/workflows/windows-ci.yml`
8. `README.md`, `README-TH.md`, `CHANGELOG.md` and release reports

Do not bump the version merely because repository documentation, CI or non-functional maintenance metadata changed. Record baseline maintenance in the changelog until a user requests a product release.
