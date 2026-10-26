# Buddy · PSK Charm Edition

See [timing controls and repaired executables](docs/Timing.md) for exact task appearance times, repeat intervals, and PSK profile migration.

A Windows 10/11 desktop companion and productivity hub for PSK, built with C# / WPF on .NET 8. Buddy walks above the primary monitor's taskbar and offers water, stretch, task, and eye-break reminders.

### ✨ PSK Charm Dashboard Features
- **🏠 Dashboard Overview:** Live companion status, quick hydration metrics, pending task spotlight, and instant summon/dismiss actions.
- **🤖 My Companion Studio:** Edit companion name (`PSK`), persona title, mood (`Energetic ⚡`, `Calm 🧘`, `Hydration Coach 💧`), tune height (140–260 DIP) and walking speed (40–240 DIP/s), toggle breathing/bounce animations, and preview live poses (`idle_front`, `walk_right_1-8`, `drink_water`, `wave`, `sad`).
- **📋 My Tasks Planner:** Add tasks with title, category (`Coding 💻`, `Health 💧`, `Work 💼`, `Personal ⭐`) and priority (`High 🔥`, `Medium ⚡`, `Low ☕`), toggle completion, filter active/completed, and have Buddy remind you of pending tasks on screen!
- **⏰ Reminder Scheduler:** Configure and toggle reminders with preset templates (`Water`, `Stretch`, `Eye Rest`, `Deep Breathing`), custom intervals, active time windows, and instant `▶ Test on Screen` buttons.
- **📊 Hydration & Stats:** Track daily glasses of water toward your 8-glass goal with quick `+ Add Glass` / `- Undo` buttons, and view your 7-day visual bar chart history.
- **⚙️ Settings & System:** Auto-start with Windows, pop chime sounds, snooze/pause companion, and diagnostic tools.

## Run

For the ready-to-run build, double-click `Buddy.exe` in the project root. It bundles .NET and the avatar assets. Click the tray icon to open the **PSK Charm Dashboard**.

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then in PowerShell:

```powershell
dotnet build Buddy.sln
dotnet run --project src/Buddy
```

This workspace also has a local SDK at `.tools/dotnet/dotnet.exe` if `dotnet` on PATH has no SDK:

```powershell
& ./.tools/dotnet/dotnet.exe build Buddy.sln
$env:DOTNET_ROOT = (Resolve-Path .tools/dotnet).Path
& ./.tools/dotnet/dotnet.exe run --project src/Buddy
```

Buddy starts with a water reminder. Find its tray icon (possibly in the hidden icons menu) to show Buddy, pause, resume, edit settings, see stats, enable startup, or quit. `--quiet` suppresses the initial appearance. Only one instance runs per Windows session.

## Avatar assets

The included sprites are generated from the photograph provided in the conversation using the built-in image tool; no API key is needed to run Buddy. The prompt set and provenance are recorded in `assets/avatar-prompts.md`. To regenerate directly through the requested OpenAI Images API, save your original photo to `assets/source/me.jpg` and configure a private key:

```powershell
Copy-Item .env.example .env
notepad .env
dotnet run --project tools/AvatarGen
# Regenerate all poses (makes paid API requests):
dotnet run --project tools/AvatarGen -- --force
# Normalize existing images / recreate icons without an API key:
dotnet run --project tools/AvatarGen -- --normalize
# Import the included 4-column × 2-row walk sheet using a common sprite scale:
dotnet run --project tools/AvatarGen -- --import-walk-sheet assets/walk-cycle.png
```

