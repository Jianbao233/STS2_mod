@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ===== ControlPanel 构建 =====
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build.ps1"
echo.
echo 退出码: %ERRORLEVEL%
if %ERRORLEVEL% neq 0 (
    echo 构建失败！
    pause
    exit /b 1
)
echo.
echo 请完全关闭游戏后重新启动，再按 F7 打开面板。
echo 若标题显示 "控制面板 v2" 则说明新版本已加载。
pause
