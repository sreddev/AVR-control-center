[Setup]
AppName=AVR Desktop Control
AppVersion=2.2.0
AppPublisher=Sergio (@sreddev)
AppPublisherURL=https://github.com/sreddev/AVR-desktop-control
AppSupportURL=https://t.me/sreddev
DefaultDirName={autopf}\AVR Desktop Control
DefaultGroupName=AVR Desktop Control
UninstallDisplayIcon={app}\AVRDesktopControl.exe
OutputDir=..\installer\out
OutputBaseFilename=AVRDesktopControl-2.2.0-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\src\AvrRemote\Assets\app.ico
LicenseFile=..\LICENSE
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AVR Desktop Control"; Filename: "{app}\AVRDesktopControl.exe"
Name: "{group}\Uninstall AVR Desktop Control"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AVR Desktop Control"; Filename: "{app}\AVRDesktopControl.exe"; Tasks: desktopicon
Name: "{autostartup}\AVR Desktop Control"; Filename: "{app}\AVRDesktopControl.exe"; Tasks: startupicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startupicon"; Description: "Start with Windows"; GroupDescription: "Options:"

[Run]
Filename: "{app}\AVRDesktopControl.exe"; Description: "Launch AVR Desktop Control"; Flags: nowait postinstall skipifsilent


