function New-LinuxVM
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $VMName,
    [Parameter(Mandatory=$True)][string] $VMSize,
    [Parameter(Mandatory=$True)][string] $CloudServiceName,
    [Parameter(Mandatory=$True)][string] $UserName,
    [Parameter(Mandatory=$True)][string] $Password,
    [Parameter(Mandatory=$False)][string] $vnetName,
    [Parameter(Mandatory=$False)][string] $subnetName,
    [Parameter(Mandatory=$True)][string] $ImageName,
    [Parameter(Mandatory=$True)][scriptblock] $setupFunction,
    [switch] $NoSshKeyGeneration
  )
  Write-Host (Get-Date).ToString() "- [Initialization] Building the base line image."

  if (-Not $NoSshKeyGeneration) {
    Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Creating Certificate."

    Invoke-WrappedCommand -Command {openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout id_rsa -out myCert.pem -subj "/C=US/ST=NoWhere/L=NoWhere/O=Dis/CN=www.deployment.com" 2> $null}
    Invoke-WrappedCommand -Command {openssl x509 -outform der -in myCert.pem -out myCert.cer}
    
    Add-AzureCertificate -ServiceName $CloudServiceName -CertToDeploy myCert.cer -Verbose:$False -EA Stop
    Write-Host " [DONE]"
  } else {
    Write-Host (Get-Date).ToString() "- [Configuration] Skipping certificate generation."
  }
  $fingerprint = openssl x509 -in myCert.pem -fingerprint -noout | sed "s/://g" | sed "s/SHA1\ Fingerprint\=//g"
  if ($LastExitCode -ne 0)
  {
     throw "Unable to get the certificate thumbprint for the SSH cert"
  }

  $Path = "/home/"+$UserName+"/.ssh/authorized_keys"
  $sshKey = New-AzureSSHKey -PublicKey -Fingerprint $fingerprint  -Path $Path

  # create initial VM
  $vmConfig = New-AzureVMConfig -Name $VMName -InstanceSize $VMSize -ImageName $ImageName -Verbose:$False -EA Stop | Add-AzureProvisioningConfig -Linux -NoSSHEndpoint -LinuxUser $UserName -Password $Password -SSHPublicKeys $sshKey -Verbose:$False -EA Stop

  Write-Host -NoNewline (Get-Date).ToString() "- [Provisioning]" $VMName
  if ($vnetName) {
    $vmConfig | Set-AzureSubnet -SubnetNames $subnetName
    $VM = New-AzureVM -ServiceName $CloudServiceName -VMs $VMConfig -WaitForBoot -VNetName $vnetName -Verbose:$False -EA Stop -WarningAction SilentlyContinue
  } else {
    $VM = New-AzureVM -ServiceName $CloudServiceName -VMs $VMConfig -WaitForBoot -Verbose:$False -EA Stop -WarningAction SilentlyContinue
  }
  Write-Host " [DONE]"

  $setupFunction.Invoke($VMName)
}


function New-LinuxVMCluster
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $imageName,
    [Parameter(Mandatory=$True)][string] $cloudServiceName,
    [Parameter(Mandatory=$True)][array] $vmNames,
    [Parameter(Mandatory=$True)][string] $VMSize,
    [Parameter(Mandatory=$True)][string] $UserName,
    [Parameter(Mandatory=$True)][string] $Password,
    [Parameter(Mandatory=$True)][string] $AvailabilitySetName,
    [Parameter(Mandatory=$False)][string] $vnetName,
    [Parameter(Mandatory=$False)][string] $subnetName,
    [Parameter(Mandatory=$True)][scriptblock] $startupFunction
  )
  Write-Host (Get-Date).ToString() "- Deploying cluster."
  Foreach ($vmName in $vmNames) {
    Write-Host -NoNewline (Get-Date).ToString() "- [Provisioning]" $VMName
    $VMConfig = New-AzureVMConfig -Name $VMName -InstanceSize $VMSize -ImageName $ImageName -AvailabilitySetName $AvailabilitySetName -Verbose:$False -EA Stop | Add-AzureProvisioningConfig -Linux -NoSSHEndpoint -LinuxUser $UserName -Password $Password -Verbose:$False -EA Stop
    if ($vnetName) {
      $VMConfig = $VMConfig | Set-AzureSubnet -SubnetNames $subnetName
      $VM = New-AzureVM -ServiceName $CloudServiceName -VMs $VMConfig -WaitForBoot -VNetName $vnetName -Verbose:$False -EA Stop -WarningAction SilentlyContinue
    } else {
      $VM = New-AzureVM -ServiceName $CloudServiceName -VMs $VMConfig -WaitForBoot -Verbose:$False -EA Stop -WarningAction SilentlyContinue
    }
    Write-Host " [DONE]"

    $startupFunction.Invoke($vmName)
  }

  Write-Host (Get-Date).ToString() "- Deploying cluster. [DONE]"
}


function createGeneralizedImage
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $sshString,
    [Parameter(Mandatory=$True)][int] $sshPort,
    [Parameter(Mandatory=$True)][string] $imageName,
    [Parameter(Mandatory=$True)][string] $cloudServiceName,
    [Parameter(Mandatory=$True)][string] $vmName
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Imaging] Creating generalized image. ["$imageName" ]"
  $command = "sudo waagent -force -deprovision"
  Invoke-WrappedCommand -Command {ssh -q -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $sshPort $sshString $command}
  
  Get-AzureVM -ServiceName $cloudServiceName -Name $vmName -Verbose:$False -EA Stop | Remove-AzureEndpoint -Name "ssh" -Verbose:$False -EA Stop | Update-AzureVM -Verbose:$False -EA Stop  
  Stop-AzureVM -ServiceName $cloudServiceName -Name $vmName -StayProvisioned -Verbose:$False -EA Stop
  Save-AzureVMImage -ServiceName $cloudServiceName -Name $vmName -OSState "Generalized" -ImageName $imageName -ImageLabel $imageName -Verbose:$False -EA Stop
  Write-Host " [DONE]"
}


function Get-VMNames
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][int] $NumInstances,
    [Parameter(Mandatory=$True)][string] $VMPrefix
  )
  $Names = @()
  for ($i=0; $i -lt $NumInstances; $i++) {
    $PaddedNumber = "{0:D3}" -f $i
    $Name = $VMPrefix + "-" + $PaddedNumber
    $Names = $Names + $Name
  }
  return $Names
}