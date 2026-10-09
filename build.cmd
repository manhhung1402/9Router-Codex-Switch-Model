@echo off
REM Rebuild the 9Router Switcher EXE from the C# sources in this folder.
REM Requires: Windows + .NET Framework (already present). Visual Studio NOT needed.
REM NOTE: plain ASCII on purpose. cmd.exe cannot pass non-ASCII file names,
REM       so we build to a temp name and let PowerShell do the final copy.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" goto :nocsc
set TMPEXE=%TEMP%\9router-build-tmp.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /win32icon:"%~dp0app.ico" /win32manifest:"%~dp0app.manifest" ^
  /out:"%TMPEXE%" ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll ^
  "%~dp0AssemblyInfo.cs" "%~dp0ModelSwitcher.cs" "%~dp0ModelSwitcher.Agents.cs" "%~dp0ModelSwitcher.RowList.cs" "%~dp0ModelSwitcher.UI.cs" "%~dp0ModelSwitcher.Main.cs"
if errorlevel 1 goto :fail
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$src = '%TEMP%\9router-build-tmp.exe'; $dst = Get-ChildItem -LiteralPath '%~dp0..' -Filter '9Router*.exe' -File | Select-Object -First 1; if ($dst) { Copy-Item -LiteralPath $src -Destination $dst.FullName -Force; Write-Host ('BUILD OK - ' + $dst.Name) } else { Write-Error 'Khong tim thay file .exe de ghi de.'; exit 1 }"
if errorlevel 1 goto :fail
del /q "%TMPEXE%" 2>nul
pause
exit /b 0
:nocsc
echo Khong tim thay csc.exe trong thu muc .NET Framework cua Windows.
pause
exit /b 1
:fail
echo.
echo BUILD LOI - xem cac dong loi ben tren.
pause
exit /b 1