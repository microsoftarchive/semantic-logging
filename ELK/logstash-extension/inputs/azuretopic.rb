# encoding: utf-8
require "logstash/inputs/base"
require "logstash/namespace"

require "azure"

# Reads events from Azure topics
class LogStash::Inputs::Azuretopic < LogStash::Inputs::Base
  class Interrupted < StandardError; end

  config_name "azuretopic"
  milestone 0
  
  default :codec, "json_list"
  
  config :namespace, :validate => :string
  config :access_key, :validate => :string
  config :subscription, :validate => :string
  config :topic, :validate => :string
  config :deliverycount, :validate => :number, :default => 10
  
  def initialize(*args)
	super(*args)
  end # def initialize
  
  public
  def register
	  Azure.configure do |config|
	    config.sb_namespace = @namespace
	    config.sb_access_key = @access_key
 	  end
	@azure_service_bus = Azure::ServiceBusService.new
  end # def register
  
  def process(output_queue)
    message = @azure_service_bus.receive_subscription_message(@topic ,@subscription, { :peek_lock => true, :timeout => 1 } )
    if message
	  codec.decode(message.body) do |event|
        output_queue << event
	  end # codec.decode
	  @azure_service_bus.delete_subscription_message(message)
	end
  rescue LogStash::ShutdownSignal => e
    raise e
  rescue => e
    @logger.error("Oh My, An error occurred.", :exception => e)
	if message and message.delivery_count > @deliverycount
		@azure_service_bus.delete_subscription_message(message)
	end
  end # def process
  
  public
  def run(output_queue)
	while true
      process(output_queue)
	end # loop
  end # def run
 
  public
  def teardown
  end # def teardown
end # class LogStash::Inputs::Azuretopic
