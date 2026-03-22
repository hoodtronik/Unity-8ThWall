@echo off
title AG Bridge Launcher
echo ============================================
echo  Antigravity + AG Bridge Launcher
echo ============================================
echo.

REM --- Step 1: Keep PC awake (prevent sleep on lid close) ---
echo [1/3] Setting power policy to prevent sleep on lid close...
powercfg /setacvalueindex SCHEME_CURRENT SUB_BUTTONS LIDACTION 0
powercfg /setactive SCHEME_CURRENT
echo      Done. Closing laptop lid will NOT suspend.
echo.

REM --- Step 2: Launch Antigravity with remote debugging + SSL bypass ---
echo [2/3] Launching Antigravity with debugging port + SSL bypass...
start "" "C:\Users\hoodt\AppData\Local\Programs\Antigravity\Antigravity.exe" --remote-debugging-port=9000 --ignore-certificate-errors --disable-features=NetworkServiceInProcess
echo      Waiting 8 seconds for Antigravity to start...
timeout /t 8 /nobreak >nul
echo.

REM --- Step 3: Launch AG Bridge ---
echo [3/3] Starting AG Bridge server...
echo      Open the URL shown below on your phone.
echo ============================================
cd /d "G:\_AR_Projects\Unity-8ThWall\ag_bridge"
node server.mjs
