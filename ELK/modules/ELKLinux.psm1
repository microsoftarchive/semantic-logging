###############################################################################
#
# ElasticSeatch
#
###############################################################################
function Install-ElasticSearch
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][array] $VMNames,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation,
    [Parameter(Mandatory=$True)][string] $ElasticSearchDebianPackageUrl,
    [Parameter(Mandatory=$False)][int] $NumberOfInstances,
    [Parameter(Mandatory=$False)][int] $NumberOfHardDisks,
    [Parameter(Mandatory=$False)][string] $ConfigFile,
    [Parameter(Mandatory=$False)][switch] $ComputeSharding,
    [switch] $NoMasterGeneration
  )
  Install-ElasticSearchBinaries -SshString $SshString -SshPort $SshPort -ElasticSearchDebianPackageUrl $ElasticSearchDebianPackageUrl
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Updating and deploying elasticsearch.yml with master nodes for discovery."

  $ListDisksCommand = "ls -d -1 /datadisk/**"
  $DataPaths = ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $ListDisksCommand

  $ModifiedFileLocation = Update-ElasticSearchConfiguration -ConfigFileLocation $ConfigFileLocation -VMNames $VMNames -DataPaths $DataPaths
  Update-FileDescriptorMax -SshString $SshString -SshPort $SshPort
  Update-Sharding -NumberOfInstances $NumberOfInstances -NumberOfHardDisks $NumberOfHardDisks -ConfigFileLocation $ModifiedFileLocation -ComputeSharding:$ComputeSharding
  Copy-ElasticSearchConfigToVM -SshString $SshString -SshPort $SshPort -ConfigFileLocation $ModifiedFileLocation
  Write-Host " [DONE]"
}

function Install-ElasticSearchBinaries
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $ElasticSearchDebianPackageUrl
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Installing ElasticSearch."
  $UpdateCommand = "sudo apt-get update"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $UpdateCommand}
  
  $InstallJreCommand = "sudo apt-get -f -y install default-jre"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $InstallJreCommand}
  
  $DownloadCommand = "wget " + $ElasticSearchDebianPackageUrl + " -O elasticsearch.deb"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $DownloadCommand}
  
  $InstallCommand = "sudo dpkg -i elasticsearch.deb"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $InstallCommand}

  # Configure VM
  $SubCommand = "ES_HEAP_SIZE=\\``free\ -m\|grep\ Mem\|\ awk\ \d039'{if\(\`$2/2\>31744\)\ print\ 31744" +'\\\\x22m\\\\x22' + "\;else\ print\ \`$2/2"  + '\\\\x22m\\\\x22' + "\;}'\d039\\``"
  $DefaultMemoryConfigCommand = "sudo sed -i '/#ES_HEAP_SIZE/c\\" + $SubCommand + "' /etc/init.d/elasticsearch"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $DefaultMemoryConfigCommand}
  
  $DefaultHostNameConfigCommand = 'sudo sed -i \"s/-d\ -p\ \$PID_FILE/-d\ -p\ \$PID_FILE -Des.node.name=\`hostname\`/\" /etc/init.d/elasticsearch'
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $DefaultHostNameConfigCommand}
  
  Write-Host " [DONE]"
}

function Copy-ElasticSearchConfigToVM
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  $Destination = $SshString + ":~/elasticsearch.yml"
  $ConfigFileLocation = ConvertTo-UnixPath -Path $ConfigFileLocation
  Invoke-WrappedCommand -Command {scp -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -P $SshPort $ConfigFileLocation $Destination}
  $Command = "sudo mv -f elasticsearch.yml /etc/elasticsearch/elasticsearch.yml"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $Command}
}

function Update-ElasticSearchConfiguration
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][array] $VMNames,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation,
    [Parameter(Mandatory=$False)][array] $DataPaths,
    [switch] $NoMasterGeneration
  )
  $OutLocation = $ConfigFileLocation + ".withMaster"

  # discoverablity
  Invoke-WrappedCommand -Command {sed "/discovery.zen.ping.multicast.enabled/c\discovery.zen.ping.multicast.enabled: false"  $ConfigFileLocation | Set-Content $OutLocation}
  $Names = $VMNames -join '\",\"'
  $Names = '[\"' + $Names + '\"]'
  if ($NoMasterGeneration) {
    return $OutLocation
  }
  Invoke-WrappedCommand -Command {sed -i "/discovery.zen.ping.unicast.hosts/c\discovery.zen.ping.unicast.hosts: $names" $OutLocation}

  # data-drives to use
  if ($DataPaths)
  {
    $Concat = $DataPaths -join ","
    Invoke-WrappedCommand -Command {sed -i "/path.data/c\path.data: $Concat" $OutLocation}
  }

  return $OutLocation
}

