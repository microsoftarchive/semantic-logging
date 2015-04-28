[CmdletBinding()]
Param (
  [Parameter(Mandatory=$True)][string] $CloudServiceName,
  [Parameter(Mandatory=$False)][string] $StorageAccountName,
  [Parameter(Mandatory=$True)][string] $VMSize,
  [Parameter(Mandatory=$True)][string] $vmPrefix,
  [Parameter(Mandatory=$True)][string] $ImageName,
  [Parameter(Mandatory=$False)][int] $SshPort = 50000,
  [Parameter(Mandatory=$False)][string] $gImageName = "kibanaBaseline",
  [Parameter(Mandatory=$False)][int] $localPort = 80,
  [Parameter(Mandatory=$False)][int] $lbPort = 80,
  [Parameter(Mandatory=$False)][string] $lbName = "myKibanaLB",
  [Parameter(Mandatory=$False)][string] $username = "kibana",
  [Parameter(Mandatory=$True)][string] $password,
  [Parameter(Mandatory=$False)][string] $availabilitySetName = "kibanaAvailabilitySet",
  [Parameter(Mandatory=$False)][string] $vnetName,
  [Parameter(Mandatory=$False)][string] $subnetName,
  [Parameter(Mandatory=$True)][int] $numInstances,
  [Parameter(Mandatory=$False)][string] $KibanaTarBallUrl,
  [Parameter(Mandatory=$True)][string] $configFile,
  [switch] $NoSshKeyGeneration,
  [Parameter(Mandatory=$False)][switch] $UseKibana4
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
  Install-Kibana -SshString $sshString -SshPort $SshPort -KibanaTarBallUrl $KibanaTarBallUrl -ConfigFileLocation $configFile -UseKibana4:$UseKibana4
  createGeneralizedImage -sshString $sshString -sshPort $SshPort -imageName $gImageName -cloudServiceName $CloudServiceName -vmName $VMName 
}

function startup
{
  Param (
    [Parameter(Mandatory=$True)][string] $VMName
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Configure Kibana Loadbalancer."
  Get-AzureVM -ServiceName $CloudServiceName -Name $VMName -Verbose:$False -EA Stop | Add-AzureEndpoint -Name $lbName -LocalPort $localPort -PublicPort $lbPort -Protocol tcp -LBSetName $lbName -DefaultProbe -Verbose:$False -EA Stop | Update-AzureVM -Verbose:$False -EA Stop  
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
$sshString = $username + "@" + $CloudServiceName + ".cloudapp.net"

$initialVMname = $vmPrefix + "-baseline"

$ignore = New-LinuxVM -VMName $initialVMname -VMSize $VMSize -CloudServiceName $CloudServiceName -UserName $username -Password $password -vnetName $vnetName -subnetName $subnetName -ImageName $ImageName -NoSshKeyGeneration:$NoSshKeyGeneration -setupFunction ${function:setup}

$ignore = New-LinuxVMCluster -imageName $gImageName -cloudServiceName $CloudServiceName -vmNames $vmNames -VMSize $vmSize -Username $username -Password $password -AvailabilitySetName $availabilitySetName -vnetName $vnetName -subnetName $subnetName -startupFunction ${function:startup}