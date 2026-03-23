# MP_PlayerManager Release 打包脚本
# 用法: .\prepare-release.ps1 -Version "1.0.0"
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

Write-Host ""
Write-Host "========================================"
Write-Host "  MP_PlayerManager Release 打包"
Write-Host "========================================"
Write-Host ""

# 1. 如果 dist 不存在，先构建
$ExePath = "dist\MP_PlayerManager-v$Version.exe"
if (-not (Test-Path $ExePath)) {
    Write-Host "[1/3] 未找到 exe，开始构建..."
    if (Test-Path "build_exe.bat") {
        cmd /c "build_exe.bat"
    } else {
        # 备用：直接用 pyinstaller
        if (Get-Command pyinstaller -ErrorAction SilentlyContinue) {
            pyinstaller --onefile --name "MP_PlayerManager-v$Version" --console manage_players.py
        } else {
            Write-Host "[错误] 未找到 build_exe.bat 且 pyinstaller 不可用"
            exit 1
        }
    }
} else {
    Write-Host "[1/3] exe 已存在，跳过构建"
}

# 2. 检查 exe 是否存在
if (-not (Test-Path $ExePath)) {
    Write-Host "[错误] 构建失败，exe 不存在"
    exit 1
}

# 3. 创建 release 目录
$ReleaseDir = "release"
if (-not (Test-Path $ReleaseDir)) {
    New-Item -ItemType Directory -Path $ReleaseDir | Out-Null
}

# 4. 创建临时打包目录
$TmpDir = "$ReleaseDir\tmp_MP_PlayerManager"
if (Test-Path $TmpDir) {
    Remove-Item -Recurse -Force $TmpDir
}
New-Item -ItemType Directory -Path $TmpDir | Out-Null

# 5. 复制文件到临时目录
Copy-Item $ExePath "$TmpDir\MP_PlayerManager-v$Version.exe"
Copy-Item "README.md" "$TmpDir\README.md"

# 6. 打包 zip
$ZipPath = "$ReleaseDir\MP_PlayerManager-v$Version.zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

# PowerShell 5.0+ 原生 zip
Compress-Archive -Path "$TmpDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal

# 7. 清理临时目录
Remove-Item -Recurse -Force $TmpDir

# 8. 完成
$ZipSize = (Get-Item $ZipPath).Length / 1MB
Write-Host ""
Write-Host "========================================"
Write-Host "  打包完成！"
Write-Host "========================================"
Write-Host "  版本: v$Version"
Write-Host "  大小: $([math]::Round($ZipSize, 2)) MB"
Write-Host "  路径: $((Resolve-Path $ZipPath).Path)"
Write-Host ""
Write-Host "GitHub Release 上传指令："
Write-Host "  gh release create v$Version `"$((Resolve-Path $ZipPath).Path)`" --title `"MP Player Manager v$Version`" --notes `"多人存档玩家管理：夺舍/添加/移除玩家`""
Write-Host ""
