@echo off
chcp 65001 >nul
echo ==========================================
echo   MP_SavePlayerRemover - 打包为 exe
echo ==========================================
echo.

cd /d "%~dp0"

where python >nul 2>&1
if errorlevel 1 (
    echo 错误: 未找到 Python，请先安装 Python 3.8+
    pause
    exit /b 1
)

echo [1/2] 安装 PyInstaller...
pip install pyinstaller -q

echo.
echo [2/2] 打包中...
pyinstaller --onefile --name "MP_SavePlayerRemover-v1.1.0" --clean remove_players.py

if errorlevel 1 (
    echo.
    echo 打包失败
    pause
    exit /b 1
)

echo.
echo 完成！exe 位于: dist\MP_SavePlayerRemover-v1.1.0.exe
pause
