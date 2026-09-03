@echo off
chcp 65001 >nul
REM ============================================================
REM  把 SimpleACR 注册进卫月的 Dev Plugins 加载列表
REM
REM  什么时候需要跑：
REM    * 第一次安装（已帮你跑过一次了）
REM    * 发现 /xlplugins 的 Dev Plugins 里没有 SimpleACR
REM      —— 因为 Dalamud 在**游戏退出时**会回写配置，
REM         游戏开着的时候注册可能被覆盖。重跑本脚本即可。
REM
REM  取消注册：register.bat --remove
REM ============================================================
setlocal

set SCRIPT=%~dp0register.py

REM 依次尝试 py / python / WorkBuddy 自带 python
where py >nul 2>nul
if %errorlevel%==0 (
    py -3 "%SCRIPT%" %*
    goto :end
)

where python >nul 2>nul
if %errorlevel%==0 (
    python "%SCRIPT%" %*
    goto :end
)

set PY=C:\Users\asus\.workbuddy\binaries\python\versions\3.13.12\python.exe
if exist "%PY%" (
    "%PY%" "%SCRIPT%" %*
    goto :end
)

echo [错误] 找不到 Python。请安装 Python 3，或在命令里手动指定路径。

:end
echo.
pause
