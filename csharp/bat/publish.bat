@echo off
pushd %~dp0
rem init
for %%i in ("%CD%") do set projectname=%%~nxi
rem for %%i in ("%CD%\..") do set solutionname=%%~nxi
for %%i in ("%CD%\..\.vs\publish") do set publishfolder=%%~fi
set foldername=%publishfolder%\%projectname%
for /f "delims=<> tokens=3" %%i in ('findstr -i "<AssemblyVersion>" "%CD%\..\Directory.Build.props"') do set filename=%foldername%_%%i.zip
rem publish
echo ---------------- 正在发布 [%projectname%] ----------------
if exist "%foldername%" rd "%foldername%" /s /q
setlocal enabledelayedexpansion
if not [%1]==[x86] (
    dotnet publish -c Release --no-self-contained -p:publishsinglefile=false -r win-x64 -o "%foldername%\x64"
    set lastcode=!ERRORLEVEL!
    rem check error
    if not [!lastcode!]==[0] (
        pause >nul
        goto :end
    )
)
if [%1]==[x86] (
    dotnet publish -c Release --no-self-contained -p:publishsinglefile=false -r win-x86 -o "%foldername%\x86"
    set lastcode=!ERRORLEVEL!
    rem check error
    if not [!lastcode!]==[0] (
        pause >nul
        goto :end
    )
)
endlocal
rem compress
if exist "%filename%" del "%filename%" /f
powershell -Command "compress-archive '%foldername%' '%filename%'"
rem open explorer
if [%1]==[] (
    echo ---------------- 定位文件 %filename% ----------------
    explorer /n,/select,"%filename%"
)
:end
popd
exit /b %lastcode%