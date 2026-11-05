# Smart Office Installer

Smart Office Installer is a Windows WinForms utility for installing or removing Microsoft Office through the official Microsoft Office Deployment Tool.

The current stable release is `v3.3.15`, with Windows file version `3.3.15.0`.

The executable is named `Smart Office Installer.exe`; its version is shown in Windows file properties and installed-app metadata.

## What it does

- Installs Office LTSC 2024 volume products.
- Installs Microsoft 365 apps with selectable update channels.
- Supports optional Visio and Project selections.
- Supports language selection.
- Can remove existing Office Click-to-Run installations.
- Can prepare an offline LTSC install package.
- Checks GitHub for new public releases, shows an update prompt, and keeps a header update button available afterward.
- Keeps one GUI per Windows session; launching it again restores and activates the running window without another UAC prompt.

Updates download inside the app after consent, verify the new executable, restart
under the same filename and preserve the previous copy with its old file version
before `.exe`, for example `Smart Office Installer-v3.3.14.0.exe`. See
[portable app updates](docs/PORTABLE-APP-UPDATES.md) for behavior and recovery.
Completed update work directories are removed automatically, including verified
leftovers from earlier releases. Pending recovery and unrecognized files are preserved.
Existing v3.3.11 and earlier copies use their previous browser download flow once
to obtain this release; subsequent updates use the new in-app flow.

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

## Compact layout

Version `3.3.11.0` fits the family screen without scrolling.
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
update HTTP and do not install or remove Office. The owner accepted the VM
candidate before publication of v3.3.11. Later releases retain that layout.

## Release artifact

Verified release EXE files are kept locally under `release-assets\` for upload to GitHub Releases. They are intentionally ignored by Git so the repository stays source-focused.

Verified stable `v3.3.15` SHA256:

```text
f6283fab9ebf4d4fe851b08b40998f69693186d79432dc52ef345f5ea697eff5
```

## Notes

- The release executable is not digitally signed.
- The app self-elevates through Windows UAC when administrator permissions are required.
- The Microsoft Office Deployment Tool is downloaded from Microsoft at runtime.

## License

No open-source license has been selected yet. Until a license is added, all rights are reserved by the owner.
