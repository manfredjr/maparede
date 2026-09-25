@echo off
rem Gera o mt-mapa-rede.exe unico e autocontido na pasta publicar.
rem Roda os testes antes: se algum falhar, o .exe nao e gerado.
setlocal
cd /d "%~dp0.."

dotnet test mapa-rede-mt.sln -c Release
if errorlevel 1 (
    echo.
    echo Os testes falharam. O .exe nao foi gerado.
    exit /b 1
)

dotnet publish src\mapa-rede-mt\mapa-rede-mt.csproj -c Release -o publicar
if errorlevel 1 exit /b 1

echo.
echo Pronto: %cd%\publicar\mt-mapa-rede.exe
endlocal
