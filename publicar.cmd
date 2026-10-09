@echo off
rem Compila la web en Release y la deja en publicacion\app, que es la copia que
rem se sirve a los compañeros (arrancar.cmd). Antes de publicar hay que cerrar la
rem ventana de arrancar.cmd: con la web en marcha sus ficheros están en uso.
rem
rem Las credenciales se leen de publicacion\.env (appsettings.Production.json).
rem Si todavía no existe, se copia el .env del proyecto.

set "RAIZ=%~dp0"
dotnet publish "%RAIZ%CDM Auditorias Calidad\CDM Auditorias Calidad.csproj" -c Release -o "%RAIZ%publicacion\app" --nologo
if errorlevel 1 (
  echo.
  echo No se pudo publicar. Si dice que un fichero esta en uso, cierra la ventana de la web y repite.
  pause
  exit /b 1
)
if not exist "%RAIZ%publicacion\.env" (
  if exist "%RAIZ%CDM Auditorias Calidad\.env" copy "%RAIZ%CDM Auditorias Calidad\.env" "%RAIZ%publicacion\.env" >nul
)
if not exist "%RAIZ%publicacion\.env" (
  echo.
  echo Falta publicacion\.env con las credenciales de SQL Server. Plantilla: CDM Auditorias Calidad\env.ejemplo
)
echo.
echo Publicado en %RAIZ%publicacion\app. Arranca con arrancar.cmd.
