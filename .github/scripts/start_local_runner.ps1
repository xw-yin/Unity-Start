param([string]$RunnerPath = 'F:\Unity\Tools\iOSCI\Runner')
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath (Join-Path $RunnerPath 'config.cmd'))) { throw 'Install the GitHub Actions Windows x64 runner first.' }
if (!(Test-Path -LiteralPath (Join-Path $RunnerPath '.runner'))) {
    $registration = gh api --method POST repos/xw-yin/Unity-Start/actions/runners/registration-token | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$registration.token) { throw 'Cannot register this runner. Check GitHub CLI access.' }
    & (Join-Path $RunnerPath 'config.cmd') --unattended --url https://github.com/xw-yin/Unity-Start --token $registration.token --name Xinwang-Unity-iOS --work _work --labels unity-homecoming --ephemeral
    if ($LASTEXITCODE -ne 0) { throw 'Runner registration failed.' }
}
Start-Process -FilePath (Join-Path $RunnerPath 'run.cmd') -WorkingDirectory $RunnerPath -WindowStyle Hidden -RedirectStandardOutput (Join-Path $RunnerPath 'runner-output.log') -RedirectStandardError (Join-Path $RunnerPath 'runner-error.log')
Write-Output 'Local runner started. It exits and unregisters after one export job.'
