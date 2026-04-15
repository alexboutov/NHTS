<#
.SYNOPSIS
    Run-AllDatesSlippage.ps1
    Runs Analyze-VPSTrades.ps1 for every trading date inferred from log files,
    then collects exit slippage min/max across all resulting reports.

.DESCRIPTION
    1. Scans NT8 log directory for log.YYYYMMDD.*.txt files to discover trading dates.
    2. For each date, skips if a report already exists in ActiveNikiAnalysis\YYYY-MM-DD\.
    3. Runs Analyze-VPSTrades.ps1 -Date for each missing date.
    4. After all dates processed, scans ALL report files for SLIPPAGE ANALYSIS sections
       and computes the overall min/max exit slippage across the entire sample.
    5. Prints a final summary table and saves it to ActiveNikiAnalysis\slippage_summary.txt.

.PARAMETER AnalyzeScript
    Full path to Analyze-VPSTrades.ps1. Defaults to the sibling script in the same folder.

.PARAMETER StartDate
    Only process dates on or after this date. Format: yyyy-MM-dd. Default: 2026-01-01.

.PARAMETER EndDate
    Only process dates on or before this date. Format: yyyy-MM-dd. Default: today.

.PARAMETER SkipAnalysis
    Skip running Analyze-VPSTrades.ps1 and only collect slippage from existing reports.
#>

param(
    [string]$AnalyzeScript = (Join-Path $PSScriptRoot "Analyze-VPSTrades.ps1"),
    [string]$StartDate = "2026-01-01",
    [string]$EndDate   = (Get-Date -Format "yyyy-MM-dd"),
    [switch]$SkipAnalysis
)

# === CONFIG ===

if (Test-Path "$env:USERPROFILE\OneDrive\Documents\NinjaTrader 8\log") {
    $NT8LogPath = "$env:USERPROFILE\OneDrive\Documents\NinjaTrader 8\log"
} elseif ($env:USERNAME -eq "Administrator") {
    $NT8LogPath = "C:\Users\Administrator\Documents\NinjaTrader 8\log"
} else {
    $NT8LogPath = "$env:USERPROFILE\Documents\NinjaTrader 8\log"
}

$AnalysisBasePath = Join-Path $NT8LogPath "ActiveNikiAnalysis"
$SummaryFile      = Join-Path $AnalysisBasePath "slippage_summary.txt"
$dtStart          = [datetime]::ParseExact($StartDate, "yyyy-MM-dd", $null)
$dtEnd            = [datetime]::ParseExact($EndDate,   "yyyy-MM-dd", $null)

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  Run-AllDatesSlippage.ps1" -ForegroundColor Cyan
Write-Host "  NT8 log path : $NT8LogPath" -ForegroundColor Cyan
Write-Host "  Date range   : $StartDate  to  $EndDate" -ForegroundColor Cyan
Write-Host "  Analyze script: $AnalyzeScript" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $AnalyzeScript)) {
    Write-Host "[ERROR] Analyze-VPSTrades.ps1 not found at: $AnalyzeScript" -ForegroundColor Red
    Write-Host "        Use -AnalyzeScript to specify its full path." -ForegroundColor Red
    exit 1
}

# === STEP 1: DISCOVER TRADING DATES FROM LOG FILES ===

Write-Host "[STEP 1] Discovering trading dates from log.YYYYMMDD.*.txt files..." -ForegroundColor Yellow

$logFiles = Get-ChildItem -Path $NT8LogPath -Filter "log.????????.*.txt" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notlike "*.en.txt" -and $_.Name -match "log\.(\d{8})\." }

$tradingDates = $logFiles | ForEach-Object {
    if ($_.Name -match "log\.(\d{8})\.") {
        $raw = $Matches[1]
        $formatted = "$($raw.Substring(0,4))-$($raw.Substring(4,2))-$($raw.Substring(6,2))"
        try {
            $dt = [datetime]::ParseExact($formatted, "yyyy-MM-dd", $null)
            if ($dt -ge $dtStart -and $dt -le $dtEnd) { $formatted }
        } catch {}
    }
} | Sort-Object -Unique

Write-Host "  Found $($tradingDates.Count) unique trading date(s) in range." -ForegroundColor Green
Write-Host ""

# === STEP 2: RUN ANALYSIS FOR EACH MISSING DATE ===

if (-not $SkipAnalysis) {
    Write-Host "[STEP 2] Running Analyze-VPSTrades.ps1 for dates without existing reports..." -ForegroundColor Yellow
    Write-Host ""

    $ran = 0
    $skipped = 0

    foreach ($date in $tradingDates) {
        $reportFolder = Join-Path $AnalysisBasePath $date
        $existingReport = Get-ChildItem -Path $reportFolder -Filter "*_Trading_Analysis.txt" -ErrorAction SilentlyContinue | Select-Object -First 1

        if ($existingReport) {
            Write-Host "  [SKIP] $date  - report already exists: $($existingReport.Name)" -ForegroundColor DarkGray
            $skipped++
        } else {
            Write-Host "  [RUN ] $date ..." -ForegroundColor White
            & $AnalyzeScript -Date $date
            $ran++
            Write-Host ""
        }
    }

    Write-Host ""
    Write-Host "  Ran analysis: $ran date(s)  |  Skipped (existing): $skipped date(s)" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "[STEP 2] Skipped (--SkipAnalysis flag set)." -ForegroundColor DarkGray
    Write-Host ""
}

