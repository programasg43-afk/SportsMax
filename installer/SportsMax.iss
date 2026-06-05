; ============================================================================
;  SportsMax - Script de instalador (Inno Setup 6)
;  Genera un instalador wizard "Siguiente > Siguiente > Instalar".
;
;  REQUISITOS PARA COMPILAR:
;    1) Publicar la app (desde la raiz del repo):
;         dotnet publish SportsMax.csproj -c Release -r win-x64 --self-contained true -o publish
;    2) (Opcional) Colocar el bootstrapper de WebView2 en installer\redist\
;         MicrosoftEdgeWebview2Setup.exe  (https://go.microsoft.com/fwlink/p/?LinkId=2124703)
;    3) Compilar este script con Inno Setup (ISCC.exe SportsMax.iss)
;
;  El instalador resultante queda en  installer\Output\
; ============================================================================

#define MyAppName "SportsMax"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "SportsMax"
#define MyAppExeName "SportsMax.exe"
#define MyPublishDir "..\publish"

[Setup]
AppId={{B7E4F2A1-9C3D-4E5F-8A6B-1D2C3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableReadyPage=no
AllowNoIcons=yes
LicenseFile=license.txt
OutputDir=Output
OutputBaseFilename=SportsMax-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\icono.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Imagenes de marca del asistente (logo de SportsMax)
WizardImageFile=wizard-large.png
WizardSmallImageFile=wizard-small.png
WizardImageStretch=yes
WizardImageAlphaFormat=defined
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
MinVersion=10.0
ShowLanguageDialog=auto

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Aplicacion publicada (self-contained, incluye .NET y libVLC)
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Documentos
Source: "..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion skipifsourcedoesntexist
; Bootstrapper de WebView2 (opcional; se ejecuta solo si falta el runtime)
Source: "redist\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Politica de Privacidad"; Filename: "{app}\PRIVACY.md"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Instalar Microsoft WebView2 Runtime solo si no esta presente
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; \
  StatusMsg: "Instalando componente Microsoft WebView2..."; \
  Check: NeedsWebView2 and WebView2InstallerExists; Flags: waituntilterminated
; Lanzar la app al terminar
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
  Flags: nowait postinstall skipifsilent

; Nota: los datos locales (logs y cache del navegador) quedan en
; %LOCALAPPDATA%\SportsMax y pueden borrarse manualmente; no se eliminan
; automaticamente para no afectar a otros perfiles de usuario.

[Code]
function NeedsWebView2: Boolean;
var
  v: String;
begin
  Result := True;
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) then
    if (v <> '') and (v <> '0.0.0.0') then Result := False;
  if Result then
    if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) then
      if (v <> '') and (v <> '0.0.0.0') then Result := False;
  if Result then
    if RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) then
      if (v <> '') and (v <> '0.0.0.0') then Result := False;
end;

function WebView2InstallerExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'));
end;
