; Inno Setup script for Jarvis for Windows
; Build via:  iscc installer\setup.iss

#define MyAppName       "Jarvis"
#define MyAppVersion    "1.0.0"
#define MyAppPublisher  "Jarvis"
#define MyAppURL        "https://github.com/S1d11/jarvis-desktop"
#define MyAppExeName    "Jarvis.exe"

[Setup]
AppId={{B7F3A2E1-4C5D-6E7F-8A9B-0C1D2E3F4051}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DefaultDirName={pf}\Jarvis
DefaultGroupName=Jarvis
DisableProgramGroupPage=no
DisableDirPage=no
OutputDir=Output
OutputBaseFilename=Jarvis-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "startmenu";   Description: "Create Start Menu entries";  GroupDescription: "Additional icons:"; Flags: checkedonce
Name: "autostart";   Description: "Start Jarvis automatically at sign-in (minimised to tray)"; GroupDescription: "Startup:"; Flags: checkedonce
Name: "shellreplace"; Description: "Replace Windows Explorer with Jarvis as the shell (reboot required)"; GroupDescription: "Shell Replacement:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Jarvis"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Jarvis (Shell Mode)"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--shell"; WorkingDir: "{app}"
Name: "{group}\Uninstall Jarvis"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Jarvis"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; WorkingDir: "{app}"

[Registry]
; Autostart (tray mode)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "Jarvis"; \
    ValueData: """{app}\{#MyAppExeName}"" --tray"; \
    Flags: uninsdeletevalue; Tasks: autostart

; Shell replacement — sets Jarvis as the Windows shell
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"; \
    ValueType: string; ValueName: "Shell"; \
    ValueData: """{app}\{#MyAppExeName}"" --shell"; \
    Tasks: shellreplace

[Run]
; Launch Jarvis now
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Jarvis now"; Flags: postinstall nowait skipifsilent

[UninstallRun]
; Restore Explorer on uninstall
Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -NoProfile -Command ""reg add 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon' /v Shell /t REG_SZ /d 'explorer.exe' /f; start explorer.exe"""; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\Jarvis"
Type: filesandordirs; Name: "{userlocaldata}\Jarvis"
