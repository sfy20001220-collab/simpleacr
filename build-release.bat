@echo off
chcp 65001 >nul
REM ============================================================
REM  SimpleACR 打包（Release）
REM
REM  Release 构建时 DalamudPackager 会在输出目录生成
REM  SimpleACR\ 文件夹，里面有 latest.zip + 补全后的清单 JSON，
REM  可直接挂到自己的插件仓库上分发。
REM
REM  注意：用 Dev Plugins 加载的话不需要跑这个，用 编译插件.bat 就行。
REM ============================================================
setlocal

set DOTNET_ROOT=D:\dotnet
set DALAMUD_HOME=%AppData%\XIVLauncherCN\addon\Hooks\dev
set DOTNET_CLI_TELEMETRY_OPTOUT=1

set DOTNET=%DOTNET_ROOT%\dotnet.exe

if not exist "%DOTNET%" (
    echo [错误] 找不到 .NET SDK：%DOTNET%
    pause
    exit /b 1
)

echo [信息] Release 打包 ...
"%DOTNET%" build "%~dp0SimpleACR.sln" -c Release %*

echo.
echo [信息] 产物：%~dp0SimpleACR\bin\x64\Release\SimpleACR\latest.zip
echo.
pause
