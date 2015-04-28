[CmdletBinding()]
Param (
  [Parameter(Mandatory=$True)][string] $StorageAccountName,
  [Parameter(Mandatory=$False)][string] $TableName,
  [Parameter(Mandatory=$False)][string] $ContainerName
)

function Update-LogstashConfigWithStorage
{
  $fileContent = Get-Content "./logstash.conf"
  $fileContent = $fileContent.Replace("STORAGE ACCOUNT NAME", $StorageAccountName)
  $fileContent = $fileContent.Replace("STORAGE ACCESS KEY", (Get-AzureStorageKey -StorageAccountName $StorageAccountName -Verbose:$False -EA Stop).Primary)
  $fileContent = $fileContent.Replace("TABLE NAME", $TableName)
  $fileContent = $fileContent.Replace("CONTAINER NAME", $ContainerName)

  Set-Content "./logstash.conf" $fileContent
}

############################
##
## Script start up
##
############################

# See if we are creating a config file for using tables or blobs
if ($TableName)
{
  copy "sample-config/azuretable.conf" "./logstash.conf"
  Update-LogstashConfigWithStorage
}
else
{
  if ($ContainerName)
  {
    copy "sample-config/azureblob.conf" "./logstash.conf"
    Update-LogstashConfigWithStorage
  }
  else
  {
    throw "Please specify either -TableName or -ContainerName for populating the config file"
  }
}
