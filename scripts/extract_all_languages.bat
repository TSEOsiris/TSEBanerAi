@echo off
REM Extract encyclopedia data for all languages (EN, RU, TR)

set "GAME_PATH=C:\TSEBanerAi\Mount & Blade II Bannerlord"
set "OUTPUT_BASE=C:\TSEBanerAi\TSEBanerAi\Database\Ingame Encyclopedia"

REM Check if game path exists
if not exist "%GAME_PATH%" (
    echo Error: Game path not found: %GAME_PATH%
    echo Please edit this file and set correct GAME_PATH
    pause
    exit /b 1
)

echo ========================================
echo Extracting Encyclopedia Data
echo ========================================
echo Game path: %GAME_PATH%
echo Output base: %OUTPUT_BASE%
echo.

REM Extract English
echo.
echo [1/3] Extracting English (EN)...
echo ----------------------------------------
py extract_encyclopedia_data.py "%GAME_PATH%" "%OUTPUT_BASE%\EN" "EN"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to extract English data
    pause
    exit /b 1
)

REM Extract Russian
echo.
echo [2/3] Extracting Russian (RU)...
echo ----------------------------------------
py extract_encyclopedia_data.py "%GAME_PATH%" "%OUTPUT_BASE%\RU" "RU"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to extract Russian data
    pause
    exit /b 1
)

REM Extract Turkish
echo.
echo [3/3] Extracting Turkish (TR)...
echo ----------------------------------------
py extract_encyclopedia_data.py "%GAME_PATH%" "%OUTPUT_BASE%\TR" "TR"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to extract Turkish data
    pause
    exit /b 1
)

echo.
echo ========================================
echo All extractions completed successfully!
echo ========================================
echo.
echo Data saved to:
echo   - %OUTPUT_BASE%\EN
echo   - %OUTPUT_BASE%\RU
echo   - %OUTPUT_BASE%\TR
echo.

pause

