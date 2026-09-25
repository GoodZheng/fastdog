#define AppName "FastDog"
#define AppVersion "1.8.0"
#define AppPublisher "FastDog Team"
#define AppURL "https://github.com/GoodZheng/fastdog"
#define AppExeName "FastDog.exe"

[Setup]
AppId={{B8F3D5A2-7C1E-4D9F-A6B2-3E8C1F4D5A7B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=FastDog-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
; 不沿用旧版本安装时的语言记录（默认 yes 会把上次 1.5.0 英文版的 english 记为默认），
; 每次安装均按系统 UI 语言检测默认语言
UsePreviousLanguage=no
UninstallDisplayName={#AppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
; english 放首位作为默认回退：系统语言无匹配时用第一个声明的语言；
; 简体中文系统（zh-CN）自动匹配 chinesesimplified。语言选择框默认选中系统语言。
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "installer\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\FastDog.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\tools\rg.exe"; DestDir: "{app}\tools"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  OldVersion: String;
  UninstallerPath: String;
  ErrorCode: Integer;
begin
  // Check if previous version is installed
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1',
    'UninstallString', UninstallerPath) then
  begin
    UninstallerPath := RemoveQuotes(UninstallerPath);
    Exec(UninstallerPath, '/SILENT /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  end;
  Result := True;
end;
