[Setup]
AppName=Salat-Al-Khayr
AppVersion=1.0
; Default installation folder
DefaultDirName={autopf}\Salat Al Khayr
; Where the final Setup.exe will be saved
OutputDir=Output
OutputBaseFilename=SalatAlKhayr_Setup
Compression=lzma2
SolidCompression=yes
; Require admin rights to install into Program Files
PrivilegesRequired=admin

[Files]
; Grab everything in your publish folder
Source: "bin\Release\net10.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Replace 'SalatAlKhayr.exe' with the actual name of your compiled EXE
Name: "{autoprograms}\Salat-Al-Khayr"; Filename: "{app}\SalatAlKhayr.exe"
Name: "{autodesktop}\Salat-Al-Khayr"; Filename: "{app}\SalatAlKhayr.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"
