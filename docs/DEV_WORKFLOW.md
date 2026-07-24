# Development Workflow

This repository uses a two-tree local workflow:

- Stable tree: `D:\pcninja-smart-office-installer` on `main`.
- Development tree: `D:\pcninja-smart-office-installer-dev` on `dev`.

## Branches

- `main`: stable source that matches public releases.
- `dev`: active integration branch for fixes, new features, and release candidates.

## Release Candidates

Use prerelease tags for VM testing:

```text
vX.Y.Z-rcN
```

Example:

```text
v3.3.4-rc1
```

Windows file versions must stay numeric, for example:

```text
3.3.4.0
```

The executable keeps the stable product name during RC testing:

```text
Smart Office Installer.exe
```

## Required Checks

Before pushing `dev` or publishing an RC:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-OfficeSmartPublicReady.ps1
```

## Promotion

1. Test the RC on a VM.
2. Fix issues on `dev`.
3. Publish additional RCs if needed.
4. Merge `dev` into `main`.
5. Build a final non-RC executable.
6. Publish a final GitHub Release.
