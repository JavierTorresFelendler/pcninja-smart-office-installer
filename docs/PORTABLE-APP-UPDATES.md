# Portable app updates

Released in v3.3.12 after owner VM acceptance of the WinUpgrade-style app updater.
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
  a browser. Existing v3.3.11 and earlier copies use their previous browser download
  flow once to obtain v3.3.12; they cannot gain new behavior retroactively.
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
- After the exact parent exits, preserve an independently verified private snapshot
  and replace the same-name canonical file using Windows `File.Replace`, retaining
  its displaced target as well. A different-name existing target is never overwritten.
  The helper verifies and pins the canonical file through launch. Launch uses no arguments;
  only an opaque readiness pipe name is passed in the child's environment.
- Authenticate the pipe client PID against the retained launched process. Confirm
  shown/painted GUI readiness before archiving the exact predecessor as OLD-.
  OLD.V1-, OLD.V2-, etc. retain collisions without overwrite (up to 1000 attempts).
- If launch fails and the successor is certainly gone, restore the predecessor
  with another retained backup. If a successor is still alive, retain it and
  the private backup; do not terminate possible user work. Report incomplete
  startup and retain diagnostic/recovery files.
- Publish a protected, flushed active-operation pointer only after the helper is
  ready. On normal GUI startup, skip a live matching helper; otherwise inspect that
  exact journal. A confirmed new GUI finishes OLD archiving, a current predecessor
  clears an uncommitted/rolled-back handoff, and an already archived predecessor is
  identified by deterministic names and exact hash. No newest-file guessing.

Office product/channel/language choices, XML generation and deployment are
unchanged. App updating is blocked during this app's Office work, and Office
work cannot start during app updating. No reboot, service, scheduled task or
OS-state reset is part of this feature.

Freshly renamed net48 executables can fail CLR startup while a DELETE-access
handle remains open (reproduced as 0x80131700). After publication, release
that movable handle and reacquire a read-only/no-delete-sharing guard, rehashing
the exact canonical file before launch. A raced substitution is refused. Rollback
also checks the current hash and retains any displaced file while restoring the snapshot.
The real renamed-executable fixture covers this distinction; a fixture that only
launches an already loaded executable can conceal the failure.

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

## Acceptance and recovery limits

The owner accepted the VM fixture from source 440d7d1: full elevated same-name
replacement, new GUI readiness, exact OLD bytes and all four recovery cases.
CI run 41849472331 passed the same checks. The released application is the exact
accepted 3.3.12.0 binary; only documentation and stable metadata changed afterward.
A future newer stable release is required to exercise public in-app version advancement.

`File.Replace` removes the application-created gap between two separate moves.
The flushed private journal and independently verified predecessor snapshot also
cover documented Windows replacement failures. ReplaceFileW can report a partial
failure with the displaced name present but the canonical name absent; the helper
restores an unoccupied canonical name from its exact snapshot. See
[Microsoft's ReplaceFileW contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).

Interrupted handoffs complete on normal startup after the window is shown/painted.
If storage failure prevents normal startup altogether, run `helper.exe` from the
exact operation directory as administrator: its no-argument recovery entry validates
its own hash and the protected journal/marker, refuses a live parent, restores only
an unoccupied original path from verified bytes and runs only a recorded version.
It never opens Office actions from staging. An unknown current hash is retained
for diagnosis rather than overwritten.

Fixtures simulate interruption before publication, after publication and after
archive; an elevated real-GUI fixture checks all three normal-startup recovery
paths. Actual electrical power-loss/storage-corruption testing is not claimed.
Staging is retained for diagnosis and may need owner-directed cleanup. Mixed-DPI,
remote sessions and public stable-to-stable acceptance remain owner VM checks.
