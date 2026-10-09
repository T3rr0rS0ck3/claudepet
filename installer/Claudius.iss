; Inno Setup script for Claudius - KI Usage Pet.
; Build: ISCC.exe /DAppVersion=1.2.3 installer\Claudius.iss   (expects .\build.ps1 -SelfContained output in dist\Claudius)

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0.0"
#endif

#define AppName "Claudius - KI Usage Pet"
; Names before the renames; their shortcuts are removed on update.
#define OldAppName "Claude Usage Pet"
#define OldAppName2 "Claudius - Claude Usage App"
#define AppExe "Claudius.exe"
; Before the rename to Claudius: program folder and exe, replaced on update.
#define OldDirName "ClaudePet"
#define OldAppExe "ClaudePet.exe"

[Setup]
AppId={{6B0C4F1E-3C7A-4E59-9C51-2D7E1F0A8B42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Robin Wessel
VersionInfoVersion={#NumericVersion}
; Per-user install, no admin rights required. Keep the path stable: the AI assistant's
; statusLine points at ClaudiusBridge.exe inside this folder.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Claudius
; Not the old folder from before the rename (same AppId), which is removed below.
UsePreviousAppDir=no
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Not the old Start menu folders from earlier installs.
UsePreviousGroup=no
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename=Claudius-Setup-{#AppVersion}
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
de.ConnectAssistant=Mit dem KI-Assistenten verbinden (statusLine eintragen, falls noch keine vorhanden)
en.ConnectAssistant=Connect to the AI assistant (register statusLine if none is set)
de.DesktopIcon=Desktop-Verknüpfung erstellen
en.DesktopIcon=Create a desktop shortcut
de.LaunchApp=Claudius starten
en.LaunchApp=Launch Claudius

[Tasks]
Name: "connect"; Description: "{cm:ConnectAssistant}"
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "..\dist\Claudius\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Shortcuts from before the rename.
Type: filesandordirs; Name: "{autoprograms}\{#OldAppName}"
Type: files; Name: "{autodesktop}\{#OldAppName}.lnk"
Type: filesandordirs; Name: "{autoprograms}\{#OldAppName2}"
Type: files; Name: "{autodesktop}\{#OldAppName2}.lnk"
; The program folder from before the rename; the pet points the statusLine at the new bridge on its next start.
Type: filesandordirs; Name: "{localappdata}\Programs\{#OldDirName}"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--connect-assistant"; Tasks: connect; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
; In-app update: the pet runs this setup silently with /RESTARTAPP=1 and quits, so start it again.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: ShouldRestartApp

[Code]
function ShouldRestartApp: Boolean;
begin
  Result := ExpandConstant('{param:RESTARTAPP|0}') = '1';
end;

{ A pet from before the rename runs from the old folder, which CloseApplications does not look at. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#OldAppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "StopPet"
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "Cleanup"
