$root = "C:\VB26\Lyracist"
$deletedCount = 0
$freedBytes = 0

function Remove-Target {
    param([System.IO.FileSystemInfo]$Item)

    $size = 0
    if ($Item.PSIsContainer) {
        $size = (Get-ChildItem -LiteralPath $Item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum
    } else {
        $size = $Item.Length
    }

    Remove-Item -LiteralPath $Item.FullName -Recurse -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path -LiteralPath $Item.FullName)) {
        $script:deletedCount++
        $script:freedBytes += [int64]$size
        Write-Host "Deleted: $($Item.FullName)"
    } else {
        Write-Host "FAILED to delete (in use?): $($Item.FullName)" -ForegroundColor Yellow
    }
}

Write-Host "Scanning $root ..."

# .vs folder (hidden — requires -Force to be seen)
Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq ".vs" } |
    ForEach-Object { Remove-Target $_ }

# bin/obj folders
Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue -Include "bin","obj" |
    ForEach-Object { Remove-Target $_ }

# loose .bin/.obj/.cache/.user files
Get-ChildItem -Path $root -File -Recurse -Force -ErrorAction SilentlyContinue -Include "*.bin","*.obj","*.cache","*.user" |
    ForEach-Object { Remove-Target $_ }

$freedMB = [math]::Round($freedBytes / 1MB, 2)
Write-Host ""
Write-Host "Done. Deleted $deletedCount item(s), freed approximately $freedMB MB." -ForegroundColor Cyan

Read-Host "Press Enter to exit"
