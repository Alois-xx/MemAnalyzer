@echo off
rem ---------------------------------------------------------------------------
rem build.cmd - builds MemAnalyzer for x86 AND x64 and collects both into one
rem deployment folder (dist\MemAnalyzer). Unit tests are NOT built and NOT
rem included. MemAnalyzer.exe (x86) and MemAnalyzerx64.exe are deployed side by
rem side; the x86 exe calls the x64 exe directly from its own folder when an
rem x64 target must be analyzed.
rem ---------------------------------------------------------------------------
setlocal
set CONFIG=Release
set SRC=%~dp0
set PROJ=%SRC%MemAnalyzer\MemAnalyzer.csproj
set BIN=%SRC%..\bin\%CONFIG%
set OUT=%SRC%dist\MemAnalyzer

rem --- locate MSBuild via vswhere ---------------------------------------------
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if not exist "%VSWHERE%" (
    echo vswhere.exe not found at "%VSWHERE%". Install Visual Studio or set MSBUILD manually.
    exit /b 1
)
set MSBUILD=
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -prerelease -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD=%%i
if "%MSBUILD%"=="" (
    echo MSBuild not found via vswhere.
    exit /b 1
)

echo === Building x86 (%CONFIG%) ===
call "%MSBUILD%" "%PROJ%" -t:Restore,Build -p:Configuration=%CONFIG% -p:Platform=x86 -v:m -nologo
if errorlevel 1 exit /b 1

echo === Building x64 (%CONFIG%) ===
call "%MSBUILD%" "%PROJ%" -t:Restore,Build -p:Configuration=%CONFIG% -p:Platform=x64 -v:m -nologo
if errorlevel 1 exit /b 1

rem --- collect deployment folder ----------------------------------------------
echo === Deploying to %OUT% ===
if exist "%OUT%" rd /s /q "%OUT%"
mkdir "%OUT%"
rem Both bitnesses build into the same bin\Release folder; copy everything
rem except test/xunit/StringAllocator artifacts (defensive filter - the app
rem project alone never produces those).
xcopy "%BIN%\*.*" "%OUT%\" /y /i /q /exclude:%~dp0build.exclude >nul
if errorlevel 1 (
    echo Copy failed.
    exit /b 1
)

echo.
echo Deployment created: %OUT%
dir /b "%OUT%"
exit /b 0
