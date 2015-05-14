# This module contains utility functions used accross various setup scripts
function Assert-AzureModuleIsInstalled
{
  if ((Get-Module -ListAvailable Azure  -Verbose:$False -EA Stop) -eq $null) 
  { 
    throw "Windows Azure Powershell not found! Please install from http://www.windowsazure.com/en-us/downloads/#cmd-line-tools" 
  }
}

function Assert-LinuxCommandsAreAvailable
{
  if ( (Get-Command ssh -errorAction SilentlyContinue) ) {
    return
  } else {
    throw "The following command cannot be found on ENV:PATH : ssh"
  }
  
  if ( (Get-Command scp -errorAction SilentlyContinue) ) {
    return
  } else {
    throw "The following command cannot be found on ENV:PATH : scp"
  }
  
  if ( (Get-Command openssl -errorAction SilentlyContinue) ) {
    return
  } else {
    throw "The following command cannot be found on ENV:PATH : openssl"
  }
  
  if ( (Get-Command sed -errorAction SilentlyContinue) ) {
    return
  } else {
    throw "The following command cannot be found on ENV:PATH : sed"
  }
}

function Set-CurrentStorageAccount
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$False)][string] $StorageAccountName
  )

  if ($StorageAccountName) {
    $SubscriptionId = (Get-AzureSubscription -Current).SubscriptionId
    Set-AzureSubscription -SubscriptionId $SubscriptionId -CurrentStorageAccountName $StorageAccountName -Verbose:$False -EA Stop
  }
}

function ConvertTo-UnixPath
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $Path
  )
  return ($Path -replace '\\','/') -replace '(.):','/$1'
}

function Invoke-WrappedCommand
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][scriptblock] $Command
  )
  & $Command
  if ($LastExitCode -ne 0) {
    throw "Exec: Failed to run $Command" 
  }
}
