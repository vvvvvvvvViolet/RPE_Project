; ---------------------------------------------------------------------------
;  RPE Reader — Inno Setup script
;
;  Build the application first:
;      pwsh .\build\publish.ps1
;  Then compile this script with Inno Setup 6:
;      "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" build\installer.iss
;
;  Output: dist\RPEReader-Setup-1.0.0-x64.exe
; ---------------------------------------------------------------------------

#define MyAppName        "RPE Reader"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "RPE Reader Project"
#define MyAppExeName     "RPEReader.exe"
#define MyAppId          "{{8B4F2C71-5E3A-4D19-9C86-2F7A1E0D5B34}"
#define SourceDir        "..\Release"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} Setup

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}

; x64 only: the application is published as win-x64 and will not run elsewhere.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Per-machine when run elevated, per-user otherwise.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

OutputDir=..\dist
OutputBaseFilename=RPEReader-Setup-{#MyAppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\RPEReader.App\Resources\app.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
    Description: "{cm:CreateDesktopIcon}"; \
    GroupDescription: "{cm:AdditionalIcons}"; \
    Flags: unchecked

Name: "associate"; \
    Description: "Open .rpe files with {#MyAppName}"; \
    GroupDescription: "File associations:"

[Files]
; The whole self-contained publish folder. recursesubdirs picks up the
; localisation and runtime subfolders the publish step creates.
Source: "{#SourceDir}\*"; \
    DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";              Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}";    Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";        Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; ---- .rpe file association -------------------------------------------------
; Written under the hive matching the install scope, so a per-user install does
; not need administrator rights.

Root: HKA; Subkey: "Software\Classes\.rpe"; \
    ValueType: string; ValueName: ""; ValueData: "RPEReader.rpe"; \
    Flags: uninsdeletevalue; Tasks: associate

Root: HKA; Subkey: "Software\Classes\RPEReader.rpe"; \
    ValueType: string; ValueName: ""; ValueData: "RPE project export"; \
    Flags: uninsdeletekey; Tasks: associate

Root: HKA; Subkey: "Software\Classes\RPEReader.rpe\DefaultIcon"; \
    ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; \
    Tasks: associate

Root: HKA; Subkey: "Software\Classes\RPEReader.rpe\shell\open\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; \
    Tasks: associate

; Advertise the application so it appears under "Open with".
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; \
    ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; \
    Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; \
    Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Logs are created beside the executable at run time, so they are not tracked
; by the installer and must be removed explicitly.
Type: filesandordirs; Name: "{app}\Logs"

[Code]

const
  SHCNE_ASSOCCHANGED = $08000000;
  SHCNF_IDLIST       = $0000;

procedure SHChangeNotify(wEventId: Integer; uFlags: Cardinal; dwItem1, dwItem2: Cardinal);
  external 'SHChangeNotify@shell32.dll stdcall';

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Ask the shell to reload associations so the .rpe icon appears without
    // requiring a sign-out.
    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, 0, 0);
end;
