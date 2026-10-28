param([Parameter(Mandatory=$true)][string]$PublishDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repoDirectory=Split-Path -Parent $PSScriptRoot
[xml]$properties=Get-Content -LiteralPath (Join-Path $repoDirectory 'Directory.Build.props') -Raw
$appVersion=$properties.Project.PropertyGroup.Version
$publishAbsolute=[IO.Path]::GetFullPath($PublishDirectory)
$outputAbsolute=[IO.Path]::GetFullPath($OutputDirectory)
if(-not (Test-Path -LiteralPath (Join-Path $publishAbsolute 'K585Studio.exe'))){throw 'Publish output is missing'}
New-Item -ItemType Directory -Force -Path $outputAbsolute | Out-Null
$compilerCommand=Get-Command ISCC.exe -ErrorAction SilentlyContinue
$compilerPath=if($compilerCommand){$compilerCommand.Source}else{'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'}
if(-not(Test-Path -LiteralPath $compilerPath)){throw 'Inno Setup compiler is required; the GitHub build prepares it on its runner.'}
& $compilerPath ('/DAppVersion='+$appVersion) ('/DPublishDir='+$publishAbsolute) ('/DInstallerOutput='+$outputAbsolute) (Join-Path $repoDirectory 'installer\K585Studio.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$setupPath=Join-Path $outputAbsolute ('K585Studio-'+$appVersion+'-Setup-win-x64.exe')
$setupHash=(Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$setupHash  $([IO.Path]::GetFileName($setupPath))" | Set-Content -LiteralPath ($setupPath+'.sha256') -Encoding ascii
Write-Output $setupPath
