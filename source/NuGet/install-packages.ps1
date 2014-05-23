# ==============================================================================
# Microsoft patterns & practices Enterprise Library
# ==============================================================================
# Copyright © Microsoft Corporation.  All rights reserved.
# THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
# OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
# LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
# FITNESS FOR A PARTICULAR PURPOSE.
# ==============================================================================

param (
    [switch] $autoAcceptTerms
)

# list all the solution folders where the "packages" folder will be placed.
$solutionRelativePaths = @('.nuget')


$scriptPath = Split-Path (Get-Variable MyInvocation -Scope 0).Value.MyCommand.Path 

$solutionFolders = New-Object object[] $solutionRelativePaths.Length
$allPackagesFiles = New-Object object[] $solutionRelativePaths.Length
for($i=0; $i -lt $solutionRelativePaths.Length; $i++)
{
    $solutionFolder = Join-Path $scriptPath $solutionRelativePaths[$i]
    $solutionFolders[$i] = $solutionFolder
    $allPackagesFiles[$i] = Get-ChildItem $solutionFolder -Include "packages.config" -Recurse
}


# get all the packages to install
$packages = @()
foreach ($packageFilesForSolution in $allPackagesFiles)
{
    $packageFilesForSolution | ForEach-Object { 
        $xml = New-Object "System.Xml.XmlDocument"
        $xml.Load($_.FullName)
        $xml | Select-Xml -XPath '//packages/package' | 
            Foreach { $packages += " - "+ $_.Node.id + " v" + $_.Node.version }
    }
}

$packages = $packages | Select -uniq | Sort-Object
$packages = [system.string]::Join("`r`n", $packages)

# prompt to continue
$caption = "DOWNLOADING NUGET PACKAGE DEPENDENCIES";
$packageInformation = "You are about to automatically download the following NuGet package dependencies required to run the service:
" + $packages + "
 
Microsoft grants you no rights for third party software.  You are responsible for and must locate and read the license terms for each of the above packages. The owners of the above packages are solely responsible for their content and behavior. Microsoft gives no express warranties, guarantees or conditions.
";

if ($autoAcceptTerms)
{
    Write-Host $caption
    Write-Host $packageInformation
}
else
{
    $message = $packageInformation + "Do you want to proceed?";

    $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes","I agree to download the NuGet packages dependencies.";
    $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No","I do not agree to download the NuGet packages dependencies.";
    $choices = [System.Management.Automation.Host.ChoiceDescription[]]($yes,$no);
    $answer = $host.ui.PromptForChoice($caption,$message,$choices,1) 

    switch ($answer){
        0 { break }
        1 { exit; break }
    } 
}

# copy NuGet.exe bootstrapper to a temp folder if it's not there (this is to avoid distributing the full version of NuGet, and avoiding source control issues with updates).
$nuget = Join-Path $scriptPath '.nuget\NuGet.exe'

$env:EnableNuGetPackageRestore=$true

for($i=0; $i -lt $solutionFolders.Length; $i++)
{
    pushd $solutionFolders[$i]

    # install the packages
    $allPackagesFiles[$i] | ForEach-Object { & $nuget install $_.FullName -o packages }

    $dependencies = @(
		'*NET45\Microsoft.Practices.EnterpriseLibrary.SemanticLogging.dll',
		'*NET45\Microsoft.Practices.EnterpriseLibrary.SemanticLogging.*.dll',
        '*portable*\Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.dll',
        '*NET45\Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling.*.dll',
        '*\EnterpriseLibrary.SemanticLogging.Database.*\scripts\*.*'
        '*NET40\Microsoft.Data.Edm.dll',
        '*NET40\Microsoft.Data.OData.dll',
        '*NET40\Microsoft.Data.Services.Client.dll',
        '*Microsoft.WindowsAzure.Configuration.dll',
        '*NET40\System.Spatial.dll',
        '*NET40\Microsoft.WindowsAzure.Storage.dll',
        '*NET40\Newtonsoft.Json.dll'
        '*net40\Microsoft.Diagnostics.Tracing.TraceEvent.dll'
        )

    
    foreach ($dependency in $dependencies)
    {
        Get-ChildItem -Recurse | Where { $_.FullName -like $dependency } | Copy-Item -Destination $scriptPath
    }

    popd
}

Write-Host "
You can now edit the configuration file to log events from your own application by opening 'SemanticLogging-svc.xml'.
To get IntelliSense from the XML schema file, you can open the configuration in Microsoft Visual Studio.
After the configuration is updated, start the Windows service by executing 'SemanticLogging-svc.exe -start' from an elevated command prompt.

Press ENTER key to finish..."
$x = $host.UI.ReadLine()