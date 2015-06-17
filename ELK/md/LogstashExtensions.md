# Logstash Extensions
This project provides extensions for [logstash](http://logstash.net/). Three of these extensions can consume data from Azure data sources and one codex can ingest a list of JSON objects.

## Input Extensions
### Azure WAD Table
The Azure WAD Table extension consumes table entries created by the Windows Azure Diagnostics extension and has the following configuration values:
- **account_name** - the account name holding the table
- **access_key** - the access key to the storage account
- **table_name** - the table name containing the diagnostics data
- **entity_count_to_process** - the number of entities to request at a time. Defaults to 100.
- **collection_start_time_utc** - the earliest time stamp of the data requested. Defaults to Time.now.
- **etw_pretty_print** - Whether to try and pretty print the ETW messages. Will override the EventMessage column. The format for this is: EventMessage:Hi this is %1, Message:adj="cool" notused=" ", where EventMessage is the format string, %1 is what to replace, and "cool" is the value to use to replace %1. Defaults to false.
- **idle_delay_seconds** - the number of seconds to delay between cycles when idle (no data is being found). Defaults to 15 seconds.

####Sample Configuration:
```
input { 
	azurewadtable {
		account_name => "STORAGE ACCOUNT NAME"
		access_key => "STORAGE ACCESS KEY"
		table_name => "TABLE NAME"
		entity_count_to_process => 100
		collection_start_time_utc => "2015-06-10T23:57:27.307Z"
		etw_pretty_print => false
		idle_delay_seconds => 15
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
