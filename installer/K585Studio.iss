#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\K585Studio-win-x64"
#endif
#ifndef InstallerOutput
  #define InstallerOutput "..\artifacts"
#endif

[Setup]
AppId=K585Studio
AppName=K585 Studio
AppVersion={#AppVersion}
AppPublisher=Esteban-3055
AppPublisherURL=https://github.com/Esteban-3055/K585Studio
AppSupportURL=https://github.com/Esteban-3055/K585Studio/issues
AppUpdatesURL=https://github.com/Esteban-3055/K585Studio/releases
DefaultDirName={localappdata}\Programs\K585Studio
DefaultGroupName=K585 Studio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
AppMutex=Local\K585Studio.v01
SetupMutex=K585StudioSetup
CloseApplications=yes
CloseApplicationsFilter=K585Studio.exe
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousLanguage=yes
DisableProgramGroupPage=yes
AllowNoIcons=yes
UninstallDisplayIcon={app}\K585Studio.exe
OutputDir={#InstallerOutput}
OutputBaseFilename=K585Studio-{#AppVersion}-Setup-win-x64
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoProductName=K585 Studio

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\K585 Studio"; Filename: "{app}\K585Studio.exe"
Name: "{autodesktop}\K585 Studio"; Filename: "{app}\K585Studio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\K585Studio.exe"; Description: "Abrir K585 Studio"; Flags: nowait postinstall skipifsilent; Check: CanLaunch
Filename: "{app}\K585Studio.exe"; Flags: nowait skipifnotsilent; Check: RestartAfterUpdate

[Code]
function OpenProcess(Access: LongWord; Inherit: Boolean; ProcessId: LongWord): THandle;
  external 'OpenProcess@kernel32.dll stdcall';
function WaitForSingleObject(Handle: THandle; Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function InitializeSetup(): Boolean;
var
  ProcessId: Integer;
  ProcessHandle: THandle;
begin
  Result := True;
  ProcessId := StrToIntDef(ExpandConstant('{param:K585WAITPID|0}'), 0);
  if ProcessId > 0 then begin
    ProcessHandle := OpenProcess($00100000, False, ProcessId);
    if ProcessHandle <> 0 then begin
      Result := WaitForSingleObject(ProcessHandle, 30000) = 0;
      CloseHandle(ProcessHandle);
      if not Result then
        MsgBox('K585 Studio sigue abierto. Cierra la aplicación y vuelve a iniciar el instalador.', mbError, MB_OK);
    end;
  end;
end;

function CanLaunch(): Boolean;
begin
  Result := ExpandConstant('{param:NOAUTOLAUNCH|0}') <> '1';
end;

function RestartAfterUpdate(): Boolean;
begin
  Result := CanLaunch() and (ExpandConstant('{param:K585RESTART|0}') = '1');
end;
