# Logstash Extensions
This project provides extensions for [logstash](http://logstash.net/). Three of these extensions can consume data from Azure data sources and one codex can ingest a list of JSON objects.

## Input Extensions
### Azure WAD Table
The Azure WAD Table extension consumes table entries created by the Windows Azure Diagnostics extension and has the following configuration values:
- **account_name** - the account name holding the table
- **access_key** - the access key to the storage account
- **table_name** - the table name containing the diagnostics data
- **entity_count_to_process** - the number of entities to request at a time
- **collection_start_time_utc** - the earliest time stamp of the data requested

####Sample Configuration:
```
input { 
	azurewadtable {
		account_name => "STORAGE ACCOUNT NAME"
		access_key => "STORAGE ACCESS KEY"
		table_name => "TABLE NAME"
	}
}

output {
	elasticsearch {
		host => "localhost"
		protocol => "http"
        port => 9200
	}
}
```

### Azure Blob Storage
The Azure Blob Storage extension consumes blobs from a given azure container and has the following configuration values:
- **codec** - codec used to parse the message content [default: json_lines]
- **storage_account_name** - storage account name containing the blobs
- **storage_access_key** - access key to the storage account
- **container** - container name containing the blobs
- **sleep_time** - sleep time between 

### Azure Topics
#### Normal [azuretopic.rb](../logstash-extension/inputs/azuretopic.rb)
The Azure Topics input extension consumes messages posted to Azure Topics and has the following configuration values:
- **codec** - codec used to parse the message content [default: json_list]
- **namespace** - name space of the service bus
- **access_key** - ACS key to access the service bus
- **subscription** - subscription to dequeue messages from
- **topic** - topic to post message to
- **deliverycount** - the number of times to try receiving the message before deleting [default: 10]

#### Threadable [azuretopicthreadable.rb](../logstash-extension/inputs/azuretopicthreadable.rb)
The Azure Topics Threadable input extension consumes messages posted to Azure Topics and has the following configuration values:
- **codec** - codec used to parse the message content [default: json]
- **namespace** - name space of the service bus
- **access_key** - ACS key to access the service bus
- **subscription** - subscription to dequeue messages from
- **topic** - topic to post message to
- **deliverycount** - nthe number of times to try receiving the message before deleting [default: 10]
- **threads** - number of concurrent threads to run at once that will read from the subscription [default: 1]
- **thread_sleep_time** - the time for the thread to sleep in seconds between iterations [default: 1.0/50.0]

## JSON list Codec
This codec allows the ingestion of JSON objects, the following values can be configured:
- **charset** - string encoding [default: utf-8]
