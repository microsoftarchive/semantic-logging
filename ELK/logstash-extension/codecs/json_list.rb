# encoding: utf-8
require "logstash/codecs/base"
require "logstash/codecs/line"
require "json"

class LogStash::Codecs::JSONList < LogStash::Codecs::Base
  config_name "json_list"
  
  milestone 0
  
  config :charset, :validate => ::Encoding.name_list, :default => "UTF-8"

  public
  def register
  end # def register
  
  public
  def decode(data)
    begin
      JSON.parse(data).each do |obj|
        yield LogStash::Event.new(obj)
      end
    rescue JSON::ParserError => e
      @logger.info("JSON parse failure. Falling back to plain-text", :error => e, :data => data)
      yield LogStash::Event.new("message" => event["message"])
    end   
  end # def decode
  
  public
  def encode(data)
    arr = Array.new
    arr << data
    @on_event.call(arr.to_json)
  end # def encode

end # class LogStash::Codecs::JSONList