@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
cd /d "%~dp0"
REM 请将本 bat 与 fix_mod_manifests.ps1 一起放入游戏 mods 目录后运行

:MENU
cls
echo.
echo  ╔══════════════════════════════════════════════════════════════╗
echo  ║    杀戮尖塔2 - 模组 Manifest 格式修复工具                    ║
echo  ║    STS2 Mod Manifest Format Fixer                           ║
echo  ╚══════════════════════════════════════════════════════════════╝
echo.
echo  当前目录: %CD%
echo.
echo  ┌─────────────────────────────────────────────────────────────┐
echo  │  请选择操作 / Select an option:                             │
echo  │                                                             │
echo  │  [1] 扫描并修复 - 自动修复格式不正确的 manifest 文件        │
echo  │  [2] 仅扫描预览 - 查看将被修复的文件，不实际修改            │
echo  │  [3] 修复错误文件名 - 将 mod_mainfest.json 重命名为正确名   │
echo  │  [4] 查看帮助说明                                           │
echo  │  [0] 退出                                                   │
echo  └─────────────────────────────────────────────────────────────┘
echo.
set /p choice=  请输入选项 (0-4): 

if "%choice%"=="1" goto FIX
if "%choice%"=="2" goto PREVIEW
if "%choice%"=="3" goto FIX_FILENAME
if "%choice%"=="4" goto HELP
if "%choice%"=="0" goto EXIT

echo.
echo  无效选项，请重新选择。
timeout /t 2 >nul
goto MENU

:FIX
echo.
echo  正在扫描并修复...
echo.
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0fix_mod_manifests.ps1" -Action fix
echo.
pause
goto MENU

:PREVIEW
echo.
echo  正在扫描（仅预览，不修改）...
echo.
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0fix_mod_manifests.ps1" -Action preview
echo.
pause
goto MENU

:FIX_FILENAME
echo.
echo  正在检查错误文件名...
echo.
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0fix_mod_manifests.ps1" -Action fixfilename
echo.
pause
goto MENU

:HELP
cls
echo.
echo  ═══════ 帮助说明 ═══════
echo.
echo  【问题背景】
echo  游戏 v0.99+ 要求 mod manifest 必须包含 id 字段，且使用 has_pck、has_dll
echo  声明。使用旧格式（如 pck_name）的模组不会被游戏识别。
echo.
echo  【修复内容】
echo  1. 将 pck_name 转换为 id（并移除 pck_name）
echo  2. 若无 id，用目录名或 pck_name 生成
echo  3. 根据同目录下是否存在 .pck/.dll 自动补充 has_pck、has_dll
echo  4. 补充缺失的 dependencies、affects_gameplay
echo.
echo  【正确格式示例】（参考 DirectConnectIP.json）
echo  ^{
echo    "id": "ModName",
echo    "name": "模组名",
echo    "version": "1.0.0",
echo    "author": "作者",
echo    "description": "描述",
echo    "dependencies": [],
echo    "has_pck": true,
echo    "has_dll": true,
echo    "affects_gameplay": true
echo  ^}
echo.
echo  【安全说明】
echo  - 格式正确的文件（如 DirectConnectIP.json）不会被修改
echo  - 建议先用 [2] 仅扫描预览 确认后再执行 [1] 修复
echo  - 修复前会备份为 .json.bak
echo.
echo  【使用建议】
echo  将 修复模组Manifest格式.bat 与 fix_mod_manifests.ps1 置于 mods 目录下运行。
echo.
pause
goto MENU

:EXIT
echo.
echo  再见！
exit /b 0
