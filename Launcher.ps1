<#
    MrBean Launcher — лаунчер Minecraft 1.21.11 + Fabric + MrBean Visuals
    Всё хранится рядом с этим файлом (D:\MrBeanLauncher).
    Скачивает: Java 21 (официальная от Mojang), Minecraft 1.21.11, Fabric Loader, Fabric API.
#>
param(
    [switch]$NoGui,
    [switch]$InstallOnly,
    [string]$Root = $PSScriptRoot,
    [string]$Nick = "",
    [int]$Ram = 0
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}
Add-Type -AssemblyName System.Net.Http
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# ------------------------------------------------------------------ константы
$MC_VERSION   = '1.21.11'
$LAUNCHER_VER = '1.0'
$IsWin        = ($env:OS -eq 'Windows_NT')
$SEP          = if ($IsWin) { ';' } else { ':' }

$GameDir    = Join-Path $Root 'game'
$LibDir     = Join-Path $GameDir 'libraries'
$AssetsDir  = Join-Path $GameDir 'assets'
$VersionsDir= Join-Path $GameDir 'versions'
$RuntimeDir = Join-Path $Root 'runtime'
$NativesDir = Join-Path $GameDir "natives\$MC_VERSION"
$ModsDir    = Join-Path $GameDir 'mods'
$BundledDir = Join-Path $Root 'bundled-mods'
$SettingsFile = Join-Path $Root 'launcher.json'
$LogFile    = Join-Path $Root 'launcher.log'

$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromMinutes(10)
$http.DefaultRequestHeaders.UserAgent.ParseAdd("MrBeanLauncher/$LAUNCHER_VER")

# ------------------------------------------------------------------ лог и прогресс
function Log([string]$s) {
    $line = "[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $s
    try { Add-Content -Path $LogFile -Value $line -Encoding UTF8 } catch {}
    if ($NoGui) { Write-Host $line }
}

$script:OnProgress = { param([double]$pct, [string]$text) if ($NoGui) { Write-Host ("  {0,5:N1}%  {1}" -f $pct, $text) } }
$script:Pump = { }
$script:lastReport = [DateTime]::MinValue

function Report([double]$pct, [string]$text, [switch]$Force) {
    $now = [DateTime]::Now
    if ($Force -or ($now - $script:lastReport).TotalMilliseconds -gt 120) {
        $script:lastReport = $now
        & $script:OnProgress $pct $text
    }
}

function Ensure-Dir([string]$p) { if (-not (Test-Path -LiteralPath $p)) { New-Item -ItemType Directory -Force -Path $p | Out-Null } }

function Get-Text([string]$url) {
    for ($i = 1; $i -le 3; $i++) {
        try {
            $t = $http.GetStringAsync($url)
            while (-not $t.IsCompleted) { & $script:Pump; Start-Sleep -Milliseconds 20 }
            if ($t.IsFaulted) { throw $t.Exception.InnerException }
            return $t.Result
        } catch {
            if ($i -eq 3) { throw "Не удалось открыть $url : $($_.Exception.Message)" }
            Start-Sleep -Seconds 2
        }
    }
}

function Get-Json([string]$url, [string]$cacheFile) {
    try {
        $txt = Get-Text $url
        if ($cacheFile) { Ensure-Dir (Split-Path $cacheFile); [IO.File]::WriteAllText($cacheFile, $txt) }
    } catch {
        if ($cacheFile -and (Test-Path $cacheFile)) { Log "Нет сети, беру из кэша: $cacheFile"; $txt = [IO.File]::ReadAllText($cacheFile) }
        else { throw }
    }
    return ($txt | ConvertFrom-Json)
}

<#  Параллельная загрузка списка файлов.
    $items — массив объектов @{ Url; Path; Size }  #>
