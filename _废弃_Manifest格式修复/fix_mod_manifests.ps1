#Requires -Version 5.1
# STS2 模组 Manifest 格式修复脚本
# 修复格式不正确的 mod_manifest.json，使其符合游戏 ModManager 要求

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('fix', 'preview', 'fixfilename', 'fixdatafiles')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'

# 排除的非 manifest 文件名
$ExcludeFiles = @('settings.json', 'completion.json', 'release_info.json', 'VC_STS2_FULL_IDS.json')

function Test-IsModManifest {
    param([hashtable]$obj)
    if (-not $obj) { return $false }
    $hasId = $null -ne $obj.id -and $obj.id -ne ''
    $hasPckName = $null -ne $obj.pck_name -and $obj.pck_name -ne ''
    $hasNameAuthor = ($null -ne $obj.name -and $obj.name -ne '') -and ($null -ne $obj.author -or $null -ne $obj.version)
    return $hasId -or $hasPckName -or $hasNameAuthor
}

function Test-NeedsFix {
    param([string]$path, [hashtable]$obj)
    $dir = [System.IO.Path]::GetDirectoryName($path)
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($path)
    $modId = $obj.id
    if ([string]::IsNullOrEmpty($modId)) {
        $modId = $obj.pck_name
    }
    if ([string]::IsNullOrEmpty($modId)) {
        $modId = [System.IO.Path]::GetFileName($dir)
    }

    $needsFix = $false
    $reasons = @()

    # 1. 缺少 id 或使用 pck_name
    if ([string]::IsNullOrEmpty($obj.id)) {
        $needsFix = $true
        $reasons += '缺少 id 字段'
    }
    if ($null -ne $obj.pck_name -and $obj.pck_name -ne '') {
        if ([string]::IsNullOrEmpty($obj.id)) {
            $reasons += "存在 pck_name，应改为 id"
        }
    }

    # 2. has_pck / has_dll 与实际文件不符
    $pckPath = Join-Path $dir "$modId.pck"
    $dllPath = Join-Path $dir "$modId.dll"
    $pckExists = Test-Path $pckPath -ErrorAction SilentlyContinue
    $dllExists = Test-Path $dllPath -ErrorAction SilentlyContinue

    if ($pckExists -and (-not $obj.has_pck)) {
        $needsFix = $true
        $reasons += '存在 .pck 文件但未声明 has_pck'
    }
    if ($dllExists -and (-not $obj.has_dll)) {
        $needsFix = $true
        $reasons += '存在 .dll 文件但未声明 has_dll'
    }

    # 3. 缺少 dependencies
    if (-not $obj.ContainsKey('dependencies')) {
        $needsFix = $true
        $reasons += '缺少 dependencies 字段'
    }

    # 4. 缺少 affects_gameplay
    if (-not $obj.ContainsKey('affects_gameplay')) {
        $needsFix = $true
        $reasons += '缺少 affects_gameplay 字段'
    }

    return @{ NeedsFix = $needsFix; Reasons = $reasons; ModId = $modId; PckExists = $pckExists; DllExists = $dllExists }
}

function Get-FixedManifest {
    param([hashtable]$obj, [string]$dir, [string]$modId)

    $pckPath = Join-Path $dir "$modId.pck"
    $dllPath = Join-Path $dir "$modId.dll"
    $hasPck = (Test-Path $pckPath -ErrorAction SilentlyContinue) -or $obj.has_pck
    $hasDll = (Test-Path $dllPath -ErrorAction SilentlyContinue) -or $obj.has_dll

    $fixed = [ordered]@{}

    # id - 必须首位
    $fixed['id'] = $modId

    # 保留原有字段（按游戏期望顺序）
    @('name', 'version', 'author', 'description') | ForEach-Object {
        if ($null -ne $obj[$_]) {
            $fixed[$_] = $obj[$_]
        } else {
            $fixed[$_] = if ($_ -eq 'version') { '1.0.0' } elseif ($_ -eq 'name') { $modId } else { 'unknown' }
        }
    }

    $fixed['dependencies'] = if ($null -ne $obj.dependencies -and $obj.dependencies -is [Array]) { @($obj.dependencies) } else { [string[]]@() }
    $fixed['has_pck'] = $hasPck
    $fixed['has_dll'] = $hasDll
    $fixed['affects_gameplay'] = if ($obj.ContainsKey('affects_gameplay')) { [bool]$obj.affects_gameplay } else { $true }

    return $fixed
}

function Get-JsonFiles {
    param([string]$SearchRoot)
    $files = @()
    Get-ChildItem -Path $SearchRoot -Filter '*.json' -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($ExcludeFiles -notcontains $_.Name) {
            $files += $_.FullName
        }
    }
    return $files
}