Alternatively set `$env:OPENAI_API_KEY` in your current shell. Never commit your `.env` or API key. The API tool uses `gpt-image-1`, multipart reference PNGs, `1024x1536`, high quality, and transparent PNG output, following the [official image-edit API reference](https://developers.openai.com/api/reference/resources/images/methods/edit). It uses your source photo plus the idle sprite for the remaining poses, skips existing assets, and retries transient failures twice. Network requests can take several minutes and require account access / billing. Original photo files are gitignored.

Each pose is trimmed by alpha and normalized to a 400×600 transparent canvas with centered, bottom-aligned feet. Missing or unreadable sprites show a colored Buddy placeholder. Restart Buddy after changing sprites; sprites are loaded and frozen at startup. `--normalize` also builds a 16/32/48/256 ICO and 32-pixel tray portrait.

The API generator creates the original four walking poses. After using `--force` or `--normalize`, import the walk sheet again to restore the eight-frame cycle at its common scale.

## Reminders and persistence

Defaults: water every 45 minutes and stretch every 60 minutes, 09:00–19:00; the 20-minute eye break is initially disabled. Settings accept `{name}` in reminder text. Set kind to `water` only for reminders that should count glasses; any other kind uses DONE. Use `drink_water.png`, `wave.png`, or another filename in the avatar folder for the pose. Intervals are clamped to 1–1440 minutes. Overnight schedules work; equal start/end times mean all day.

Reminders queue while Buddy is showing. Idle time above five minutes and screen locking delay delivery; unlocking adds a two-minute quiet period. Fullscreen applications defer delivery five minutes. Two fullscreen deferrals trigger an actionable Windows toast; toast availability depends on Windows notification settings. A manual tray request can show Buddy even during a pause. Pauses apply to automatic reminders.

Settings and date-keyed hydration statistics are stored in `%AppData%\Buddy\settings.json` and `stats.json`, written using a temporary file followed by replacement. Invalid JSON is backed up before defaults load. Recoverable toast diagnostics are saved in that folder; unhandled failures go to `%LocalAppData%\Buddy-error.log`. Times use the Windows user's local timezone.

## Publish a standalone executable

```powershell
dotnet publish src/Buddy -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false `
  -o artifacts/publish
```

Run `artifacts/publish/Buddy.exe`. Assets are bundled and extracted with the app on first launch. To customize the bundled artwork, change the source assets and republish. The Settings folder button opens the active extracted avatar directory. The `Windows.pubxml` profile includes the same publish options.

## Installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php), publish first, then:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer/buddy.iss
```

The installer goes to `artifacts/installer/Buddy-Setup.exe`. It installs per user, adds a Start Menu entry, and offers desktop and startup checkboxes. Quit Buddy before manually replacing its executable.

## Troubleshooting

- **SmartScreen:** the executable is unsigned. Windows may show an unknown publisher warning. Distribution should use code signing; only run builds you trust.
- **DPI / taskbar:** Buddy uses PerMonitorV2 awareness and the primary monitor's work area. It recalculates position when display, DPI, or taskbar work area changes. Auto-hide taskbars may overlap briefly while animating. Buddy currently lives on the primary monitor.
- **No reminder:** check active hours, enabled reminders, pause, idle time, screen lock, and fullscreen applications. Tray → Show buddy now is a manual override.
- **No toast / sound:** enable Buddy notifications in Windows and the sound toggle in Buddy settings. Notification failures do not prevent normal reminders.
- **API generation:** place the source JPEG at the exact path above, check your key privately, and confirm model access. A failed pose can be resumed without `--force`.

The UI uses H.NotifyIcon.Wpf, MVVM observable models from CommunityToolkit.Mvvm, Windows toast support from Microsoft.Toolkit.Uwp.Notifications, and built-in System.Text.Json. AvatarGen uses ImageSharp. Review their licenses before distributing.

## Validation

```powershell
dotnet run --project tests/Buddy.Checks
```

This Windows-only validation harness checks settings and statistics using isolated temporary data, invalid JSON recovery, overnight schedules, snooze and duplicate-confirmation behavior, every sprite's size/alpha/foot alignment, sound integrity, native overlay styles, taskbar alignment, UI loading, and the rendered walk/response/exit lifecycle. It briefly opens test windows and does not run Buddy's normal startup logic. Fullscreen apps, lock/unlock, toast activation, and display changes also need manual testing on your target PCs.
