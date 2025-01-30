@echo off
setlocal EnableDelayedExpansion

:: Enable virtual terminal processing
for /f %%a in ('echo prompt $E ^| cmd') do set "ESC=%%a"
set "GREEN=%ESC%[32m"
set "RED=%ESC%[31m"
set "YELLOW=%ESC%[33m"
set "CYAN=%ESC%[36m"
set "RESET=%ESC%[0m"

:: Set the path to your executable
set "EXE_PATH=bin\Debug\net48\AuroraAssetEditor.exe"

:: Delete the existing executable if it exists
if exist "%EXE_PATH%" (
    echo Deleting existing %CYAN%%EXE_PATH%%RESET%...
    del "%EXE_PATH%"
    if errorlevel 1 (
        echo %RED%Failed to delete existing executable.%RESET%
        exit /b 1
    )
)

:: Run dotnet build with cleaner output
echo %YELLOW%Building project...%RESET%
dotnet build --nologo --no-incremental -clp:NoSummary -v minimal
if errorlevel 1 (
    echo %RED%Build failed.%RESET%
    exit /b 1
)

:: Check if the executable was created and run it
if exist "%EXE_PATH%" (
    echo %GREEN%Starting%RESET% %CYAN%AuroraAssetEditor%RESET%...
    start "" "%EXE_PATH%"
) else (
    echo %YELLOW%Build succeeded but executable was not found at:%RESET% %CYAN%%EXE_PATH%%RESET%
)