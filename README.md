# Smart Office Installer

Smart Office Installer is a Windows WinForms utility for installing or removing Microsoft Office through the official Microsoft Office Deployment Tool.

The current stable release is `v3.3.10`, with Windows file version `3.3.10.0`.

The executable is named `Smart Office Installer.exe`; its version is shown in Windows file properties and installed-app metadata.

## What it does

- Installs Office LTSC 2024 volume products.
- Installs Microsoft 365 apps with selectable update channels.
- Supports optional Visio and Project selections.
- Supports language selection.
- Can remove existing Office Click-to-Run installations.
- Can prepare an offline LTSC install package.
- Checks GitHub for new public releases, shows an update prompt, and keeps a header update button available afterward.

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
bin\Release\net48\Smart Office Installer.exe
```

You can also run:

```powershell
.\build.ps1
```

which builds the app and copies the executable into `artifacts\`.

## Compact layout candidate

The unpublished `3.3.11.0` candidate fits the family screen without scrolling.
When space is limited, it keeps the website link and product choices and hides
the large logo and repeated card descriptions. Navigation remains in the footer.
The existing scrolling on later product and language pages is retained.

Run the GUI-only regression check after building:

```powershell
powershell -NoProfile -STA -ExecutionPolicy Bypass -File scripts\Test-CompactLayout.ps1
```

Local coverage includes client areas 740x800, 740x600, 740x420, 720x500 and
500x480, navigation across LTSC, Microsoft 365, removal and offline package flows,
returning from a scrolled page, and completion controls. These checks suppress
update HTTP and do not install or remove Office. Real resolution/DPI VM acceptance
is pending. The stable release and update manifest remain at v3.3.10.

## Release artifact

Verified release EXE files are kept locally under `release-assets\` for upload to GitHub Releases. They are intentionally ignored by Git so the repository stays source-focused.

Verified stable `v3.3.10` SHA256:

```text
18761e60b81a1ec68f600d7e8fdc61989f48bbf6beefa3ee14266c2c30605ff2
```

## Notes

- The release executable is not digitally signed.
- The app self-elevates through Windows UAC when administrator permissions are required.
- The Microsoft Office Deployment Tool is downloaded from Microsoft at runtime.

## License

No open-source license has been selected yet. Until a license is added, all rights are reserved by the owner.
