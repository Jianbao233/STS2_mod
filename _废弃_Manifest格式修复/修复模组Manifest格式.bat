@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "FIX_TOOL_DIR=%~dp0"
set "FIX_TOOL_BAT=%~f0"
if "%FIX_TOOL_DIR:~-1%"=="\" set "FIX_TOOL_DIR=%FIX_TOOL_DIR:~0,-1%"

:MENU
cls
echo.
echo  ============================================================
echo   STS2 Mod Manifest Fixer - Put this .bat in game mods folder
echo  ============================================================
echo   Dir: %CD%
echo  ============================================================
echo   [1] Fix - repair manifest format
echo   [2] Preview - scan only, no changes
echo   [3] Fix filename - mod_mainfest to mod_manifest
echo   [4] Fix data files - move VC_STS2_FULL_IDS, rename settings
echo   [5] Help
echo   [0] Exit
echo  ============================================================
echo.
set /p choice=  Select (0-5): 

if "%choice%"=="1" set "FIX_TOOL_ACTION=fix" & goto RUN
if "%choice%"=="2" set "FIX_TOOL_ACTION=preview" & goto RUN
if "%choice%"=="3" set "FIX_TOOL_ACTION=fixfilename" & goto RUN
if "%choice%"=="4" set "FIX_TOOL_ACTION=fixdatafiles" & goto RUN
if "%choice%"=="5" goto HELP
if "%choice%"=="0" goto EXIT

echo  Invalid. Retry.
timeout /t 2 >nul
goto MENU

:RUN
echo.
powershell -ExecutionPolicy Bypass -NoProfile -Command "& { $bat=[IO.File]::ReadAllText($env:FIX_TOOL_BAT,[Text.Encoding]::UTF8); $m1='REM ___PS1_START___'; $m2='REM ___PS1_END___'; $i1=$bat.IndexOf($m1)+$m1.Length; $i2=$bat.IndexOf($m2); $code=$bat.Substring($i1,$i2-$i1).Trim(); $tmp=$env:TEMP+'\fix_mod_'+[Guid]::NewGuid().ToString('N')+'.ps1'; [IO.File]::WriteAllText($tmp,$code,[Text.UTF8Encoding]::new($false)); try { & $tmp } finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue } }"
echo.
pause
goto MENU

:HELP
powershell -ExecutionPolicy Bypass -NoProfile -Command "Write-Host ''; Write-Host 'Game v0.99+ requires: id, has_pck, has_dll in manifest.'; Write-Host 'This tool: pck_name to id, add has_pck/has_dll/dependencies.'; Write-Host '[4] moves VC_STS2_FULL_IDS.json to game root, settings.json to settings.cfg.'; Write-Host 'Safe: correct files unchanged. Backup as .json.bak'; Write-Host 'Usage: Put this .bat in game mods folder. No other files needed.'; Write-Host ''"
pause
goto MENU

:EXIT
echo  Bye.
exit /b 0

