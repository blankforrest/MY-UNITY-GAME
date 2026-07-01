$unityDir = "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Data"
$cscPath = Join-Path $unityDir "MonoBleedingEdge\lib\mono\4.5\csc.exe"
$monoPath = Join-Path $unityDir "MonoBleedingEdge\bin\mono.exe"

Write-Host "Using csc: $cscPath"
Write-Host "Using mono: $monoPath"

if (-not (Test-Path "Assembly-CSharp.csproj")) {
    Write-Error "Assembly-CSharp.csproj not found!"
    exit 1
}

[xml]$proj = Get-Content -Path "Assembly-CSharp.csproj"
$uniqueRefs = @()

foreach ($ref in $proj.Project.ItemGroup.Reference) {
    if ($ref.HintPath) {
        $absPath = [System.IO.Path]::GetFullPath($ref.HintPath)
        if (Test-Path $absPath) {
            $uniqueRefs += $absPath
        }
    }
}

$csFiles = Get-ChildItem -Path "Assets\Scripts" -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

# Prepare response file lines
$rspLines = @("/target:library", "/out:TempAssembly.dll", "/noconfig", "/nostdlib+")

$defines = $proj.Project.PropertyGroup.DefineConstants
$validDefines = @()
if ($defines) {
    foreach ($d in $defines) {
        if ($d -and $d.Trim() -ne "") {
            $validDefines += $d.Trim()
        }
    }
}

if ($validDefines.Count -gt 0) {
    $defStr = $validDefines -join ";"
    $rspLines += "/define:$defStr"
} else {
    $rspLines += "/define:UNITY_6000_4_6;UNITY_6000_4;UNITY_6000;UNITY_EDITOR;UNITY_STANDALONE_WIN;NETSTANDARD;NETSTANDARD2_1"
}

$allowUnsafe = $proj.Project.PropertyGroup.AllowUnsafeBlocks
$hasUnsafe = $false
if ($allowUnsafe) {
    foreach ($au in $allowUnsafe) {
        if ($au -eq "True" -or $au -eq "true") {
            $hasUnsafe = $true
        }
    }
}
if ($hasUnsafe) {
    $rspLines += "/allowunsafe+"
}

foreach ($ref in $uniqueRefs) {
    $rspLines += "/reference:`"$ref`""
}

foreach ($file in $csFiles) {
    $rspLines += "`"$file`""
}

$rspPath = Join-Path $pwd "csc_args.rsp"
$rspLines | Out-File -FilePath $rspPath -Encoding UTF8

Write-Host "Running csc..."
& $monoPath $cscPath "@$rspPath" > csc_out.txt 2> csc_err.txt
$exitCode = $LASTEXITCODE
Write-Host "csc exited with code $exitCode"

if (Test-Path "csc_out.txt") {
    $outLines = Get-Content -Path "csc_out.txt"
    Write-Host "--- Raw csc_out.txt (First 50 lines) ---"
    $outLines | Select-Object -First 50 | Write-Host
    
    $totalErrors = $outLines | Where-Object { $_ -like "*: error CS*" }
    Write-Host "Total errors found: $($totalErrors.Count)"
}
if (Test-Path "csc_err.txt") {
    $errLines = Get-Content -Path "csc_err.txt"
    Write-Host "--- Raw csc_err.txt (First 50 lines) ---"
    $errLines | Select-Object -First 50 | Write-Host
}

# Clean up
if (Test-Path "TempAssembly.dll") { Remove-Item "TempAssembly.dll" -ErrorAction SilentlyContinue }
if (Test-Path $rspPath) { Remove-Item $rspPath -ErrorAction SilentlyContinue }
