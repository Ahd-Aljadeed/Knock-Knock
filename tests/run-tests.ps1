# Replays synthetic recordings through the real detector code and checks what fires.
#   .\tests\run-tests.ps1        (needs Node.js to generate the test audio)
# The detector is compiled in-process, so no test .exe is needed (and Smart App Control can't block it).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Add-Type -Path "$root\src\Detector.cs", "$root\src\TestMode.cs", "$root\src\Native.cs" -ReferencedAssemblies System.Core
node "$PSScriptRoot\make-audio.mjs" | Out-Null
if ($LASTEXITCODE) { throw 'Could not generate test audio' }

$failed = 0
function Check($name, $wav, [string[]]$exeArgs, [string[]]$expect) {
    $out = [KnockKnock.TestMode]::Analyze([string[]](@('--test', "$PSScriptRoot\out\$wav") + $exeArgs)) | Where-Object { $_ -match '^(FIRED|REJECTED|IGNORED|LEARNED|TOTAL)' }
    $got = @($out | ForEach-Object { ($_ -split ' at | \d+ms')[0].Trim() })
    $ok = ($got -join '|') -eq ($expect -join '|')
    if ($ok) { Write-Host "  PASS  $name" -ForegroundColor Green }
    else {
        $script:failed++
        Write-Host "  FAIL  $name" -ForegroundColor Red
        Write-Host "        expected: $($expect -join ' | ')"
        Write-Host "        got:      $($got -join ' | ')"
        $out | ForEach-Object { Write-Host "        > $_" }
    }
}

Write-Host 'Knock detection tests'
Check 'single knock, talking and slow knocks are ignored; doubles fire' 'basic.wav' @('2') @('FIRED 2', 'FIRED 2', 'TOTAL 2')
Check 'counts 2, 3 and 4 knocks'                                       'counts.wav' @('4') @('FIRED 2', 'FIRED 3', 'FIRED 4', 'TOTAL 3')
Check 'one-screen mode fires on the 2nd knock'                         'counts.wav' @('2') @('FIRED 2', 'FIRED 2', 'FIRED 2', 'TOTAL 3')
Check 'rhythm check rejects uneven spacing and strength'               'rhythm.wav' @('4') @('REJECTED 3', 'REJECTED 2', 'TOTAL 0')
Check 'learned knock accepts knocks and ignores chair bumps'           'profile.wav' @('2', '--learn', '6') @('LEARNED great', 'FIRED 2', 'IGNORED', 'IGNORED', 'TOTAL 1')

if ($failed) { Write-Host "$failed test(s) failed" -ForegroundColor Red; exit 1 }
Write-Host 'All tests passed' -ForegroundColor Green
