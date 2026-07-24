# Changelog

## v3.3.8

- Restored the previous installer/window icon design with a transparent icon background.
- Replaced the in-window brand image with the new PcNinja logo asset.
- Removed the black background from the in-window brand image so it renders transparently.
- Windows file version: `3.3.8.0`.

## v3.3.7

- Replaced the header logo mark with "JT" initials.
- Windows file version: `3.3.7.0`.

## v3.3.6

- Added a persistent header update button so users can still open the newest GitHub release after dismissing the startup update prompt.
- The update button now shows `Check updates`, `Update vX.Y.Z`, or `Up to date` based on the GitHub manifest result.
- Manual update checks now tell the user when the running version is already current.
- Expanded the update manifest regression check to cover the persistent update button flow.
- Windows file version: `3.3.6.0`.

## v3.3.5

- Public stable release promoted from the GitHub update notification RC line.
- Added a GitHub update check based on the public `update-manifest.json` flow used by PcNinja WinUpdate Tool.
- The app now checks GitHub on startup and prompts the user when a newer Smart Office Installer release is available.
- The update prompt opens the GitHub-hosted release asset URL instead of attempting to replace the running executable.
- Added `public-release/update-manifest.json` and a regression check for manifest shape, HTTPS URLs, SHA256 format, and updater source wiring.
- Windows file version: `3.3.5.1`.

## v3.3.5-rc1

- Added a GitHub update check based on the public `update-manifest.json` flow used by PcNinja WinUpdate Tool.
- The app now checks GitHub on startup and prompts the user when a newer Smart Office Installer release is available.
- The update prompt opens the GitHub-hosted release asset URL instead of attempting to replace the running executable.
- Added `public-release/update-manifest.json` and a regression check for manifest shape, HTTPS URLs, SHA256 format, and updater source wiring.
- Windows file version: `3.3.5.0`.

## v3.3.4

- Public stable release promoted from the offline-package RC line.
- Renamed the app title and metadata to `PcNinja's Smart Office Installer`.
- Clarified offline mode as `Create offline package only - do not install on this PC`.
- Clarified volume activation and previous-Office removal labels so package settings are not confused with immediate local actions.
- Offline package mode now uses package-oriented step and button wording.
- Final offline packages are created under `OFFICE-OFFLINE` with `Data`, `setup.exe`, `Office_Config.xml`, and `Install-Office.bat` together.
- Windows file version: `3.3.4.3`.

## v3.3.4-rc3

- Changed offline package creation so the selected folder is treated as the parent destination.
- The final movable package is now created as an `OFFICE-OFFLINE` folder under the selected location.
- If the selected folder is already named `OFFICE-OFFLINE`, it is used directly instead of nesting another folder.
- Updated offline mode UI text and regression checks for the `OFFICE-OFFLINE` workflow.

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
- Product metadata: `PcNinja Smart Office Installer`, `PcNinja.Pro`.
- Includes embedded PcNinja icon and branding resources.
- Supports LTSC, Microsoft 365, and Office removal flows.
- Supports offline LTSC package preparation.
