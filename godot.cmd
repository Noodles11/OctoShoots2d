@echo off
rem Launches the real Godot .NET executable for this project.
rem The "godot" link that WinGet puts on PATH hides the GodotSharp folder, so C# fails to load.
rem   godot          open the editor
rem   godot --run    run the game
rem   godot <args>   pass any other Godot arguments
setlocal

set "GODOT_EXE="
for /d %%D in ("%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*") do (
    for /d %%E in ("%%D\Godot_v*_mono_win64") do (
        for %%F in ("%%E\Godot_v*_mono_win64_console.exe") do set "GODOT_EXE=%%~fF"
    )
)
if defined GODOT_PATH set "GODOT_EXE=%GODOT_PATH%"

if not defined GODOT_EXE (
    echo Could not find Godot .NET. Install it with: winget install GodotEngine.GodotEngine.Mono
    echo or set GODOT_PATH to the full path of Godot_v*_mono_win64_console.exe
    exit /b 1
)

if "%~1"=="" (
    "%GODOT_EXE%" --path "%~dp0." --editor
) else if /i "%~1"=="--run" (
    "%GODOT_EXE%" --path "%~dp0."
) else (
    "%GODOT_EXE%" --path "%~dp0." %*
)
