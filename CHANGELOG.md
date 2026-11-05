# Changelog

## v3.3.15

- Name predecessor backups with their actual four-part file version before `.exe`, instead of OLD prefixes.
- Reuse an existing byte-identical backup; if a version filename contains different bytes, preserve both using a SHA256 suffix for the additional predecessor.
- Convert the current upgrade's legacy OLD archive when upgrading from earlier helpers, while retaining compatibility with old recovery journals and completed staging cleanup.
- Keep the active EXE name unchanged and remove completed update work directories.
- Windows file version: `3.3.15.0`.

## v3.3.14

- Automatically remove verified update work files after successful upgrade, leaving the current EXE and its OLD predecessor beside each other.
- Clean completed staging from earlier releases when the new GUI starts; a separate internal cleanup process can finish even if the GUI is closed immediately.
- Preserve pending recovery, unknown files and changed bytes. Never recursively delete application directories or remove OLD archives.
- Windows file version: `3.3.14.0`.

## v3.3.13

- Opening the installer while it is already running no longer starts a second copy;
  the running window is restored and brought to the front, without a UAC prompt.
- Uses the shared `shared/PcNinjaSingleInstance.cs` guard (same file in all PcNinja tools).
- Verify update/recovery handoff with the single-instance guard held by the old GUI and reacquired by its successor.
- Windows file version: `3.3.13.0`. Owner VM checks and manual GUI acceptance completed before publication.

## v3.3.12

- Add consent-based in-app download, verification and same-name restart with OLD predecessor archival.
- Add cancellation, busy-state guards, bounded helper handoff, GUI readiness and recovery diagnostics.
- Replace the same-name EXE with a verified backup and complete interrupted updates on the next normal startup.
- Windows file version: `3.3.12.0`. Owner VM acceptance completed for same-name replacement, exact OLD archive and all four interrupted-update recovery cases.
- Earlier releases retain their browser download flow for the one-time upgrade to this version.

## v3.3.11

- Fit the family screen without scrolling; compact displays retain the website link instead of the large logo and repeated descriptive details.
- Keep navigation buttons accessible after changing steps and completing an operation.
- Windows file version: `3.3.11.0`. Owner VM acceptance completed before publication.

## v3.3.10

- Fixed installer layout on small displays so the window fits the available screen area and key controls remain accessible.
- Windows file version: `3.3.10.0`.

## v3.3.9

- Renamed the application and release executable to `Smart Office Installer`.
- Moved the release version to Windows file properties and installed-app metadata instead of the executable filename.
- Improved offline download progress reporting with observed download size and transfer rate.
- Windows file version: `3.3.9.0`.

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
