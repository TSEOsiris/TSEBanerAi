@echo off
REM Extract encyclopedia data from Mount & Blade II Bannerlord game files

set GAME_PATH=C:\TSEBanerAi\Mount & Blade II Bannerlord
set OUTPUT_DIR=Database\encyclopedia
set LANGUAGE=RU

REM Check if game path exists
if not exist "%GAME_PATH%" (
    echo Error: Game path not found: %GAME_PATH%
    echo Please edit this file and set correct GAME_PATH
    pause
    exit /b 1
)

echo Extracting encyclopedia data...
echo Game path: %GAME_PATH%
echo Output directory: %OUTPUT_DIR%
echo Language: %LANGUAGE%
echo.

python extract_encyclopedia_data.py "%GAME_PATH%" "%OUTPUT_DIR%" %LANGUAGE%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Extraction completed successfully!
) else (
    echo.
    echo Extraction failed with error code %ERRORLEVEL%
)

pause








