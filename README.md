# Claude Usage Pet

Ein kleines Windows-Desktop-Pet, das deinen Claude-Verbrauch (5-Stunden-Session und Wochenlimit)
über die `statusLine`-Schnittstelle von Claude Code beobachtet und darauf reagiert.

## Bauen & Starten

Voraussetzung: .NET 8 Desktop Runtime (zum Bauen ein .NET SDK ≥ 8).

```powershell
.\build.ps1                       # -> dist\ClaudePet\ClaudePet.exe + ClaudePetBridge.exe
.\dist\ClaudePet\ClaudePet.exe
```

Dann **Rechtsklick auf das Pet → „Claude Code verbinden…“**. Dabei wird in `~/.claude/settings.json`
(bzw. `%CLAUDE_CONFIG_DIR%`) folgender Eintrag gesetzt; eine Sicherung landet als `settings.json.claudepet-backup` daneben:

```json
"statusLine": { "type": "command", "command": "E:/…/dist/ClaudePet/ClaudePetBridge.exe", "padding": 0 }
```

Die Werte erscheinen nach der nächsten Antwort in Claude Code. Nur Pro/Max-Abos liefern `rate_limits`.

## Funktionsweise

```text
Claude Code ──stdin JSON──▶ ClaudePetBridge.exe ──▶ %LOCALAPPDATA%\ClaudePet\usage.json ◀── ClaudePet.exe (pollt)
                                    │                         history.jsonl (für Prognose)
                                    └──stdout──▶ Statuszeile in Claude Code: "[Opus] Session 73% (↻ 2h 14m) · Woche 61%"
```

- **Bridge** (`src/ClaudePet.Bridge`): liest `rate_limits.five_hour` / `seven_day`, schreibt atomar nach
  `usage.json` und hängt Änderungen an `history.jsonl` an. Fehlt ein Fenster, bleibt der letzte Wert
  erhalten; Fenster mit abgelaufenem `resets_at` zählt die App als 0 %. Die Bridge stürzt nie ab und gibt immer eine Zeile aus.
- **App** (`src/ClaudePet`, WPF): Pet, Usage-Overlay, Sprechblasen, Tray-Icon, Windows-Benachrichtigungen,
  Einstellungen. Läuft unabhängig davon, ob gerade ein Claude-Code-Terminal offen ist.

## Bedienung

| Aktion | Wirkung |
|---|---|
| Linksklick aufs Pet | Usage-Overlay öffnen/schließen |
| Ziehen | Pet verschieben (Position wird gespeichert) |
| Rechtsklick aufs Pet | Menü (Usage, Vordergrund, in Tray minimieren, Claude Code verbinden, Einstellungen, Beenden) |
| Linksklick aufs Tray-Icon | Usage-Overlay |
| `ClaudePet.exe` erneut starten | holt das laufende Pet nach vorne und öffnet das Overlay |

## Zustände

Maßgeblich ist der höhere Wert aus Session und Woche (Grenzen in den Einstellungen änderbar):

| Verbrauch | Zustand | Animation |
|---:|---|---|
| 0–49 % | entspannt | atmet, blinzelt, winkt ab und zu |
| 50–69 % | normal | schaut sich um |
| 70–84 % | aufmerksam | nachdenklich, Denkpunkte |
| 85–89 % | nervös | Schweißtropfen, zappelt |
| 90–94 % | besorgt | große Augen, zittert |
| 95–99 % | Panik | rot, Arme hoch, „!“ |
| 100 % | schläft bis zum Reset | Zzz |

Solange Claude Code gerade Daten liefert (letzte 20 s) und der Zustand höchstens „aufmerksam“ ist, „tippt“ das Pet.
Nach einem Reset jubelt es kurz.

## Konfiguration

`%LOCALAPPDATA%\ClaudePet\settings.json`. Die meisten Werte lassen sich über *Einstellungen…* ändern.
Sprechblasentexte stehen unter `Texts` (mehrere Varianten pro Ereignis, zufällig gewählt; Platzhalter
`{name}`, `{NAME}`, `{percent}`, `{session}`, `{week}`). Handänderungen an der Datei werden live übernommen.

Zum Testen ohne echte Daten kann der Datenordner per `CLAUDEPET_DATA_DIR` umgebogen werden.

## Projektstruktur

```text
src/Shared/UsageData.cs        Datenmodell + Dateizugriff (von Bridge und App gemeinsam genutzt)
src/ClaudePet.Bridge/          statusLine-Befehl
src/ClaudePet/Core/            Settings, Zustandslogik, Prognose, Autostart, Claude-Code-Setup
src/ClaudePet/Pet/             Pixel-Sprite (prozedural), Animationen, Pet-Fenster
src/ClaudePet/Views/           Usage-Overlay, Einstellungen
```
