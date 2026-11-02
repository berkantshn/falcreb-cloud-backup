; FalcrebCloudBackup kurulum betiği (Inno Setup 6).
; Derlemek için: ISCC.exe FalcrebCloudBackup.iss
; Çıktı: installer\output\FalcrebCloudBackup-Setup.exe

#define MyAppName "FalcrebCloudBackup"
; Yayın hattı sürümü /DMyAppVersion=... ile tag'den verir.
#ifndef MyAppVersion
  #define MyAppVersion "1.5.0"
#endif
#define MyAppPublisher "FALCREB"
#define MyAppExeName "FalcrebCloudBackup.App.exe"
#define MySourceDir "..\src\FalcrebCloudBackup.App\bin\Release\net48"

[Setup]
AppId={{B7B6C6C0-6B7B-4A5B-9D8A-2F3E7E6D2C1A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=output
OutputBaseFilename=FalcrebCloudBackup-Setup
SetupIconFile={#MySourceDir}\Assets\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Uygulamanın kendisi zaten yönetici hakkı istiyor (Task Scheduler kaydı için) —
; kurulum da aynı seviyede çalışmalı, aksi halde Program Files'a yazamaz.
PrivilegesRequired=admin
; Güncellemede çalışan uygulama kapatılır; yeniden açma işini [Run] bölümü yapar.
CloseApplications=yes
RestartApplications=no
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

; Kurulum paketi OAuth kimliği içermez: her kullanıcı kendi google_client_secret.json ve
; onedrive_client_config.json dosyasını %ProgramData%\FalcrebCloudBackup altına koyar (bkz. README).

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

; Uygulama kendi manifestosunda yönetici izni istiyor (requireAdministrator) — Setup zaten
; yönetici olarak çalışırken bir alt işlemi UAC istemi olmadan CreateProcess ile başlatamıyor
; (kod 740). Kısayoldan (Başlat menüsü/masaüstü) çift tıklandığında bu sorun olmuyor, çünkü
; Explorer o çağrıyı ShellExecute ile yapıyor ve UAC istemini kendisi düzgün tetikliyor —
; bu yüzden kurulum sonrası başlatma ShellExecute (shellexec) ile yapılır ve yalnızca sessiz
; kurulumda, yani uygulama içinden gelen güncellemede çalışır.

[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait shellexec; Check: WizardSilent
