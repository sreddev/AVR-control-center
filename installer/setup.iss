[Setup]
; AppId is the pre-rename name, so this upgrades an existing
; "AVR Desktop Control" install in place instead of adding a second app.
AppId=AVR Desktop Control
AppName=AVR Control Center
AppVersion=3.0.0
AppPublisher=Sergio (@sreddev)
AppPublisherURL=https://github.com/sreddev/AVR-control-center
AppSupportURL=https://t.me/sreddev
DefaultDirName={autopf}\AVR Control Center
DefaultGroupName=AVR Control Center
UsePreviousGroup=no
UninstallDisplayIcon={app}\AVRControlCenter.exe
OutputDir=..\installer\out
OutputBaseFilename=AVRControlCenter-3.0.0-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\src\AvrControlCenter\Assets\app.ico
LicenseFile=..\LICENSE
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[InstallDelete]
; Leftovers from versions before the rename (2.5 and older)
Type: files; Name: "{app}\AVRDesktopControl.exe"
Type: filesandordirs; Name: "{autoprograms}\AVR Desktop Control"
Type: files; Name: "{autodesktop}\AVR Desktop Control.lnk"
Type: files; Name: "{autostartup}\AVR Desktop Control.lnk"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AVR Control Center"; Filename: "{app}\AVRControlCenter.exe"
Name: "{group}\Uninstall AVR Control Center"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AVR Control Center"; Filename: "{app}\AVRControlCenter.exe"; Tasks: desktopicon
Name: "{autostartup}\AVR Control Center"; Filename: "{app}\AVRControlCenter.exe"; Tasks: startupicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Options:"

[Run]
Filename: "{app}\AVRControlCenter.exe"; Description: "Launch AVR Control Center"; Flags: nowait postinstall skipifsilent


