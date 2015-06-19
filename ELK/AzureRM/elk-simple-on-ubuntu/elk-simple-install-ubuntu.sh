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

#Loop through options passed
while getopts :n:v:d:e:sh optname; do
    log "Option $optname set with value ${OPTARG}"
  case $optname in
	n)
	  ES_CLUSTER_NAME=${OPTARG}
	  ;;
	v)
	  ES_VERSION=${OPTARG}
	  ;;
	d)
	  ES_DISCOVERY_HOSTS=${OPTARG}
	  ;;
	e)
	  ENCODED_LOGSTASH_CONFIG=${OPTARG}
	  ;;
	s)  #skip common install steps
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

#ELK (Simple) Install Script

#Install ELK
wget https://raw.githubusercontent.com/juliusl/azure-quickstart-templates/master/elasticsearch/elasticsearch-ubuntu-install.sh
bash ./elasticsearch-ubuntu-install.sh -xn $ES_CLUSTER_NAME -v $ES_VERSION -d $ES_DISCOVERY_HOSTS

#Install Logstash
wget https://raw.githubusercontent.com/mspnp/semantic-logging/v3/ELK/AzureRM/logstash-on-ubuntu/logstash-install-ubuntu.sh
bash ./logstash-install-ubuntu -e $ENCODED_LOGSTASH_CONFIG

wget https://raw.githubusercontent.com/mspnp/semantic-logging/v3/ELK/AzureRM/elk-simple-on-ubuntu/kibana4-install-ubuntu.sh
bash ./kibana4-install-ubuntu.sh
