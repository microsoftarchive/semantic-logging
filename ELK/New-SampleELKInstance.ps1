[CmdletBinding()]
Param (
  [Parameter(Mandatory=$False)][string] $CloudServiceName,
  [Parameter(Mandatory=$False)][string] $Location="West US",
  [Parameter(Mandatory=$False)][string] $StorageAccountName,
  [Parameter(Mandatory=$False)][string] $VMSize="Small",
  [Parameter(Mandatory=$False)][string] $VMName="elk-sample",
  [Parameter(Mandatory=$False)][string] $ImageName="b39f27a8b8c64d52b05eac6a62ebad85__Ubuntu-14_04_1-LTS-amd64-server-20141125-en-us-30GB",
  [Parameter(Mandatory=$False)][int] $SshPort = 50000,
  [Parameter(Mandatory=$False)][string] $Username = "elk",
  [Parameter(Mandatory=$False)][string] $Password = "Elk1234",
  [Parameter(Mandatory=$False)][string] $AvailabilitySetName = "elk-as",
  [Parameter(Mandatory=$False)][string] $VirtualNetworkName,
  [Parameter(Mandatory=$False)][string] $SubnetName,
  [Parameter(Mandatory=$False)][string] $ElasticSearchDebianPackageUrl = "https://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-1.5.2.deb",
  [Parameter(Mandatory=$False)][string] $ElasticSearchConfig="sample-config/elasticsearch.yml",
  [Parameter(Mandatory=$False)][string] $LogstashDebianPackageUrl = "https://download.elasticsearch.org/logstash/logstash/packages/debian/logstash_1.4.2-1-2c0f5a1_all.deb",
  [Parameter(Mandatory=$True)][string] $LogstashConfig,
  [Parameter(Mandatory=$False)][string] $LogstashPluginLocation="logstash-extension",
  [Parameter(Mandatory=$False)][string] $KibanaTarBallUrl,
  [Parameter(Mandatory=$False)][string] $KibanaConfig,
  [Parameter(Mandatory=$False)][string] $ACLRule,
  [Parameter(Mandatory=$False)][switch] $UseKibana4
)

function Setup
{
  if ($ACLRule) {
    $Acl = New-AzureAclConfig
    Set-AzureAclConfig -AddRule -ACL $Acl -Order 100 -Action permit -RemoteSubnet $ACLRule -Verbose:$False -EA Stop
    Get-AzureVM -ServiceName $CloudServiceName -Name $VMName -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "ssh" -Protocol "tcp" -PublicPort $SshPort -LocalPort 22 -ACL $Acl -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "elasticsearch" -LocalPort 9200 -PublicPort 9200 -Protocol tcp -LBSetName "elasticsearch" -DefaultProbe -ACL $Acl -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "kibana" -LocalPort 80 -PublicPort 80 -Protocol tcp -LBSetName "kibana" -DefaultProbe -ACL $Acl -Verbose:$False -EA Stop  | Update-AzureVM -Verbose:$False -EA Stop
  }
  else
  {
    Get-AzureVM -ServiceName $CloudServiceName -Name $VMName -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "ssh" -Protocol "tcp" -PublicPort $SshPort -LocalPort 22 -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "elasticsearch" -LocalPort 9200 -PublicPort 9200 -Protocol tcp -LBSetName "elasticsearch" -DefaultProbe -Verbose:$False -EA Stop | Add-AzureEndpoint -Name "kibana" -LocalPort 80 -PublicPort 80 -Protocol tcp -LBSetName "kibana" -DefaultProbe -Verbose:$False -EA Stop  | Update-AzureVM -Verbose:$False -EA Stop
  }

  # Determine the kibana config file
  if (-not $KibanaConfig)
  {
    if ($UseKibana4)
    {
      $KibanaConfig = "sample-config/kibana4.yml";
    }
    else
    {
      $KibanaConfig = "sample-config/config.js";
    }
  }

  # Install ELK
  $TmpElasticSearchConfig = $ElasticSearchConfig + ".tmp"
  (Get-Content $ElasticSearchConfig).replace("REPLACEME","http://" + $CloudServiceName.ToLower() + ".cloudapp.net") | Set-Content $TmpElasticSearchConfig
  Install-ElasticSearch -SshString $SshString -SshPort $SshPort -VMNames @($VMName) -ElasticSearchDebianPackageUrl $ElasticSearchDebianPackageUrl -ConfigFileLocation $TmpElasticSearchConfig
  Install-Logstash -SshString $SshString -SshPort $SshPort -LogstashDebianPackageUrl $LogstashDebianPackageUrl -LogstashPluginLocation $LogstashPluginLocation -ConfigFileLocation $LogstashConfig
  $TmpKibanaConfig = $KibanaConfig + ".tmp"
  (Get-Content $KibanaConfig).replace("REPLACEME","http://"+$CloudServiceName.ToLower() + ".cloudapp.net:9200") | Set-Content $TmpKibanaConfig
  Install-Kibana -SshString $SshString -SshPort $SshPort -KibanaTarBallUrl $KibanaTarBallUrl -ConfigFileLocation $TmpKibanaConfig -UseKibana4:$UseKibana4

  # Start ELK - K is only a set of static HTML/JS at the moment so it is already running
  Start-ElasticSearch -SshString $SshString -SshPort $SshPort
  Start-Logstash -SshString $SshString -SshPort $SshPort
  Start-Kibana -SshString $SshString -SshPort $SshPort -UseKibana4:$UseKibana4
}

