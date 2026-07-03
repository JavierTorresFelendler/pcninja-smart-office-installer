# Agent Guide

This repository is the public source for PcNinja Smart Office Installer, a Windows WinForms utility for Microsoft Office deployment through the official Microsoft Office Deployment Tool.

## Ground Rules

- Keep responses and project text in English unless the user explicitly asks otherwise.
- Treat the repository as public. Do not add private names, local machine paths, institutional references, private authentication material, or licensing data.
- Do not commit release executables. Put manually verified binaries under `release-assets\` only for GitHub Release upload; `.gitignore` keeps them out of source commits.
- Do not add Office binaries, cracked software, activation bypasses, or private licensing material.
- Use official Microsoft sources when changing Office Deployment Tool URLs, product IDs, channel IDs, or XML behavior.
- Prefer small, focused changes. This app is a single WinForms surface; avoid framework migrations unless the user asks.

## Important Files

- `PcNinja.SmartOfficeInstaller.csproj`: .NET Framework 4.8 WinForms project.
- `OfficeSmart\InstallerForm.cs`: main UI, flow, Office XML generation, ODT download/install/remove logic.
- `OfficeSmart\Program.cs`: UAC self-elevation and app startup.
- `OfficeSmart\OfficeFamily.cs`: LTSC, Microsoft 365, and Remove flow enum.
- `Properties\AssemblyInfo.cs`: product metadata and version numbers.
- `app.ico`, `OfficeSmart.favicon.ico`, `OfficeSmart.Ninja-DMT.png`: embedded branding resources.
- `scripts\Test-OfficeSmartPublicReady.ps1`: build and public-release safety check.
- `scripts\Set-OfficeSmartVersion.ps1`: version bump helper.

## Build

Use:

```powershell
dotnet build .\PcNinja.SmartOfficeInstaller.csproj -c Release
```

or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

## Required Validation

Before any release or public push:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-OfficeSmartPublicReady.ps1
```

The check must build successfully, confirm version metadata, and return no sensitive-string findings.

## Versioning

- `AssemblyVersion` uses the stable API-style version, currently `3.3.0.0`.
- `AssemblyFileVersion` and Windows ProductVersion use the precise release version, currently `3.3.3.1`.
- The executable name currently follows `OfficeSmart-v3.3.exe`.
- When preparing a new public release, update `Properties\AssemblyInfo.cs`, `README.md`, `CHANGELOG.md`, and GitHub release notes together.

## Release Flow

1. Make the code change.
2. Run the public-ready script.
3. Commit source only.
4. Push `main`.
5. Create a GitHub Release tag such as `v3.4`.
6. Upload the verified EXE as a release asset.
7. Confirm the uploaded asset digest matches the local SHA256.
