#define MyAppName "SafeDrive Backup"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Songnam Saraphai"
#define MyAppExeName "SafeDriveBackup.exe"
#define MyAppCopyright "Copyright (c) 2026 Songnam Saraphai"
#define MySourceExe "SafeDriveBackup.App\bin\Release\net8.0-windows\win-x64\publish\SafeDriveBackup.exe"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright={#MyAppCopyright}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription=SafeDrive Backup - Local file backup utility

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

LicenseFile=LICENSE.txt

OutputDir=installer-output
OutputBaseFilename=SafeDriveBackup-Setup-v{#MyAppVersion}

MinVersion=10.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern

SetupIconFile=SafeDriveBackup.App\SafeDrive.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"

[Files]
Source: "{#MySourceExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
; Remove "Start with Windows" entry the app may have written
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueName: "SafeDriveBackup"; Flags: deletevalue uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/f /im {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; Remove all app settings so reinstall starts fresh with setup wizard
Type: filesandordirs; Name: "{userappdata}\SafeDriveBackup"

[Code]
procedure InitializeWizard();
begin
  WizardForm.WelcomeLabel2.Caption :=
    'This will install SafeDrive Backup on your computer.' + #13#10 + #13#10 +
    'SafeDrive Backup is a local file backup utility that automatically ' +
    'protects your Desktop, Documents, Pictures and other folders.' + #13#10 + #13#10 +
    'Developed by Songnam Saraphai' + #13#10 +
    'Licensed under the MIT License.';
end;
