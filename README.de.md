# Claudius - KI Usage Pet

**Ein kleines Pixel-Desktop-Pet für Windows, das die Nutzungslimits deines KI-Coding-Assistenten im Blick behält – und sichtbar nervös wird, wenn das Kontingent knapp wird.**

[![Release](https://github.com/T3rr0rS0ck3/claudius/actions/workflows/release.yml/badge.svg)](https://github.com/T3rr0rS0ck3/claudius/actions/workflows/release.yml)
![Plattform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4)
![Lizenz](https://img.shields.io/badge/license-MIT-green)

🇬🇧 [English version](README.md)

![Das Pet in verschiedenen Stimmungen: freut sich nach dem Reset, denkt nach, Panik, schläft](docs/moods.png)

Viele KI-Coding-Abos haben ein rollierendes **5-Stunden-Session-Limit** und ein **Wochenlimit**.
**Claudius - KI Usage Pet** sitzt auf deinem Desktop und zeigt, wie viel davon verbraucht ist, ohne dass du etwas
öffnen musst. Je höher der Verbrauch, desto mehr ändert sich seine Stimmung: entspannt → nachdenklich → nervös →
Panik → schläft bis zum Reset.

> Die Oberfläche gibt es auf Englisch und Deutsch. Neu installiert startet Claudius auf Englisch; umstellen unter
> *Settings → Language*. Bestehende Installationen bleiben auf Deutsch.

## Funktionen

- 🟠 **Desktop-Pet**: transparent, randlos, frei verschiebbar, optional immer im Vordergrund
- 🚶 **Läuft herum**: bummelt über die Taskleiste und die Oberkanten offener Fenster, springt auf Fenster,
  klettert an den Bildschirmrändern hoch, hangelt sich am oberen Rand entlang, bis es runterfällt, und fällt
  runter, wenn ein Fenster verschwindet; nach einem tiefen Sturz klatscht es platt auf und formt sich wieder.
  Das Tempo hängt von der Stimmung ab (abschaltbar). Auf Wunsch läuft oder hüpft es auch zum Nachbarmonitor,
  passend zur Monitoranordnung in Windows
- 🐭 **Jagt den Mauszeiger**: ab und zu fixiert es in ruhiger Stimmung den Zeiger in seiner Nähe und rennt ihm hinterher (abschaltbar)
- 🎨 **Deine Farbe**: das Pet in beliebiger Farbe; Usage-Fenster, Menüs und Sprechblasen übernehmen sie als Akzentfarbe
- 👑 **Passend zum Modell gekleidet**: eine Krone bei Opus, eine Sonnenbrille bei Sonnet, eine kleine Blume bei Haiku, ein Zauberhut bei Fable (abschaltbar)
- 🎃 **Saisonale Kopfbedeckung**: Hexenhut und Besen im Oktober, Weihnachtsmütze bis Weihnachten, Hasenohren an Ostern und ein Partyhut
  an Silvester und deinem Geburtstag; sie ersetzen die Kopfbedeckung des Modells (abschaltbar)
- 🌙 **Nachtmodus**: zwischen 23 und 6 Uhr (einstellbar) trägt das Pet eine Schlafmütze, läuft langsamer, gähnt und sagt gute Nacht
- 📊 **Usage-Fenster**: Klick aufs Pet zeigt Session- und Wochenverbrauch, Reset-Zeiten und Prognose
- 🎭 **7 Stimmungen** mit eigenen Animationen, abhängig vom höheren der beiden Werte
- 💬 **Sprechblasen** beim Überschreiten von Schwellen, z. B. *„90 %! 😰“*, ohne Dauergequatsche
- 🔔 **Windows-Benachrichtigungen** bei einstellbaren Warnschwellen für Session und Woche
- 📈 **Prognose**: „Limit bei aktuellem Verbrauch in 1h 42m“
- 🧺 **Tray-Icon** in der aktuellen Stimmung, Autostart mit Windows
- 👻 **Geist ziehen**: einen Geist des Pets mit der linken Maustaste auf ein Explorer-Fenster ziehen und der Assistent öffnet sich in diesem Ordner
- 📂 **Projektliste**: Doppelklick aufs Pet, Projekt aus deinem Repo-Ordner wählen, der Assistent startet dort
- 🎤 **Sprachchat** (optional): öffnet den Assistenten mit eingeschaltetem Sprachdiktat; das Pet hört zu, während du sprichst
- ❓ **Session-Status**: ein gelbes „?“, wenn eine Session etwas fragt, eine Sprechblase, wenn sie fertig ist;
  optional ein Baby-Pet pro Session, das dem Pet hinterherläuft; Klick aufs „?“ oder ein Baby springt zum Fenster der Session
- ⚙️ **Konfigurierbar**: Schwellen, Texte, Größe, Aktualisierungsintervall, Konsole und mehr
- 🔒 **Rein lokal**: das Pet selbst greift nicht aufs Netzwerk zu, braucht keinen Login und liest keine Tokens; es liest nur, was der Assistent ohnehin an seine Statuszeile übergibt

<p align="center"><img src="docs/overlay.png" alt="Usage-Fenster mit Session 80 %, Woche 70 %, Reset-Zeiten und Prognose" width="390"></p>

## Voraussetzungen

- Windows 10 oder 11 (x64)
- Eine KI-Coding-Assistent-CLI, die Limit-Daten an einen statusLine-Befehl übergibt, angemeldet mit einem Abo mit
  Session- und Wochenlimit (nur dafür werden Limit-Daten geliefert)

Eine .NET-Installation ist nicht nötig, der Installer bringt alles mit.

## Installation

Das Pet kommt in den **Microsoft Store** als *Claudius - KI Usage Pet* (von Microsoft signiert, Updates über den Store).
Bis es dort gelistet ist, oder wenn du GitHub bevorzugst:

1. **`Claudius-Setup-x.y.z.exe`** aus dem [neuesten Release](https://github.com/T3rr0rS0ck3/claudius/releases/latest) herunterladen.
2. Ausführen. Admin-Rechte sind nicht nötig (Installation nach `%LOCALAPPDATA%\Programs\Claudius`).
3. **„Mit dem KI-Assistenten verbinden“** angehakt lassen. Damit wird das Pet als `statusLine` im Assistenten eingetragen, aber nur, wenn noch keine existiert.
4. Im Assistenten eine Nachricht senden. Nach der ersten Antwort kennt das Pet deinen Verbrauch.

Ohne Installer: `portable.zip` aus dem Release entpacken und `Claudius.exe` starten.
Danach Rechtsklick aufs Pet → *KI-Assistent verbinden…*

> **Windows SmartScreen** warnt eventuell vor einem unbekannten Herausgeber, weil der Installer nicht signiert ist.
> *Weitere Informationen → Trotzdem ausführen*.

### Updates

Das Pet sucht beim Start und einmal am Tag nach einem neuen Release. Gibt es eins, sagt es Bescheid, und im
Rechtsklick-Menü erscheint **Update auf x.y.z installieren…**: Ein Klick lädt das Setup, prüft es gegen die
`SHA256SUMS.txt` des Releases, installiert still und startet das Pet neu. Portable Kopien bekommen nur einen Link
zur Release-Seite. In den Einstellungen (*Updates*) lässt sich die Suche abschalten oder von Hand starten.

Versionen von vor der Umbenennung in Claudius aktualisieren sich genauso: Die neue Version übernimmt ihre
Einstellungen, stellt die Statuszeile auf die neue Bridge um und entfernt den alten Programmordner.

## Bedienung

| Aktion | Wirkung |
|---|---|
| Linksklick aufs Pet | Usage-Fenster öffnen/schließen |
| Doppelklick aufs Pet | Projektliste: den Assistenten in einem deiner Projekte öffnen |
| Pet mit rechter Maustaste ziehen | Verschieben (Position wird gespeichert); es strampelt beim Tragen und fällt beim Herumlaufen von dort herunter |
| Pet auf ein Explorer-Fenster ziehen | Den Assistenten in diesem Ordner öffnen (siehe [Assistent öffnen](#assistent-öffnen)) |
| Mit der Maus übers Pet fahren | Emote-Knöpfe: füttern 🍪, streicheln ❤, spielen ⚽ und kitzeln 🪶, jeweils mit eigener Reaktion (abschaltbar) |
| Rechtsklick aufs Pet | Menü: Usage, Assistent öffnen, Sprachchat, Hallo sagen, Vordergrund, Herumlaufen, in den Tray, KI-Assistent verbinden, Einstellungen, Beenden |
| Linksklick aufs Tray-Icon | Usage-Fenster öffnen |
| `Claudius.exe` erneut starten | Holt das laufende Pet zurück und öffnet das Usage-Fenster |

## Assistent öffnen

Das Pet kann die CLI des Assistenten in einem Ordner deiner Wahl öffnen. Wo sie geöffnet wird, legst du in den
Einstellungen fest (*Assistent öffnen in*): *Automatisch* (Windows Terminal, falls installiert, sonst
Eingabeaufforderung), Windows Terminal, Eingabeaufforderung (cmd), PowerShell oder *Desktop-App (Code-Tab)*. Die
Desktop-Option startet die CLI mit `--desktop` im Ordner; dafür braucht es die Desktop-App des Assistenten und eine
aktuelle CLI.

**Geist ziehen.** Mit der **linken** Maustaste aufs Pet drücken und ziehen. Ein halbdurchsichtiger Geist folgt dem
Mauszeiger, das Pet selbst bleibt stehen und schaut ihm nach. Über einem Explorer-Fenster oder dem Desktop leuchtet der
Geist auf und winkt. Dort loslassen, und der Assistent öffnet sich in diesem Ordner (bei Windows-11-Tabs im aktiven Tab);
der Geist schwebt davon. Loslassen woanders oder `Esc` bricht ab. Bei Ordnern ohne Dateisystempfad (z. B. *Dieser PC*)
sagt das Pet kurz Bescheid. Lässt sich in den Einstellungen abschalten.

**Projektliste.** Doppelklick aufs Pet (oder Rechtsklick → *Assistent öffnen…*, auch im Tray-Menü). Oben stehen die zuletzt
geöffneten Projekte, darunter alle Unterordner deines **Repo-Ordners** (versteckte Ordner und solche mit `.` am Anfang
werden ausgelassen). *Anderen Ordner wählen…* öffnet einen beliebigen Ordner, *Repo-Ordner festlegen…* ändert den
Repo-Ordner. Beim ersten Mal fragt das Pet direkt nach dem Repo-Ordner. Ein einfacher Klick wartet jetzt die
Doppelklickzeit ab, bevor das Usage-Fenster aufgeht.

**Sprachchat.** Standardmäßig aus. In den Einstellungen *Sprachchat anbieten* einschalten, dann erscheint der Menüeintrag
*Sprachchat starten…*. Er nutzt das Sprachdiktat des Assistenten: Beim ersten Mal fragt das Pet, ob es `voice.enabled`
in der `settings.json` des Assistenten setzen darf (mit Sicherung), danach kommt die Projektliste. In der Konsole
**Leertaste gedrückt halten** und sprechen. Voraussetzungen: Der Assistent ist mit einem Konto angemeldet (kein API-Key)
und die Konsole darf aufs Mikrofon zugreifen (Windows-Einstellungen → Datenschutz und Sicherheit → Mikrofon). Die CLI hat
keinen Startparameter für den Sprachmodus, deshalb ist das Diktat in allen ihren Sitzungen aktiv, solange die Option an
ist. *Sprachchat anbieten* wieder ausschalten blendet den Menüeintrag aus und setzt `voice.enabled` zurück auf `false`.
Hältst du in einem Terminal mit dem Assistenten (bei eingeschaltetem Diktat) die Leertaste gedrückt, bleibt das Pet stehen
und spricht in ein Mikrofon, über dem Schallwellen aufsteigen. Der Assistent meldet das Diktat nicht selbst, deshalb richtet
sich das Pet nach der gehaltenen Leertaste.

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

Solange der Assistent gerade Daten liefert, „tippt“ ein ruhiges Pet mit. Nach einem Reset jubelt es.

## Funktionsweise

```text
KI-Assistent ── statusLine-JSON (stdin) ──▶ ClaudiusBridge.exe ──▶ %LOCALAPPDATA%\Claudius\usage.json
                                                   │                                  ▲
                                                   │                                  │ liest
                                                   └── gibt die Statuszeile aus  Claudius.exe (das Pet)
```

Der Assistent führt nach jeder Antwort den konfigurierten statusLine-Befehl aus und übergibt Sitzungsdaten inklusive
`rate_limits.five_hour` und `rate_limits.seven_day` als JSON. `ClaudiusBridge.exe` ist dieser Befehl. Sie speichert die
Werte lokal und gibt eine kompakte Statuszeile an den Assistenten zurück:

```text
[Opus] Session 73% (↻ 2h 14m) · Woche 61%
```

Das Pet läuft unabhängig davon weiter, auch ohne offenes Terminal des Assistenten. Werte, deren Reset-Zeit
vorbei ist, zählen als 0 %. Es wird nichts von Webseiten ausgelesen und es werden keine Zugangsdaten gelesen.

## Fragen und fertige Arbeit

Ist der Assistent verbunden, trägt das Pet zusätzlich ein paar Hooks in dessen `settings.json` ein (mit Sicherung;
eigene Hooks bleiben erhalten). Dann gilt:

- **„?“** (gelb) erscheint, wenn eine Session eine Berechtigung braucht, per *AskUserQuestion* fragt oder auf eine
  Eingabe wartet, dazu eine Sprechblase wie *„mein-projekt: Der Assistent hat eine Frage.“*
- Ist eine Session mit ihrer Antwort fertig, erscheint eine Sprechblase wie *„mein-projekt: Der Assistent ist fertig!“*; am
  Pet bleibt kein Zeichen stehen.
- Bei mehreren Sessions bleibt das „?“, solange irgendeine davon auf eine Antwort wartet.
- *Baby-Pet für jede Assistenten-Session* (Einstellungen): ein kleines Pet pro laufender Session folgt dem Pet, zeigt
  deren „?“ und trägt das Outfit für das Modell der Session; der Tooltip nennt den Ordner und ob die
  Session arbeitet, wartet oder fertig ist.
- **Zur Session springen**: Ein Klick aufs „?“ holt das Fenster der fragenden Session nach vorn, ein Klick auf ein Baby-Pet
  das seiner Session: das Konsolenfenster, das Windows-Terminal-Fenster mit dem richtigen Tab oder die Desktop-App. Der
  Hook merkt sich das Fenster beim Start der Session und wenn sie fragt; ältere Sessions werden über den Ordnernamen gesucht.

Es hängen nur seltene Ereignisse dran (Prompt abgeschickt, Antwort fertig, Berechtigung/Frage, Session-Start/-Ende),
damit der Assistent nicht ausgebremst wird. Kein Hook meldet eine beantwortete Berechtigungsfrage; das „?“ verschwindet,
sobald das Gesprächsprotokoll der Session wieder wächst. *„?“ bei Fragen …* in den Einstellungen ausschalten entfernt die
Hooks wieder. Sprechblasentexte: `Question`, `Done`, `NoWindow` (mit `{folder}`).

### Desktop-App

Der Code-Tab der Desktop-App des Assistenten liest dieselbe `settings.json` und führt dieselben Hooks aus, deshalb
bekommen seine Sessions ebenfalls „?“, Sprechblasen und Baby-Pets, neben den Terminal-Sessions; Sprechblasen markieren
sie mit *(Desktop)*. Solange irgendeine Session arbeitet, tippt das Pet, auch wenn keine Statuszeilen-Daten kommen.
Stimmung und Limits kommen weiterhin aus der Statuszeile, die die Desktop-App eventuell nicht ausführt; dann
aktualisieren sie sich nur über Terminal-Sessions. Das Diktat wird auch in der Desktop-App erkannt. Der normale
Desktop-Chat bietet keine Hooks und wird nicht unterstützt.

## Konfiguration

Die meisten Optionen gibt es unter Rechtsklick → **Einstellungen…**: Sprache, Repo-Ordner, Konsole, Geist ziehen, Sprachchat,
Größe, Farbe, Vordergrund, Animationen, Herumlaufen, Monitorwechsel, Mauszeiger jagen, Emotes, Modell-Outfits, saisonale Kopfbedeckung und Geburtstag, Nachtmodus und Nachtzeit, Autostart, Update-Suche, Aktualisierungsintervall, Warn- und Zustandsschwellen, Sprechblasen,
Benachrichtigungen und dein Name.

Alles liegt in `%LOCALAPPDATA%\Claudius\settings.json`. Handänderungen werden sofort übernommen.
`Language` ist `en` oder `de`. Die Sprechblasentexte stehen unter `Texts`; ein Sprachwechsel in den Einstellungen setzt
sie auf die Standardtexte der neuen Sprache zurück. Pro Ereignis kann es mehrere Varianten geben, eine wird zufällig gewählt:

```json
"Texts": {
  "Worried":   ["{percent} %! 😰"],
  "Panic":     ["{NAME}. FAST LEER."],
  "Exhausted": ["Okay... ich schlafe jetzt bis zum Reset."],
  "Reset":     ["Frisches Kontingent! Los geht's!"]
}
```

Ereignisse: `Greeting`, `NoData`, `Normal`, `Attentive`, `Nervous`, `Worried`, `Panic`, `Exhausted`, `Reset`, `Poke`,
`Launch` (Assistent geöffnet), `NoFolder` (Geist dort losgelassen, wo kein Ordner ist), `Voice` (Sprachchat gestartet),
`GoodNight` (die Nacht beginnt).
Platzhalter: `{name}`, `{NAME}` (Großbuchstaben), `{percent}` (höherer Wert), `{session}`, `{week}`,
`{folder}` (Name des geöffneten Ordners, bei `Launch` und `Voice`).

Weitere Schlüssel zum Öffnen des Assistenten: `ReposPath`, `Terminal` (`Auto`, `WindowsTerminal`, `Cmd`, `PowerShell`, `Desktop`),
`GhostDrag`, `VoiceChat` und `RecentProjects` (die letzten 10 geöffneten Ordner).

Saisonale Kopfbedeckung und Nacht: `SeasonalAccessories`, `Birthday` (`"MM-dd"`, z. B. `"03-14"`), `NightMode`, `NightStart` und
`NightEnd` (`"HH:mm"`, die Nacht darf über Mitternacht gehen). Mit `CLAUDIUS_FAKE_NOW` (z. B. `2026-12-24T22:59`) lassen sie sich an einem anderen Datum ausprobieren.

## Fehlerbehebung

**Das Pet wartet auf Daten.**
Prüfe, ob das Pet verbunden ist (Rechtsklick → Einstellungen zeigt den Status), und sende eine Nachricht im Assistenten.
Limit-Daten kommen erst nach der ersten Antwort einer Session und nur bei Abos mit Limits.

**Ich hatte schon eine eigene Statuszeile.**
Der Installer überschreibt sie nie. Beim Verbinden aus der App wird vorher gefragt und eine Sicherung neben der
`settings.json` des Assistenten angelegt (`settings.json.claudius-backup`). Es kann nur ein statusLine-Befehl aktiv sein.

**Beim Öffnen kommt „Der KI-Assistent wurde nicht gefunden“.**
Das Pet sucht die CLI im `PATH` und in `%USERPROFILE%\.local\bin`. Im Einstellungsfenster steht, welche gefunden wurde.
Die CLI installieren oder in den `PATH` aufnehmen und das Pet neu starten.

**Der Geist leuchtet über dem Explorer nicht auf.**
Ziele sind nur Explorer-Fenster und der Desktop; andere Programme (z. B. Dateidialoge oder IDEs) und das Pet selbst werden ignoriert.

**Das Sprachdiktat reagiert nicht.**
Mikrofonfreigabe für die Konsole prüfen und ob der Assistent mit einem Konto angemeldet ist.
Der Sprach-Befehl des Assistenten zeigt den Status.

**Irgendwas stimmt nicht.**
Schau in `%LOCALAPPDATA%\Claudius\log.txt`. Die Datei `usage.json` im selben Ordner zeigt die zuletzt empfangenen Werte.

## Deinstallation

Über Windows-Einstellungen → Apps *Claudius - KI Usage Pet* deinstallieren. Dabei werden auch der statusLine-Eintrag im
Assistenten und der Autostart entfernt. Die Einstellungen bleiben in `%LOCALAPPDATA%\Claudius`. Diesen Ordner löschen,
wenn alles weg soll.

**Store-Version:** Store-Apps können beim Deinstallieren nichts ausführen. Deshalb vorher in den Einstellungen des Pets
auf *Trennen* klicken, sonst ruft der Assistent weiter die entfernte Bridge auf.

## Selbst bauen

Benötigt das .NET 8 SDK (oder neuer).

```powershell
git clone https://github.com/T3rr0rS0ck3/claudius.git
cd claudius
.\build.ps1                        # Build mit vorausgesetzter .NET-Runtime → dist\Claudius
.\build.ps1 -SelfContained -Version 1.2.3
.\dist\Claudius\Claudius.exe
```

Mit `CLAUDIUS_DATA_DIR` lassen sich App und Bridge auf einen anderen Datenordner umlenken, z. B. zum Testen mit eigenen `usage.json`-Dateien.

### Releases

Ein Tag wie `v1.2.3` startet den [Release-Workflow](.github/workflows/release.yml). Er baut eine Self-Contained-Version,
erstellt den Inno-Setup-Installer und eine portable ZIP und veröffentlicht ein GitHub-Release. Tags mit Suffix
(`v1.2.3-beta`) werden Pre-Releases. Bestehende Tags lassen sich über *Actions → Release → Run workflow* neu bauen.
Das Release enthält das Setup zusätzlich unter seinem Namen von vor der Umbenennung, nach dem ältere Versionen beim
Update suchen.

Für den **Microsoft Store** baut der Workflow zusätzlich ein unsigniertes `Claudius-1.2.3.msix` ([packaging/](packaging/))
und hängt es an den Lauf und das Release (so nicht installierbar). Das wird im Partner Center hochgeladen, der Store signiert es. Lokal testen
(Entwicklermodus an): `.\build.ps1 -SelfContained -Version 1.2.3; .\packaging\build-msix.ps1 -Version 1.2.3 -Register`.
Datenschutzerklärung für den Store-Eintrag: [PRIVACY.md](PRIVACY.md).

Nach der ersten, von Hand im Partner Center gemachten Einreichung kann der Workflow neue Versionen selbst einreichen
(Microsoft Store CLI). Das passiert, sobald im Repository (*Settings → Secrets and variables → Actions*) Folgendes hinterlegt ist:

| Name | Art | Wert |
|---|---|---|
| `PARTNER_CENTER_TENANT_ID` | Secret | Mandanten-ID der im Partner Center verknüpften Entra-ID-App |
| `PARTNER_CENTER_CLIENT_ID` | Secret | Client-ID dieser App |
| `PARTNER_CENTER_CLIENT_SECRET` | Secret | Ihr Client-Secret |
| `PARTNER_CENTER_SELLER_ID` | Secret | Verkäufer-ID (Partner Center → Kontoeinstellungen) |
| `MSSTORE_PRODUCT_ID` | Variable | Store-ID der App (z. B. `9N…`) |

### Projektstruktur

```text
src/Shared/           Datenmodell und Dateizugriff (App und Bridge); feste Namen der CLI (AssistantCli)
src/Claudius.Bridge/  Der statusLine-Befehl
src/Claudius/Core/    Einstellungen, Stimmungslogik, Prognose, Autostart, Assistenten-Setup, Assistent starten,
                      Explorer-Ordner unter dem Mauszeiger finden, Übernahme älterer Versionen (Legacy)
src/Claudius/Pet/     Prozedurales Pixel-Sprite, Animationen, Pet-Fenster, Geist-Fenster
src/Claudius/Views/   Usage-Fenster, Einstellungen, Projektliste
installer/            Inno-Setup-Skript
```

## Roadmap

- Limits pro Modell (z. B. Opus / Sonnet), sobald die Daten zuverlässig verfügbar sind
- Verbrauchshistorie und Statistik
- Weitere Charaktere, Skins und Soundeffekte

## Hinweis

Unabhängiges Projekt. Der Pixel-Charakter ist eine eigene Zeichnung.

## Lizenz

[MIT](LICENSE) © T3rr0rS0ck3
