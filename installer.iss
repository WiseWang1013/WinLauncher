; ============================================================
;  WinLauncher 3.0.0 —— Inno Setup 6 安装脚本
;  只打包运行必需的三个文件，其余（pdb / xml / 未使用的 dll）已剔除
; ============================================================

#define MyAppName "WinLauncher"
#define MyAppVersion "3.0.0"
#define MyAppPublisher "WiseWang1013"
#define MyAppURL "https://github.com/WiseWang1013"
#define MyAppExeName "WinLauncher.exe"
#define BuildDir "D:\WinLauncher"

[Setup]
; AppId 保持不变，这样新版本能覆盖升级旧版本
AppId={{56520CB3-1800-4CDE-97CD-79748C948B79}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
DisableProgramGroupPage=yes
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=D:\
OutputBaseFilename=WinLauncher_Setup_{#MyAppVersion}
SetupIconFile=D:\ico.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern polar
; .NET Framework 4.8 最低要求 Win7 SP1
MinVersion=6.1sp1
; 安装前关掉正在运行的旧版本
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion=3.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english";           MessagesFile: "compiler:Default.isl"
Name: "french";            MessagesFile: "compiler:Languages\French.isl"
Name: "arabic";            MessagesFile: "compiler:Languages\Arabic.isl"
Name: "russian";           MessagesFile: "compiler:Languages\Russian.isl"
Name: "spanish";           MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 运行必需的三个文件，其余一律不打包
Source: "{#BuildDir}\{#MyAppExeName}";          DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\{#MyAppExeName}.config";   DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\Newtonsoft.Json.dll";      DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 卸载时顺手清掉程序目录里运行时生成的配置
Type: files; Name: "{app}\config.json"
