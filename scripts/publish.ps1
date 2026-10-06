param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskAppletRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPublishDirectory = if ($OutputDirectory) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskAppletRoot 'publish\Applet.WallpaperSlideshow.at365' }
$taskManifest = Get-Content -LiteralPath (Join-Path $taskAppletRoot 'extension.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskProject = [xml](Get-Content -LiteralPath (Join-Path $taskAppletRoot 'Applet.WallpaperSlideshow\Applet.WallpaperSlideshow.csproj') -Raw)
if ($taskManifest.version -ne $taskProject.Project.PropertyGroup.Version) { throw 'EXEとmanifestのバージョンが一致しません。' }
dotnet publish (Join-Path $taskAppletRoot 'Applet.WallpaperSlideshow\Applet.WallpaperSlideshow.csproj') -c Release -r win-x64 --self-contained true -o $taskPublishDirectory
if ($LASTEXITCODE -ne 0) { throw "Wallpaper Applet publish failed ($LASTEXITCODE)" }
Copy-Item -LiteralPath (Join-Path $taskAppletRoot 'extension.json') -Destination (Join-Path $taskPublishDirectory 'extension.json') -Force
$taskProductVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskPublishDirectory $taskManifest.entry)).ProductVersion.Split('+')[0]
if ($taskProductVersion -ne $taskManifest.version) { throw '発行されたEXEとmanifestのバージョンが一致しません。' }
foreach ($taskSymbols in @('AppDock.Runtime.pdb', 'AppDock.SDK.pdb')) {
    $taskSymbolFile = Join-Path $taskPublishDirectory $taskSymbols
    if (Test-Path -LiteralPath $taskSymbolFile -PathType Leaf) { Remove-Item -LiteralPath $taskSymbolFile }
}
Write-Output "Applet output: $taskPublishDirectory"
