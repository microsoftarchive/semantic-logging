#:warning: This template is still under development.

# Install Logstash cluster on Ubuntu machines
<a href="http://codepen.io/juliusl/pen/ZGJJQB" target="_blank">Configure a logstash configuration</a> - Use this to help populate the "encodedConfigString" parameter for the template.

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fmspnp%2Fsemantic-logging%2Fv3%2FELK%2FAzureRM%2Flogstash-on-ubuntu%2Fazuredeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

This template deploys and installs logstash with azure plugins. TODO: For now we only install azurewadtable logstash plugin for testing purposes with the others on the way.

Below are the parameters that the template expects:

|Name   |Description    |
|:---   |:---|
|newStorageAccountName  |Name of the storage account to create with the machine.    |
|adminUsername  |Name of the admin user of the machine. |
|adminPassword  |Admin password of the machine. |
|dnsNameForPublicIP |Public dns name for the virtual machine.   |
|encodedConfigString    |Base64 encoded string which is the logstash configuration. <a href="http://codepen.io/juliusl/pen/ZGJJQB" target="_blank">Configure a logstash configuration</a>  |

#Notes & Limitations
- Currently only supports Logstash version 1.4.2


