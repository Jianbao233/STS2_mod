Write-Host "=== Step 1: Godot running?"
Get-Process godot* -ErrorAction SilentlyContinue | Select-Object Name, Id
Write-Host ""
Write-Host "=== Step 2: Running build_and_sync.ps1..."
& "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\build_and_sync.ps1"
Write-Host ""
Write-Host "=== Step 3: Checking mods folder..."
Get-ChildItem "K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager" | Select-Object Name, Length
