@echo off
rem Arranca la web publicada (publicar.cmd) en este equipo, en su propia ventana.
rem Los compañeros entran por la IP de este equipo y el puerto 5180:
rem
rem   http://<IP de este equipo>:5180
rem
rem Cerrar la ventana (o Ctrl+C dentro) la para.

set "RAIZ=%~dp0"
set "PUBLICADO=%RAIZ%publicacion\app"
if not exist "%PUBLICADO%\CDM Auditorias Calidad.dll" (
  echo No hay nada publicado en %PUBLICADO%. Ejecuta antes publicar.cmd.
  pause
  exit /b 1
)
start "CDM Auditorias Calidad (5180)" cmd /k "cd /d "%PUBLICADO%" && set ASPNETCORE_ENVIRONMENT=Production&& set ASPNETCORE_URLS=http://0.0.0.0:5180&& dotnet "CDM Auditorias Calidad.dll""