function Download-Files($items, [string]$label, [double]$pctFrom, [double]$pctTo, [int]$parallel = 16) {
    $todo = New-Object System.Collections.ArrayList
    foreach ($it in $items) {
        $need = $true
        if (Test-Path -LiteralPath $it.Path) {
            if (-not $it.Size -or (Get-Item -LiteralPath $it.Path).Length -eq [long]$it.Size) { $need = $false }
        }
        if ($need) { [void]$todo.Add($it) }
    }
    $total = $todo.Count
    if ($total -eq 0) { Report $pctTo "$label — готово" -Force; return }
    Log "$label : нужно скачать $total файлов"
    $done = 0; $i = 0
    $running = New-Object System.Collections.ArrayList
    while ($done -lt $total) {
        while ($running.Count -lt $parallel -and $i -lt $total) {
            $it = $todo[$i]; $i++
            [void]$running.Add(@{ T = $http.GetByteArrayAsync($it.Url); It = $it; Try = 1 })
        }
        foreach ($r in @($running)) {
            if (-not $r.T.IsCompleted) { continue }
            $running.Remove($r)
            if ($r.T.IsFaulted -or $r.T.IsCanceled) {
                if ($r.Try -lt 4) {
                    $r.Try++; $r.T = $http.GetByteArrayAsync($r.It.Url); [void]$running.Add($r)
                    continue
                }
                throw "Не удалось скачать: $($r.It.Url)"
            }
            Ensure-Dir (Split-Path -Parent $r.It.Path)
            [IO.File]::WriteAllBytes($r.It.Path, $r.T.Result)
            $done++
        }
        $pct = $pctFrom + ($pctTo - $pctFrom) * ($done / [double]$total)
        Report $pct ("{0}: {1} / {2}" -f $label, $done, $total)
        & $script:Pump
        Start-Sleep -Milliseconds 10
    }
    Report $pctTo "$label — готово" -Force
}

# ------------------------------------------------------------------ правила библиотек Mojang
function Test-Rules($rules) {
    if (-not $rules) { return $true }
    $allowed = $false
    foreach ($r in $rules) {
        $match = $true
        if ($r.features) { $match = $false }
        if ($r.os) {
            if ($r.os.name -and $r.os.name -ne 'windows') { $match = $false }
            if ($r.os.arch -and $r.os.arch -ne 'x64') { $match = $false }
        }
        if ($match) { $allowed = ($r.action -eq 'allow') }
    }
    return $allowed
}

function Maven-Path([string]$coord) {
    $p = $coord.Split(':')
    $group = $p[0].Replace('.', '/'); $art = $p[1]; $ver = $p[2]
    $file = "$art-$ver"
    if ($p.Count -ge 4) { $file += "-$($p[3])" }
    return "$group/$art/$ver/$file.jar"
}

function Lib-Key([string]$coord) {
    $p = $coord.Split(':')
    if ($p.Count -ge 4) { return "$($p[0]):$($p[1]):$($p[3])" }
    return "$($p[0]):$($p[1])"
}

# ------------------------------------------------------------------ настройки
function Load-Settings {
    $s = [pscustomobject]@{ nick = 'MrBean'; ram = 4096; closeOnStart = $true }
    if (Test-Path $SettingsFile) {
        try {
            $j = Get-Content $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($j.nick) { $s.nick = $j.nick }
            if ($j.ram) { $s.ram = [int]$j.ram }
            if ($null -ne $j.closeOnStart) { $s.closeOnStart = [bool]$j.closeOnStart }
        } catch {}
    }
    return $s
}
function Save-Settings($s) { ($s | ConvertTo-Json) | Set-Content -Path $SettingsFile -Encoding UTF8 }

function Offline-UUID([string]$name) {
    $md5 = [Security.Cryptography.MD5]::Create()
    $b = $md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("OfflinePlayer:$name"))
    $b[6] = ($b[6] -band 0x0f) -bor 0x30
    $b[8] = ($b[8] -band 0x3f) -bor 0x80
    return (($b | ForEach-Object { $_.ToString('x2') }) -join '')
}

