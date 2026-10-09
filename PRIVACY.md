# Datenschutzerklärung / Privacy Policy

**Claudius - KI Usage Pet** · Stand / Last updated: 2026-10-09

## Deutsch

Claudius ist ein Desktop-Haustier, das den Verbrauch deines KI-Coding-Assistenten anzeigt. Es läuft vollständig auf deinem Rechner.

- **Keine Konten, keine Telemetrie, keine Werbung.** Die App sammelt keine personenbezogenen Daten und sendet nichts an den Entwickler oder an Dritte.
- **Lokale Daten, die die App liest:** die Verbrauchswerte, Sitzungsinfos und Projektordner, die der Assistent über die statusLine und Hooks an die App übergibt, sowie die Einstellungen des Assistenten (seine `settings.json` im Benutzerprofil).
- **Lokale Daten, die die App schreibt:**
  - ihre eigenen Einstellungen, den Verlauf und ein Log in `%LOCALAPPDATA%\Claudius` (beim Update von einer älteren Version übernimmt sie dorthin die Daten aus deren Ordner);
  - in der `settings.json` des Assistenten die statusLine und die Hooks (mit Sicherung). Diese Einträge lassen sich in den Einstellungen mit *Trennen* wieder entfernen;
  - auf Wunsch ein Farb-Theme im Theme-Ordner des Assistenten.
- **Netzwerk:** Nur die GitHub-Version fragt beim Start und einmal am Tag bei `api.github.com` nach einer neuen Version und lädt Updates von GitHub herunter. Das lässt sich in den Einstellungen abschalten. Dabei werden keine persönlichen Daten übertragen, nur die übliche Anfrage mit der App-Version als User-Agent. Die Microsoft-Store-Version stellt keine eigenen Netzwerkverbindungen her; sie wird über den Store aktualisiert.
- **Löschen:** Alle Daten liegen auf deinem Rechner. Du kannst den Ordner `%LOCALAPPDATA%\Claudius` jederzeit löschen.

Fragen: [GitHub Issues](https://github.com/T3rr0rS0ck3/claudius/issues)

## English

Claudius is a desktop pet that shows the usage of your AI coding assistant. It runs entirely on your computer.

- **No accounts, no telemetry, no ads.** The app collects no personal data and sends nothing to the developer or any third party.
- **Local data the app reads:** the usage values, session info and project folders that the assistant passes to it through the statusLine and hooks, and the assistant's settings (its `settings.json` in the user profile).
- **Local data the app writes:**
  - its own settings, history and a log in `%LOCALAPPDATA%\Claudius` (when updating from an older version, it moves that version's data there);
  - the statusLine and hooks in the assistant's `settings.json` (with a backup). *Disconnect* in the settings removes them again;
  - optionally a color theme in the assistant's themes folder.
- **Network:** Only the GitHub version asks `api.github.com` for a new version at start and once a day, and downloads updates from GitHub. This can be switched off in the settings. No personal data is sent, only the usual request with the app version as user agent. The Microsoft Store version makes no network connections of its own; it is updated through the Store.
- **Deletion:** All data stays on your computer. You can delete the `%LOCALAPPDATA%\Claudius` folder at any time.

Questions: [GitHub Issues](https://github.com/T3rr0rS0ck3/claudius/issues)
