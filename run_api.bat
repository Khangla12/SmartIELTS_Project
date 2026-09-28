@echo off
title SmartIELTS - Khoi chay Web API (Swagger UI)
echo ===================================================
echo   DANG KHOI CHAY ASP.NET CORE WEB API...
echo   He thong: SmartIELTS (SWD392 & PRN222)
echo   Swagger UI: http://localhost:5294/
echo ===================================================
cd /d "%~dp0"
dotnet run --project src/SmartIELTS.WebApi/SmartIELTS.WebApi.csproj
pause
