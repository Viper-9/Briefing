# 개발용 배포: 빌드한 DLL 과 번들을 SPT 플러그인 폴더에 복사한다. 게임이 켜져 있으면 DLL 이 잠겨 실패한다.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content (Join-Path $root 'Briefing.local.props')
$spt = $props.Project.PropertyGroup.SptRoot
$dest = Join-Path $spt 'BepInEx\plugins\Briefing'
New-Item -ItemType Directory -Force $dest | Out-Null

dotnet build (Join-Path $root 'client\Briefing.Client\Briefing.Client.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'build failed' }
Copy-Item (Join-Path $root 'client\Briefing.Client\bin\Release\netstandard2.1\Briefing.Client.dll') $dest -Force

$bundle = Join-Path $root 'unity\Build\briefing.bundle'
if (Test-Path $bundle) { Copy-Item $bundle $dest -Force } else { Write-Host 'briefing.bundle 없음 (Task 4 전이면 정상)' }
Write-Host "deployed to $dest"
