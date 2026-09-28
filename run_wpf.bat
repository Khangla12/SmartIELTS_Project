@echo off
title SmartIELTS - Khoi chay WPF Kiosk Client
echo ===================================================
echo   DANG KHOI CHAY UNG DUNG MAY TRAM KIOSK WPF...
echo   He thong: SmartIELTS (SWD392 & PRN222)
echo ===================================================
cd /d "%~dp0"
dotnet run --project src/SmartIELTS.WpfApp/SmartIELTS.WpfApp.csproj
pause
