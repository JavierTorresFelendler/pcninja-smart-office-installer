# Portable app update POC

Draft implementation for the owner-requested WinUpgrade-style app updater.
Reference reviewed: Win-Upgrade-Tool-by-PcNinja main `06f5344`, including
`docs/APPLICATION-UPDATES.md`, AppUpdateFiles, AppUpdateInstaller and GUI readiness.
This application remains .NET Framework 4.8; no WinUpgrade/Windows Setup dependency
or framework migration is introduced.

## Intended behavior

- Keep `Smart Office Installer.exe` as the local name. File version identifies a
  release; versioned filenames are unnecessary. GitHub retains its compact asset
  name `SmartOfficeInstaller.exe`.
- Startup checks metadata only. Explicit consent starts an in-app download dialog
  with cancellation. Opening the main update button no longer downloads through
  a browser. Existing v3.3.11 copies need one manual bootstrap to a future approved
  release containing this updater; they cannot gain new behavior retroactively.
- Pin stable product/repository/tag/asset identity. Download the tag-specific EXE,
  bounded by time and bytes. Verify checksum, x64 PE, product/company and newer
  Windows file version. Honor minimumSupportedVersion. Refuse signed executables
  or required-signature releases until a publisher-verification policy is added.
  Current unsigned releases rely on GitHub HTTPS and published checksum trust.
- Use the exact running EXE directory, never the shell working directory. Accept
  ordinary fixed local NTFS paths only, rejecting reparse points and hard links.
  A protected Administrators/SYSTEM staging directory contains a unique operation,
  candidate, helper and recovery journal. Existing unsafe staging is refused.
- A verified private copy of the current app runs an internal helper. Bounded
  stdin/stdout READY/GO binds the handoff to the parent PID, start time, path and
  checksum. The old window remains open until the helper is ready.
- After the exact parent exits, handle-bound, no-overwrite renames move the exact
  predecessor into private backup and the candidate to the canonical name. The
  helper retains file and ancestor locks through launch. Launch uses no arguments;
  only an opaque readiness pipe name is passed in the child's environment.
- Authenticate the pipe client PID against the retained launched process. Confirm
  shown/painted GUI readiness before archiving the exact predecessor as OLD-.
  OLD.V1-, OLD.V2-, etc. retain collisions without overwrite (up to 1000 attempts).
- If launch fails and the successor is certainly gone, restore the predecessor
  without overwriting another file. If a successor is still alive, retain it and
  the private backup; do not terminate possible user work. Report incomplete
  startup and retain diagnostic/recovery files.

Office product/channel/language choices, XML generation and deployment are
unchanged. App updating is blocked during this app's Office work, and Office
work cannot start during app updating. No reboot, service, scheduled task or
OS-state reset is part of this feature.

## Verification

`Test-OfficeSmartPublicReady.ps1` builds, checks the existing package/manifest and
scans public source. Its subprocess failures now fail the outer check; precise
.NET cancellation identifiers are excluded from a credential-word false positive.
Actual credential identifiers are still scanned.

```powershell
dotnet build tests/UpdaterChecks/UpdaterChecks.csproj -c Release -warnaserror
& ./tests/UpdaterChecks/bin/Release/net48/UpdaterChecks.exe
powershell -NoProfile -STA -ExecutionPolicy Bypass -File scripts/Test-CompactLayout.ps1
powershell -NoProfile -STA -ExecutionPolicy Bypass -File scripts/Test-AppUpdateGui.ps1
```

Fixtures cover pinned offers, bounded/cancelled transfers, fake HTTP status and
redirect refusal, length mismatch, authentic candidate metadata/hash checks,
unsafe inherited staging ACL refusal, handle-bound rename, occupied target,
changed predecessor, OLD collision, rollback and real child GUI readiness. File
transaction fixtures use disposable non-executable bytes and a test-only ACL
scope restricted to an exact random child of the current temporary directory.
No production argument enables that scope.
Mocked GUI checks additionally verify decline, cancellation, transfer failure and
duplicate update attempts without any HTTP, helper or Office operations.

`scripts/Test-UpdateLifecycle.ps1` additionally builds two harmless executable
fixtures and, when elevated, tests the actual parent/helper same-name handoff,
production staging ACLs, new GUI readiness and predecessor hash/archive. It skips
explicitly without an administrator context. Fixtures never deploy Office. CI
runs these checks; local non-elevated results alone do not prove the full handoff.

## Release hold and recovery limits

This is a draft POC, not authorization to publish or advance the stable manifest.
The stable manifest remains at v3.3.11. A future newer, separately approved
stable release is required for a real in-app update through public metadata.

The two no-overwrite renames are not an atomic single replacement. Power loss or
helper termination between them can temporarily leave the canonical path absent.
A flushed private `transaction.json` records exact paths/hashes before the move;
the previous bytes remain at `previous.exe` in that same operation directory.
If there is no confirmed successful startup, inspect that exact journal/result
as administrator and recover only its matching previous file into an unoccupied
original path. Never scan for a newest EXE or overwrite an unrelated file.
There is no automatic interrupted-operation recovery/cleanup yet. Add durable
recovery/atomicity coverage before production release. Retained staging may need
owner-directed cleanup after acceptance. Mixed-DPI/remote-session behavior and
real public stable-to-stable update acceptance remain VM work.
