# Claude Usage Pet

**Ein kleines Pixel-Desktop-Pet für Windows, das deine Claude-Limits im Blick behält – und sichtbar nervös wird, wenn das Kontingent knapp wird.**

[![Release](https://github.com/T3rr0rS0ck3/claudepet/actions/workflows/release.yml/badge.svg)](https://github.com/T3rr0rS0ck3/claudepet/actions/workflows/release.yml)
![Plattform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4)
![Lizenz](https://img.shields.io/badge/license-MIT-green)

🇬🇧 [English version](README.md)

![Das Pet in verschiedenen Stimmungen: freut sich nach dem Reset, denkt nach, Panik, schläft](docs/moods.png)

Claude Pro und Max haben ein rollierendes **5-Stunden-Session-Limit** und ein **Wochenlimit**. Claude Usage Pet sitzt
auf deinem Desktop und zeigt, wie viel davon verbraucht ist, ohne dass du etwas öffnen musst. Je höher der Verbrauch,
desto mehr ändert sich seine Stimmung: entspannt → nachdenklich → nervös → Panik → schläft bis zum Reset.

## Funktionen

- 🟠 **Desktop-Pet**: transparent, randlos, frei verschiebbar, optional immer im Vordergrund
- 🚶 **Läuft herum**: bummelt über die Taskleiste und die Oberkanten offener Fenster, springt auf Fenster,
  klettert an den Bildschirmrändern hoch, hangelt sich am oberen Rand entlang, bis es runterfällt, und fällt
  runter, wenn ein Fenster verschwindet; das Tempo hängt von der Stimmung ab (abschaltbar)
- 🎨 **Deine Farbe**: das Pet in beliebiger Farbe; Usage-Fenster, Menüs und Sprechblasen übernehmen sie als Akzentfarbe
- 📊 **Usage-Fenster**: Klick aufs Pet zeigt Session- und Wochenverbrauch, Reset-Zeiten und Prognose
- 🎭 **7 Stimmungen** mit eigenen Animationen, abhängig vom höheren der beiden Werte
- 💬 **Sprechblasen** beim Überschreiten von Schwellen, z. B. *„90 %! 😰“*, ohne Dauergequatsche
- 🔔 **Windows-Benachrichtigungen** bei einstellbaren Warnschwellen für Session und Woche
- 📈 **Prognose**: „Limit bei aktuellem Verbrauch in 1h 42m“
- 🧺 **Tray-Icon** in der aktuellen Stimmung, Autostart mit Windows
- 👻 **Geist ziehen**: einen Geist des Pets mit der linken Maustaste auf ein Explorer-Fenster ziehen und Claude Code öffnet sich in diesem Ordner
- 📂 **Projektliste**: Doppelklick aufs Pet, Projekt aus deinem Repo-Ordner wählen, Claude Code startet dort
- 🎤 **Sprachchat** (optional): öffnet Claude Code mit eingeschaltetem Sprachdiktat; das Pet hört zu, während du sprichst
- ❓❗ **Session-Status**: ein gelbes „?“, wenn eine Claude-Code-Session etwas fragt, ein grünes „!“, wenn sie fertig ist;
  optional ein Baby-Pet pro Session, das dem Pet hinterherläuft
- ⚙️ **Konfigurierbar**: Schwellen, Texte, Größe, Aktualisierungsintervall, Konsole und mehr
- 🔒 **Rein lokal**: das Pet selbst greift nicht aufs Netzwerk zu, braucht keinen Login und liest keine Tokens; es liest nur, was Claude Code ohnehin an seine Statuszeile übergibt

<p align="center"><img src="docs/overlay.png" alt="Usage-Fenster mit Session 80 %, Woche 70 %, Reset-Zeiten und Prognose" width="390"></p>

## Voraussetzungen

- Windows 10 oder 11 (x64)
- [Claude Code](https://code.claude.com) (CLI), angemeldet mit einem **Claude-Pro- oder Max-Abo**
  (nur dafür liefert Claude Code die Limit-Daten)

Eine .NET-Installation ist nicht nötig, der Installer bringt alles mit.

## Installation

1. **`ClaudePet-Setup-x.y.z.exe`** aus dem [neuesten Release](https://github.com/T3rr0rS0ck3/claudepet/releases/latest) herunterladen.
2. Ausführen. Admin-Rechte sind nicht nötig (Installation nach `%LOCALAPPDATA%\Programs\ClaudePet`).
3. **„Mit Claude Code verbinden“** angehakt lassen. Damit wird das Pet als `statusLine` in Claude Code eingetragen, aber nur, wenn noch keine existiert.
4. In Claude Code eine Nachricht senden. Nach der ersten Antwort kennt das Pet deinen Verbrauch.

Ohne Installer: `portable.zip` aus dem Release entpacken und `ClaudePet.exe` starten.
Danach Rechtsklick aufs Pet → *Claude Code verbinden…*

> **Windows SmartScreen** warnt eventuell vor einem unbekannten Herausgeber, weil der Installer nicht signiert ist.
> *Weitere Informationen → Trotzdem ausführen*.

## Bedienung

| Aktion | Wirkung |
|---|---|
| Linksklick aufs Pet | Usage-Fenster öffnen/schließen |
| Doppelklick aufs Pet | Projektliste: Claude Code in einem deiner Projekte öffnen |
| Pet mit rechter Maustaste ziehen | Verschieben (Position wird gespeichert); es strampelt beim Tragen und fällt beim Herumlaufen von dort herunter |
| Pet auf ein Explorer-Fenster ziehen | Claude Code in diesem Ordner öffnen (siehe [Claude Code öffnen](#claude-code-öffnen)) |
| Rechtsklick aufs Pet | Menü: Usage, Claude öffnen, Sprachchat, Hallo sagen, Vordergrund, Herumlaufen, in den Tray, Claude Code verbinden, Einstellungen, Beenden |
| Linksklick aufs Tray-Icon | Usage-Fenster öffnen |
| `ClaudePet.exe` erneut starten | Holt das laufende Pet zurück und öffnet das Usage-Fenster |

## Claude Code öffnen

Das Pet kann eine Konsole mit Claude Code (`claude`) in einem Ordner deiner Wahl öffnen. Welche Konsole verwendet wird,
legst du in den Einstellungen fest: *Automatisch* (Windows Terminal, falls installiert, sonst Eingabeaufforderung),
Windows Terminal, Eingabeaufforderung (cmd) oder PowerShell.

**Geist ziehen.** Mit der **linken** Maustaste aufs Pet drücken und ziehen. Ein halbdurchsichtiger Geist folgt dem
Mauszeiger, das Pet selbst bleibt stehen und schaut ihm nach. Über einem Explorer-Fenster oder dem Desktop leuchtet der
Geist auf und winkt. Dort loslassen, und Claude Code öffnet sich in diesem Ordner (bei Windows-11-Tabs im aktiven Tab);
der Geist schwebt davon. Loslassen woanders oder `Esc` bricht ab. Bei Ordnern ohne Dateisystempfad (z. B. *Dieser PC*)
sagt das Pet kurz Bescheid. Lässt sich in den Einstellungen abschalten.

**Projektliste.** Doppelklick aufs Pet (oder Rechtsklick → *Claude öffnen…*, auch im Tray-Menü). Oben stehen die zuletzt
geöffneten Projekte, darunter alle Unterordner deines **Repo-Ordners** (versteckte Ordner und solche mit `.` am Anfang
werden ausgelassen). *Anderen Ordner wählen…* öffnet einen beliebigen Ordner, *Repo-Ordner festlegen…* ändert den
Repo-Ordner. Beim ersten Mal fragt das Pet direkt nach dem Repo-Ordner. Ein einfacher Klick wartet jetzt die
Doppelklickzeit ab, bevor das Usage-Fenster aufgeht.

**Sprachchat.** Standardmäßig aus. In den Einstellungen *Sprachchat anbieten* einschalten, dann erscheint der Menüeintrag
*Sprachchat starten…*. Er nutzt das [Sprachdiktat von Claude Code](https://code.claude.com/docs/en/voice-dictation):
Beim ersten Mal fragt das Pet, ob es `voice.enabled` in `~/.claude/settings.json` setzen darf (mit Sicherung), danach
kommt die Projektliste. In der Konsole **Leertaste gedrückt halten** und sprechen. Voraussetzungen: Claude Code ist mit
einem claude.ai-Konto angemeldet (kein API-Key) und die Konsole darf aufs Mikrofon zugreifen (Windows-Einstellungen →
Datenschutz und Sicherheit → Mikrofon). Claude Code hat keinen Startparameter für den Sprachmodus, deshalb ist das
Diktat in allen Claude-Code-Sitzungen aktiv, solange die Option an ist. *Sprachchat anbieten* wieder ausschalten
blendet den Menüeintrag aus und setzt `voice.enabled` zurück auf `false`.
Hältst du in einem Terminal mit Claude Code (bei eingeschaltetem Diktat) die Leertaste gedrückt, bleibt das Pet stehen
und hört zu, mit Schallwellen neben dem Kopf. Claude Code meldet das Diktat nicht selbst, deshalb richtet sich das Pet
nach der gehaltenen Leertaste.

### Stimmungen

Maßgeblich ist der **höhere** Wert aus Session und Woche. Alle Grenzen sind einstellbar.

| Verbrauch | Stimmung | Was das Pet macht |
|---:|---|---|
| 0–49 % | 🟢 entspannt | atmet, blinzelt, winkt ab und zu |
| 50–69 % | 🟢 normal | schaut sich um |
| 70–84 % | 🟡 aufmerksam | denkt angestrengt nach |
| 85–89 % | 🟠 nervös | schwitzt, zappelt |
| 90–94 % | 🔴 besorgt | große Augen, zittert |
| 95–99 % | 🔴 Panik | läuft rot an, Arme hoch, blinkendes „!“ |
| 100 % | 😴 Limit erreicht | schläft bis zum Reset |

Solange Claude Code gerade Daten liefert, „tippt“ ein ruhiges Pet mit. Nach einem Reset jubelt es.

## Funktionsweise

```text
Claude Code ── statusLine-JSON (stdin) ──▶ ClaudePetBridge.exe ──▶ %LOCALAPPDATA%\ClaudePet\usage.json
                                                  │                                  ▲
                                                  │                                  │ liest
                                                  └── gibt die Statuszeile aus  ClaudePet.exe (das Pet)
```

Claude Code führt nach jeder Antwort den konfigurierten [statusLine-Befehl](https://code.claude.com/docs/en/statusline)
aus und übergibt Sitzungsdaten inklusive `rate_limits.five_hour` und `rate_limits.seven_day` als JSON.
`ClaudePetBridge.exe` ist dieser Befehl. Sie speichert die Werte lokal und gibt eine kompakte Statuszeile an Claude Code zurück:

```text
[Opus] Session 73% (↻ 2h 14m) · Woche 61%
```

Das Pet läuft unabhängig davon weiter, auch ohne offenes Claude-Code-Terminal. Werte, deren Reset-Zeit
vorbei ist, zählen als 0 %. Es wird nichts von claude.ai ausgelesen und es werden keine Zugangsdaten gelesen.

## Fragen und fertige Arbeit (? und !)

Ist Claude Code verbunden, trägt das Pet zusätzlich ein paar [Hooks](https://code.claude.com/docs/en/hooks) in
`~/.claude/settings.json` ein (mit Sicherung; eigene Hooks bleiben erhalten). Dann gilt:

- **„?“** (gelb) erscheint, wenn eine Session eine Berechtigung braucht, per *AskUserQuestion* fragt oder auf eine
  Eingabe wartet, dazu eine Sprechblase wie *„mein-projekt: Claude hat eine Frage.“*
- **„!“** (grün) erscheint, wenn eine Session mit ihrer Antwort fertig ist. Es bleibt, bis du dort den nächsten Prompt
  abschickst.
- Eine Frage geht vor „fertig“; bei mehreren Sessions zeigt das Pet die dringendste.
- *Baby-Pet für jede Claude-Session* (Einstellungen): ein kleines Pet pro laufender Session folgt dem Pet und zeigt
  deren ? oder !; der Tooltip nennt den Ordner.

Es hängen nur seltene Ereignisse dran (Prompt abgeschickt, Antwort fertig, Berechtigung/Frage, Session-Start/-Ende),
damit Claude nicht ausgebremst wird. Kein Hook meldet eine beantwortete Berechtigungsfrage; das „?“ verschwindet, sobald
das Gesprächsprotokoll der Session wieder wächst. *„?“ bei Fragen …* in den Einstellungen ausschalten entfernt die
Hooks wieder. Sprechblasentexte: `Question`, `Done` (mit `{folder}`).

## Konfiguration

Die meisten Optionen gibt es unter Rechtsklick → **Einstellungen…**: Repo-Ordner, Konsole, Geist ziehen, Sprachchat,
Größe, Farbe, Vordergrund, Animationen, Herumlaufen, Autostart, Aktualisierungsintervall, Warn- und Zustandsschwellen, Sprechblasen,
Benachrichtigungen und dein Name.

Alles liegt in `%LOCALAPPDATA%\ClaudePet\settings.json`. Handänderungen werden sofort übernommen.
Die Sprechblasentexte stehen unter `Texts`. Pro Ereignis kann es mehrere Varianten geben, eine wird zufällig gewählt:

```json
"Texts": {
  "Worried":   ["{percent} %! 😰"],
  "Panic":     ["{NAME}. FAST LEER."],
  "Exhausted": ["Okay... ich schlafe jetzt bis zum Reset."],
  "Reset":     ["Frisches Kontingent! Los geht's!"]
}
```

Ereignisse: `Greeting`, `NoData`, `Normal`, `Attentive`, `Nervous`, `Worried`, `Panic`, `Exhausted`, `Reset`, `Poke`,
`Launch` (Claude Code geöffnet), `NoFolder` (Geist dort losgelassen, wo kein Ordner ist), `Voice` (Sprachchat gestartet).
Platzhalter: `{name}`, `{NAME}` (Großbuchstaben), `{percent}` (höherer Wert), `{session}`, `{week}`,
`{folder}` (Name des geöffneten Ordners, bei `Launch` und `Voice`).

Weitere Schlüssel zum Öffnen von Claude Code: `ReposPath`, `Terminal` (`Auto`, `WindowsTerminal`, `Cmd`, `PowerShell`),
`GhostDrag`, `VoiceChat` und `RecentProjects` (die letzten 10 geöffneten Ordner).

## Fehlerbehebung

**Das Pet wartet auf Daten.**
Prüfe, ob das Pet verbunden ist (Rechtsklick → Einstellungen zeigt den Status), und sende eine Nachricht in Claude Code.
Limit-Daten kommen erst nach der ersten Antwort einer Session und nur bei Pro/Max.

**Ich hatte schon eine eigene Statuszeile.**
Der Installer überschreibt sie nie. Beim Verbinden aus der App wird vorher gefragt und eine Sicherung unter
`~/.claude/settings.json.claudepet-backup` angelegt. Es kann nur ein statusLine-Befehl aktiv sein.

**Beim Öffnen kommt „Claude Code wurde nicht gefunden“.**
Das Pet sucht `claude` im `PATH` und in `%USERPROFILE%\.local\bin`. Im Einstellungsfenster steht, welches `claude`
gefunden wurde. Claude Code installieren oder in den `PATH` aufnehmen und das Pet neu starten.

**Der Geist leuchtet über dem Explorer nicht auf.**
Ziele sind nur Explorer-Fenster und der Desktop; andere Programme (z. B. Dateidialoge oder IDEs) und das Pet selbst werden ignoriert.

**Das Sprachdiktat reagiert nicht.**
Mikrofonfreigabe für die Konsole prüfen und ob Claude Code mit einem claude.ai-Konto angemeldet ist.
`/voice` in Claude Code zeigt den Status.

**Irgendwas stimmt nicht.**
Schau in `%LOCALAPPDATA%\ClaudePet\log.txt`. Die Datei `usage.json` im selben Ordner zeigt die zuletzt empfangenen Werte.

## Deinstallation

Über Windows-Einstellungen → Apps *Claude Usage Pet* deinstallieren. Dabei werden auch der statusLine-Eintrag in Claude Code
und der Autostart entfernt. Die Einstellungen bleiben in `%LOCALAPPDATA%\ClaudePet`. Diesen Ordner löschen, wenn alles weg soll.

## Selbst bauen

Benötigt das .NET 8 SDK (oder neuer).

```powershell
git clone https://github.com/T3rr0rS0ck3/claudepet.git
cd claudepet
.\build.ps1                        # Build mit vorausgesetzter .NET-Runtime → dist\ClaudePet
.\build.ps1 -SelfContained -Version 1.2.3
.\dist\ClaudePet\ClaudePet.exe
```

Mit `CLAUDEPET_DATA_DIR` lassen sich App und Bridge auf einen anderen Datenordner umlenken, z. B. zum Testen mit eigenen `usage.json`-Dateien.

### Releases

Ein Tag wie `v1.2.3` startet den [Release-Workflow](.github/workflows/release.yml). Er baut eine Self-Contained-Version,
erstellt den Inno-Setup-Installer und eine portable ZIP und veröffentlicht ein GitHub-Release. Tags mit Suffix
(`v1.2.3-beta`) werden Pre-Releases. Bestehende Tags lassen sich über *Actions → Release → Run workflow* neu bauen.

### Projektstruktur

```text
src/Shared/            Datenmodell und Dateizugriff (App und Bridge)
src/ClaudePet.Bridge/  Der statusLine-Befehl
src/ClaudePet/Core/    Einstellungen, Stimmungslogik, Prognose, Autostart, Claude-Code-Setup,
                       Claude Code starten, Explorer-Ordner unter dem Mauszeiger finden
src/ClaudePet/Pet/     Prozedurales Pixel-Sprite, Animationen, Pet-Fenster, Geist-Fenster
src/ClaudePet/Views/   Usage-Fenster, Einstellungen, Projektliste
installer/             Inno-Setup-Skript
```

## Roadmap

- Englische Oberfläche / Sprachauswahl
- Limits pro Modell (z. B. Opus / Sonnet), sobald die Daten zuverlässig verfügbar sind
- Verbrauchshistorie und Statistik
- Weitere Charaktere, Skins und Soundeffekte

## Hinweis

Inoffizielles Fan-Projekt, nicht mit Anthropic verbunden oder von Anthropic unterstützt.
Der Pixel-Charakter ist eine eigene Zeichnung, inspiriert vom Claude-Maskottchen. „Claude“ ist eine Marke von Anthropic.

## Lizenz

[MIT](LICENSE) © Robin Wessel