function Update-Sharding
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$False)][int] $NumberOfInstances,
    [Parameter(Mandatory=$False)][int] $NumberOfHardDisks,
    [Parameter(Mandatory=$False)][string] $ConfigFileLocation,
    [Parameter(Mandatory=$False)][switch] $ComputeShardings
  )
  # we are currently sticking to the 1 replica default setting
  if ($ComputeSharding)
  {
    if ($NumberOfHardDisks -eq 0)
    {
      $NumberOfHardDisks = 1
    }
    $FudgeFactor = 5 # TODO have something that scales with the initial size of the cluster rather than a constant
    $NumberOfPrimaryShards = $NumberOfInstances * $NumberOfHardDisks + $FudgeFactor
    Invoke-WrappedCommand -Command {sed -i "/index.number_of_shards/c\index.number_of_shards: $NumberOfPrimaryShards" $ConfigFileLocation}
  }
}

function Update-FileDescriptorMax
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort
  )
  $IncreaseFileDescriptorCommand = 'echo "vm.max_map_count=262144" | sudo tee -a /etc/sysctl.conf'
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $IncreaseFileDescriptorCommand}
  $IncreaseFileDescriptorCommand = 'sudo sysctl -w vm.max_map_count=262144'
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $IncreaseFileDescriptorCommand}
}


function Start-ElasticSearch
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort
  )
  $Command = "sudo update-rc.d elasticsearch defaults 95 10"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
  $Command = "sudo /etc/init.d/elasticsearch restart"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
}

###############################################################################
#
# Logstash
#
###############################################################################
function Install-Logstash
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $LogstashDebianPackageUrl,
    [Parameter(Mandatory=$True)][string] $LogstashPluginLocation,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  
  Install-LogstashBinaries -SshString $SshString -SshPort $SshPort -LogstashDebianPackageUrl $LogstashDebianPackageUrl
  Copy-LogstashConfigToVM -SshString $SshString -SshPort $SshPort -ConfigFileLocation $ConfigFileLocation
  Install-LogstashPlugIns -SshString $SshString -SshPort $SshPort -LogstashPluginLocation $LogstashPluginLocation
}

function Install-LogstashBinaries
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $LogstashDebianPackageUrl
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Installing logstash."
  $UpdateCommand = "sudo apt-get update"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $UpdateCommand}

  $InstallJreCommand = "sudo apt-get -f -y install default-jre"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $InstallJreCommand}

  $DownloadCommand = "wget " + $LogstashDebianPackageUrl + " -O logstash.deb"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $DownloadCommand}

  $InstallCommand = "sudo dpkg -i logstash.deb"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $InstallCommand}

  Write-Host " [DONE]"
}

function Copy-LogstashConfigToVM
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Deploying logstash.conf."
  $destination = $SshString + ":~/logstash.conf"
  $ConfigFileLocation = ConvertTo-UnixPath -Path $ConfigFileLocation
  Invoke-WrappedCommand -Command {scp -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -P $SshPort $ConfigFileLocation $destination}
  $Command = "sudo \cp -f logstash.conf /etc/logstash/conf.d/"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $Command}
  Write-Host " [DONE]"
}

function Install-LogStashPlugIns
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $LogstashPluginLocation
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Deploying logstash plugins."
  # copy custom plug-ins
  $Destination = $SshString + ":~/logstash-extension"
  $LogstashPluginLocation = ConvertTo-UnixPath -Path $LogstashPluginLocation
  Invoke-WrappedCommand -Command {scp -o ConnectTimeout=360 -r -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -P $SshPort $LogstashPluginLocation $Destination}
  $Command = "sudo \cp -rf logstash-extension/* /opt/logstash/lib/logstash"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $Command}
  $Command = "sudo \cp logstash-extension/jars/* /opt/logstash/lib"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $Command}
  # install azure ruby sdk
  $Command = 'sudo env GEM_HOME=/opt/logstash/vendor/bundle/jruby/1.9 GEM_PATH=\"\" java -jar /opt/logstash/vendor/jar/jruby-complete-1.7.11.jar -S gem install azure'
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString  $Command}
  Write-Host " [DONE]"
}

function Start-Logstash
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort
  )
  $Command = "sudo update-rc.d logstash defaults 95 10"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
  $Command = "sudo service logstash start"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
}

