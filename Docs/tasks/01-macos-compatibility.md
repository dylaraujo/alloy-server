# Task 01: macOS Compatibility

Status: **mostly done — see note below**
Depends on: nothing — this is the first task
Design doc reference: [`TRAINER_DESIGN.md` §9](../TRAINER_DESIGN.md#9-cross-platform-support-windows--macos)
Repos touched: `AlloyClient` (real bug), `alloy-server` (verification only)

Audio path fix and server-stack verification are both done and confirmed
on a real Mac (see checklist below). While verifying the "full session"
item, found that the client's renderer crashes on macOS for a reason this
task didn't anticipate: four shaders use Shader Storage Buffer Objects,
which require OpenGL 4.3+, and macOS's native OpenGL caps at 4.1. That's
a real engine change, not a quick path fix, so it's split out into
[`01.1-macos-opengl-rendering.md`](01.1-macos-opengl-rendering.md). This
task is done once that one lands and the full-session checkbox below can
be checked for real.

## Why this is first

Every other task in this plan gets built and tested on whatever machine is
handy — if that's a Mac and macOS is broken, every later task either gets
developed blind on Windows-only or silently inherits the same gap. Fixing
this first means every subsequent task doc can assume "runs on Mac" as a
given, not something to retrofit at the end.

## Scope

### 1. Fix the real bug: audio native-library path resolution
- File: `AlloyClient/Alloy.Audio/Utils/InternalUtils.cs`,
  `GetAudioBinaryPath()`.
- Currently only branches on `OSPlatform.Linux`/`OSPlatform.FreeBSD` and
  `OSPlatform.Windows`; anything else (including macOS) throws
  `NotSupportedException`.
- Fix: add an `OSPlatform.OSX` branch. Map to `osx-arm64` or `osx-x64`
  (check `RuntimeInformation.ProcessArchitecture`) and file `libopenal.dylib`.
- **No new native binary needed** — `Silk.NET.OpenAL.Soft.Native` (already
  referenced in `Alloy.Audio.csproj`) ships `libopenal.dylib` for both
  `osx-x64` and `osx-arm64` already; confirmed present under
  `AlloyClient/bin/Debug/net10.0/runtimes/osx-{x64,arm64}/native/` in a
  local build. This is purely a missing path-mapping branch.

### 2. Confirm (don't touch unless broken): Windows-only perf P/Invokes
- File: `AlloyClient/Alloy.Engine/GameWindow.cs`, the "Windows Jank" region
  (`SetWindowsJank`/`ClearWindowsJank`, P/Invoking `kernel32`/`winmm.dll`).
- Both call sites (`GameWindow.cs:77-78, 113-114`) are already guarded by
  `OperatingSystem.IsWindows()`. On macOS these calls are skipped entirely.
- Action: none expected. Just confirm during manual testing that startup/
  shutdown on macOS doesn't hit this code path at all (it shouldn't).

### 3. Verify the server stack on macOS
- Run `AccountServer` and `GameServer` via `dotnet run --project <X>` on
  macOS. Confirm:
  - `Common/Messaging/IpcServer.cs`/`IpcClient.cs`'s `NamedPipeServerStream`/
    `NamedPipeClientStream` IPC connects cleanly (.NET implements these over
    Unix domain sockets on macOS — expected to just work, but unverified).
  - LiteDB opens the DB file without issue in `ConnectionType.Shared` mode.
  - Config/resource loading resolves correctly (already uses
    `AppContext.BaseDirectory` + `Path.Combine`, fixed in `265a0e3` —
    should already be fine, just confirm).
- No code changes expected here unless something actually fails — this is
  a verification pass, not a rewrite.

### Explicitly out of scope for this task
- `Alloy.ContentBuilder`'s `FontBuilder` (MSDF font-atlas generation) is
  Windows-exe-bound (`msdf-atlas-gen-w64.exe`) and not fixed here. It's the
  offline asset pipeline, not the runtime game — doesn't block playing.
  Revisit only if someone needs to rebuild font atlases on a Mac.

## Definition of done
- [x] `GetAudioBinaryPath()` resolves correctly on macOS (both Intel and
      Apple Silicon) instead of throwing. Fixed in
      `AlloyClient/Alloy.Audio/Utils/InternalUtils.cs` — added an
      `OSPlatform.OSX` branch mapping to `osx-arm64`/`osx-x64` and
      `libopenal.dylib`.
- [ ] `AlloyClient` launches on macOS with working audio. Blocked on
      [`01.1-macos-opengl-rendering.md`](01.1-macos-opengl-rendering.md) — the
      client currently crashes before reaching gameplay (SSBO/OpenGL 4.3+
      vs. macOS's 4.1 cap, unrelated to audio).
- [x] `AccountServer` and `GameServer` both start cleanly on macOS, IPC pipe
      connects, LiteDB file opens — no code changes needed, or any that
      were needed are documented here. Verified: HTTP listener up on
      127.0.0.1:8080, `alloy_rpc` named-pipe IPC connects (confirmed over
      macOS's Unix-domain-socket-backed implementation), GameServer
      listens on port 2050, LiteDB opens via `DbClient.Load`/
      `RealmManager.Init()` with no errors. One unrelated local-env fix
      needed: `Common.csproj` expects a sibling repo folder literally
      named `AlloyClient` (matching the GitHub rename already done via a
      `fork-origin` remote); if your local client checkout is still
      named `TOTMGClient`, add a symlink
      (`ln -s /path/to/TOTMGClient /path/to/AlloyClient`, as a sibling of
      `TOTMGServer`) rather than renaming your working copy.
- [ ] A full session (connect → see Nexus → move around) works on macOS
      using whatever login flow exists at the time this task is done
      (full Join-menu flow lands in Task 02 — this task just needs *a*
      working connect path to verify against, even the current
      username/password login is fine as a smoke test). Blocked on the
      same rendering issue as above — can't reach Nexus until
      `01.1-macos-opengl-rendering.md` is done.
