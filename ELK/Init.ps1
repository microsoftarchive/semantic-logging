[CmdletBinding()]
Param (
  [Parameter(Mandatory=$False)][string] $StorageAccountName
)

############################
##
## Helper functions
##
############################
function Load-Module
{
  Param (
    $ModuleName,
    $ModuleLocation
  )
  if (Get-Module -Name $ModuleName -Verbose:$False -EA Stop)
  {
    Remove-Module -Name $ModuleName -Verbose:$False -EA Stop
  }
  $QualifiedModuleName = $ModuleLocation + "\" + $ModuleName
  $Ignore = Import-Module -Name $QualifiedModuleName -PassThru -Verbose:$False -EA Stop
}

##
# script initialization
##
Load-Module -ModuleName deployment -ModuleLocation .\modules
Load-Module -ModuleName ELKLinux -ModuleLocation .\modules
Load-Module -ModuleName Utilities -ModuleLocation .\modules

Assert-AzureModuleIsInstalled
Assert-LinuxCommandsAreAvailable

Set-CurrentStorageAccount -StorageAccountName $StorageAccountName

if (-not $ENV:OPENSSL_CONF -or -not [System.IO.File]::Exists($ENV:OPENSSL_CONF))
{
  throw "OPENSSL_CONF environment variable is not set to a valid openssl.cnf file. Please make sure it's set to the path of an existing openssl.conf file"
}
