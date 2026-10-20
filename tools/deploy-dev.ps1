# 개발용 배포: 빌드한 DLL 과 번들을 SPT 플러그인 폴더에 복사한다. 게임이 켜져 있으면 DLL 이 잠겨 실패한다.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content (Join-Path $root 'Briefing.local.props')
$spt = $props.Project.PropertyGroup.SptRoot
$dest = Join-Path $spt 'BepInEx\plugins\Briefing'
New-Item -ItemType Directory -Force $dest | Out-Null

# Briefing.Svg 를 빌드하면 Client·Core 도 같이 빌드된다
dotnet build (Join-Path $root 'client\Briefing.Svg\Briefing.Svg.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
$bin = Join-Path $root 'client\Briefing.Client\bin\Release\netstandard2.1'
foreach ($dll in 'Briefing.Client.dll', 'Briefing.Core.dll') { Copy-Item (Join-Path $bin $dll) $dest -Force }

# VectorGraphics 와 그것을 쓰는 Briefing.Svg 는 .bin 으로 둔다. .dll 이면 BepInEx·FixPluginTypesSerialization 이 시작 때 미리 올린다
$lib = Join-Path $dest 'lib'
New-Item -ItemType Directory -Force $lib | Out-Null
Copy-Item (Join-Path $root 'client\Briefing.Svg\bin\Release\netstandard2.1\Briefing.Svg.dll') (Join-Path $lib 'Briefing.Svg.bin') -Force
foreach ($name in 'Unity.VectorGraphics', 'Unity.InternalAPIEngineBridge.003') {
    Copy-Item (Join-Path $root "client\lib\$name.dll") (Join-Path $lib "$name.bin") -Force
}

$bundle = Join-Path $root 'unity\Build\briefing.bundle'
if (Test-Path $bundle) { Copy-Item $bundle $dest -Force } else { Write-Host 'briefing.bundle 없음 (Task 4 전이면 정상)' }
Write-Host "deployed to $dest"
