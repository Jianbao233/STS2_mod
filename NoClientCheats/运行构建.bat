@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ===== NoClientCheats 构建 =====
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build.ps1"
echo.
echo 退出码: %ERRORLEVEL%
if %ERRORLEVEL% neq 0 (
    echo 构建失败！
    pause
    exit /b 1
)
echo.
echo 请完全关闭游戏后重新启动。仅房主需安装此 Mod。
echo 游戏内 模组配置 可开关「禁止客机作弊」。
pause
