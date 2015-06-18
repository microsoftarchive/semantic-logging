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
	echo "This script installs Kibana 4 on Ubuntu, and configures it to be used with a user's elastic search cluster"
	echo "Parameters:"
	echo "p - The kibana package to use"
	echo "s - Skip common install steps (sudo apt-get update/ installing default-jre)"
	echo ""
	echo ""
}

log()
{
	echo "$1"
}

# Add to Kibana Config
atkcfg()
{
	echo "$1" > ~/kibana.yml
}

#Script Parameters
KIBANA_PACKAGE_URL="https://download.elasticsearch.org/kibana/kibana/kibana-4.0.0-linux-x64.tar.gz"

#Loop through options passed
while getopts :p:s optname; do
    log "Option $optname set with value ${OPTARG}"
  case $optname in
    p)  #package url
      KIBANA_PACKAGE_URL=${OPTARG}
      ;;
	d)  #skip common install steps
	  SKIP_COMMON_INSTALL="YES"
	  ;;
    h)  #show help
      help
      exit 2
      ;;
    \?) #unrecognized option - show help
      echo -e \\n"Option -${BOLD}$OPTARG${NORM} not allowed."
      help
      exit 2
      ;;
  esac
done

# Install Kibana
if [ -z $SKIP_COMMON_INSTALL ] 
then
  log "Updating apt-get"
  sudo apt-get update
  log "Installing Java Runtime"
  sudo apt-get -f -y install default-jre
else
  log "Skipping common install"
fi

log "Installing apache2"
sudo apt-get -f -y install apache2

log "Downloading Kibana"
wget $KIBANA_PACKAGE_URL -O ~/kibana.tar.gz

log "Download Completed, Installing Kibana"
sudo tar -xf ~/kibana.tar.gz -C /var/www/html/ --strip 1

#Create User Configuration
atkcfg "port: 80"
atkcfg "host: \"0.0.0.0\""
#TODO: This should be configurable from the script
atkcfg "elasticsearch_url: \"http://localhost:9200\""
atkcfg "elasticsearch_preserve_host: true"
atkcfg "kibana_index: \".kibana\""
atkcfg "default_app_id: \"discover\"" 
atkcfg "request_timeout: 500000"
atkcfg "shard_timeout: 0"
atkcfg "verify_ssl: true"
atkcfg "bundled_plugin_ids:"
atkcfg " - plugins/dashboard/index"
atkcfg " - plugins/discover/index"
atkcfg " - plugins/doc/index"
atkcfg " - plugins/kibana/index"
atkcfg " - plugins/markdown_vis/index"
atkcfg " - plugins/metric_vis/index"
atkcfg " - plugins/settings/index"
atkcfg " - plugins/table_vis/index"
atkcfg " - plugins/vis_types/index"
atkcfg " - plugins/visualize/index"

#Install User Configuration
log "Installing user configuration file"
sudo \cp ~/kibana.yml kibana/config/

# Configure Start
log "Starting Kibana"
screen -d -m sh -c \"while :; do sudo kibana/bin/kibana; done;\"
