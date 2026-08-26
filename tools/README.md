# Local tools

Place a local copy of PuTTY 0.80 `plink.exe` in this directory only when testing or packaging SSH Config Collector.

The executable is ignored by Git. Before packaging, `scripts/Package-NetStuck.ps1` verifies this SHA256:

`06861c22056919216f925892334ba29b4a2848a7a09c3611540b16e993fd6cc3`

The license committed at `third-party/PuTTY-LICENCE.txt` must be included beside Plink in every portable release.
