$godot = "K:\杀戮尖塔mod制作\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"
$projectDir = "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"
$pckSrc = Join-Path $projectDir "MP_PlayerManager.pck"
$pckDest = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager\MP_PlayerManager.pck"
$dllSrc = ".godot\mono\temp\bin\Debug\MP_PlayerManager.dll"
$dllDest = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager\MP_PlayerManager.dll"
$modDir = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager"

Write-Host "[1/4] Exporting PCK..."
if (Test-Path $pckSrc) { Remove-Item $pckSrc -Force }
$p = Start-Process $godot -ArgumentList "--path `"$projectDir`" --export-pack `"Windows Desktop`" `"$pckSrc`" --headless" -NoNewWindow -PassThru
$wait = 0
while (-not (Test-Path $pckSrc) -and $wait -lt 60 -and -not $p.HasExited) {
    Start-Sleep 2; $wait += 2
}
if ($p.HasExited) { Write-Host "[ERR] Godot exited: $($p.ExitCode)"; exit 1 }
if (-not (Test-Path $pckSrc)) { Write-Host "[ERR] PCK not created"; exit 1 }
Write-Host "[OK] PCK: $((Get-Item $pckSrc).Length) bytes"

Write-Host "[2/4] Copying DLL..."
if (Test-Path $dllDest) {
    try { Copy-Item $dllSrc $dllDest -Force -ErrorAction Stop; Write-Host "[OK] DLL copied" }
    catch { Write-Host "[WARN] DLL in use (game running): $_" }
}
else { Copy-Item $dllSrc $dllDest -Force; Write-Host "[OK] DLL copied" }

Write-Host "[3/4] Copying PCK..."
Copy-Item $pckSrc $pckDest -Force; Write-Host "[OK] PCK synced"

Write-Host "[4/4] Cleanup loose files..."
Remove-Item "$modDir\localization" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$modDir\config.json" -Force -ErrorAction SilentlyContinue
Set-Content -Path "$modDir\last_build.txt" -Value "v0.1.0 $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Encoding UTF8

Write-Host ""
Write-Host "=========================================="
Write-Host "  BUILD SUCCESS"
Write-Host "=========================================="
Write-Host "  DLL  : $dllDest"
Write-Host "  PCK  : $pckDest"
Write-Host "  Time : $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
