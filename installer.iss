[Setup]
AppName=Salat-Al-Khayr
AppVersion=1.0
; The default installation folder
DefaultDirName={autopf}\Salat Al Khayr
; Where the final Setup.exe will be saved
OutputDir=Output
OutputBaseFilename=SalatAlKhayr_Setup
Compression=lzma2
SolidCompression=yes
; Require admin rights to install into Program Files
PrivilegesRequired=admin

; IMPORTANT: Since you are publishing for win-x64, you need these to ensure 
; it installs into the 64-bit "Program Files" folder, not "Program Files (x86)".
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
; Grabs everything in your publish folder
Source: "bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
Name: "autorun"; Description: "Auto run on startup";

[Icons]
; Creates a Start Menu shortcut. 
; Replaced the placeholder with the likely name of your executable.
Name: "{autoprograms}\Salat Al Khayr"; Filename: "{app}\Salat-Al-Khayr.exe"
; Creates a Desktop shortcut.
Name: "{autodesktop}\Salat Al Khayr"; Filename: "{app}\Salat-Al-Khayr.exe"; Tasks: desktopicon

[Registry]
; Adds the app to the Windows Startup registry key. 
; It only applies if the user checked the "autorun" box during installation.
; The 'uninsdeletevalue' flag ensures it is cleanly removed if the user uninstalls the app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SalatAlKhayr"; ValueData: """{app}\Salat-Al-Khayr.exe"""; Flags: uninsdeletevalue; Tasks: autorun

