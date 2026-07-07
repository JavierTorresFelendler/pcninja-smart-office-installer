# Changelog

## v3.3.4-rc2

- Removed the extra offline package destination prompt during download.
- Changed offline package creation so the selected folder becomes the complete movable package root.
- Final offline package contents now stay together in one folder: `Data`, `setup.exe`, `Office_Config.xml`, and `Install-Office.bat`.
- Added internal staging under the selected folder and cleans it after the package is created.
- Updated the offline package layout regression check for the single-folder workflow.

## v3.3.4-rc1

- Fixed offline package layout so the selected destination folder is the final package root containing `setup.exe`, `Office_Config.xml`, `Install-Office.bat`, and the downloaded `Office` source folder.
- Added a regression check for offline package layout.

## v3.3

- Public release candidate based on the verified `OfficeSmart-v3.3.exe` artifact.
- Windows file version: `3.3.3.1`.
- Assembly version: `3.3.0.0`.
- Product metadata: `PcNinja Ultimate Office Installer`, `PcNinja.Pro`.
- Includes embedded PcNinja icon and branding resources.
- Supports LTSC, Microsoft 365, and Office removal flows.
- Supports offline LTSC package preparation.
