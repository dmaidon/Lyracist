# Created on Aug 30, 2026 @ 10:56:00 -> Tracked Lyracist deployment script for laptop show synchronization
$Target = "\\LUCY\C_Lucy\Lyracist"
$Source = "C:\VB26\Release\Lyracist\Debug\net10.0-windows"
$LogFile = "C:\Temp\Deploy-Lyracist.log"

# Consolidated folder taxonomy:
# - TabletClient : Mobile performer tablet & kiosk web clients
# - Banners      : All app banners (KSRotation, Lyracist, LyracistTrivia, KnockoutTrivia)
# - Data         : Shared databases (trivia.db question database, etc.)
# - Packs        : 16 curated JSON trivia question packs
# - Settings     : Per-app settings & venue graphics
# - Assets       : App branding and UI assets
# - libvlc       : Media player native libraries
# - runtimes     : Platform-specific runtime binaries (SQLite, FFmpeg, etc.)
$Folders = @("TabletClient", "Banners", "Data", "Packs", "Settings", "Assets", "libvlc", "runtimes")

function Log($msg) {
    Add-Content -Path $LogFile -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $msg"
}

function Run-RoboCopy {
    param(
        [string]$Src,
        [string]$Dst,
        [string]$Label,
        [string]$Files,
        [bool]$ForceCopy,
        [string[]]$ExcludeFiles = @()
    )

    Log "===== $Label ====="

    if ($ForceCopy) {
        # ALWAYS overwrite loose files (binaries, DLLs, EXEs)
        robocopy $Src $Dst $Files /COPY:DAT /DCOPY:DA /R:1 /W:1 /NFL /NDL
    }
    else {
        # Folders: timestamp-based, skip unless changed
        if ($ExcludeFiles.Count -gt 0) {
            robocopy $Src $Dst /E /FFT /NFL /NDL /R:1 /W:1 /XF $ExcludeFiles
        }
        else {
            robocopy $Src $Dst /E /FFT /NFL /NDL /R:1 /W:1
        }
    }

    $ExitCode = $LASTEXITCODE
    Log "Robocopy exit code: $ExitCode"
}

Log "================ RUN START ================"

if (!(Test-Path $Target)) { Log "ERROR: Target missing: $Target"; exit }
if (!(Test-Path $Source)) { Log "ERROR: Source missing: $Source"; exit }

# Loose files — ALWAYS overwrite
Run-RoboCopy -Src $Source -Dst $Target -Label "Loose Files" -Files "*.*" -ForceCopy $true

# Folders — timestamp-based with targeted exclusions
foreach ($Folder in $Folders) {
    $Src = Join-Path $Source $Folder
    $Dst = Join-Path $Target $Folder

    if (!(Test-Path $Src)) {
        Log "WARNING: Missing folder: $Src"
        continue
    }

    $Exclusions = @()

    # Settings: Never clobber live DJ / venue configurations configured on the target laptop
    if ($Folder -eq "Settings") {
        $Exclusions = @("*_settings.json", "ksrotation_venues.json", "ksrotation_djs.json", "lyracist_trivia_selected_announcement.json")
    }

    # Banners: Protect live announcement selection state on show laptop
    elseif ($Folder -eq "Banners") {
        $Exclusions = @("selected_announcement.json")
    }

    # Data: Protect live show rotation history, known singers DB, and local gig databases from dev overwrite.
    # Note: trivia.db is NOT excluded so new/updated questions deploy automatically.
    elseif ($Folder -eq "Data") {
        $Exclusions = @("lyracist.db", "ksrotation_night_db.json", "ksrotation_singers.json", "wifi_passwords.json", "keygen.db")
    }

    Run-RoboCopy -Src $Src -Dst $Dst -Label "Folder: $Folder" -Files "" -ForceCopy $false -ExcludeFiles $Exclusions
}

Log "================ RUN COMPLETE ================"
Write-Host "Deployment complete."
