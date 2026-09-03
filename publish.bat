@echo off
chcp 65001 >nul
REM ============================================================
REM  SimpleACR 一键发布到 GitHub
REM
REM  做四件事：
REM    1. Release 编译 + 重新生成 dist\latest.zip 与 dist\pluginmaster.json
REM    2. git add
REM    3. git commit
REM    4. git push
REM
REM  用法：
REM    publish.bat                 提交信息用默认文本
REM    publish.bat "改了蝰蛇循环"   自定义提交信息
REM
REM  改完代码跑这个，玩家那边 /xlplugins 里点更新就能拿到新版。
REM ============================================================
setlocal

set OWNER=__OWNER__
set REPO=SimpleACR
set PY=C:\Users\asus\.workbuddy\binaries\python\versions\3.13.12\python.exe

if not exist "%PY%" set PY=python

set MSG=%~1
if "%MSG%"=="" set MSG=更新 SimpleACR

echo [1/4] 编译并生成 dist ...
"%PY%" "%~dp0make_repo.py" --owner %OWNER% --repo %REPO%
if errorlevel 1 (
    echo [失败] 编译或打包出错，已中止，未提交。
    pause
    exit /b 1
)

echo.
echo [2/4] git add ...
git -C "%~dp0" add -A
if errorlevel 1 goto fail

echo [3/4] git commit ...
git -C "%~dp0" commit -m "%MSG%"
if errorlevel 1 (
    echo [提示] 没有需要提交的改动，跳过。
)

echo [4/4] git push ...
git -C "%~dp0" push
if errorlevel 1 goto fail

echo.
echo [完成] 已推送。仓库地址：
echo     https://raw.githubusercontent.com/%OWNER%/%REPO%/main/dist/pluginmaster.json
echo.
pause
exit /b 0

:fail
echo [失败] 上一步出错，请看下方的提示。
pause
exit /b 1
