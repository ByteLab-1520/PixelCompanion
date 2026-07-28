#ifndef MyAppVersion
  #define MyAppVersion "0.4.0"
#endif

#ifndef MyAppName
#define MyAppName "Pixel Companion"
#endif
#define MyAppPublisher "Pixel Companion contributors"
#ifndef MyAppId
  #define MyAppId "{{7C0E4C61-4D4A-4E64-A9E4-4CD74A040D92}"
#endif
#ifndef MyAppExeName
#define MyAppExeName "PixelCompanion.exe"
#endif
#ifndef MyConfigExeName
#define MyConfigExeName "PixelCompanion.Config.exe"
#endif
#ifndef MyInstallFolder
  #define MyInstallFolder "PixelCompanion"
#endif
#ifndef MyOutputStem
  #define MyOutputStem "PixelCompanion-" + MyAppVersion + "-win-x64-Setup"
#endif
#ifndef MyAutoStartName
  #define MyAutoStartName "PixelCompanion"
#endif
#ifndef MyStagingRoot
  #define MyStagingRoot "..\..\artifacts\windows\standard\staging"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyInstallFolder}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\artifacts\windows\installer
OutputBaseFilename={#MyOutputStem}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start {#MyAppName} when I sign in / 로그인할 때 {#MyAppName} 시작"; GroupDescription: "Startup / 자동 시작"; Flags: unchecked

[Files]
Source: "{#MyStagingRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Advanced Settings"; Filename: "{app}\{#MyConfigExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAutoStartName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
