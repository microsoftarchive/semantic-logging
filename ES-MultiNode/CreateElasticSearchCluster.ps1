param(
[string] $TemplateParameterFile = ".\azuredeploy.json",
[Parameter(Mandatory=$true)][string] $ResourceGroupName,
[Parameter(Mandatory=$true)][string] $Region,
[Parameter(Mandatory=$true)][string] $EsPassword,
[string] $DeploymentName = "ES-default-deployment",
[string] $PublicSshKeyFile = "",
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
$templateParameters['esPassword'] = $EsPassword

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

$templateParameters['dnsDomainForLoadBalancerIP'] = $Region.ToLowerInvariant().Replace(" ", "") + ".cloudapp.azure.com";
Write-Verbose "Using $($templateParameters['dnsDomainForLoadBalancerIP']) as the domain name for the ElasticSearch cluster"

Get-AzureRmResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue -ErrorVariable groupMissing
if ($groupMissing)
{
    Write-Host "Creating new resource group $ResourceGroupName ..."
    New-AzureRmResourceGroup -Name $ResourceGroupName -Location $Region
}
else 
{
    if ($RemoveExistingResourceGroup)
    {
        Write-Host "Removing old resource group $ResourceGroupName..."
        Remove-AzureRmResourceGroup -Name $ResourceGroupName -Force -ErrorAction Continue -Verbose
        Write-Host "Creating new resource group $ResourceGroupName..."
        New-AzureRmResourceGroup -Name $ResourceGroupName -Location $Region
    }
    else 
    {
        throw "Resource group '$ResourceGroupName' already exists"
    }
}


Write-Host "Creating ElasticSearch cluster deployment..."
New-AzureRmResourceGroupDeployment `
    -Name $DeploymentName `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile '.\azuredeploy.json' `
    -TemplateParameterObject $templateParameters `
    -Verbose
Write-Host "ElasticSearch cluster deployment completed."
    
