@echo off
REM Extract data from Mount & Blade II Bannerlord Digital Companion

set "COMPANION_PATH=C:\TSEBanerAi\Mount & Blade II Bannerlord\DigitalCompanion"
set "OUTPUT_DIR=C:\TSEBanerAi\TSEBanerAi\Database\Digital Companion"

REM Check if companion path exists
if not exist "%COMPANION_PATH%" (
    echo Error: Digital Companion path not found: %COMPANION_PATH%
    echo Please edit this file and set correct COMPANION_PATH
    pause
    exit /b 1
)

echo ========================================
echo Extracting Digital Companion Data
echo ========================================
echo Companion path: %COMPANION_PATH%
echo Output directory: %OUTPUT_DIR%
echo.

py extract_digital_companion_data.py "%COMPANION_PATH%" "%OUTPUT_DIR%"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Extraction completed successfully!
) else (
    echo.
    echo Extraction failed with error code %ERRORLEVEL%
)

pause