function New-CloudService
{
  if (!$CloudServiceName)
  {
    $CloudServiceName = "elksample" + (Get-Date -format "yyMMddhhmm").ToString()
  }
  $Service = Get-AzureService -ServiceName $CloudServiceName -Verbose:$False -EA SilentlyContinue -WarningAction SilentlyContinue
  if (!$Service)
  {
    Write-Host -NoNewline (Get-Date).ToString() "- [Initialization] Creating cloud service " $CloudServiceName "."
    $Ignore = New-AzureService -ServiceName $CloudServiceName -Location $Location -Description "Sample ELK setup" -Verbose:$False -EA Stop -WarningAction SilentlyContinue
    Write-Host " [DONE]"
  }
  $CloudServiceName
}

function New-StorageAccount
{
  if (!$StorageAccountName)
  {
    $StorageAccountName = "elksample" + (Get-Date -format "yyMMddhhmm").ToString()
  }
  $Storage = Get-AzureStorageAccount -StorageAccountName $StorageAccountName -Verbose:$False -EA SilentlyContinue -WarningAction SilentlyContinue
  if (!$Storage)
  {
    $Location = (Get-AzureService -ServiceName $CloudServiceName -Verbose:$False -EA Stop -WarningAction SilentlyContinue).Location

    Write-Host -NoNewline (Get-Date).ToString() "- [Initialization] Creating storage account " $StorageAccountName "."
    $Ignore = New-AzureStorageAccount -StorageAccountName $StorageAccountName -Location $Location -Verbose:$False -EA Stop -WarningAction SilentlyContinue
    Write-Host " [DONE]"
  }
  $StorageAccountName
}

function Show-Details
{
  Write-Host "Created CloudService: "$CloudServiceName
  Write-Host "Created Storageaccount: "$StorageAccountName
  Write-Host "Connecting to the VM:"
  Write-Host "ssh -q -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString"
  Write-Host "To see your Kibana dashboard visit:"
  Write-Host $CloudServiceName".cloudapp.net"
}

############################
##
## Script start up
##
############################

# Load modules and make sure we have a subscription and storage account context set
.\Init.ps1 -StorageAccountName $StorageAccountName

$CloudServiceName = New-CloudService
$StorageAccountName = New-StorageAccount

Set-CurrentStorageAccount -StorageAccountName $StorageAccountName

$SshString = $Username + "@" + $CloudServiceName + ".cloudapp.net"

$ignore = New-LinuxVM -VMName $VMname -VMSize $VMSize -CloudServiceName $CloudServiceName -UserName $Username -Password $Password -vnetName $VirtualNetworkName -subnetName $SubnetName -ImageName $ImageName -setupFunction ${function:setup}

Show-Details
