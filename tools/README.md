# Local tools

Place a local copy of PuTTY 0.80 `plink.exe` in this directory only when testing or packaging SSH Config Collector.

The executable is ignored by Git. Before packaging, `scripts/Package-NetStuck.ps1` verifies this SHA256:

`e7d461204302d4ed4f47079a70070da9f6fe10b074c37cf8463f91f4709cdfb8`

The license committed at `third-party/PuTTY-LICENCE.txt` must be included beside Plink in every portable release.
