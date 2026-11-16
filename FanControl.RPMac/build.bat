@echo off
setlocal
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo ERROR: .NET Framework 4 C# compiler ^(csc.exe^) not found.
  exit /b 1
)

set "ROOT=%~dp0"
set "RPMAC=%ROOT%.."
set "OUT=%ROOT%build"
set "PLUG=%RPMAC%\plugin\FanControl.Plugins.dll"
set "FAC=C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades"

if not exist "%PLUG%" (
  echo ERROR: Missing %PLUG%
  echo Extract FanControl.Plugins.dll from FanControl.zip into RPMAC\plugin\
  exit /b 1
)

if not exist "%OUT%" mkdir "%OUT%"

echo Compiling FanControl.RPMac.dll...
"%FW%\csc.exe" /noconfig /nologo /target:library /platform:x64 ^
  /out:"%OUT%\FanControl.RPMac.dll" ^
  /reference:"%PLUG%" ^
  /reference:"%FW%\System.dll" ^
  /reference:"%FW%\System.Core.dll" ^
  /reference:"%FAC%\netstandard.dll" ^
  /reference:"%FAC%\System.Runtime.dll" ^
  "%RPMAC%\src\gui\Smc.cs" ^
  "%ROOT%RPMacPlugin.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  exit /b 1
)

copy /y "%RPMAC%\third_party\InpOut32\bin\Win32\inpout32.dll" "%OUT%\inpout32.dll" >nul 2>&1
copy /y "%RPMAC%\third_party\InpOut32\bin\x64\inpoutx64.dll" "%OUT%\inpoutx64.dll" >nul 2>&1
if not exist "%OUT%\inpout32.dll" echo WARNING: inpout32.dll not copied.
if not exist "%OUT%\inpoutx64.dll" echo WARNING: inpoutx64.dll not copied - 64-bit SMC access may fail.
copy /y "%RPMAC%\dist\AppleT2Smc.bin" "%OUT%\AppleT2Smc.bin" >nul 2>&1

echo.
echo BUILD OK  -^>  %OUT%\FanControl.RPMac.dll
echo.
echo Install:
echo   1. Close Fan Control
echo   2. Copy ALL files from this build\ folder into Fan Control's Plugins\ directory
echo      ^(FanControl.RPMac.dll, inpout32.dll, inpoutx64.dll, AppleT2Smc.bin^)
echo   3. Run Fan Control as Administrator
endlocal
exit /b 0