# ------------------------------------------------------------------ установка
function Install-Game {
    Ensure-Dir $GameDir; Ensure-Dir $LibDir; Ensure-Dir $AssetsDir; Ensure-Dir $VersionsDir; Ensure-Dir $ModsDir; Ensure-Dir $NativesDir

    # 1. Описание версии
    Report 1 "Получаю список версий Mojang..." -Force
    $verJsonPath = Join-Path $VersionsDir "$MC_VERSION\$MC_VERSION.json"
    $ver = $null
    try {
        $manifest = Get-Json 'https://piston-meta.mojang.com/mc/game/version_manifest_v2.json'
        $entry = $manifest.versions | Where-Object { $_.id -eq $MC_VERSION } | Select-Object -First 1
        $ver = Get-Json $entry.url $verJsonPath
    } catch {
        if (Test-Path $verJsonPath) { $ver = [IO.File]::ReadAllText($verJsonPath) | ConvertFrom-Json } else { throw }
    }

    # 2. Java от Mojang
    $component = $ver.javaVersion.component
    $javaRoot = Join-Path $RuntimeDir $component
    $javaw = Join-Path $javaRoot 'bin\javaw.exe'
    if (-not $IsWin) { $javaw = Join-Path $javaRoot 'bin/java' }
    $javaMarker = Join-Path $javaRoot '.installed'
    if (-not (Test-Path $javaMarker)) {
        Report 3 "Скачиваю Java $($ver.javaVersion.majorVersion)..." -Force
        $all = Get-Json 'https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json'
        $rt = $all.'windows-x64'.$component | Select-Object -First 1
        $rtMan = Get-Json $rt.manifest.url
        $items = @()
        foreach ($prop in $rtMan.files.PSObject.Properties) {
            $f = $prop.Value
            $dst = Join-Path $javaRoot $prop.Name
            if ($f.type -eq 'directory') { Ensure-Dir $dst; continue }
            if ($f.type -ne 'file') { continue }
            $items += [pscustomobject]@{ Url = $f.downloads.raw.url; Path = $dst; Size = $f.downloads.raw.size }
        }
        Download-Files $items 'Java' 3 15
        Set-Content -Path $javaMarker -Value $rt.version.name
    } else { Report 15 "Java уже установлена" -Force }

    # 3. Клиент
    Report 15 "Скачиваю Minecraft $MC_VERSION..." -Force
    $clientJar = Join-Path $VersionsDir "$MC_VERSION\$MC_VERSION.jar"
    Download-Files @([pscustomobject]@{ Url = $ver.downloads.client.url; Path = $clientJar; Size = $ver.downloads.client.size }) 'Клиент' 15 20

    # 4. Библиотеки
    $libs = [ordered]@{}
    $libItems = @()
    $nativeJars = @()
    foreach ($l in $ver.libraries) {
        if (-not (Test-Rules $l.rules)) { continue }
        if ($l.name -match ':natives-windows-(arm64|x86)$') { continue }
        $a = $l.downloads.artifact
        if (-not $a) { continue }
        $path = Join-Path $LibDir ($a.path.Replace('/', '\'))
        if (-not $IsWin) { $path = Join-Path $LibDir $a.path }
        $libItems += [pscustomobject]@{ Url = $a.url; Path = $path; Size = $a.size }
        $libs[(Lib-Key $l.name)] = $path
        if ($l.name -match ':natives-windows$') { $nativeJars += $path }
    }

    # Fabric Loader
    Report 20 "Получаю Fabric..." -Force
    $loaders = Get-Json "https://meta.fabricmc.net/v2/versions/loader/$MC_VERSION" (Join-Path $Root 'cache\fabric-loaders.json')
    $loader = ($loaders | Where-Object { $_.loader.stable } | Select-Object -First 1).loader.version
    if (-not $loader) { $loader = $loaders[0].loader.version }
    $fabric = Get-Json "https://meta.fabricmc.net/v2/versions/loader/$MC_VERSION/$loader/profile/json" (Join-Path $Root "cache\fabric-$loader.json")
    foreach ($l in $fabric.libraries) {
        $rel = Maven-Path $l.name
        $base = $l.url; if (-not $base.EndsWith('/')) { $base += '/' }
        $path = Join-Path $LibDir ($rel.Replace('/', '\'))
        if (-not $IsWin) { $path = Join-Path $LibDir $rel }
        $size = $null; if ($l.size) { $size = $l.size }
        $libItems += [pscustomobject]@{ Url = $base + $rel; Path = $path; Size = $size }
        $libs[(Lib-Key $l.name)] = $path   # Fabric перекрывает одноимённые библиотеки
    }
    Download-Files $libItems 'Библиотеки' 20 35

    # Нативные dll
    Report 35 "Распаковываю natives..." -Force
    foreach ($nj in $nativeJars) {
        $zip = [IO.Compression.ZipFile]::OpenRead($nj)
        try {
            foreach ($e in $zip.Entries) {
                if ($e.FullName -like 'META-INF*') { continue }
                if ($e.Name -match '\.(dll|so|dylib)$') {
                    $dst = Join-Path $NativesDir $e.Name
                    if (-not (Test-Path $dst)) { [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dst, $true) }
                }
            }
        } finally { $zip.Dispose() }
    }

    # 5. Ассеты (звуки, текстуры, языки)
    Report 36 "Получаю список ресурсов..." -Force
    $idxPath = Join-Path $AssetsDir "indexes\$($ver.assetIndex.id).json"
    $idx = Get-Json $ver.assetIndex.url $idxPath
    $assetItems = New-Object System.Collections.ArrayList
    foreach ($prop in $idx.objects.PSObject.Properties) {
        $h = $prop.Value.hash; $pre = $h.Substring(0, 2)
        [void]$assetItems.Add([pscustomobject]@{
            Url  = "https://resources.download.minecraft.net/$pre/$h"
            Path = (Join-Path $AssetsDir "objects\$pre\$h")
            Size = $prop.Value.size })
    }
    Download-Files $assetItems 'Ресурсы' 36 92 24

    # 6. Моды: Fabric API + MrBean Visuals
    Report 92 "Ставлю моды..." -Force
    $haveApi = @(Get-ChildItem -Path $ModsDir -Filter 'fabric-api-*.jar' -ErrorAction SilentlyContinue).Count -gt 0
    if (-not $haveApi) {
        $apiUrl = 'https://cdn.modrinth.com/data/P7dR8mSH/versions/6qAuTtLR/fabric-api-0.141.6%2B1.21.11.jar'
        $apiName = 'fabric-api-0.141.6+1.21.11.jar'
        try {
            $q = [Uri]::EscapeDataString('["' + $MC_VERSION + '"]'); $ql = [Uri]::EscapeDataString('["fabric"]')
            $vers = Get-Json "https://api.modrinth.com/v2/project/fabric-api/version?game_versions=$q&loaders=$ql"
            if ($vers -and $vers[0].files) { $apiUrl = $vers[0].files[0].url; $apiName = $vers[0].files[0].filename }
        } catch { Log "Modrinth недоступен, беру зашитую ссылку Fabric API" }
        Download-Files @([pscustomobject]@{ Url = $apiUrl; Path = (Join-Path $ModsDir $apiName); Size = $null }) 'Fabric API' 92 96
    }
    if (Test-Path $BundledDir) {
        foreach ($m in Get-ChildItem $BundledDir -Filter *.jar) {
            Copy-Item $m.FullName (Join-Path $ModsDir $m.Name) -Force
        }
    }

    # Русский язык по умолчанию при первом запуске
    $opt = Join-Path $GameDir 'options.txt'
    if (-not (Test-Path $opt)) { Set-Content -Path $opt -Value "lang:ru_ru" -Encoding ASCII }

    Report 100 "Готово!" -Force
    Log "Установка завершена (Fabric $loader)"

    $cp = @($libs.Values) + @($clientJar)
    return [pscustomobject]@{
        Java = $javaw; Classpath = ($cp -join $SEP); MainClass = $fabric.mainClass
        FabricJvm = @($fabric.arguments.jvm); VersionId = $fabric.id; AssetIndex = $ver.assetIndex.id
        Missing = @($cp | Where-Object { -not (Test-Path -LiteralPath $_) })
    }
}

function Quote-Arg([string]$a) {
    if ($a -eq '') { return '""' }
    if ($a -notmatch '[\s"]') { return $a }
    return '"' + ($a -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

function Build-Args($info, [string]$nick, [int]$ram) {
    $jvm = @(
        "-Xms1024M", "-Xmx$($ram)M",
        "-XX:+UseG1GC", "-XX:+UnlockExperimentalVMOptions", "-XX:G1NewSizePercent=20",
        "-XX:G1ReservePercent=20", "-XX:MaxGCPauseMillis=50", "-XX:G1HeapRegionSize=32M",
        "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump",
        "-Djava.library.path=$NativesDir", "-Djna.tmpdir=$NativesDir",
        "-Dorg.lwjgl.system.SharedLibraryExtractPath=$NativesDir", "-Dio.netty.native.workdir=$NativesDir",
        "-Dminecraft.launcher.brand=MrBeanLauncher", "-Dminecraft.launcher.version=$LAUNCHER_VER",
        "-Dfile.encoding=UTF-8", "-Dstdout.encoding=UTF-8"
    ) + $info.FabricJvm + @("-cp", $info.Classpath, $info.MainClass)
    $game = @(
        "--username", $nick, "--version", $info.VersionId, "--gameDir", $GameDir,
        "--assetsDir", $AssetsDir, "--assetIndex", $info.AssetIndex,
        "--uuid", (Offline-UUID $nick), "--accessToken", "0", "--clientId", "0", "--xuid", "0",
        "--userType", "legacy", "--versionType", "MrBean Launcher"
    )
    return @($jvm + $game)
}

function Start-Game($info, [string]$nick, [int]$ram) {
    $argList = Build-Args $info $nick $ram
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $info.Java
    $psi.Arguments = (($argList | ForEach-Object { Quote-Arg $_ }) -join ' ')
    $psi.WorkingDirectory = $GameDir
    $psi.UseShellExecute = $false
    Log "Запуск: $($info.Java) (ник $nick, RAM $ram МБ)"
    return [Diagnostics.Process]::Start($psi)
}

# ------------------------------------------------------------------ режим без окна (для проверки)
if ($NoGui) {
    $s = Load-Settings
    if ($Nick) { $s.nick = $Nick }; if ($Ram) { $s.ram = $Ram }
    $info = Install-Game
    if ($info.Missing.Count) { Write-Host "НЕ ХВАТАЕТ ФАЙЛОВ:"; $info.Missing | ForEach-Object { Write-Host "  $_" } }
    $a = Build-Args $info $s.nick $s.ram
    Write-Host "Java: $($info.Java)"
    Write-Host "Main: $($info.MainClass)  Version: $($info.VersionId)"
    Write-Host ("Аргументов: {0}, длина командной строки: {1}" -f $a.Count, (($a | ForEach-Object { Quote-Arg $_ }) -join ' ').Length)
    if (-not $InstallOnly) { Start-Game $info $s.nick $s.ram | Out-Null }
    return
}

# ================================================================== ОКНО ЛАУНЧЕРА
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$C_BG     = [Drawing.Color]::FromArgb(16, 17, 24)
$C_PANEL  = [Drawing.Color]::FromArgb(26, 28, 38)
$C_TEXT   = [Drawing.Color]::FromArgb(235, 237, 245)
$C_MUTED  = [Drawing.Color]::FromArgb(140, 146, 165)
$C_GREEN  = [Drawing.Color]::FromArgb(124, 217, 78)
$C_YELLOW = [Drawing.Color]::FromArgb(244, 224, 77)

$settings = Load-Settings
$form = New-Object Windows.Forms.Form
$form.Text = "MrBean Launcher"
$form.ClientSize = New-Object Drawing.Size(860, 520)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.BackColor = $C_BG
$form.ForeColor = $C_TEXT
$form.Font = New-Object Drawing.Font('Segoe UI', 10)
$iconPath = Join-Path $Root 'icon.ico'
if (Test-Path $iconPath) { $form.Icon = New-Object Drawing.Icon($iconPath) }
$form.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($form, $true, $null)

$script:anim = 0.0
$header = New-Object Windows.Forms.Panel
$header.SetBounds(0, 0, 860, 170)
$header.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($header, $true, $null)
$header.Add_Paint({
    param($s, $e)
    $g = $e.Graphics
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $r = New-Object Drawing.Rectangle(0, 0, 860, 170)
    $shift = [Math]::Sin($script:anim) * 0.5 + 0.5
    $c1 = [Drawing.Color]::FromArgb(255, [int](30 + 20 * $shift), [int](70 + 30 * $shift), 40)
    $c2 = [Drawing.Color]::FromArgb(255, 20, 22, 32)
    $br = New-Object Drawing.Drawing2D.LinearGradientBrush($r, $c1, $c2, [single](20 + 25 * $shift))
    $g.FillRectangle($br, $r)
    # фоновые «бобы»
    for ($i = 0; $i -lt 7; $i++) {
        $x = (($i * 137 + $script:anim * 25) % 940) - 60
        $y = 20 + ($i * 53) % 120
        $a = 18 + ($i * 7) % 20
        $b = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb($a, $C_YELLOW))
        $g.FillEllipse($b, [single]$x, [single]$y, 70, 48)
    }
    # логотип
    $gb = New-Object Drawing.SolidBrush($C_GREEN)
    $g.FillEllipse($gb, 40, 45, 80, 70)
    $g.FillEllipse((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(150, 225, 95))), 66, 36, 50, 46)
    $g.FillEllipse((New-Object Drawing.SolidBrush($C_BG)), 60, 70, 20, 26)
    $g.FillEllipse((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(200, 255, 255, 255))), 92, 46, 12, 12)
    $titleFont = New-Object Drawing.Font('Segoe UI', 34, [Drawing.FontStyle]::Bold)
    $tr = New-Object Drawing.RectangleF(138, 38, 600, 60)
    $tb = New-Object Drawing.Drawing2D.LinearGradientBrush($tr, $C_GREEN, $C_YELLOW, [single]0)
    $g.DrawString("MrBean Launcher", $titleFont, $tb, 136, 36)
    $g.DrawString("Minecraft $MC_VERSION  •  Fabric  •  MrBean Visuals", (New-Object Drawing.Font('Segoe UI', 12)), (New-Object Drawing.SolidBrush($C_MUTED)), 142, 98)
})
$form.Controls.Add($header)

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 40
$timer.Add_Tick({ $script:anim += 0.03; $header.Invalidate() })
$timer.Start()

function New-Label($text, $x, $y, $w, $h, $color, $size = 10, $bold = $false) {
    $l = New-Object Windows.Forms.Label
    $l.Text = $text; $l.SetBounds($x, $y, $w, $h); $l.ForeColor = $color; $l.BackColor = [Drawing.Color]::Transparent
    $style = if ($bold) { [Drawing.FontStyle]::Bold } else { [Drawing.FontStyle]::Regular }
    $l.Font = New-Object Drawing.Font('Segoe UI', $size, $style)
    $form.Controls.Add($l); return $l
}

function New-Button($text, $x, $y, $w, $h, $bg, $fg, $size = 10) {
    $b = New-Object Windows.Forms.Button
    $b.Text = $text; $b.SetBounds($x, $y, $w, $h)
    $b.FlatStyle = 'Flat'; $b.FlatAppearance.BorderSize = 0
    $b.BackColor = $bg; $b.ForeColor = $fg; $b.Cursor = 'Hand'
    $b.Font = New-Object Drawing.Font('Segoe UI', $size, [Drawing.FontStyle]::Bold)
    $form.Controls.Add($b); return $b
}

# Блок «что внутри»
$info = New-Object Windows.Forms.Panel
$info.SetBounds(24, 186, 500, 220); $info.BackColor = $C_PANEL
$form.Controls.Add($info)
$feat = New-Object Windows.Forms.Label
$feat.SetBounds(16, 12, 470, 200); $feat.ForeColor = $C_TEXT; $feat.BackColor = $C_PANEL
$feat.Font = New-Object Drawing.Font('Segoe UI', 10)
$feat.Text = @"
Что внутри MrBean Visuals:

✦  Ватермарка, список модулей, клавиши + CPS, броня, эффекты
⚔  Таргет-HUD, счётчик тотемов/перлов, комбо, перезарядка удара
☘  Свет под ногами, день мира, точка смерти, сытость
☀  Зум (C), яркость, без тряски, свой прицел, время суток
⌨  Макросы — 6 клавиш с командами
Меню мода в игре — ПРАВЫЙ SHIFT
"@
$info.Controls.Add($feat)

# Правый блок: ник, память
New-Label "Ник" 548 186 280 22 $C_MUTED 10 | Out-Null
$nickBox = New-Object Windows.Forms.TextBox
$nickBox.SetBounds(548, 210, 288, 30); $nickBox.Text = $settings.nick; $nickBox.MaxLength = 16
$nickBox.BackColor = $C_PANEL; $nickBox.ForeColor = $C_TEXT; $nickBox.BorderStyle = 'FixedSingle'
$nickBox.Font = New-Object Drawing.Font('Segoe UI', 13)
$form.Controls.Add($nickBox)

$totalMb = 8192
try { $totalMb = [int]((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1MB) } catch {}
$maxRam = [Math]::Max(2048, [Math]::Min(16384, $totalMb - 1536))
$ramLabel = New-Label "" 548 252 288 22 $C_MUTED 10
$ram = New-Object Windows.Forms.TrackBar
$ram.SetBounds(544, 274, 296, 40); $ram.Minimum = 2; $ram.Maximum = [int]($maxRam / 1024); $ram.TickFrequency = 1
$ram.Value = [Math]::Max($ram.Minimum, [Math]::Min($ram.Maximum, [int]($settings.ram / 1024)))
$ram.BackColor = $C_BG
$updRam = { $ramLabel.Text = "Память для игры: $($ram.Value) ГБ  (всего $([Math]::Round($totalMb/1024)) ГБ)" }
& $updRam
$ram.Add_ValueChanged($updRam)
$form.Controls.Add($ram)

$closeBox = New-Object Windows.Forms.CheckBox
$closeBox.Text = "Скрывать лаунчер во время игры"; $closeBox.Checked = $settings.closeOnStart
$closeBox.SetBounds(548, 318, 290, 24); $closeBox.ForeColor = $C_MUTED
$form.Controls.Add($closeBox)

$btnFolder = New-Button "Папка игры" 548 352 140 34 $C_PANEL $C_TEXT 9
$btnMods   = New-Button "Моды" 696 352 140 34 $C_PANEL $C_TEXT 9
$btnLogs   = New-Button "Логи" 548 392 140 34 $C_PANEL $C_TEXT 9
$btnRepair = New-Button "Проверить файлы" 696 392 140 34 $C_PANEL $C_TEXT 9

# Низ: прогресс и кнопка «Играть»
$status = New-Label "Готов к запуску" 24 420 500 22 $C_MUTED 10
$bar = New-Object Windows.Forms.ProgressBar
$bar.SetBounds(24, 446, 500, 14); $bar.Minimum = 0; $bar.Maximum = 1000; $bar.Style = 'Continuous'
$form.Controls.Add($bar)
$play = New-Button "ИГРАТЬ" 548 436 288 60 $C_GREEN ([Drawing.Color]::FromArgb(16, 17, 24)) 18
New-Label "Офлайн-ник: одиночная игра и серверы без лицензии. Мод работает только на клиенте." 24 470 520 40 $C_MUTED 8 | Out-Null

$script:OnProgress = {
    param([double]$pct, [string]$text)
    $bar.Value = [int][Math]::Max(0, [Math]::Min(1000, $pct * 10))
    $status.Text = $text
    [System.Windows.Forms.Application]::DoEvents()
}
$script:Pump = { [System.Windows.Forms.Application]::DoEvents() }

function Set-Busy([bool]$b) {
    foreach ($c in @($play, $btnRepair, $nickBox, $ram)) { $c.Enabled = -not $b }
    if ($b) { $play.Text = "ЗАГРУЗКА..."; $play.BackColor = [Drawing.Color]::FromArgb(70, 90, 60) }
    else { $play.Text = "ИГРАТЬ"; $play.BackColor = $C_GREEN }
}

$btnFolder.Add_Click({ Ensure-Dir $GameDir; Start-Process explorer.exe $GameDir })
$btnMods.Add_Click({ Ensure-Dir $ModsDir; Start-Process explorer.exe $ModsDir })
$btnLogs.Add_Click({ $p = Join-Path $GameDir 'logs'; if (Test-Path $p) { Start-Process explorer.exe $p } elseif (Test-Path $LogFile) { Start-Process notepad.exe $LogFile } })

$btnRepair.Add_Click({
    Set-Busy $true
    try {
        $jm = Get-ChildItem $RuntimeDir -Filter '.installed' -Recurse -Force -ErrorAction SilentlyContinue
        $jm | Remove-Item -Force -ErrorAction SilentlyContinue
        Install-Game | Out-Null
        $status.Text = "Все файлы на месте ✔"
    } catch {
        Log "Ошибка: $_"; [Windows.Forms.MessageBox]::Show("Ошибка: $_", "MrBean Launcher", 'OK', 'Error') | Out-Null
        $status.Text = "Ошибка — подробности в launcher.log"
    } finally { Set-Busy $false }
})

$play.Add_Click({
    $nick = $nickBox.Text.Trim()
    if ($nick -notmatch '^[A-Za-z0-9_]{3,16}$') {
        [Windows.Forms.MessageBox]::Show("Ник: 3–16 символов, только латиница, цифры и _", "MrBean Launcher", 'OK', 'Warning') | Out-Null
        return
    }
    $settings.nick = $nick; $settings.ram = $ram.Value * 1024; $settings.closeOnStart = $closeBox.Checked
    Save-Settings $settings
    Set-Busy $true
    try {
        $gi = Install-Game
        if ($gi.Missing.Count -gt 0) { throw "Не хватает файлов: $($gi.Missing[0]) ..." }
        $status.Text = "Запускаю Minecraft..."
        [System.Windows.Forms.Application]::DoEvents()
        $proc = Start-Game $gi $nick $settings.ram
        if ($settings.closeOnStart) {
            $form.Hide()
            while (-not $proc.HasExited) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 300 }
            $form.Show()
            if ($proc.ExitCode -ne 0) { $status.Text = "Игра закрылась с ошибкой (код $($proc.ExitCode)) — кнопка «Логи»" }
            else { $status.Text = "Игра закрыта. Ещё раз?" }
        } else { $status.Text = "Игра запущена!" }
    } catch {
        Log "Ошибка: $_"
        [Windows.Forms.MessageBox]::Show("Не получилось: $_`n`nПроверь интернет и попробуй ещё раз.", "MrBean Launcher", 'OK', 'Error') | Out-Null
        $status.Text = "Ошибка — подробности в launcher.log"
    } finally { Set-Busy $false; $form.Show() }
})

# Ярлык на рабочем столе (создаётся один раз, заменяет .bat)
try {
    $desk = [Environment]::GetFolderPath('Desktop')
    $lnk = Join-Path $desk 'MrBean Launcher.lnk'
    if (-not (Test-Path $lnk)) {
        $sh = New-Object -ComObject WScript.Shell
        $sc = $sh.CreateShortcut($lnk)
        $sc.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
        $sc.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$(Join-Path $Root 'Launcher.ps1')`""
        $sc.WorkingDirectory = $Root
        if (Test-Path $iconPath) { $sc.IconLocation = $iconPath }
        $sc.Description = 'MrBean Launcher — Minecraft 1.21.11'
        $sc.Save()
        $bat = Join-Path $desk 'MrBean Launcher.bat'
        if ((Test-Path $bat) -and ((Get-Content $bat -Raw) -match 'MrBeanLauncher')) { Remove-Item $bat -Force }
    }
} catch { Log "Не удалось создать ярлык: $_" }

$form.Add_FormClosing({ $timer.Stop() })
[void]$form.ShowDialog()
