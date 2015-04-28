# Preparation
Follow the [setup instructions](/md/SetupInstructions.md) on this page to install tools such as Azure Commandlets, git, ssh, scp, and openssl.

# Scenarios
The following consumption scenarios are available:
- [Windows Azure Diagnostics](/md/AzureDiagnostics.md)
- [Blob Storage (e.g. from Application Insights)](/md/ApplicationInsights.md)


# Commandlet Usage

## ElasticSearch
We have two commandlets to set up a Elastic Search cluster: (1) the simple script sets up a cluster with every node being master, query, and index node, whereas the (2) advanced script sets up a cluster that seperates the three node types.

###New-ElasticsearchCluster
- -**CloudServiceName**
- -**StorageAccountName**
- -**VMSize**
- -**vmPrefix** - name prefix of vms that will be created
- -**ImageName** - name of the Linux base image
- -**SshPort** - free port that can be used for ssh [default: 50000]
- -**gImageName** - name of the generalized image created to base VMs of [default: "elasticSearchBaseline"]
- -**localPort** - elastic search port [default: 9200]
- -**lbPort** - external port mapping to the -localPort [default: 9200]
- -**lbName** - load blancer name [default: "myElasticSearchLB"]
- -**username** - linux user name [default: "elasticSearch"]
- -**password** - linux password
- -**availabilitySetName** - [default: "elasticSearchAvailabilitySet"]
- -**vnetName** - virtual network name the vms will be added to [optional]
- -**subnetName** - subnet name the vms will be added to [optional]
- -**numInstances** - number of instances that will be created
- -**ElasticSearchDebianPackageUrl** - [default: "https://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-1.4.4.deb"]
- -**configFile** - [default: "elaseticsearch.yml"]
- -**NumberOfHardDisks** - [default: 0]
- -**DiskSizeInGB** - [default: 50]
- -**NoMasterGeneration** - flag preventing the generation of master node list added to the configuration file [optinal]
- -**NoSshKeyGeneration** - flag preventing the generation of a new ssh key added to the VMs [optional]
- -**ComputeSharding** - flag enable computing the number of shards created by the cluster [optional]

###New-AdvancedElasticsearchCluster
- -**CloudServiceName**
- -**StorageAccountName**
- -**VMSizeMaster** - VM size for the master nodes
- -**VMSizeIndex** - VM size for the indexing nodes
- -**VMSizeQuery** - VM size for the query nodes
- -**vmPrefix** - name prefix of vms that will be created
- -**ImageName** - name of the Linux base image
- -**SshPort** - free port that can be used for ssh [default: 50000]
- -**gImagePrefix** - prefix for name generation for the generalized image created to base VMs of [default: "elasticSearchBaseline"]
- -**localPort** - elastic search port [default: 9200]
- -**lbMasterPort** - external port mapping to the -localPort running master node [default: 9400]
- -**lbIndexPort** - external port mapping to the -localPort running index node [default: 9300]
- -**lbQueryPort** - external port mapping to the -localPort running query node [default: 9200]
- -**lbNamePrefix** - [default: "myElasticSearchLB"]
- -**username** - linux user name [default: "elasticSearch"]
- -**password** - linux password
- -**availabilitySetPrefix** - prefix for load balancer name generation [default: "elasticSearchAvailabilitySet"]
- -**vnetName** - virtual network name the vms will be added to [optional]
- -**subnetName** - subnet name the vms will be added to [optional]
- -**numMasterInstances** - number of master node instances that will be created
- -**numIndexInstances** - number of index node instances that will be created
- -**numQueryInstances** - number of query node instances that will be created
- -**ElasticSearchDebianPackageUrl** - [default: "https://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-1.4.4.deb"]
- -**masterNodeYml** - master node configuration file
- -**indexNodeYml** - index node configuration file
- -**queryNodeYml** - query node configuration file

## LogStash
### New-LogstashCluster
- -**CloudServiceName**
- -**StorageAccountName**
- -**VMSize**
- -**vmPrefix** - name prefix of vms that will be created
- -**ImageName** - name of the Linux base image
- -**SshPort** - free port that can be used for ssh [default: 50000]
- -**gImagePrefix** - prefix for name generation for the generalized image created to base VMs of [default: "logstashBaseline"]
- -**username** - linux user name [default: "logstash"]
- -**password** - linux password
- -**availabilitySetName** - [default: "logstashAvailabilitySet"]
- -**vnetName** - virtual network name the vms will be added to [optional]
- -**subnetName** - subnet name the vms will be added to [optional]
- -**numInstances** - number of instances that will be created
- -**LogstashDebianPackageUrl** - [default: "https://download.elasticsearch.org/logstash/logstash/packages/debian/logstash_1.4.2-1-2c0f5a1_all.deb"]
- -**configFile** - logstash configuration file [default: "logstash.conf"]
- -**LogstashPluginLocation** - location of the logstash plubins to be installed [default: "logstash-extension"]
- -**NoSshKeyGeneration** - flag preventing the generation of a new ssh key added to the VMs[optional]

## Kibana
### New-KibanaCluster
- -**CloudServiceName**
- -**StorageAccountName**
- -**VMSize**
- -**vmPrefix** - name prefix of vms that will be created
- -**ImageName** - name of the Linux base image
- -**SshPort** - free port that can be used for ssh [default: 50000]
- -**gImagePrefix** - prefix for name generation for the generalized image created to base VMs of [default: "kibanaBaseline"]
- -**localPort** - port apache is running on  [default: 80]
- -**lbPort** - load balancer port [default: 80]
- -**lbName** - load blancer name [default: "myKibanaLB"]
- -**username** - linux user name [default: "kibana"]
- -**password** - linux password
- -**availabilitySetName** - [default: "kibanaAvailabilitySet"]
- -**vnetName** - virtual network name the vms will be added to [optional]
- -**subnetName** - subnet name the vms will be added to [optional]
- -**numInstances** - number of instances that will be created
- -**KibanaTarBallUrl** - [default: "https://download.elasticsearch.org/kibana/kibana/kibana-3.1.2.tar.gz" for Kibana 3 and "https://download.elasticsearch.org/kibana/kibana/kibana-4.0.0-linux-x64.tar.gz" for Kibana 4]
- -**configFile** - Kibana configuration file [default: "config.js"]
- -**NoSshKeyGeneration** - flag preventing the generation of a new ssh key added to the VMs[optional]
- -**UseKibana4** - flag to use Kibana 4 instead of Kibana 3

# Logstash Extensions
To make the usage of logstash easier with Azure we provide a set of logstash extensions that can be configured [(link to page)](/md/LogstashExtensions.md).