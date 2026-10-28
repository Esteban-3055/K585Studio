param([Parameter(Mandatory=$true)][string]$Installer,[Parameter(Mandatory=$true)][string]$TestDirectory)
$ErrorActionPreference='Stop'
$installerAbsolute=(Resolve-Path -LiteralPath $Installer).Path
$testAbsolute=[IO.Path]::GetFullPath($TestDirectory)
if(Test-Path -LiteralPath $testAbsolute){throw 'Installation test directory must be absent'}
$privateRoot=Join-Path $env:LOCALAPPDATA 'K585Studio'
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
$sentinel=Join-Path $privateRoot ('install-test-'+[guid]::NewGuid().ToString('N')+'.txt')
[IO.File]::WriteAllText($sentinel,'Installer must preserve private app data')
$sentinelHash=(Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
foreach($phase in @('install','upgrade')){
  $setup=Start-Process -FilePath $installerAbsolute -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/NOAUTOLAUNCH=1',('/DIR="'+$testAbsolute+'"') -WindowStyle Hidden -PassThru -Wait
  if($setup.ExitCode -ne 0){throw "$phase failed: $($setup.ExitCode)"}
  $installedApp=Join-Path $testAbsolute 'K585Studio.exe'
  if(-not(Test-Path -LiteralPath $installedApp)){throw 'Installed executable missing'}
  $renderDirectory=Join-Path $testAbsolute ('checks-'+$phase)
  $smoke=Start-Process -FilePath $installedApp -ArgumentList '--ui-self-test',('"'+$renderDirectory+'"') -WindowStyle Hidden -PassThru -Wait
  if($smoke.ExitCode -ne 0){Get-Content -LiteralPath (Join-Path $renderDirectory 'ui-error.txt');throw 'Installed UI checks failed'}
  if((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash){throw 'Private data changed during install/upgrade'}
}
$uninstaller=Join-Path $testAbsolute 'unins000.exe'
$uninstall=Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -PassThru -Wait
if($uninstall.ExitCode -ne 0){throw 'Uninstall failed'}
if((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash){throw 'Uninstall removed private data'}
Remove-Item -LiteralPath $sentinel
Write-Output 'PASS: install, installed UI, in-place upgrade, uninstall, private data preserved.'
