# Build script: generates single-file bat with embedded base64
$embed = @'
$ErrorActionPreference='Stop'
$ExcludeFiles=@('settings.json','completion.json','release_info.json','VC_STS2_FULL_IDS.json')
$root=if($env:FIX_TOOL_DIR){$env:FIX_TOOL_DIR}else{(Get-Location).Path}
$Action=$env:FIX_TOOL_ACTION;if(-not$Action){$Action='preview'}
function Test-IsModManifest{param($obj)
if(-not$obj){return $false}
return ($null -ne $obj.id -and $obj.id -ne '') -or ($null -ne $obj.pck_name -and $obj.pck_name -ne '') -or (($null -ne $obj.name -and $obj.name -ne '') -and ($null -ne $obj.author -or $null -ne $obj.version))
}
function Test-NeedsFix{param($path,$obj)
$dir=[System.IO.Path]::GetDirectoryName($path)
$modId=if($obj.id){$obj.id}elseif($obj.pck_name){$obj.pck_name}else{[System.IO.Path]::GetFileName($dir)}
$needsFix=$false;$reasons=@()
if([string]::IsNullOrEmpty($obj.id)){$needsFix=$true;$reasons+='Missing id field'}
if($obj.pck_name -and [string]::IsNullOrEmpty($obj.id)){$reasons+='Has pck_name, should use id'}
$pckPath=Join-Path $dir "$modId.pck";$dllPath=Join-Path $dir "$modId.dll"
$pckExists=Test-Path $pckPath -EA 0;$dllExists=Test-Path $dllPath -EA 0
if($pckExists -and -not $obj.has_pck){$needsFix=$true;$reasons+='.pck exists but has_pck not set'}
if($dllExists -and -not $obj.has_dll){$needsFix=$true;$reasons+='.dll exists but has_dll not set'}
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
$moved=0;$renamed=0;$vcPath=Join-Path $root "ControlPanel\VC_STS2_FULL_IDS.json"
if(Test-Path $vcPath){$dest=Join-Path $gameRoot "VC_STS2_FULL_IDS.json";Copy-Item $vcPath -Destination $dest -Force;Remove-Item $vcPath -Force;Write-Host "  [Moved] VC_STS2_FULL_IDS.json -> game root" -ForegroundColor Green;$moved++}
Get-ChildItem -Path $root -Directory -EA 0|ForEach-Object{$modDir=$_.FullName;$sp=Join-Path $modDir "settings.json";$hm=(Test-Path (Join-Path $modDir "mod_manifest.json")) -or (Test-Path (Join-Path $modDir "mod_mainfest.json"));if((Test-Path $sp) -and $hm){$cp=Join-Path $modDir "settings.cfg";if(-not(Test-Path $cp)){Rename-Item $sp -NewName "settings.cfg" -Force;Write-Host "  [Renamed] $($_.Name)\settings.json -> settings.cfg" -ForegroundColor Green;$renamed++}}}
if($moved -eq 0 -and $renamed -eq 0){Write-Host "  No data files to process" -ForegroundColor Yellow};return
}
if($Action -eq 'fixfilename'){
$n=0;Get-ChildItem -Path $root -Filter 'mod_mainfest.json' -Recurse -File -EA 0|ForEach-Object{
$np=$_.FullName -replace 'mod_mainfest\.json$','mod_manifest.json'
if($np -ne $_.FullName -and -not(Test-Path $np)){Rename-Item $_.FullName -NewName 'mod_manifest.json' -Force;Write-Host "  [Fixed] $($_.FullName) -> mod_manifest.json" -ForegroundColor Green;$n++}
}
if($n -eq 0){Write-Host "  No mod_mainfest.json found to rename" -ForegroundColor Yellow};return
}
$jsonFiles=Get-JsonFiles -SearchRoot $root;Write-Host "Scanned $($jsonFiles.Count) JSON file(s)" -ForegroundColor Gray
$toFix=@()
foreach($path in $jsonFiles){try{
$raw=Get-Content -Path $path -Raw -Encoding UTF8 -EA Stop
if([string]::IsNullOrWhiteSpace($raw)){continue}
$obj=$raw|ConvertFrom-Json -EA Stop;$objHash=@{};$obj.PSObject.Properties|ForEach-Object{$objHash[$_.Name]=$_.Value}
}catch{continue}
if(-not(Test-IsModManifest -obj $objHash)){continue}
$dir=[System.IO.Path]::GetDirectoryName($path);$check=Test-NeedsFix -path $path -obj $objHash
if($check.NeedsFix){$toFix+=@{Path=$path;RelPath=($path.Replace(($root -replace '[\\/]+$',''),'.') -replace '^[\\/]+','');Obj=$objHash;Check=$check}}
}
Write-Host "To fix: $($toFix.Count) file(s)" -ForegroundColor Cyan
if($toFix.Count -eq 0){Write-Host "All manifest files are correct, no fix needed." -ForegroundColor Green;return}
Write-Host "Found $($toFix.Count) file(s) to fix:" -ForegroundColor Yellow;Write-Host ""
foreach($item in $toFix){
Write-Host "  $($item.RelPath)" -ForegroundColor White
$item.Check.Reasons|ForEach-Object{Write-Host "    - $_" -ForegroundColor DarkGray}
if($Action -eq 'fix'){try{
$modId=$item.Check.ModId;$dir=[System.IO.Path]::GetDirectoryName($item.Path)
$fixed=Get-FixedManifest -obj $item.Obj -dir $dir -modId $modId
Copy-Item -Path $item.Path -Destination ($item.Path+'.bak') -Force
$jsonOut=$fixed|ConvertTo-Json -Depth 10;$jsonOut=$jsonOut -replace '"dependencies"\s*:\s*\{\s*\}','"dependencies": []'
[System.IO.File]::WriteAllText($item.Path,$jsonOut,[System.Text.UTF8Encoding]::new($false))
Write-Host "    [Fixed, backup saved as .json.bak]" -ForegroundColor Green
}catch{Write-Host "    [Error] $_" -ForegroundColor Red}}
Write-Host ""}
if($Action -eq 'preview'){Write-Host "Preview only. Use option 1 to apply fixes." -ForegroundColor Cyan}
}catch{Write-Host "Error: $_" -ForegroundColor Red;exit 1}
'@
$b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($embed))
$batHeader = @'
@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "FIX_TOOL_DIR=%~dp0"
if "%FIX_TOOL_DIR:~-1%"=="\" set "FIX_TOOL_DIR=%FIX_TOOL_DIR:~0,-1%"

