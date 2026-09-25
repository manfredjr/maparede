@echo off
rem Gera o maparede.exe unico e autocontido na pasta publicar.
rem Roda os testes antes: se algum falhar, o .exe nao e gerado.
setlocal
cd /d "%~dp0.."

dotnet test maparede.sln -c Release
if errorlevel 1 (
    echo.
    echo Os testes falharam. O .exe nao foi gerado.
    exit /b 1
)

dotnet publish src\maparede\maparede.csproj -c Release -o publicar
if errorlevel 1 exit /b 1

echo.
echo Pronto: %cd%\publicar\maparede.exe
endlocal
