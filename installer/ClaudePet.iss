; Inno Setup script for Claude Usage Pet.
; Build: ISCC.exe /DAppVersion=1.2.3 installer\ClaudePet.iss   (expects .\build.ps1 -SelfContained output in dist\ClaudePet)

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0.0"
#endif

#define AppName "Claude Usage Pet"
#define AppExe "ClaudePet.exe"

[Setup]
AppId={{6B0C4F1E-3C7A-4E59-9C51-2D7E1F0A8B42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Robin Wessel
VersionInfoVersion={#NumericVersion}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany=Robin Wessel
VersionInfoDescription={#AppName} Setup
; Per-user install, no admin rights required. Keep the path stable: Claude Code's
; statusLine points at ClaudePetBridge.exe inside this folder.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\ClaudePet
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename=ClaudePet-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
de.ConnectClaudeCode=Mit Claude Code verbinden (statusLine eintragen, falls noch keine vorhanden)
en.ConnectClaudeCode=Connect to Claude Code (register statusLine if none is set)
de.DesktopIcon=Desktop-Verknüpfung erstellen
en.DesktopIcon=Create a desktop shortcut
de.LaunchApp=Claude Usage Pet starten
en.LaunchApp=Launch Claude Usage Pet

[Tasks]
Name: "connect"; Description: "{cm:ConnectClaudeCode}"
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "..\dist\ClaudePet\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--connect-claude-code"; Tasks: connect; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "StopPet"
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "Cleanup"
