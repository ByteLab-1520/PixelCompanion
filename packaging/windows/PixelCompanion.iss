#ifndef MyAppVersion
  #define MyAppVersion "0.5.2"
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
#ifdef MySetupIcon
SetupIconFile={#MySetupIcon}
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[CustomMessages]
english.StandardUninstallMissing=Pixel Companion is registered as installed, but its uninstaller could not be found.
korean.StandardUninstallMissing=기존 Pixel Companion이 설치되어 있지만 제거 프로그램을 찾지 못했습니다.
english.StandardUninstallFailed=The existing Pixel Companion installation could not be removed. Please uninstall it manually and try again.
korean.StandardUninstallFailed=기존 Pixel Companion을 자동으로 제거하지 못했습니다. 직접 제거한 뒤 다시 시도해 주세요.

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

#ifdef RemoveStandardEdition
[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Uninstaller: String;
  ResultCode: Integer;
begin
  Result := '';
  if not RegQueryStringValue(
    HKCU,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7C0E4C61-4D4A-4E64-A9E4-4CD74A040D92}_is1',
    'UninstallString',
    Uninstaller) then
    exit;

  Uninstaller := RemoveQuotes(Uninstaller);
  if (Uninstaller = '') or not FileExists(Uninstaller) then
  begin
    Result := CustomMessage('StandardUninstallMissing');
    exit;
  end;

  if not Exec(
    Uninstaller,
    '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) or (ResultCode <> 0) then
    Result := CustomMessage('StandardUninstallFailed');
end;
#endif
