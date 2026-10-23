@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "DEST=C:\Program Files (x86)\Steam\steamapps\common\Muck\PurpleHaze\plugins\muckmod.template"
set "OUT=%~dp0bin\Release\netstandard2.0"
set "NUGET=%USERPROFILE%\.nuget\packages"

echo Building...
dotnet build -c Release
if errorlevel 1 (
  echo BUILD FAILED
  pause
  exit /b 1
)

echo.
echo Copying Harmony from NuGet if needed...
if not exist "%OUT%\0Harmony.dll" (
  for /r "%NUGET%\harmonyx" %%F in (0Harmony.dll) do (
    copy /Y "%%F" "%OUT%\0Harmony.dll"
    echo Got 0Harmony from %%F
    goto :hok
  )
  echo FAILED to find 0Harmony in %NUGET%\harmonyx
  echo Run: dotnet restore
  pause
  exit /b 1
)
:hok

for %%P in (monomod.runtimedetour monomod.utils monomod.core monomod.backports monomod.ilhelpers monomod.iced mono.cecil) do (
  if exist "%NUGET%\%%P" (
    for /r "%NUGET%\%%P" %%F in (*.dll) do (
      if /I "%%~nxF"=="MonoMod.RuntimeDetour.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="MonoMod.Utils.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="MonoMod.Core.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="MonoMod.Backports.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="MonoMod.ILHelpers.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="MonoMod.Iced.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="Mono.Cecil.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="Mono.Cecil.Mdb.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="Mono.Cecil.Pdb.dll" copy /Y "%%F" "%OUT%\" >nul
      if /I "%%~nxF"=="Mono.Cecil.Rocks.dll" copy /Y "%%F" "%OUT%\" >nul
    )
  )
)

mkdir "%DEST%" 2>nul
echo Installing DLLs to:
echo   %DEST%
xcopy /Y /Q "%OUT%\*.dll" "%DEST%\"
copy /Y "%~dp0manifest.json" "%DEST%\manifest.json" >nul

echo.
echo DLLs now in plugin folder:
dir /b "%DEST%\*.dll"
echo.
if exist "%DEST%\0Harmony.dll" (
  echo SUCCESS - 0Harmony.dll present. Start Muck.
) else (
  echo FAIL - 0Harmony still missing.
)
pause
