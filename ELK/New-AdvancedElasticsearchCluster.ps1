[CmdletBinding()]
Param (
  [Parameter(Mandatory=$True)][string] $CloudServiceName,
  [Parameter(Mandatory=$False)][string] $StorageAccountName,
  [Parameter(Mandatory=$True)][string] $VMSizeMaster,
  [Parameter(Mandatory=$True)][string] $VMSizeIndex,
  [Parameter(Mandatory=$True)][string] $VMSizeQuery,
  [Parameter(Mandatory=$True)][string] $vmPrefix,
  [Parameter(Mandatory=$True)][string] $ImageName,
  [Parameter(Mandatory=$False)][int] $SshPort = 50000,
  [Parameter(Mandatory=$False)][string] $gImagePrefix = "elasticSearchBaseline",
  [Parameter(Mandatory=$False)][int] $localPort = 9200,
  [Parameter(Mandatory=$False)][int] $lbMasterPort = 9400,
  [Parameter(Mandatory=$False)][int] $lbIndexPort = 9300,
  [Parameter(Mandatory=$False)][int] $lbQueryPort = 9200,
  [Parameter(Mandatory=$False)][string] $lbNamePrefix = "myElasticSearchLB",
  [Parameter(Mandatory=$False)][string] $username = "elasticSearch",
  [Parameter(Mandatory=$True)][string] $password,
  [Parameter(Mandatory=$False)][string] $availabilitySetPrefix = "elasticSearchAvailabilitySet",
  [Parameter(Mandatory=$False)][string] $vnetName,
  [Parameter(Mandatory=$False)][string] $subnetName,
  [Parameter(Mandatory=$True)][int] $numMasterInstances,
  [Parameter(Mandatory=$True)][int] $numIndexInstances,
  [Parameter(Mandatory=$True)][int] $numQueryInstances,
  [Parameter(Mandatory=$False)][string] $ElasticSearchDebianPackageUrl = "https://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-1.4.4.deb",
  [Parameter(Mandatory=$True)][string] $masterNodeYml,
  [Parameter(Mandatory=$True)][string] $indexNodeYml,
  [Parameter(Mandatory=$True)][string] $queryNodeYml
)

function createMasterCluster
{
  Write-Host (Get-Date).ToString() "[Cluster] creating master cluster."
  # creating node type specific names
  $outImage = $gImagePrefix + "-master"
  $asName = $availabilitySetPrefix + "-master"
  $vmMasterPrefix = $vmPrefix + "-master"
  $lbName = $lbNamePrefix + "-master"
  
  # create the cluster
  .\New-ElasticsearchCluster -StorageAccountName $StorageAccountName -VMSize $VMSizeMaster -vmPrefix $vmMasterPrefix -CloudServiceName $CloudServiceName -ImageName $ImageName -SshPort $SshPort -gImageName $outImage -localPort $localPort -lbPort $lbMasterPort -lbName $lbName -username $username -password $password -availabilitySetName $asName -vnetName $vnetName -subnetName $subnetName -numInstances $numMasterInstances -ElasticSearchDebianPackageUrl $ElasticSearchDebianPackageUrl -configFile $masterNodeYml
  Write-Host (Get-Date).ToString() "[Cluster] creating master cluster. [DONE]"
}

function createIndexCluster
{
  Write-Host (Get-Date).ToString() "[Cluster] creating index cluster."
  # need to get the list of master nodes and add them to config file
  $masterNodeFile = $masterNodeYml + ".withMaster"
  $masterNodeListString = Get-Content $masterNodeFile | Select-String "discovery.zen.ping.unicast"
  $outLocation = $indexNodeYml + ".withMaster"
  sed "/discovery.zen.ping.unicast.hosts/c\$masterNodeListString" $indexNodeYml | Set-Content $outLocation
  
  # creating node type specific names
  $outImage = $gImagePrefix + "-index"
  $asName = $availabilitySetPrefix + "-index"
  $vmIndexPrefix = $vmPrefix + "-index"
  $lbName = $lbNamePrefix + "-index"
  
  # create the cluster
  .\New-ElasticsearchCluster -StorageAccountName $StorageAccountName -VMSize $VMSizeIndex -vmPrefix $vmIndexPrefix -CloudServiceName $CloudServiceName -ImageName $ImageName -SshPort $SshPort -gImageName $outImage -localPort $localPort -lbPort $lbIndexPort -lbName $lbName -username $username -password $password -availabilitySetName $asName -vnetName $vnetName -subnetName $subnetName -numInstances $numIndexInstances -ElasticSearchDebianPackageUrl $ElasticSearchDebianPackageUrl -configFile $outLocation -NoMasterGeneration -NoSshKeyGeneration
  Write-Host (Get-Date).ToString() "[Cluster] creating index cluster. [DONE]"
}

function createQueryCluster
{
  Write-Host (Get-Date).ToString() "[Cluster] creating query cluster."  # need to get the list of master nodes and add them to config file
  $masterNodeFile = $masterNodeYml + ".withMaster"
  $masterNodeListString = Get-Content $masterNodeFile | Select-String "discovery.zen.ping.unicast"
  $outLocation = $queryNodeYml + ".withMaster"
  sed "/discovery.zen.ping.unicast.hosts/c\$masterNodeListString" $queryNodeYml | Set-Content $outLocation
  
  # creating node type specific names
  $outImage = $gImagePrefix + "-query"
  $asName = $availabilitySetPrefix + "-query"
  $vmQueryPrefix = $vmPrefix + "-query"
  $lbName = $lbNamePrefix + "-query"
  
  # create the cluster
  .\New-ElasticsearchCluster -StorageAccountName $StorageAccountName -VMSize $VMSizeIndex -vmPrefix $vmQueryPrefix -CloudServiceName $CloudServiceName -ImageName $ImageName -SshPort $SshPort -gImageName $outImage -localPort $localPort -lbPort $lbQueryPort -lbName $lbName -username $username -password $password -availabilitySetName $asName -vnetName $vnetName -subnetName $subnetName -numInstances $numQueryInstances -ElasticSearchDebianPackageUrl $ElasticSearchDebianPackageUrl -configFile $outLocation -NoMasterGeneration -NoSshKeyGeneration
  Write-Host (Get-Date).ToString() "[Cluster] creating query cluster. [DONE]"
}

createMasterCLuster
createIndexCluster
createQueryCluster