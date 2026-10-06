param([string]$AppDockDirectory = (Join-Path $PSScriptRoot '..\..\AppDock.at365\publish'))
$ErrorActionPreference = 'Stop'
$taskAppletRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskHostDirectory = [System.IO.Path]::GetFullPath($AppDockDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $taskHostDirectory 'AppDock.at365.exe') -PathType Leaf)) { throw 'AppDock.at365.exeがあるフォルダーを指定してください。' }
$taskDestination = Join-Path $taskHostDirectory 'extensions\Applet.WallpaperSlideshow.at365'
$taskSource = Join-Path $taskAppletRoot 'publish\Applet.WallpaperSlideshow.at365'
if (-not (Test-Path -LiteralPath (Join-Path $taskSource 'Applet.WallpaperSlideshow.at365.exe') -PathType Leaf)) { throw '先にscripts\publish.ps1でビルドしてください。' }
New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
foreach ($taskFile in @('Applet.WallpaperSlideshow.at365.exe', 'extension.json')) { Copy-Item -LiteralPath (Join-Path $taskSource $taskFile) -Destination (Join-Path $taskDestination $taskFile) -Force }
Write-Output "Appletを配置しました: $taskDestination"
Write-Output 'AppDockを起動し直し、Applet.WallpaperSlideshow.at365を有効にしてください。'