:MENU
cls
echo.
echo  ========== STS2 Mod Manifest Fixer ==========
echo.
echo  Dir: %CD%
echo.
echo  [1] Fix - Repair incorrect manifest format
echo  [2] Preview - Scan only, no changes
echo  [3] Fix filename - Rename mod_mainfest to mod_manifest
echo  [4] Fix data files - Move VC_STS2_FULL_IDS, rename settings.json
echo  [5] Help
echo  [0] Exit
echo.
set /p choice= Select (0-5): 

if "%choice%"=="1" set "FIX_TOOL_ACTION=fix" & goto RUN
if "%choice%"=="2" set "FIX_TOOL_ACTION=preview" & goto RUN
if "%choice%"=="3" set "FIX_TOOL_ACTION=fixfilename" & goto RUN
if "%choice%"=="4" set "FIX_TOOL_ACTION=fixdatafiles" & goto RUN
if "%choice%"=="5" goto HELP
if "%choice%"=="0" goto EXIT

echo Invalid option.
timeout /t 2 >nul
goto MENU

:RUN
echo.
if "%FIX_TOOL_ACTION%"=="fix" echo Fixing...
if "%FIX_TOOL_ACTION%"=="preview" echo Preview...
if "%FIX_TOOL_ACTION%"=="fixfilename" echo Fix filename...
if "%FIX_TOOL_ACTION%"=="fixdatafiles" echo Fix data files...
echo.
powershell -ExecutionPolicy Bypass -NoProfile -Command "$c=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('%B64%')); $t=$env:TEMP+'\fix_'+[Guid]::NewGuid().ToString('N')+'.ps1'; [IO.File]::WriteAllText($t,$c); try { & $t } finally { Remove-Item $t -Force -EA 0 }"
echo.
pause
goto MENU

:HELP
cls
echo.
echo --- Help ---
echo Game v0.99+ requires id, has_pck, has_dll in manifest.
echo This tool fixes: pck_name-^>id, adds has_pck/has_dll/dependencies.
echo [4] moves VC_STS2_FULL_IDS.json to game root, renames settings.json-^>settings.cfg.
echo Safe: correct files unchanged; backups as .json.bak
echo Usage: Put this .bat in game mods folder and run. No other files needed.
echo.
pause
goto MENU

