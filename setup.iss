[Setup]
; Uygulama bilgileri
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName=Otomatik Metin Genişletici
AppVersion=1.2.3
AppVerName=Otomatik Metin Genişletici v1.2.3
AppPublisher=Saffet Celik
AppPublisherURL=https://github.com/saffetcelik/Text-Expander
AppSupportURL=https://github.com/saffetcelik/Text-Expander/issues
AppUpdatesURL=https://github.com/saffetcelik/Text-Expander/releases
AppCopyright=© 2024 Saffet Celik

; Kurulum ayarları
DefaultDirName={userappdata}\Otomatik Metin Genisletici
DefaultGroupName=Otomatik Metin Genişletici
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=installer
OutputBaseFilename=MetinGenisletici-Setup-v1.2.3
SetupIconFile=icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

; Sistem gereksinimleri
MinVersion=10.0
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Dil desteği
ShowLanguageDialog=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "publish\OtomatikMetinGenisletici.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\imlec.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Otomatik Metin Genişletici"; Filename: "{app}\OtomatikMetinGenisletici.exe"; IconFilename: "{app}\icon.ico"
Name: "{group}\{cm:UninstallProgram,Otomatik Metin Genişletici}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Otomatik Metin Genişletici"; Filename: "{app}\OtomatikMetinGenisletici.exe"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Otomatik Metin Genişletici"; Filename: "{app}\OtomatikMetinGenisletici.exe"; IconFilename: "{app}\icon.ico"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\OtomatikMetinGenisletici.exe"; Description: "{cm:LaunchProgram,Otomatik Metin Genişletici}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Kurulum sonrası işlemler
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  // Kurulum öncesi kontroller
end;
