[CmdletBinding()]
Param (
  [Parameter(Mandatory=$True)][string] $CloudServiceName,
  [Parameter(Mandatory=$False)][string] $StorageAccountName,
  [Parameter(Mandatory=$True)][string] $VMSize,
  [Parameter(Mandatory=$True)][string] $vmPrefix,
  [Parameter(Mandatory=$True)][string] $ImageName,
  [Parameter(Mandatory=$False)][int] $SshPort = 50000,
  [Parameter(Mandatory=$False)][string] $gImageName = "logstashBaseline",
  [Parameter(Mandatory=$False)][string] $username = "logstash",
  [Parameter(Mandatory=$True)][string] $password,
  [Parameter(Mandatory=$False)][string] $availabilitySetName = "logstashAvailabilitySet",
  [Parameter(Mandatory=$False)][string] $vnetName,
  [Parameter(Mandatory=$False)][string] $subnetName,
  [Parameter(Mandatory=$True)][int] $numInstances,
  [Parameter(Mandatory=$False)][string] $LogstashDebianPackageUrl = "https://download.elasticsearch.org/logstash/logstash/packages/debian/logstash_1.4.2-1-2c0f5a1_all.deb",
  [Parameter(Mandatory=$True)][string] $configFile,
  [Parameter(Mandatory=$False)][string] $LogstashPluginLocation="logstash-extension",
  [switch] $NoSshKeyGeneration
)

############################
##
## VM deployment
##
############################
function setup
{
  Param (
    [Parameter(Mandatory=$True)][string] $VMName
  )
  Get-AzureVM -ServiceName $CloudServiceName -Name $VMName -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "ssh" -Protocol "tcp" -PublicPort $SshPort -LocalPort 22 -Verbose:$False -EA Stop | Update-AzureVM -Verbose:$False -EA Stop
  Install-Logstash -SshString $SshString -SshPort $SshPort -LogstashDebianPackageUrl $LogstashDebianPackageUrl -LogstashPluginLocation $LogstashPluginLocation -ConfigFileLocation $configFile
  createGeneralizedImage -SshString $SshString -sshPort $SshPort -imageName $gImageName -cloudServiceName $CloudServiceName -vmName $VMName 
}

function startup
{
  Param (
    [Parameter(Mandatory=$True)][string] $VMName
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Configure logstash to start up on boot."
  Get-AzureVM -ServiceName $CloudServiceName -Name $VMName -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "ssh" -Protocol "tcp" -PublicPort $SshPort -LocalPort 22 -Verbose:$False -EA Stop | Update-AzureVM -Verbose:$False -EA Stop
  Start-Logstash -SshString $SshString -SshPort $SshPort
  Get-AzureVM -ServiceName $CloudServiceName -Name $VMName -Verbose:$False -EA Stop | Remove-AzureEndpoint -Name "ssh" -Verbose:$False -EA Stop | Update-AzureVM -Verbose:$False -EA Stop
  Write-Host " [DONE]"
}

############################
##
## Script start up
##
############################

# Load modules and make sure we have a subscription and storage account context set
.\Init.ps1 -StorageAccountName $StorageAccountName

$vmNames = Get-VMNames -NumInstances $numInstances -VMPrefix $vmPrefix
$SshString = $username + "@" + $CloudServiceName + ".cloudapp.net"

$initialVMname = $vmPrefix + "-baseline"

$ignore = New-LinuxVM -VMName $initialVMname -VMSize $VMSize -CloudServiceName $CloudServiceName -UserName $username -Password $password -vnetName $vnetName -subnetName $subnetName -ImageName $ImageName -NoSshKeyGeneration:$NoSshKeyGeneration -setupFunction ${function:setup}

$ignore = New-LinuxVMCluster -imageName $gImageName -cloudServiceName $CloudServiceName -vmNames $vmNames -VMSize $vmSize -Username $username -Password $password -AvailabilitySetName $availabilitySetName -vnetName $vnetName -subnetName $subnetName -startupFunction ${function:startup}