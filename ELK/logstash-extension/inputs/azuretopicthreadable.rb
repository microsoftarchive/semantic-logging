# encoding: utf-8
require "logstash/inputs/base"
require "logstash/namespace"

require "thread"

require "azure"

# Reads events from Azure topics
class LogStash::Inputs::Azuretopicthreadable < LogStash::Inputs::Base
  class Interrupted < StandardError; end

  config_name "azuretopicthreadable"
  milestone 0
  
  default :codec, "json" # default json codec
  
  config :namespace, :validate => :string
  config :access_key, :validate => :string
  config :subscription, :validate => :string
  config :topic, :validate => :string
  config :deliverycount, :validate => :number, :default => 10
  config :threads, :validate => :number, :default => 1
  config :thread_sleep_time, :validate => :number, :default => 1.0/50.0
  
  def initialize(*args)
	super(*args)
  end # def initialize
  
  public
  def register
	  # Configure credentials
	  Azure.configure do |config|
	    config.sb_namespace = @namespace
	    config.sb_access_key = @access_key
 	  end
  end # def register
  
  def process(output_queue, pid)
    # Get a new instance of a service
  	azure_service_bus = Azure::ServiceBus::ServiceBusService.new
	while true
		begin
	    # check if we have a message in the subscription
		message = azure_service_bus.receive_subscription_message(@topic ,@subscription, { :peek_lock => true, :timeout => 1 } )
		if message
		    # decoding returns a yield
			codec.decode(message.body) do |event|
				output_queue << event
			end # codec.decode
			# delete the message after reading it
			azure_service_bus.delete_subscription_message(message)
		end
		rescue LogStash::ShutdownSignal => e
			raise e
		rescue => e
			@logger.error("Oh My, An error occurred. Thread id:" + pid.to_s, :exception => e)
			if message and message.delivery_count > @deliverycount
				azure_service_bus.delete_subscription_message(message)
			end
		end
	sleep(@thread_sleep_time)
	end
  end # def process
  
  public
  def run(output_queue)
	threads = []
    (0..(@threads-1)).each do |pid|
      threads << Thread.new { process(output_queue, pid) }
    end
    threads.each { |thr| thr.join }
  end # def run
 
  public
  def teardown
  end # def teardown
end # class LogStash::Inputs::Azuretopic
