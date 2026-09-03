@echo off
chcp 65001 >nul
REM ============================================================
REM  把 SimpleACR 的线上仓库地址写进卫月（Dalamud）配置
REM
REM  ⚠ 请先完全退出 FF14 再运行。
REM    Dalamud 在游戏退出时会回写配置文件，
REM    游戏开着跑这个，改动会被覆盖掉。
REM ============================================================
setlocal

set PY=C:\Users\asus\.workbuddy\binaries\python\versions\3.13.12\python.exe
if not exist "%PY%" set PY=python

"%PY%" "%~dp0register_repo.py" %*
if errorlevel 1 goto fail

echo.
pause
exit /b 0

:fail
echo [失败] 请看上方的提示。
pause
exit /b 1
