# Claude Usage Pet

**A tiny pixel-art desktop pet for Windows that keeps an eye on your Claude usage limits — and gets visibly nervous when you're about to run out.**

[![Release](https://github.com/T3rr0rS0ck3/claudepet/actions/workflows/release.yml/badge.svg)](https://github.com/T3rr0rS0ck3/claudepet/actions/workflows/release.yml)
![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4)
![License](https://img.shields.io/badge/license-MIT-green)

🇩🇪 [Deutsche Version](README.de.md)

![The pet in different moods: happy after a reset, thinking, panicking, sleeping](docs/moods.png)

Claude Pro and Max plans have a rolling **5-hour session limit** and a **weekly limit**. Claude Usage Pet sits on your
desktop and shows how much of both you've used — at a glance, without opening anything. The more you use, the more
its mood changes: relaxed → thoughtful → nervous → panicking → asleep until the limit resets.

> The app's interface is currently in German. Bubble texts can be changed to any language (see [Configuration](#configuration)).

## Features

- 🟠 **Desktop pet** — transparent, borderless, draggable, optionally always on top
- 📊 **Usage overlay** — click the pet to see session and weekly usage, reset times and a forecast
- 🎭 **7 moods** with their own animations, based on the higher of the two usage values
- 💬 **Speech bubbles** when thresholds are crossed, e.g. *"90 %! 😰"* — not constantly chattering
- 🔔 **Windows notifications** at configurable session and weekly warning thresholds
- 📈 **Forecast** — "at the current pace you'll hit the limit in 1h 42m"
- 🧺 **Tray icon** that reflects the current mood, plus autostart with Windows
- ⚙️ **Configurable** thresholds, texts, size, update interval and more
- 🔒 **Local only** — no network access, no login, no tokens; it only reads what Claude Code already hands to its status line

<p align="center"><img src="docs/overlay.png" alt="Usage overlay showing session 80 %, week 70 %, reset times and a forecast" width="390"></p>

## Requirements

- Windows 10 or 11 (x64)
- [Claude Code](https://code.claude.com) (the CLI), signed in with a **Claude Pro or Max** subscription
  (Claude Code only receives rate-limit data for these plans)

No .NET installation needed — the installer is self-contained.

## Installation

1. Download **`ClaudePet-Setup-x.y.z.exe`** from the [latest release](https://github.com/T3rr0rS0ck3/claudepet/releases/latest).
2. Run it. No admin rights required (installs to `%LOCALAPPDATA%\Programs\ClaudePet`).
3. Keep **"Connect to Claude Code"** checked. This adds the pet as Claude Code's `statusLine` (only if you don't have one yet).
4. Send any message in Claude Code — after the first response the pet knows your usage.

Prefer no installer? Grab the `portable.zip` from the release, extract it anywhere and start `ClaudePet.exe`.
Then right-click the pet → *Claude Code verbinden…* (connect).

> **Windows SmartScreen** may warn about an unknown publisher because the installer isn't code-signed.
> Click *More info → Run anyway*.

## Using the pet

| Action | Result |
|---|---|
| Left-click the pet | Open / close the usage overlay |
| Drag the pet | Move it (position is remembered) |
| Right-click the pet | Menu: usage, say hello, always on top, minimize to tray, connect Claude Code, settings, quit |
| Left-click the tray icon | Open the usage overlay |
| Start `ClaudePet.exe` again | Brings the running pet back and opens the overlay |

### Moods

The mood follows the **higher** of session and weekly usage. All thresholds are configurable.

| Usage | Mood | What the pet does |
|---:|---|---|
| 0–49 % | 🟢 relaxed | breathes, blinks, waves now and then |
| 50–69 % | 🟢 normal | looks around |
| 70–84 % | 🟡 attentive | thinks hard (thought dots) |
| 85–89 % | 🟠 nervous | sweats, fidgets |
| 90–94 % | 🔴 worried | big eyes, trembling |
| 95–99 % | 🔴 panic | turns red, arms up, flashing "!" |
| 100 % | 😴 limit reached | sleeps until the reset |

While Claude Code is actively sending data, a calm pet "types" along. After a reset it cheers.

## How it works

```text
Claude Code ── status line JSON (stdin) ──▶ ClaudePetBridge.exe ──▶ %LOCALAPPDATA%\ClaudePet\usage.json
                                                   │                                  ▲
                                                   │                                  │ polls
                                                   └── prints the status line   ClaudePet.exe (the pet)
```

Claude Code runs its configured [status line command](https://code.claude.com/docs/en/statusline) after each response
and passes session data — including `rate_limits.five_hour` and `rate_limits.seven_day` — as JSON on stdin.
`ClaudePetBridge.exe` is that command: it stores the values locally and prints a compact status line back into Claude Code:

```text
[Opus] Session 73% (↻ 2h 14m) · Woche 61%
```

The pet app runs independently and keeps working when no Claude Code terminal is open; values whose reset time
has passed are treated as 0 %. Nothing is scraped from claude.ai and no credentials are read.

## Configuration

Most options are available via right-click → **Einstellungen…** (settings): size, always on top, animations, autostart,
update interval, warning thresholds, mood thresholds, speech bubbles, notifications and your name.

Everything lives in `%LOCALAPPDATA%\ClaudePet\settings.json`, and manual edits are picked up live.
Speech bubble texts are under `Texts`. Each event can have several variants; one is picked at random:

```json
"Texts": {
  "Worried":   ["{percent} %! 😰"],
  "Panic":     ["{NAME}. ALMOST EMPTY."],
  "Exhausted": ["Okay... sleeping until the reset."],
  "Reset":     ["Fresh quota! Let's go!"]
}
```

Events: `Greeting`, `NoData`, `Normal`, `Attentive`, `Nervous`, `Worried`, `Panic`, `Exhausted`, `Reset`, `Poke`.
Placeholders: `{name}`, `{NAME}` (upper case), `{percent}` (the higher value), `{session}`, `{week}`.

## Troubleshooting

**The pet says it's waiting for data.**
Check that the pet is connected (right-click → settings shows the status), then send a message in Claude Code.
Rate-limit data only arrives after the first response of a session and only for Pro/Max plans.

**I already had a custom status line.**
The installer never overwrites it. Connecting from the app asks first and saves a backup as
`~/.claude/settings.json.claudepet-backup`. Only one status line command can be active.

**Something seems off.**
Look at `%LOCALAPPDATA%\ClaudePet\log.txt`. `usage.json` there shows the last values received from Claude Code.

## Uninstall

Uninstall *Claude Usage Pet* via Windows Settings → Apps. This also removes the status line entry from Claude Code and
the autostart entry. Your settings stay in `%LOCALAPPDATA%\ClaudePet` — delete that folder for a clean slate.

## Building from source

Requires the .NET 8 SDK (or newer).

```powershell
git clone https://github.com/T3rr0rS0ck3/claudepet.git
cd claudepet
.\build.ps1                        # framework-dependent build → dist\ClaudePet
.\build.ps1 -SelfContained -Version 1.2.3
.\dist\ClaudePet\ClaudePet.exe
```

Set `CLAUDEPET_DATA_DIR` to point the app and bridge at a different data folder, e.g. for testing with fake `usage.json` files.

### Releases

Pushing a tag like `v1.2.3` runs the [release workflow](.github/workflows/release.yml). It builds a self-contained
version, packs the Inno Setup installer and a portable zip, and publishes a GitHub release. Tags with a suffix
(`v1.2.3-beta`) become pre-releases. Existing tags can be rebuilt via *Actions → Release → Run workflow*.

Signing through SignPath runs once the repository variable `SIGNPATH_ORGANIZATION_ID` and the secret
`SIGNPATH_API_TOKEN` are set (optional: `SIGNPATH_PROJECT_SLUG`, default `claudepet`, and
`SIGNPATH_SIGNING_POLICY_SLUG`, default `release-signing`). The files to sign are defined in
[`.signpath/artifact-configuration.xml`](.signpath/artifact-configuration.xml). Without these settings, releases are built unsigned.

### Project structure

```text
src/Shared/            Data model and file access shared by app and bridge
src/ClaudePet.Bridge/  The status line command
src/ClaudePet/Core/    Settings, mood logic, forecast, autostart, Claude Code setup
src/ClaudePet/Pet/     Procedural pixel sprite, animations, pet window
src/ClaudePet/Views/   Usage overlay and settings window
installer/             Inno Setup script
```

## Roadmap

- English UI / language switch
- Per-model limits (e.g. Opus / Sonnet) once the data is reliably available
- Usage history and statistics
- More characters, skins and sound effects

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Only release builds produced by the [release workflow](.github/workflows/release.yml) on GitHub-hosted runners
  from the source code in this repository are signed.
- Every signing request is approved manually.

| Role | Members |
|---|---|
| Committers and reviewers | [T3rr0rS0ck3](https://github.com/T3rr0rS0ck3) |
| Approvers | [T3rr0rS0ck3](https://github.com/T3rr0rS0ck3) |

**Privacy:** This program will not transfer any information to other networked systems unless specifically requested
by the user. It only reads the status line data that Claude Code passes to it locally and stores it in `%LOCALAPPDATA%\ClaudePet`.

## Disclaimer

This is an unofficial fan project and is not affiliated with or endorsed by Anthropic.
The pixel character is an original drawing inspired by the Claude mascot. "Claude" is a trademark of Anthropic.

## License

[MIT](LICENSE) © Robin Wessel
