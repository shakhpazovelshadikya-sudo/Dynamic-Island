; Установщик Dynamic Island -> DinamicIslandsetap.exe (собирается Inno Setup 6)
#define AppName "Dynamic Island"
#define AppExe  "DynamicIsland.exe"
#ifndef AppVer
  #define AppVer "1.0.0"
#endif

[Setup]
AppId={{044908CD-3FC4-4F70-9818-2A9FE2984636}
AppName={#AppName}
AppVersion={#AppVer}
AppPublisher=Dynamic Island
; По умолчанию ставим на диск C в папку C:\DynamicIsland (в мастере можно выбрать другую)
DefaultDirName=C:\DynamicIsland
DefaultGroupName={#AppName}
DisableProgramGroupPage=no
AllowNoIcons=yes
DisableDirPage=no
DisableReadyPage=no
UsePreviousTasks=yes
UninstallDisplayName={#AppName}
; Права администратора не нужны: пользователь может создавать папки в корне диска C
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=DinamicIslandsetap
SetupIconFile=..\assets\DynamicIsland.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительно:"
Name: "startmenuicon"; Description: "Создать ярлык в меню «Пуск»"; GroupDescription: "Дополнительно:"
Name: "autostart"; Description: "Запускать вместе с Windows"; GroupDescription: "Дополнительно:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: startmenuicon
Name: "{group}\Удалить {#AppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Выбор автозапуска делает установщик; приложение при первом запуске его уже не меняет (FirstRunDone)
Root: HKCU; Subkey: "Software\DynamicIsland"; ValueName: "FirstRunDone"; ValueType: dword; ValueData: 1; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "DynamicIsland"; ValueType: string; ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "DynamicIsland"; ValueType: none; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Запустить {#AppName}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExe}"; Flags: nowait skipifnotsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "KillDynamicIsland"

[Code]
{ Приложение собрано без встроенного .NET (установщик ~5 МБ вместо ~150 МБ).
  Если на компьютере нет .NET 8 Desktop Runtime — установщик сам скачает и поставит его. }
const
  RuntimeUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';

function RuntimeInstalled: Boolean;
var
  Dir: String;
  R: TFindRec;
begin
  Result := False;
  Dir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\');
  if FindFirst(Dir + '8.*', R) then
  try
    repeat
      if R.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then Result := True;
    until Result or not FindNext(R);
  finally
    FindClose(R);
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Installer: String;
begin
  Result := '';
  if RuntimeInstalled then Exit;

  WizardForm.StatusLabel.Caption := 'Downloading .NET 8 Desktop Runtime...';
  try
    DownloadTemporaryFile(RuntimeUrl, 'dotnet-desktop-runtime.exe', '', @OnDownloadProgress);
  except
    Result := '.NET 8 Desktop Runtime download failed: ' + GetExceptionMessage + #13#10 +
              'Check your internet connection or install it from https://dotnet.microsoft.com/download/dotnet/8.0';
    Exit;
  end;

  Installer := ExpandConstant('{tmp}\dotnet-desktop-runtime.exe');
  WizardForm.StatusLabel.Caption := 'Installing .NET 8 Desktop Runtime...';
  if not ShellExec('runas', Installer, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code)
     or ((Code <> 0) and (Code <> 3010)) then
    Result := '.NET 8 Desktop Runtime installation failed (code ' + IntToStr(Code) + ').';
end;