# === STEP 3: COLLECT SLIPPAGE FROM ALL REPORTS ===

Write-Host "[STEP 3] Collecting exit slippage from all reports..." -ForegroundColor Yellow

# Per-date results
$results = @()

# Overall min/max trackers
$globalMinExit = [int]::MaxValue
$globalMaxExit = [int]::MinValue

$reportFiles = Get-ChildItem -Path $AnalysisBasePath -Recurse -Filter "*_Trading_Analysis.txt" -ErrorAction SilentlyContinue |
    Sort-Object FullName

foreach ($reportFile in $reportFiles) {
    $content = Get-Content $reportFile.FullName -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    # Extract date from parent folder name (yyyy-MM-dd)
    $folderName = Split-Path (Split-Path $reportFile.FullName -Parent) -Leaf
    $reportDate = if ($folderName -match "^\d{4}-\d{2}-\d{2}$") { $folderName } else { $reportFile.BaseName }

    # Find exit slippage range line: "   Range:  +0t to +1t"
    # This appears in the EXIT SLIPPAGE section
    $inExitSection = $false
    $exitMin = $null
    $exitMax = $null
    $trades  = $null

    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]

        if ($line -match "EXIT SLIPPAGE \(all exits\)") {
            $inExitSection = $true
            continue
        }

        # Stop at next major section
        if ($inExitSection -and $line -match "^EXIT SLIPPAGE BY REASON|^COST SUMMARY|^={10}") {
            $inExitSection = $false
        }

        if ($inExitSection) {
            if ($line -match "Trades with data:\s+(\d+)") {
                $trades = [int]$Matches[1]
            }
            if ($line -match "Range:\s+\+?(-?\d+)t to \+?(-?\d+)t") {
                $exitMin = [int]$Matches[1]
                $exitMax = [int]$Matches[2]
            }
        }
    }

    $results += [PSCustomObject]@{
        Date    = $reportDate
        Trades  = if ($null -ne $trades) { $trades } else { 0 }
        ExitMin = $exitMin
        ExitMax = $exitMax
        File    = $reportFile.Name
    }

    if ($null -ne $exitMin -and $exitMin -lt $globalMinExit) { $globalMinExit = $exitMin }
    if ($null -ne $exitMax -and $exitMax -gt $globalMaxExit) { $globalMaxExit = $exitMax }
}

# === STEP 4: PRINT SUMMARY ===

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  SLIPPAGE SUMMARY - PER DATE" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host ("{0,-12} {1,8} {2,12} {3,12}" -f "Date", "Trades", "ExitMin(t)", "ExitMax(t)")
Write-Host ("{0,-12} {1,8} {2,12} {3,12}" -f "------------", "--------", "----------", "----------")

$summaryLines = @()
$summaryLines += "Run: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$summaryLines += "Date range: $StartDate to $EndDate"
$summaryLines += ""
$summaryLines += ("{0,-12} {1,8} {2,12} {3,12}" -f "Date", "Trades", "ExitMin(t)", "ExitMax(t)")
$summaryLines += ("{0,-12} {1,8} {2,12} {3,12}" -f "------------", "--------", "----------", "----------")

foreach ($r in $results) {
    $minStr = if ($null -ne $r.ExitMin) { "+$($r.ExitMin)t" } else { "N/A" }
    $maxStr = if ($null -ne $r.ExitMax) { "+$($r.ExitMax)t" } else { "N/A" }
    $line   = ("{0,-12} {1,8} {2,12} {3,12}" -f $r.Date, $r.Trades, $minStr, $maxStr)
    Write-Host $line
    $summaryLines += $line
}

$totalTrades = ($results | Measure-Object -Property Trades -Sum).Sum

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  OVERALL EXIT SLIPPAGE (all dates combined)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

$overallMin = if ($globalMinExit -ne [int]::MaxValue) { "+${globalMinExit}t" } else { "N/A" }
$overallMax = if ($globalMaxExit -ne [int]::MinValue) { "+${globalMaxExit}t" } else { "N/A" }

Write-Host "  Total trades with slippage data : $totalTrades"
Write-Host "  Overall exit slippage MIN        : $overallMin" -ForegroundColor Green
Write-Host "  Overall exit slippage MAX        : $overallMax" -ForegroundColor Red
Write-Host ""

$summaryLines += ""
$summaryLines += "======================================================"
$summaryLines += "OVERALL EXIT SLIPPAGE (all dates combined)"
$summaryLines += "======================================================"
$summaryLines += "Total trades with slippage data : $totalTrades"
$summaryLines += "Overall exit slippage MIN        : $overallMin"
$summaryLines += "Overall exit slippage MAX        : $overallMax"

# Save summary
if (-not (Test-Path $AnalysisBasePath)) {
    New-Item -ItemType Directory -Path $AnalysisBasePath -Force | Out-Null
}
$summaryLines | Out-File -FilePath $SummaryFile -Encoding UTF8
Write-Host "  Summary saved to: $SummaryFile" -ForegroundColor Green
Write-Host ""
