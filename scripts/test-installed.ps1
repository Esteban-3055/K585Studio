param([Parameter(Mandatory=$true)][string]$Installer,[Parameter(Mandatory=$true)][string]$TestDirectory,[string]$PreviousInstaller)
$ErrorActionPreference='Stop'
$installerAbsolute=(Resolve-Path -LiteralPath $Installer).Path
$testAbsolute=[IO.Path]::GetFullPath($TestDirectory)
if(Test-Path -LiteralPath $testAbsolute){throw 'Installation test directory must be absent'}
$privateRoot=Join-Path $env:LOCALAPPDATA 'K585Studio'
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
$sentinel=Join-Path $privateRoot ('install-test-'+[guid]::NewGuid().ToString('N')+'.txt')
[IO.File]::WriteAllText($sentinel,'Installer must preserve private app data')
$sentinelHash=(Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
if($PreviousInstaller){$firstInstaller=(Resolve-Path -LiteralPath $PreviousInstaller).Path}else{$firstInstaller=$installerAbsolute}
$phases=@(@{Name='install';Installer=$firstInstaller},@{Name='upgrade';Installer=$installerAbsolute})
foreach($item in $phases){
  $phase=$item.Name
  $setup=Start-Process -FilePath $item.Installer -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/NOAUTOLAUNCH=1',('/DIR="'+$testAbsolute+'"') -WindowStyle Hidden -PassThru -Wait
  if($setup.ExitCode -ne 0){throw "$phase failed: $($setup.ExitCode)"}
  $installedApp=Join-Path $testAbsolute 'K585Studio.exe'
  if(-not(Test-Path -LiteralPath $installedApp)){throw 'Installed executable missing'}
  if($phase -eq 'upgrade'){
    [xml]$versionProps=Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Directory.Build.props') -Raw
    $expectedVersion=[version]($versionProps.Project.PropertyGroup.Version+'.0')
    if([version](Get-Item -LiteralPath $installedApp).VersionInfo.FileVersion -ne $expectedVersion){throw 'Installed upgrade has the wrong file version'}
  }
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
