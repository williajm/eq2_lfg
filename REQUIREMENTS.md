# EQ2 LFG Monitor — Requirements

## Purpose

A Windows system-tray application (WPF, .NET 8) that continuously tails the active
EverQuest II chat log and alerts the user when a group advertises needing a
role/class/level that one of the user's enabled characters can fill.

Example: the LFG channel shows `Hyldor tells LFG (3), "NEED TANK 4 DPS CMM"` —
the app recognises "CMM" (Castle Mistmoore), sees the group needs DPS, and alerts
that **Nobwick lvl 62 Conjuror** is a good match.

## Data sources

### Character roster
- Parsed from `E:\games\eq2\williajm*_characters.ini` (all account files,
  including the `-eu` file). Each entry provides character name + server.
- The EQ2 install path is configurable (default `E:\games\eq2`).

### Class and level
- **Primary:** Daybreak Census API, no key required:
  `https://census.daybreakgames.com/json/get/eq2/character/?name.first=<Name>`
  filtered by `locationdata.world` = server. Provides class, adventure level,
  AA, tradeskill class/level.
- Fetched at startup, cached locally so the app works offline; refreshed
  periodically (configurable interval) and bumped live when a level-up message
  appears in the chat log.
- **Fallback hint:** the class chat channel recorded in
  `<Server>_<Name>_eq2_uisettings.xml` (EQ2 auto-joins each character to its
  class channel), used when Census is unavailable and no cache exists.

### Chat log
- The app watches the single active log file `logs\<Server>\eq2log_<Name>.txt`.
  The user usually runs one client; the most recently written log file
  identifies both the file to tail and the currently played character.
- Log lines look like:
  `(1784976352)[Sat Jul 25 11:45:52 2026] \aPC -1 Hyldor:Hyldor\/a tells LFG (3), "NEED TANK 4 DPS CMM "`

## Matching

### Scope
- Scan **all chat channels** — LFG, General, guild, ooc, custom channels, etc.
- **Group ads** (a group looking for members, e.g. `need tank 2 dps cmm`) are
  the alertable events.
- **Individual player posts** (`52 Warlock LF exp group`) are shown in the
  in-app traffic list for context but never trigger alerts.
- Sales / powerlevel-service spam (`WTS powerleveling 1-70 ...`) is filtered out.

### Intelligence
- Parses **roles**: tank, healer, DPS, support/utility.
- Parses **class names and common abbreviations** (nec, ilu, swash, sin, troub,
  wiz, lock, conj, mystic, fury, ...).
- Built-in **class → role mapping** (e.g. Warden/Fury/Mystic/Templar → healer;
  Guardian/Berserker/Monk/... → tank; Conjuror/Wizard/Warlock/... → DPS).
- Parses **stated levels** (`52 warlock`, `Lv45 Fury`).

### Zone-aware level matching
- A seeded zone table maps abbreviations → zone name → level band
  (e.g. `CMM` → Castle Mistmoore → 60–70).
- Seed data targets the current Wuoshi (TLE) era — Echoes of Faydwer,
  level cap 70 — and is stored in an editable config file **and** editable
  from a settings screen in the app, so the list can grow as expansions unlock.
- A group ad matches an enabled character when:
  1. the ad asks for the character's class explicitly, or its role; and
  2. the character's level fits the ad's stated level or the zone's level band
     (tolerance configurable).

### Character selection
- Availability is configured as a three-level hierarchy so entire branches can
  be excluded with a single click:
  1. **Account** (e.g. `williajm`, `williajm2`, ... `williajm-eu`)
  2. **Server** within the account (e.g. Wuoshi, Antonia Bayle, Thurgadin)
  3. **Character** within the server
- Each level has a checkbox; unticking an account or server disables everything
  beneath it without touching the individual character settings (tri-state
  display: checked / unchecked / partially checked).
- Matching runs only against characters whose account, server, and own checkbox
  are all enabled.
- The currently played character is included — a match may be for the character
  the user is already on.

## Alerting

- Alert styles are individually toggleable in settings:
  - **In-app** — match appears in the main window's match list.
  - **Windows toast notification** — visible even when EQ2 and the app are
    minimized.
  - **Sound** — distinct chime.
- **Cooldown per advertiser:** the first sighting of an ad alerts; repeats from
  the same advertiser are suppressed for a configurable window (default 15 min)
  unless the message materially changes (e.g. "need tank 4 dps" → "need tank").
  The in-app list instead shows the ad as still active with a last-seen time.
- No clipboard or auto-reply automation: alerts are informational only.

### Match display

When the app window is open, each match row must show **which of the user's
characters matched and why**, in the form:

> **Bramwick (lvl 59 Warden)** matches `Hyldor: "need healer CMM"` — healer, Castle Mistmoore 60–70

i.e. character name, level, class, the matched ad (advertiser + message), and
the reason for the match (role/class hit, zone/level fit). If several enabled
characters match one ad, all are listed on the match.

## App shape

- System-tray icon; closing the window minimizes to tray, app keeps monitoring.
- Main window:
  - **Matches** view — current/recent matches in the format above, newest first.
  - **Traffic** view — all recognised LFG-relevant chat (group ads and
    individual LFG posts) with last-seen times.
  - **Settings** — account/server/character availability tree, alert style toggles, cooldown window,
    level tolerance, EQ2 path, Census refresh interval, zone table editor.

## Non-functional

- Windows 11, .NET 8, WPF, MVVM.
- Tolerant of the log file being rotated/recreated and of EQ2 not running
  (waits and re-attaches).
- Census outages must not break the app (serve cached data).
- Repository workflow: development on `dev` branch, PRs into `main`,
  GitHub Actions CI runs `dotnet build` + tests; linters and unit tests pass
  before any push.
