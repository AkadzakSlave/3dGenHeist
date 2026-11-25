param(
    [Parameter(Mandatory=$true)]
    [string]$PackagePath,
    [Parameter(Mandatory=$false)]
    [string]$TargetRoot = "."
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $PackagePath)) {
    Write-Error "Package not found: $PackagePath"
}

$tempExtract = Join-Path "BuildTools" ("temp_unpack_" + [System.IO.Path]::GetFileNameWithoutExtension($PackagePath))
if (Test-Path $tempExtract) {
    Remove-Item -Recurse -Force $tempExtract
}
New-Item -ItemType Directory -Force $tempExtract | Out-Null

Write-Host "Decompressing $PackagePath with tar..."
tar -xzf $PackagePath -C $tempExtract

Write-Host "Placing unpacked assets into project..."
$count = 0
$guidDirs = Get-ChildItem -Path $tempExtract -Directory

foreach ($dir in $guidDirs) {
    $pathnameFile = Join-Path $dir.FullName "pathname"
    if (!(Test-Path $pathnameFile)) {
        continue
    }

    $relativePath = (Get-Content $pathnameFile -TotalCount 1).Trim()
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        continue
    }

    # Normalize slashes
    $relativePath = $relativePath.Replace('/', '\')
    $targetPath = Join-Path $TargetRoot $relativePath
    $assetFile = Join-Path $dir.FullName "asset"
    $metaFile = Join-Path $dir.FullName "asset.meta"

    if (Test-Path $assetFile) {
        $parentDir = [System.IO.Path]::GetDirectoryName($targetPath)
        if (!(Test-Path $parentDir)) {
            New-Item -ItemType Directory -Force $parentDir | Out-Null
        }
        Copy-Item -Path $assetFile -Destination $targetPath -Force
        $count++
    } else {
        # Directory entry
        if (!(Test-Path $targetPath)) {
            New-Item -ItemType Directory -Force $targetPath | Out-Null
        }
    }

    if (Test-Path $metaFile) {
        $targetMeta = $targetPath + ".meta"
        Copy-Item -Path $metaFile -Destination $targetMeta -Force
    }
}

Remove-Item -Recurse -Force $tempExtract
Write-Host "Successfully unpacked $count files from $([System.IO.Path]::GetFileName($PackagePath))."
