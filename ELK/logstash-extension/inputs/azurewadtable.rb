# encoding: utf-8
require "logstash/inputs/base"
require "logstash/namespace"

require "azure"

class LogStash::Inputs::AzureWADTable < LogStash::Inputs::Base
  class Interrupted < StandardError; end

  config_name "azurewadtable"
  milestone 0
  
  config :account_name, :validate => :string
  config :access_key, :validate => :string
  config :table_name, :validate => :string
  config :entity_count_to_process, :validate => :string, :default => 100
  config :collection_start_time_utc, :validate => :string, :default => Time.now.utc.iso8601
  config :etw_pretty_print, :validate => :boolean, :default => false
  config :idle_delay_seconds, :validate => :number, :default => 15

  TICKS_SINCE_EPOCH = Time.utc(0001, 01, 01).to_i * 10000000

  def initialize(*args)
    super(*args)
  end # initialize

  public
  def register
    Azure.configure do |config|
      config.storage_account_name = @account_name
      config.storage_access_key = @access_key
     end
    @azure_table_service = Azure::Table::TableService.new
    @last_timestamp = @collection_start_time_utc
	@idle_delay = @idle_delay_seconds
  end # register
  
  public
  def run(output_queue)
    loop do
	  @logger.debug("Starting process method @" + Time.now.to_s);
      process(output_queue)
	  @logger.debug("Starting delay of: " + @idle_delay_seconds.to_s + " seconds @" + Time.now.to_s);
	  sleep @idle_delay
    end # loop
  end # run
 
  public
  def teardown
  end  

  def process(output_queue)
    @logger.debug(@last_timestamp)
    # query data using start_from_time
    query_filter = "PartitionKey gt '#{partitionkey_from_datetime(@last_timestamp)}' and PreciseTimeStamp gt datetime'#{@last_timestamp}'".gsub('"','')
    query = { :top => @entity_count_to_process, :filter => query_filter }
    result = @azure_table_service.query_entities(@table_name, query)
    
    if result and result.length > 0
      result.each do |entity|
        event = LogStash::Event.new(entity.properties)
        event["type"] = @table_name

		# Help pretty print etw files
		if (@etw_pretty_print && !event["EventMessage"].nil? && !event["Message"].nil?)
		  logger.debug("event: " + event.to_s)
		  eventMessage = event["EventMessage"].to_s
		  message = event["Message"].to_s
		  logger.debug("EventMessage: " + eventMessage)
		  logger.debug("Message: " + message)
		  if (eventMessage.include? "%")
		    logger.debug("starting pretty print")
		    toReplace = eventMessage.scan(/%\d+/)
		    payload = message.scan(/(?<!\\S)([a-zA-Z]+)=(\"[^\"]*\")(?!\\S)/)
		    # Split up the format string to seperate all of the numbers
	        toReplace.each do |key| 
		      logger.debug("Replacing key: " + key.to_s)
		      index = key.scan(/\d+/).join.to_i
			  newValue = payload[index - 1][1]
			  logger.debug("New Value: " + newValue)
		      eventMessage[key] = newValue
		    end
		  event["EventMessage"] = eventMessage
		  logger.debug("pretty print end. result: " + event["EventMessage"].to_s)
		  end
		end
		
        output_queue << event
      end # each block
      @idle_delay = 0
      @last_timestamp = result.last.properties["PreciseTimeStamp"].iso8601
    else
      @logger.debug("No new results found.")
	  @idle_delay = @idle_delay_seconds
    end # if block
    
  rescue => e
    @logger.error("Oh My, An error occurred.", :exception => e)
    raise
  end # process

  # Windows Azure Diagnostic's algorithm for determining the partition key based on time is as follows:
  # 1. Take time in UTC without seconds.
  # 2. Convert it into .net ticks
  # 3. add a '0' prefix.
  def partitionkey_from_datetime(time_string)
    collection_time = Time.parse(time_string)
    if collection_time
      @logger.debug("collection time parsed successfully #{collection_time}")
    else
      raise(ArgumentError, "Could not parse the time_string")
    end # if else block

    collection_time -= collection_time.sec
    ticks = to_ticks(collection_time)
    "0#{ticks}"
  end # partitionkey_from_datetime
  
  # Convert time to ticks
  def to_ticks(time_to_convert)
    @logger.debug("Converting time to ticks")
    time_to_convert.to_i * 10000000 - TICKS_SINCE_EPOCH 
  end # to_ticks

end # LogStash::Inputs::AzureWADTable
