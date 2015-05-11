# encoding: utf-8
require "logstash/inputs/base"
require "logstash/namespace"

require "azure"
require "securerandom"

# Reads events from Azure Blobs
class LogStash::Inputs::Azureblob < LogStash::Inputs::Base

  config_name "azureblob"
  milestone 0
  
  default :codec, "json_lines"
  
  config :storage_account_name, :validate => :string
  config :storage_access_key, :validate => :string
  
  config :container, :validate => :string
  config :sleep_time, :validate => :number, :default => 10
  
  def initialize(*args)
    super(*args)
  end # def initialize
  
  public
  def register
    Azure.configure do |config|
      config.storage_account_name = @storage_account_name
      config.storage_access_key = @storage_access_key
    end
    @azure_blob = Azure::BlobService.new
  end # def register
  
  def list_blob_names
    blob_names = Set.new []
    loop do
      continuation_token = NIL
      entries = @azure_blob.list_blobs(@container, { :timeout => 10, :marker => continuation_token})
      entries.each do |entry|
        blob_names << entry.name
      end
      continuation_token = entries.continuation_token
      break if continuation_token.empty?
    end
    return blob_names
  end # def list_blobs
  
  def acquire_lock(blob_name)
    @azure_blob.create_page_blob(@container, blob_name, 512)
    @azure_blob.acquire_lease(@container, blob_name,{:duration=>60, :timeout=>10, :proposed_lease_id=>SecureRandom.uuid})
    return true
  rescue LogStash::ShutdownSignal => e
    raise e
  rescue => e
    @logger.error("Caught exception while locking", :exception => e)
    return false
  end # def acquire_lock
  
  def lock_blob(blob_names)
    real_blob_names = blob_names.select { |name| !name.end_with?(".lock") }
    real_blob_names.each do |blob_name|
      if !blob_names.include?(blob_name + ".lock")
        if acquire_lock(blob_name + ".lock")
          return blob_name
        end
      end
    end
    return NIL
  end # def lock_blob
  
  def process(output_queue)
    blob_names = list_blob_names
    blob_name = lock_blob(blob_names)
    return if !blob_name
    blob, content = @azure_blob.get_blob(@container, blob_name)
    @codec.decode(content) do |event|
      output_queue << event
    end
  rescue LogStash::ShutdownSignal => e
    raise e
  rescue => e
    @logger.error("Oh My, An error occurred.", :exception => e)
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
