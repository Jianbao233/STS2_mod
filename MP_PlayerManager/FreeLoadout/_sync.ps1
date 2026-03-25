$src = "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"
$dst = "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager"
Copy-Item "$src\.godot\mono\temp\bin\Debug\MP_PlayerManager.dll" "$dst\MP_PlayerManager.dll" -Force
Copy-Item "$src\MP_PlayerManager.pck" "$dst\MP_PlayerManager.pck" -Force
Remove-Item "$dst\localization" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "$dst\config.json" -Force -ErrorAction SilentlyContinue
Set-Content -Path "$dst\last_build.txt" -Value "v0.1.0 $(Get-Date -Format 'yyyy-MM-dd HH:mm')" -Encoding UTF8
Write-Host "Sync done"
dir $dst
