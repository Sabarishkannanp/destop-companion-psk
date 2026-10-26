#define MyAppName "Buddy"
#define MyAppVersion "1.0.0"
[Setup]
AppId={{DF4763FC-86DF-4ACD-A069-BB1B2509CE01}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\Buddy
DefaultGroupName=Buddy
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Buddy-Setup
SetupIconFile=..\src\Buddy\Assets\app.ico
UninstallDisplayIcon={app}\Buddy.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start Buddy with Windows"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\Buddy.exe"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Buddy"; Filename: "{app}\Buddy.exe"
Name: "{autodesktop}\Buddy"; Filename: "{app}\Buddy.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Buddy"; ValueData: """{app}\Buddy.exe"" --quiet"; Tasks: startup; Flags: uninsdeletevalue
[Run]
Filename: "{app}\Buddy.exe"; Description: "Meet Buddy"; Flags: nowait postinstall skipifsilent
