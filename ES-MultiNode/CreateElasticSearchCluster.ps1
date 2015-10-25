param(
[string] $TemplateParameterFile = ".\azuredeploy-parameters.json",
[string] $PublicSshKeyFile = "",
[Parameter(Mandatory=$true)][string] $ResourceGroupName,
[string] $DeploymentName = "ES-default-deployment",
[switch] $RemoveExistingResourceGroup
)

$ErrorActionPreference = "Stop" # Stop on any error by default

function ExtractKeyDataFromFile($filePath)
{
    $keyData = ""
    $lines = Get-Content -Path $filePath 
    foreach ($line in $lines)
    {
        if (($line -notlike '*begin certificate*') -and ($line -notlike '*end certificate*'))
        {
            $keyData += $line
        }
    }
    $keyData
}

[void][System.Reflection.Assembly]::LoadWithPartialName("System.Web.Extensions")
$deserializer = New-Object -TypeName System.Web.Script.Serialization.JavaScriptSerializer
$parameterFileContent = Get-Content -Raw -Path $TemplateParameterFile
$templateParametersAzureFormat = $deserializer.DeserializeObject($parameterFileContent)
$templateParameters = @{}
foreach($key in $templateParametersAzureFormat.Keys)
{
    $templateParameters[$key] = $templateParametersAzureFormat[$key].value;
}

if (!$PublicSshKeyFile)
{
    $privateKeyFileName = ($templateParameters.dnsNameForLoadBalancerIP + ".key");
    $publicKeyFileName = ($templateParameters.dnsNameForLoadBalancerIP + ".pem");
    
    .\openssl.cmd req `
        -batch `
        -x509 `
        -nodes `
        -days 365 `
        -newkey rsa:2048 `
        -keyout $privateKeyFileName `
        -out $publicKeyFileName `
        -subj "/C=US/ST=Unknown/L=Unknown/O=Unknown/CN=$($templateParameters.dnsNameForLoadBalancerIP).cloudapp.azure.com"
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not create a SSH key file for user authentication"
    }
    $keyData = ExtractKeyDataFromFile $publicKeyFileName
}
else 
{
    $keyData = ExtractKeyDataFromFile $PublicSshKeyFile
}
$templateParameters['sshKeyData'] = $keyData

$templateParameters['dnsDomainForLoadBalancerIP'] = ([string] $templateParameters['region']).ToLowerInvariant().Replace(" ", "") + ".cloudapp.azure.com";
Write-Verbose "Using $($templateParameters['dnsDomainForLoadBalancerIP']) as the domain name for the ElasticSearch cluster"

Switch-AzureMode AzureResourceManager

if (-Not (Test-AzureResourceGroup -ResourceGroupName $ResourceGroupName))
{
    Write-Host "Creating new resource group $ResourceGroupName..."
    New-AzureResourceGroup -Name $ResourceGroupName -Location $templateParameters.region
}
else 
{
    if ($RemoveExistingResourceGroup)
    {
        Write-Host "Removing old resource group $ResourceGroupName..."
        Remove-AzureResourceGroup -Name $ResourceGroupName -Force -ErrorAction Continue -Verbose
        Write-Host "Creating new resource group $ResourceGroupName..."
        New-AzureResourceGroup -Name $ResourceGroupName -Location $templateParameters.region
    }
    else 
    {
        throw "Resource group '$ResourceGroupName' already exists"
    }
}

# Workaround for issue https://github.com/Azure/azure-powershell/issues/309
# Using TemplateParameterObject parameter with New-AzureResourceGroupDeployment causes the cmdlet to stop outputting error messages completely
# so we will create and use a temporary parameter file instead.
$parameterFileContent = "{ "
foreach($key in $templateParameters.Keys)
{
    if ($templateParameters[$key] -is [int]) 
    {
        $parameterFileContent += '"{0}": {{"value": {1}}},' -f $key, $templateParameters[$key]
    }
    else 
    {
        $parameterFileContent += '"{0}": {{"value": "{1}"}},' -f $key, $templateParameters[$key]
    }
}
$parameterFileContent = $parameterFileContent.Substring(0, $parameterFileContent.Length - 1)   # Remove last comma
$parameterFileContent += " }"
$TemplateParameterFile = $TemplateParameterFile + ".temp"
$parameterFileContent | Out-File -FilePath $TemplateParameterFile


Write-Host "Creating ElasticSearch cluster deployment..."
New-AzureResourceGroupDeployment `
    -Name $DeploymentName `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile '.\azuredeploy.json' `
    -TemplateParameterFile $TemplateParameterFile `
    -Verbose
Write-Host "ElasticSearch cluster deployment completed."
    
