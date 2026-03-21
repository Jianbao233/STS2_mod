@echo off
REM RunHistoryAnalyzer 构建脚本
REM 使用方法：在 Godot 编辑器中构建，或在 Godot 安装目录运行：
REM godot --headless --build-solutions --path .

echo Building RunHistoryAnalyzer...

REM 构建后复制到游戏 mods 目录
set "GAME_MODS=K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods"
set "MOD_TARGET=%GAME_MODS%\RunHistoryAnalyzer"

if not exist "%MOD_TARGET%" mkdir "%MOD_TARGET%"

echo Build complete.
echo Output should be in: .godot/mono/temp/bin/Debug/
echo.
pause