REM ___PS1_START___
$ErrorActionPreference='Stop'
$ExcludeFiles=@('settings.json','completion.json','release_info.json','VC_STS2_FULL_IDS.json')
$root=if($env:FIX_TOOL_DIR){$env:FIX_TOOL_DIR}else{(Get-Location).Path}
$Action=$env:FIX_TOOL_ACTION
if(-not$Action){$Action='preview'}
function Test-IsModManifest{param($obj)
if(-not$obj){return $false}
return ($null -ne $obj.id -and $obj.id -ne '') -or ($null -ne $obj.pck_name -and $obj.pck_name -ne '') -or (($null -ne $obj.name -and $obj.name -ne '') -and ($null -ne $obj.author -or $null -ne $obj.version))
}
function Test-NeedsFix{param($path,$obj)
$dir=[System.IO.Path]::GetDirectoryName($path)
$modId=if($obj.id){$obj.id}elseif($obj.pck_name){$obj.pck_name}else{[System.IO.Path]::GetFileName($dir)}
$needsFix=$false;$reasons=@()
if([string]::IsNullOrEmpty($obj.id)){$needsFix=$true;$reasons+='Missing id'}
if($obj.pck_name -and [string]::IsNullOrEmpty($obj.id)){$reasons+='pck_name should be id'}
$pckPath=Join-Path $dir "$modId.pck";$dllPath=Join-Path $dir "$modId.dll"
$pckExists=Test-Path $pckPath -EA 0;$dllExists=Test-Path $dllPath -EA 0
if($pckExists -and -not $obj.has_pck){$needsFix=$true;$reasons+='Missing has_pck'}
if($dllExists -and -not $obj.has_dll){$needsFix=$true;$reasons+='Missing has_dll'}
if(-not $obj.ContainsKey('dependencies')){$needsFix=$true;$reasons+='Missing dependencies'}
if(-not $obj.ContainsKey('affects_gameplay')){$needsFix=$true;$reasons+='Missing affects_gameplay'}
return @{NeedsFix=$needsFix;Reasons=$reasons;ModId=$modId;PckExists=$pckExists;DllExists=$dllExists}
}
function Get-FixedManifest{param($obj,$dir,$modId)
$hasPck=(Test-Path (Join-Path $dir "$modId.pck") -EA 0) -or $obj.has_pck
$hasDll=(Test-Path (Join-Path $dir "$modId.dll") -EA 0) -or $obj.has_dll
$fixed=[ordered]@{id=$modId;name=$obj.name;version=$obj.version;author=$obj.author;description=$obj.description}
if(-not $fixed.name){$fixed.name=$modId};if(-not $fixed.version){$fixed.version='1.0.0'};if(-not $fixed.author){$fixed.author='unknown'}
$fixed.dependencies=if($obj.dependencies -is [Array]){@($obj.dependencies)}else{[string[]]@()}
$fixed.has_pck=$hasPck;$fixed.has_dll=$hasDll
$fixed.affects_gameplay=if($obj.ContainsKey('affects_gameplay')){[bool]$obj.affects_gameplay}else{$true}
return $fixed
}
function Get-JsonFiles{param($SearchRoot)
$files=@();Get-ChildItem -Path $SearchRoot -Filter '*.json' -Recurse -File -EA 0|ForEach-Object{if($ExcludeFiles -notcontains $_.Name){$files+=$_.FullName}};return $files
}
try{
Write-Host "Working dir: $root" -ForegroundColor Cyan;Write-Host ""
if($Action -eq 'fixdatafiles'){
$gameRoot=[System.IO.Path]::GetDirectoryName($root.TrimEnd('\'))
$moved=0;$renamed=0
$vcPath=Join-Path $root "ControlPanel\VC_STS2_FULL_IDS.json"
if(Test-Path $vcPath){$dest=Join-Path $gameRoot "VC_STS2_FULL_IDS.json";Copy-Item $vcPath -Destination $dest -Force;Remove-Item $vcPath -Force;Write-Host "  [Moved] VC_STS2_FULL_IDS.json -> game root" -ForegroundColor Green;$moved++}
Get-ChildItem -Path $root -Directory -EA 0|ForEach-Object{$modDir=$_.FullName;$sp=Join-Path $modDir "settings.json";$hm=(Test-Path (Join-Path $modDir "mod_manifest.json")) -or (Test-Path (Join-Path $modDir "mod_mainfest.json"));if((Test-Path $sp) -and $hm){$cp=Join-Path $modDir "settings.cfg";if(-not(Test-Path $cp)){Rename-Item $sp -NewName "settings.cfg" -Force;Write-Host "  [Renamed] $($_.Name)\settings.json -> settings.cfg" -ForegroundColor Green;$renamed++}}}
if($moved -eq 0 -and $renamed -eq 0){Write-Host "  No data files to process" -ForegroundColor Yellow};return
}
if($Action -eq 'fixfilename'){
$n=0;Get-ChildItem -Path $root -Filter 'mod_mainfest.json' -Recurse -File -EA 0|ForEach-Object{$np=$_.FullName -replace 'mod_mainfest\.json$','mod_manifest.json';if($np -ne $_.FullName -and -not(Test-Path $np)){Rename-Item $_.FullName -NewName 'mod_manifest.json' -Force;Write-Host "  [Fixed] -> mod_manifest.json" -ForegroundColor Green;$n++}}
if($n -eq 0){Write-Host "  No mod_mainfest.json found" -ForegroundColor Yellow};return
}
$jsonFiles=Get-JsonFiles -SearchRoot $root;Write-Host "Scanned $($jsonFiles.Count) JSON file(s)" -ForegroundColor Gray
$toFix=@()
foreach($path in $jsonFiles){try{$raw=Get-Content -Path $path -Raw -Encoding UTF8 -EA Stop;if([string]::IsNullOrWhiteSpace($raw)){continue};$obj=$raw|ConvertFrom-Json -EA Stop;$objHash=@{};$obj.PSObject.Properties|ForEach-Object{$objHash[$_.Name]=$_.Value}}catch{continue}
if(-not(Test-IsModManifest -obj $objHash)){continue};$dir=[System.IO.Path]::GetDirectoryName($path);$check=Test-NeedsFix -path $path -obj $objHash
if($check.NeedsFix){$toFix+=@{Path=$path;RelPath=($path.Replace(($root -replace '[\\/]+$',''),'.') -replace '^[\\/]+','');Obj=$objHash;Check=$check}}
}
Write-Host "To fix: $($toFix.Count) file(s)" -ForegroundColor Cyan
if($toFix.Count -eq 0){Write-Host "All manifest files are correct." -ForegroundColor Green;return}
Write-Host "Found $($toFix.Count) file(s) to fix:" -ForegroundColor Yellow;Write-Host ""
foreach($item in $toFix){Write-Host "  $($item.RelPath)" -ForegroundColor White
$item.Check.Reasons|ForEach-Object{Write-Host "    - $_" -ForegroundColor DarkGray}
if($Action -eq 'fix'){try{$modId=$item.Check.ModId;$dir=[System.IO.Path]::GetDirectoryName($item.Path);$fixed=Get-FixedManifest -obj $item.Obj -dir $dir -modId $modId;Copy-Item -Path $item.Path -Destination ($item.Path+'.bak') -Force;$jsonOut=$fixed|ConvertTo-Json -Depth 10;$jsonOut=$jsonOut -replace '"dependencies"\s*:\s*\{\s*\}','"dependencies": []';[System.IO.File]::WriteAllText($item.Path,$jsonOut,[System.Text.UTF8Encoding]::new($false));Write-Host "    [Fixed, backup .json.bak]" -ForegroundColor Green}catch{Write-Host "    [Error] $_" -ForegroundColor Red}}
Write-Host ""}
if($Action -eq 'preview'){Write-Host "Preview only. Use [1] to apply fixes." -ForegroundColor Cyan}
}catch{Write-Host "Error: $_" -ForegroundColor Red;exit 1}
REM ___PS1_END___