:EXIT
echo Bye.
exit /b 0
'@
$batHeader = $batHeader -replace '%B64%',$b64
$batHeaderZh = @'
@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "FIX_TOOL_DIR=%~dp0"
if "%FIX_TOOL_DIR:~-1%"=="\" set "FIX_TOOL_DIR=%FIX_TOOL_DIR:~0,-1%"

:MENU
cls
echo.
echo  ╔══════════════════════════════════════════════════════════════╗
echo  ║    杀戮尖塔2 - 模组 Manifest 格式修复工具                    ║
echo  ║    遇到「检测到错误」或 manifest 格式问题时使用              ║
echo  ╚══════════════════════════════════════════════════════════════╝
echo.
echo  当前目录: %CD%
echo.
echo  ┌─────────────────────────────────────────────────────────────┐
echo  │  [1] 扫描并修复 - 自动修复格式不正确的 manifest 文件        │
echo  │  [2] 仅扫描预览 - 查看将被修复的文件，不实际修改            │
echo  │  [3] 修复错误文件名 - 将 mod_mainfest.json 重命名为正确名   │
echo  │  [4] 修复数据文件 - 移动 VC_STS2_FULL_IDS、重命名 settings  │
echo  │  [5] 查看帮助说明                                           │
echo  │  [0] 退出                                                   │
echo  └─────────────────────────────────────────────────────────────┘
echo.
set /p choice=  请输入选项 (0-5): 

if "%choice%"=="1" set "FIX_TOOL_ACTION=fix" & goto RUN
if "%choice%"=="2" set "FIX_TOOL_ACTION=preview" & goto RUN
if "%choice%"=="3" set "FIX_TOOL_ACTION=fixfilename" & goto RUN
if "%choice%"=="4" set "FIX_TOOL_ACTION=fixdatafiles" & goto RUN
if "%choice%"=="5" goto HELP
if "%choice%"=="0" goto EXIT

echo  无效选项。
timeout /t 2 >nul
goto MENU

:RUN
echo.
if "%FIX_TOOL_ACTION%"=="fix" echo  正在扫描并修复...
if "%FIX_TOOL_ACTION%"=="preview" echo  正在扫描（仅预览）...
if "%FIX_TOOL_ACTION%"=="fixfilename" echo  正在修复文件名...
if "%FIX_TOOL_ACTION%"=="fixdatafiles" echo  正在修复数据文件...
echo.
powershell -ExecutionPolicy Bypass -NoProfile -Command "$c=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('%B64%')); $t=$env:TEMP+'\fix_'+[Guid]::NewGuid().ToString('N')+'.ps1'; [IO.File]::WriteAllText($t,$c); try { & $t } finally { Remove-Item $t -Force -EA 0 }"
echo.
pause
goto MENU

:HELP
cls
echo.
echo  【问题】游戏 v0.99+ 要求 manifest 必须含 id、has_pck、has_dll
echo  【修复】本工具将 pck_name 转 id，补充 has_pck/has_dll/dependencies
echo  【[4]】移动 VC_STS2_FULL_IDS.json 到游戏根目录，settings.json 改名为 settings.cfg
echo  【安全】格式正确的文件不会被修改；修复前会备份为 .json.bak
echo  【用法】将此 bat 放入「游戏安装目录\mods」文件夹后运行，无需其他文件
echo.
pause
goto MENU

:EXIT
echo  再见！
exit /b 0
'@
$batHeaderZh = $batHeaderZh -replace '%B64%',$b64

$dir = Split-Path $MyInvocation.MyCommand.Path
$outPath = Join-Path $dir "修复模组Manifest格式.bat"
$outPathEn = Join-Path $dir "STS2_ModManifestFixer.bat"
[IO.File]::WriteAllText($outPath, $batHeader, [System.Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($outPathEn, $batHeader, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated: $outPath"
Write-Host "Generated: $outPathEn (English name)"

# 中文版输出到 STS2_mod 供分发给玩家
$sts2Mod = "K:\杀戮尖塔mod制作\STS2_mod"
if (Test-Path $sts2Mod) {
    $distPath = Join-Path $sts2Mod "修复模组Manifest格式.bat"
    [IO.File]::WriteAllText($distPath, $batHeaderZh, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Generated (中文版，供玩家使用): $distPath"
}