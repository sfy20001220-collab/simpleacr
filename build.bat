@echo off
chcp 65001 >nul
REM ============================================================
REM  SimpleACR 一键编译（Debug）
REM
REM  两个环境变量是本机必须设的：
REM    DOTNET_ROOT    .NET SDK 装在 D:\dotnet（因为不是管理员，装不进 C:\Program Files\dotnet）
REM    DALAMUD_HOME   国服卫月的 Dalamud 引用目录。
REM                   Dalamud.NET.Sdk 里 DalamudLibPath 默认硬写国际服的
REM                   %AppData%\XIVLauncher\addon\Hooks\dev\，且会无条件覆盖
REM                   csproj 里写的值；唯一能覆盖它的分支就是 DALAMUD_HOME。
REM ============================================================
setlocal

set DOTNET_ROOT=D:\dotnet
set DALAMUD_HOME=%AppData%\XIVLauncherCN\addon\Hooks\dev
set DOTNET_CLI_TELEMETRY_OPTOUT=1

set DOTNET=%DOTNET_ROOT%\dotnet.exe

if not exist "%DOTNET%" (
    echo [错误] 找不到 .NET SDK：%DOTNET%
    echo        请先运行 安装.NET8和10SDK.bat 或手动安装。
    pause
    exit /b 1
)

echo [信息] 编译 SimpleACR ...
"%DOTNET%" build "%~dp0SimpleACR.sln" -c Debug %*

echo.
echo [信息] 输出目录：%~dp0SimpleACR\bin\x64\Debug\
echo        游戏内：/xlplugins → Dev Plugins → SimpleACR → Load
echo        （改了代码：先 Unload，再跑本脚本，再 Load）
echo.
pause
