@echo off
chcp 65001 >nul
echo ========================================
echo   MP_PlayerManager 构建脚本
echo ========================================
echo.

REM 检测 Python
python --version >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 Python，请先安装 Python 3.8+
    echo   https://www.python.org/downloads/
    pause
    exit /b 1
)

REM 安装 PyInstaller
echo [1/3] 检查 PyInstaller...
pip show pyinstaller >nul 2>&1
if errorlevel 1 (
    echo [1/3] 安装 PyInstaller...
    pip install pyinstaller
)

REM 清理旧构建
echo [2/3] 清理旧构建...
if exist dist rmdir /s /q dist
if exist build rmdir /s /q build

REM 打包
echo [3/3] 打包为 exe...
pyinstaller --onefile --name "MP_PlayerManager-v1.0.0" --console --clean manage_players.py

if exist "dist\MP_PlayerManager-v1.0.0.exe" (
    echo.
    echo ========================================
    echo   构建成功！
    echo   输出: dist\MP_PlayerManager-v1.0.0.exe
    echo ========================================
) else (
    echo.
    echo [错误] 构建失败，请检查上方输出
)

pause
