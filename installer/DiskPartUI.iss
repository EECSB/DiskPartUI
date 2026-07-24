; Inno Setup script for DiskPart UI.
;
; Build the payload first (self-contained, so the target machine needs no runtimes):
;   dotnet publish DiskPartUI.csproj -c Release -f net10.0-windows10.0.19041.0 ^
;       -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true
;
; Then compile this script:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\DiskPartUI.iss
;
; Output: bin\DiskPartUI-v<version>-setup.exe

#define AppName      "DiskPart UI"
#define AppVersion   "1.0.1"
#define AppPublisher "The EECS Blog"
#define AppURL       "https://github.com/EECSB/DiskPartUI"
#define AppExe       "DiskPartUI.exe"
#define BuildDir     "..\bin\Release\net10.0-windows10.0.19041.0\win-x64"

[Setup]
AppId={{A7F3C2E1-5D48-4B96-9E17-3C6D8B2F4A05}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}

DefaultDirName={autopf}\DiskPartUI
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile={#BuildDir}\appicon.ico

OutputDir=..\bin
OutputBaseFilename=DiskPartUI-v{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

; diskpart needs elevation and the app itself is marked requireAdministrator,
; so install per-machine into Program Files.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildDir}\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
