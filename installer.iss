[Setup]
AppName=Salat-Al-Khayr
AppVersion=1.0
; The default installation folder (usually C:\Program Files\Salat Al Khayr)
DefaultDirName={autopf}\Salat Al Khayr
; Where the final Setup.exe will be saved
OutputDir=Output
OutputBaseFilename=SalatAlKhayr_Setup
Compression=lzma2
SolidCompression=yes
; Require admin rights to install into Program Files
PrivilegesRequired=admin

[Files]
; This grabs everything in your AOT publish folder (exe, dlls, and the assets folder)
Source: "bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Creates a Start Menu shortcut. 
; IMPORTANT: Change "YourAppExecutableName.exe" to the actual name of your compiled app!
Name: "{autoprograms}\Adhan Player"; Filename: "{app}\YourAppExecutableName.exe"
Name: "{autodesktop}\Adhan Player"; Filename: "{app}\YourAppExecutableName.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"