# 主逻辑
try {
    # 使用脚本所在目录为 mods 根目录（与批处理同目录）
    $root = $PSScriptRoot
    if ([string]::IsNullOrEmpty($root)) {
        $root = (Get-Location).Path
    }
    Write-Host "Working dir: $root" -ForegroundColor Cyan
    Write-Host ""

    if ($Action -eq "fixdatafiles") {
        # 处理会被游戏误当成 manifest 的非 manifest 数据文件
        $gameRoot = [System.IO.Path]::GetDirectoryName($root.TrimEnd('\', '/'))
        $moved = 0
        $renamed = 0
        $vcPath = Join-Path $root "ControlPanel\VC_STS2_FULL_IDS.json"
        if (Test-Path $vcPath) {
            $dest = Join-Path $gameRoot "VC_STS2_FULL_IDS.json"
            Copy-Item $vcPath -Destination $dest -Force
            Remove-Item $vcPath -Force
            Write-Host "  [已移动] ControlPanel\VC_STS2_FULL_IDS.json -> 游戏根目录" -ForegroundColor Green
            $moved++
        }
        Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $modDir = $_.FullName
            $settingsPath = Join-Path $modDir "settings.json"
            $hasManifest = (Test-Path (Join-Path $modDir "mod_manifest.json")) -or (Test-Path (Join-Path $modDir "mod_mainfest.json"))
            if ((Test-Path $settingsPath) -and $hasManifest) {
                $cfgPath = Join-Path $modDir "settings.cfg"
                if (-not (Test-Path $cfgPath)) {
                    Rename-Item $settingsPath -NewName "settings.cfg" -Force
                    Write-Host "  [已重命名] $($_.Name)\settings.json -> settings.cfg" -ForegroundColor Green
                    $renamed++
                }
            }
        }
        if ($moved -eq 0 -and $renamed -eq 0) {
            Write-Host "  未发现需要处理的数据文件" -ForegroundColor Yellow
        }
        return
    }

    if ($Action -eq "fixfilename") {
        $renamed = 0
        Get-ChildItem -Path $root -Filter 'mod_mainfest.json' -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $newPath = $_.FullName -replace 'mod_mainfest\.json$', 'mod_manifest.json'
            if ($newPath -ne $_.FullName -and -not (Test-Path $newPath)) {
                Rename-Item -Path $_.FullName -NewName 'mod_manifest.json' -Force
                Write-Host "  [已修复] $($_.FullName) -> mod_manifest.json" -ForegroundColor Green
                $renamed++
            }
        }
        if ($renamed -eq 0) {
            Write-Host "  未发现需要重命名的 mod_mainfest.json" -ForegroundColor Yellow
        }
        return
    }

    $jsonFiles = Get-JsonFiles -SearchRoot $root
    Write-Host "Scanned $($jsonFiles.Count) JSON file(s)" -ForegroundColor Gray
    $toFix = @()
    $skipped = 0

    foreach ($path in $jsonFiles) {
        try {
            $raw = Get-Content -Path $path -Raw -Encoding UTF8 -ErrorAction Stop
            if ([string]::IsNullOrWhiteSpace($raw)) { continue }
            $obj = $raw | ConvertFrom-Json -ErrorAction Stop
            $objHash = @{}
            $obj.PSObject.Properties | ForEach-Object { $objHash[$_.Name] = $_.Value }
        } catch {
            continue
        }

        if (-not (Test-IsModManifest -obj $objHash)) {
            $skipped++
            continue
        }

        $dir = [System.IO.Path]::GetDirectoryName($path)
        $check = Test-NeedsFix -path $path -obj $objHash

        if ($check.NeedsFix) {
            $toFix += @{
                Path    = $path
                RelPath = ($path.Replace(($root -replace '[\\/]+$', ''), '.') -replace '^[\\/]+', '')
                Obj     = $objHash
                Check   = $check
            }
        }
    }

    $toFixCount = $toFix.Count
    Write-Host "To fix: $toFixCount file(s)" -ForegroundColor Cyan
    if ($toFixCount -eq 0) {
        Write-Host "All manifest files are correct, no fix needed." -ForegroundColor Green
        return
    }

    Write-Host "Found $toFixCount file(s) to fix:" -ForegroundColor Yellow
    Write-Host ""

    foreach ($item in $toFix) {
        $path = $item.Path
        $rel = $item.RelPath
        $check = $item.Check
        Write-Host "  $rel" -ForegroundColor White
        $check.Reasons | ForEach-Object { Write-Host "    - $_" -ForegroundColor DarkGray }

        if ($Action -eq "fix") {
            try {
                $modId = $check.ModId
                $dir = [System.IO.Path]::GetDirectoryName($path)
                $fixed = Get-FixedManifest -obj $item.Obj -dir $dir -modId $modId

                $bakPath = $path + ".bak"
                Copy-Item -Path $path -Destination $bakPath -Force
                $jsonOut = $fixed | ConvertTo-Json -Depth 10
                # Ensure dependencies is array not object (PowerShell 5.1 quirk)
                $jsonOut = $jsonOut -replace '"dependencies"\s*:\s*\{\s*\}', '"dependencies": []'
                [System.IO.File]::WriteAllText($path, $jsonOut, [System.Text.UTF8Encoding]::new($false))
                Write-Host "    [Fixed, backup saved as .json.bak]" -ForegroundColor Green
            } catch {
                Write-Host "    [Error] $_" -ForegroundColor Red
                Write-Host "    $($_.ScriptStackTrace)" -ForegroundColor DarkRed
            }
        }
        Write-Host ""
    }

    if ($Action -eq "preview") {
        Write-Host "Preview only. Use option 1 to apply fixes." -ForegroundColor Cyan
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
