# PcNinja Smart Office Installer

PcNinja Smart Office Installer is a Windows WinForms utility for installing or removing Microsoft Office through the official Microsoft Office Deployment Tool.

The current stable release is `v3.3`, with Windows file version `3.3.3.1`.

The current release candidate is `v3.3.4-rc1`, with Windows file version `3.3.4.0`.

## What it does

- Installs Office LTSC 2024 volume products.
- Installs Microsoft 365 apps with selectable update channels.
- Supports optional Visio and Project selections.
- Supports language selection.
- Can remove existing Office Click-to-Run installations.
- Can prepare an offline LTSC install package.

This project does not include Microsoft Office, private licensing material, activation bypasses, or cracked software. Users must have valid Microsoft licensing for the products they deploy.

## Requirements

- Windows x64.
- .NET Framework 4.8 runtime to run the app.
- Administrator permissions for installation and removal operations.
- Internet access when downloading the Microsoft Office Deployment Tool or Office installation files.

## Build

Build on Windows with a recent .NET SDK:

```powershell
dotnet build .\PcNinja.SmartOfficeInstaller.csproj -c Release
```

The compiled executable is written to:

```text
bin\Release\net48\OfficeSmart-v3.3.4-rc1.exe
```

You can also run:

```powershell
.\build.ps1
```

which builds the app and copies the executable into `artifacts\`.

## Release artifact

Verified release EXE files are kept locally under `release-assets\` for upload to GitHub Releases. They are intentionally ignored by Git so the repository stays source-focused.

Verified stable `v3.3` SHA256:

```text
498922A68EA7A2BCC94B96E5E8A15425BAFE842B6B5613F0304821D634080ED1
```

## Notes

- The release executable is not digitally signed.
- The app self-elevates through Windows UAC when administrator permissions are required.
- The Microsoft Office Deployment Tool is downloaded from Microsoft at runtime.

## License

No open-source license has been selected yet. Until a license is added, all rights are reserved by the owner.
