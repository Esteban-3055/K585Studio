param([Parameter(Mandatory=$true)][string]$Installer,[Parameter(Mandatory=$true)][string]$TestDirectory,[string]$PreviousInstaller)
$ErrorActionPreference='Stop'
function Invoke-TestProcess([string]$File,[string[]]$Arguments,[string]$Stage,[int]$TimeoutMilliseconds=90000){
  # Use CreateProcess directly: PowerShell's Start-Process can itself wait in
  # ShellExecute before returning a Process, outside our timeout.
  $start=[Diagnostics.ProcessStartInfo]::new()
  $start.FileName=$File
  $start.Arguments=[string]::Join(' ',$Arguments)
  $start.UseShellExecute=$false
  $start.CreateNoWindow=$true
  $start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
  $process=[Diagnostics.Process]::new()
  $process.StartInfo=$start
  Write-Host "$Stage launching: $File"
  if(-not $process.Start()){throw "$Stage could not start"}
  Write-Host "$Stage process started: $($process.Id)"
  # Start-Process -Wait also waits for descendants. Wait only for the exact
  # installer/app process, whose exit code and outputs are checked below.
  if(-not $process.WaitForExit($TimeoutMilliseconds)){
    try{$process.Kill();$null=$process.WaitForExit(10000)}catch{Write-Warning $_.Exception.Message}
    foreach($log in Get-ChildItem -LiteralPath $script:logRoot -Filter '*.log' -ErrorAction SilentlyContinue){
      try{Get-Content -LiteralPath $log.FullName -Tail 60 | Write-Host}catch{Write-Warning $_.Exception.Message}
    }
    throw "$Stage exceeded $TimeoutMilliseconds ms"
  }
  $code=$process.ExitCode;$process.Dispose();return $code
}
$installerAbsolute=(Resolve-Path -LiteralPath $Installer).Path
$testAbsolute=[IO.Path]::GetFullPath($TestDirectory)
if(Test-Path -LiteralPath $testAbsolute){throw 'Installation test directory must be absent'}
$script:logRoot=$testAbsolute+'-logs'
New-Item -ItemType Directory -Path $script:logRoot -Force | Out-Null
$privateRoot=Join-Path $env:LOCALAPPDATA 'K585Studio'
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
$sentinel=Join-Path $privateRoot ('install-test-'+[guid]::NewGuid().ToString('N')+'.txt')
[IO.File]::WriteAllText($sentinel,'Installer must preserve private app data')
$sentinelHash=(Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
if($PreviousInstaller){$firstInstaller=(Resolve-Path -LiteralPath $PreviousInstaller).Path}else{$firstInstaller=$installerAbsolute}
$phases=@(@{Name='install';Installer=$firstInstaller},@{Name='upgrade';Installer=$installerAbsolute})
foreach($item in $phases){
  $phase=$item.Name
  Write-Output "Starting $phase using $($item.Installer)"
  $setupCode=Invoke-TestProcess -File $item.Installer -Arguments @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/NOAUTOLAUNCH=1',('/DIR="'+$testAbsolute+'"'),('/LOG="'+(Join-Path $script:logRoot ($phase+'.log'))+'"')) -Stage $phase
  if($setupCode -ne 0){throw "$phase failed: $setupCode"}
  $installedApp=Join-Path $testAbsolute 'K585Studio.exe'
  if(-not(Test-Path -LiteralPath $installedApp)){throw 'Installed executable missing'}
  if($phase -eq 'upgrade'){
    [xml]$versionProps=Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Directory.Build.props') -Raw
    $expectedVersion=[version]($versionProps.Project.PropertyGroup.Version+'.0')
    if([version](Get-Item -LiteralPath $installedApp).VersionInfo.FileVersion -ne $expectedVersion){throw 'Installed upgrade has the wrong file version'}
  }
  $renderDirectory=Join-Path $testAbsolute ('checks-'+$phase)
  Write-Output "Starting installed UI check after $phase"
  $smokeCode=Invoke-TestProcess -File $installedApp -Arguments @('--ui-self-test',('"'+$renderDirectory+'"')) -Stage ('UI-'+$phase)
  if($smokeCode -ne 0){Get-Content -LiteralPath (Join-Path $renderDirectory 'ui-error.txt');throw 'Installed UI checks failed'}
  if((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash){throw 'Private data changed during install/upgrade'}
  Write-Output "Completed $phase and installed UI check"
}
$uninstaller=Join-Path $testAbsolute 'unins000.exe'
Write-Output 'Starting uninstall check'
$uninstallCode=Invoke-TestProcess -File $uninstaller -Arguments @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $script:logRoot 'uninstall.log')+'"')) -Stage 'uninstall'
if($uninstallCode -ne 0){throw 'Uninstall failed'}
if((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash){throw 'Uninstall removed private data'}
Remove-Item -LiteralPath $sentinel
Write-Output 'PASS: install, installed UI, in-place upgrade, uninstall, private data preserved.'
