@echo off
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
tasklist /fi "imagename eq DefenderExclusions.exe" | find /i "DefenderExclusions.exe" >nul && (echo DefenderExclusions.exe is running. Close it first. & pause & exit /b 1)
"%CSC%" /nologo /target:winexe /optimize+ /out:DefenderExclusions.exe /win32icon:defender_small.ico /win32manifest:DefenderExclusions.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /r:System.Core.dll DefenderExclusions.cs
if errorlevel 1 (echo BUILD FAILED) else (echo BUILD OK)
pause