###############################################################################
#
# Kibana
#
###############################################################################
function Install-Kibana
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$False)][string] $KibanaTarBallUrl,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation,
    [Parameter(Mandatory=$False)][switch] $UseKibana4
  )
  if ($UseKibana4)
  {
    if (-not $KibanaTarBallUrl)
    {
        $KibanaTarBallUrl = "https://download.elasticsearch.org/kibana/kibana/kibana-4.0.0-linux-x64.tar.gz"
    }

    Install-Kibana4 -SshString $SshString -SshPort $SshPort -KibanaTarBallUrl $KibanaTarBallUrl -ConfigFileLocation $ConfigFileLocation
  }
  else
  {
    if (-not $KibanaTarBallUrl)
    {
        $KibanaTarBallUrl = "https://download.elasticsearch.org/kibana/kibana/kibana-3.1.2.tar.gz"
    }
   
    Install-Kibana3 -SshString $SshString -SshPort $SshPort -KibanaTarBallUrl $KibanaTarBallUrl -ConfigFileLocation $ConfigFileLocation
  }
}

function Install-Kibana3
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $KibanaTarBallUrl,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  Install-Kibana3Binaries -SshString $SshString -SshPort $SshPort -KibanaTarBallUrl $KibanaTarBallUrl
  Copy-Kibana3ConfigToVM -SshString $SshString -SshPort $SshPort -ConfigFileLocation $ConfigFileLocation
}

function Install-Kibana3Binaries
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $KibanaTarBallUrl
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Installing Kibana."
  $UpdateCommand = "sudo apt-get update"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $UpdateCommand}
  
  $InstallJreCommand = "sudo apt-get -f -y install default-jre"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $InstallJreCommand}
  
  $InstallApacheCommand = "sudo apt-get -f -y install apache2"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $InstallApacheCommand}
  
  $DownloadCommand = "wget " + $KibanaTarBallUrl + " -O kibana.tar.gz"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $DownloadCommand}
  
  $InstallCommand = "sudo tar -xf kibana.tar.gz -C /var/www/html/ --strip 1"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $InstallCommand}
  Write-Host " [DONE]"
}

function Copy-Kibana3ConfigToVM
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Deploying Kibana config."
  $Destination = $SshString + ":~/config.js"
  $ConfigFileLocation = ConvertTo-UnixPath -Path $ConfigFileLocation
  Invoke-WrappedCommand -Command {scp -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -P $SshPort $ConfigFileLocation $Destination}
  $Command = "sudo \cp config.js /var/www/html/"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
  Write-Host " [DONE]"
}

function Install-Kibana4
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $KibanaTarBallUrl,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  Install-Kibana4Binaries -SshString $SshString -SshPort $SshPort -KibanaTarBallUrl $KibanaTarBallUrl
  Copy-Kibana4ConfigToVM -SshString $SshString -SshPort $SshPort -ConfigFileLocation $ConfigFileLocation
}

function Install-Kibana4Binaries
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $KibanaTarBallUrl
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Installing Kibana."
  $UpdateCommand = "sudo apt-get update"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $UpdateCommand}

  $InstallJreCommand = "sudo apt-get -f -y install default-jre"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $InstallJreCommand}

  $DownloadCommand = "wget " + $KibanaTarBallUrl + " -O kibana.tar.gz"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $DownloadCommand}

  $CreateDirectoryCommand = "mkdir kibana"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $CreateDirectoryCommand}

  $InstallCommand = "sudo tar -xf kibana.tar.gz -C kibana --strip-components=1"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $sshString $InstallCommand}

  Write-Host " [DONE]"
}

function Copy-Kibana4ConfigToVM
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$True)][string] $ConfigFileLocation
  )
  Write-Host -NoNewline (Get-Date).ToString() "- [Configuration] Deploying Kibana config."
  $Destination = $SshString + ":~/kibana.yml"
  $ConfigFileLocation = ConvertTo-UnixPath -Path $ConfigFileLocation
  Invoke-WrappedCommand -Command {scp -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -P $SshPort $ConfigFileLocation $Destination}
  $Command = "sudo \cp kibana.yml kibana/config/"
  Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
  Write-Host " [DONE]"
}

function Start-Kibana
{
  [CmdletBinding()]
  Param (
    [Parameter(Mandatory=$True)][string] $SshString,
    [Parameter(Mandatory=$True)][int] $SshPort,
    [Parameter(Mandatory=$False)][switch] $UseKibana4
  )
  if ($UseKibana4)
  {
    $Command = 'screen -d -m sh -c \"while :; do sudo kibana/bin/kibana; done;\"'
    Invoke-WrappedCommand -Command {ssh -o ConnectTimeout=360 -o StrictHostKeychecking=no -o UserKnownHostsFile=NUL -i id_rsa -p $SshPort $SshString $Command}
  }
}