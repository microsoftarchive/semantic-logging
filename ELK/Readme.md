#Microsoft Azure Diagnostics with ELK
This repository contains PowerShell Scripts to setup Elastic Search, LogStash and Kibana (ELK) on Azure. Also included are Logstash plugins to pull in Microsoft Azure Diagnostics data into Elastic Search.
  


#Quick Setup
The following step by step instructions show you how to create a minimalist ELK setup and is ideal for folks who want to try out the ELK stack.
See [Reference](Reference.md) for more details on the available commandlets and extensions. 

## Pre-requisite
The assumption is that you already have a VM or cloud service deployment using Microsoft Azure diagnostics to collect and transfer diagnostics data to Azure Storage tables.
For more information on setting up a project to use Microsoft Azure diagnostics, refer to the documentation [here](https://msdn.microsoft.com/en-us/library/azure/dn186185.aspx).

## Setting up the environment

### Installation
1. Install Visual Studio 2013
2. Launch Web Platform Installer and install Microsoft Azure SDK for Visual Studio 2013   
![SetupEnv-1.png](/md-images/SetupEnv-1.png) 
3. Install Microsoft Azure Powershell  
![SetupEnv-2.png](/md-images/SetupEnv-2.png)
4. Launch Microsoft Azure Powershell and type "Get-Module" and see if the Azure module is imported. If not, try rebooting and then check again. In certain cases after powershell installation, the powershell module path is not updated properly and requires a reboot.    
![SetupEnv-3.png](/md-images/SetupEnv-3.png)
5. Install msysgit from [here]( https://github.com/msysgit/msysgit/releases/). On the site, choose the a stable version of msysGit-netinstall exe and install the tools

### Environment Variables
6. Add the required environment variables. These environment variables allow the ELK setup scripts to locate the required tools. If you want to add them just to your session of powershell, skip this step for now.  
If you want to add them globally so they apply to all new processes, add these to the PATH variable. Go to **control panel->System and Security->System**, open Advanced system settings, then click on Environment Variables, add
    ```
    "<Installed location of msysgit>\bin"
    ```
for the PATH variable.
Since locations of these tools can change from version to version, check the path for the following tools and add their paths as well 
>ssh: to run commands on the remote machine  
>scp: to copy files onto the remote machine  
>openssl: to generate certificates for ssh connection  
>sed: to edit of the configuration files deployed to the remote machines

### Setting up the proper powershell environment
1. Launch the Windows Azure Powershell in administrator mode
2. Change the execution policy to bypass. This will stop powershell prompting you for confirmation about running the scripts.
    ```
    Set-ExecutionPolicy bypass -Scope Process
    ```
3. If you didnâ€™t add the environment variables in step 5 of "Setting up the environment", follow that step to determine all the paths you need and add them as such:
    ```
    $ENV:PATH="$ENV:PATH;<Installed location of msysgit>\bin;<any other paths needed>"
    ```
4. Add an environment variable for openssl config file
    ```
    $ENV:OPENSSL_CONF ="<Installed location of msysgit>\ssl\openssl.cnf"
    ```
5. If your powershell is not associated with an account yet, run Add-AzureAccount.	This will pop up a sign in dialog.
6. Run Get-AzureSubscription to see your list of subscriptions. Pick the one you want to use and run
    ```
    Select-AzureSubscription -subscriptionid <your subscription id>
    ```
7. Run Get-AzureSubscription again and check that its IsCurrent field is marked as true
8. Change to the directory where all the PowerShell Scripts are located, so it's ready for next set of configuration steps 
9. Do not close this powershell window until you are fully setup

## Setting up ELK
### Logstash config file
1. Run New-SampleLogstashConfigWithStorage.ps1 with the name of the storage account and table where the Microsoft Azure Diagnostics data located
    ```
    .\New-SampleLogstashConfigWithStorage.ps1 -StorageAccountName "<Your storage account name>" -TableName "<Your table name>"
    ```
This command will create a logstash.conf file which can configure logstash to pull in data from your Diagnostics storage account. Note that the generated configuration will cause Logstash to only collect entries that are generated after logstash started up. By default the configuration will only pull in the Table specified in the script parameter. You can manually edit the file to pull in additional tables. 
 
### Creating the ELK VM
2. Run the follow command to start the ELK VM creation. Please choose your own user name, password, and location that is suitable for you.
    ```
    .\New-SampleELKInstance.ps1 -Location "West US" -Username "elk" -Password "Elk1234" -LogstashConfig "logstash.conf" -UseKibana4
    ```
3. Wait for the script to complete. This  will take about 10 to 20 min. Along with the ELK VM, it will also create a new cloud service and storage account for you. Once completed, it should show you the name of the cloud service after the text "To see your kibana dashboard visit". Verify your setup by launching the browser and navigating to http://yourcloudservice.cloudapp.net to see the default Kibana page.  
![SetupElk-1.png](/md-images/SetupElk-1.png)
    
### Verifying data on Kibana
1. Once the cloud service is deployed, refresh your browser on the kibana dashboard. Check the "User event times to create index names" checkbox, it should use the logstash timestamp pattern to match and find some indices. Select Timestamp as the Time-field name and click Create      
![VerifyData-1.png](/md-images/VerifyData-1.png)
2. Click Discover at the top, and you should start seeing data  
![VerifyData-2.png](/md-images/VerifyData-2.png)
3. Your ELK stack is now up and running.  

