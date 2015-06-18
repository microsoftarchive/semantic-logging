#!/bin/bash
#
# ========================================================================================
# Microsoft patterns & practices (http://microsoft.com/practices)
# SEMANTIC LOGGING APPLICATION BLOCK
# ========================================================================================
#
# Copyright (c) Microsoft.  All rights reserved.
# Microsoft would like to thank its contributors, a list
# of whom are at http://aka.ms/entlib-contributors
#
# Licensed under the Apache License, Version 2.0 (the "License"); you
# may not use this file except in compliance with the License. You may
# obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
# implied. See the License for the specific language governing permissions
# and limitations under the License.
#

help()
{
    echo ""
    echo ""
	echo "This script installs Logstash 1.4.2 on Ubuntu, and configures it to be used with user plugins/configurations"
	echo "Parameters:"
	echo "p - The logstash package url to use. Currently tested to work on Logstash version 1.4.2."
	echo "e - The encoded configuration string."
	echo ""
	echo ""
	echo ""
}

log()
{
	echo "$1"
}

#Script Parameters
LOGSTASH_DEBIAN_PACKAGE_URL="https://download.elasticsearch.org/logstash/logstash/packages/debian/logstash_1.4.2-1-2c0f5a1_all.deb"

#Loop through options passed
while getopts :p:e:hs optname; do
    log "Option $optname set with value ${OPTARG}"
  case $optname in
    p)  #package url
      LOGSTASH_DEBIAN_PACKAGE_URL=${OPTARG}
      ;;
	s)  #skip common install steps
	  SKIP_COMMON_INSTALL="YES"
	  ;;
    h)  #show help
      help
      exit 2
      ;;
    e)  #set the encoded configuration string
	  log "Setting the encoded configuration string"
      CONF_FILE_ENCODED_STRING="${OPTARG}"
      USE_CONF_FILE_FROM_ENCODED_STRING="true"
      ;;
    \?) #unrecognized option - show help
      echo -e \\n"Option -${BOLD}$OPTARG${NORM} not allowed."
      help
      exit 2
      ;;
  esac
done

# Install Logstash

if [ -z $SKIP_COMMON_INSTALL ] 
then
  log "Updating apt-get"
  sudo apt-get update
  log "Installing Java Runtime"
  sudo apt-get -f -y install default-jre
else
  log "Skipping common install"
fi

log "Downloading logstash package"
wget ${LOGSTASH_DEBIAN_PACKAGE_URL} -O logstash.deb
log "Download completed, Installing Logstash"
sudo dpkg -i logstash.deb

# Install User Configuration from encoded string
if [ ! -z $USE_CONF_FILE_FROM_ENCODED_STRING ] 
then
  log "Decoding configuration string"
  log "$CONF_FILE_ENCODED_STRING"
  echo $CONF_FILE_ENCODED_STRING > logstash.conf.encoded
  DECODED_STRING=$(base64 -d logstash.conf.encoded)
  log "$DECODED_STRING"
  echo $DECODED_STRING > ~/logstash.conf
fi

# Install Azure
log "Installing Azure SDK"
sudo env GEM_HOME=/opt/logstash/vendor/bundle/jruby/1.9 GEM_PATH=\"\" java -jar /opt/logstash/vendor/jar/jruby-complete-1.7.11.jar -S gem install azure

# Install Plugins
log "Installing Plugins"
sudo env GEM_HOME=/opt/logstash/vendor/bundle/jruby/1.9 GEM_PATH=\"\" java -jar /opt/logstash/vendor/jar/jruby-complete-1.7.11.jar -S gem install logstash-input-azurewadtable

# Copy Plugins
log "Copying plugins"
sudo \cp -f /opt/logstash/vendor/bundle/jruby/1.9/gems/logstash-input-azurewadtable-0.9.2/lib/logstash/inputs/azurewadtable.rb /opt/logstash/lib/logstash/inputs/azurewadtable.rb

#log "Installing user configuration file"
log "Installing user configuration file"
sudo \cp -f ~/logstash.conf /etc/logstash/conf.d/

# Configure Start
log "Configure start up service"
sudo update-rc.d logstash defaults 95 10
sudo service logstash start
