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
        '*Microsoft.WindowsAzure.Configuration.dll',
        '*NET40\System.Spatial.dll',
        '*NET40\Microsoft.WindowsAzure.Storage.dll',
        '*NET40\Newtonsoft.Json.dll'
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


# SIG # Begin signature block
# MIIavwYJKoZIhvcNAQcCoIIasDCCGqwCAQExCzAJBgUrDgMCGgUAMGkGCisGAQQB
# gjcCAQSgWzBZMDQGCisGAQQBgjcCAR4wJgIDAQAABBAfzDtgWUsITrck0sYpfvNR
# AgEAAgEAAgEAAgEAAgEAMCEwCQYFKw4DAhoFAAQUW4J7u87QC07eqE/K5oriXr9V
# K3KgghWCMIIEwzCCA6ugAwIBAgITMwAAADPlJ4ajDkoqgAAAAAAAMzANBgkqhkiG
# 9w0BAQUFADB3MQswCQYDVQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4G
# A1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSEw
# HwYDVQQDExhNaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EwHhcNMTMwMzI3MjAwODIz
# WhcNMTQwNjI3MjAwODIzWjCBszELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hp
# bmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jw
# b3JhdGlvbjENMAsGA1UECxMETU9QUjEnMCUGA1UECxMebkNpcGhlciBEU0UgRVNO
# OkY1MjgtMzc3Ny04QTc2MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFtcCBT
# ZXJ2aWNlMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyt7KGQ8fllaC
# X9hCMtQIbbadwMLtfDirWDOta4FQuIghCl2vly2QWsfDLrJM1GN0WP3fxYlU0AvM
# /ZyEEXmsoyEibTPgrt4lQEWSTg1jCCuLN91PB2rcKs8QWo9XXZ09+hdjAsZwPrsi
# 7Vux9zK65HG8ef/4y+lXP3R75vJ9fFdYL6zSDqjZiNlAHzoiQeIJJgKgzOUlzoxn
# g99G+IVNw9pmHsdzfju0dhempaCgdFWo5WAYQWI4x2VGqwQWZlbq+abLQs9dVGQv
# gfjPOAAPEGvhgy6NPkjsSVZK7Jpp9MsPEPsHNEpibAGNbscghMpc0WOZHo5d7A+l
# Fkiqa94hLwIDAQABo4IBCTCCAQUwHQYDVR0OBBYEFABYGz7txfEGk74xPTa0rAtd
# MvCBMB8GA1UdIwQYMBaAFCM0+NlSRnAK7UD7dvuzK7DDNbMPMFQGA1UdHwRNMEsw
# SaBHoEWGQ2h0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3Rz
# L01pY3Jvc29mdFRpbWVTdGFtcFBDQS5jcmwwWAYIKwYBBQUHAQEETDBKMEgGCCsG
# AQUFBzAChjxodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2NlcnRzL01pY3Jv
# c29mdFRpbWVTdGFtcFBDQS5jcnQwEwYDVR0lBAwwCgYIKwYBBQUHAwgwDQYJKoZI
# hvcNAQEFBQADggEBAAL/44wD6u9+OLm5fJ87UoOk+iM41AO4alm16uBviAP0b1Fq
# lTp1hegc3AfFTp0bqM4kRxQkTzV3sZy8J3uPXU/8BouXl/kpm/dAHVKBjnZIA37y
# mxe3rtlbIpFjOzJfNfvGkTzM7w6ZgD4GkTgTegxMvjPbv+2tQcZ8GyR8E9wK/EuK
# IAUdCYmROQdOIU7ebHxwu6vxII74mHhg3IuUz2W+lpAPoJyE7Vy1fEGgYS29Q2dl
# GiqC1KeKWfcy46PnxY2yIruSKNiwjFOPaEdHodgBsPFhFcQXoS3jOmxPb6897t4p
# sETLw5JnugDOD44R79ECgjFJlJidUUh4rR3WQLYwggTsMIID1KADAgECAhMzAAAA
# sBGvCovQO5/dAAEAAACwMA0GCSqGSIb3DQEBBQUAMHkxCzAJBgNVBAYTAlVTMRMw
# EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVN
# aWNyb3NvZnQgQ29ycG9yYXRpb24xIzAhBgNVBAMTGk1pY3Jvc29mdCBDb2RlIFNp
# Z25pbmcgUENBMB4XDTEzMDEyNDIyMzMzOVoXDTE0MDQyNDIyMzMzOVowgYMxCzAJ
# BgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25k
# MR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xDTALBgNVBAsTBE1PUFIx
# HjAcBgNVBAMTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjCCASIwDQYJKoZIhvcNAQEB
# BQADggEPADCCAQoCggEBAOivXKIgDfgofLwFe3+t7ut2rChTPzrbQH2zjjPmVz+l
# URU0VKXPtIupP6g34S1Q7TUWTu9NetsTdoiwLPBZXKnr4dcpdeQbhSeb8/gtnkE2
# KwtA+747urlcdZMWUkvKM8U3sPPrfqj1QRVcCGUdITfwLLoiCxCxEJ13IoWEfE+5
# G5Cw9aP+i/QMmk6g9ckKIeKq4wE2R/0vgmqBA/WpNdyUV537S9QOgts4jxL+49Z6
# dIhk4WLEJS4qrp0YHw4etsKvJLQOULzeHJNcSaZ5tbbbzvlweygBhLgqKc+/qQUF
# 4eAPcU39rVwjgynrx8VKyOgnhNN+xkMLlQAFsU9lccUCAwEAAaOCAWAwggFcMBMG
# A1UdJQQMMAoGCCsGAQUFBwMDMB0GA1UdDgQWBBRZcaZaM03amAeA/4Qevof5cjJB
# 8jBRBgNVHREESjBIpEYwRDENMAsGA1UECxMETU9QUjEzMDEGA1UEBRMqMzE1OTUr
# NGZhZjBiNzEtYWQzNy00YWEzLWE2NzEtNzZiYzA1MjM0NGFkMB8GA1UdIwQYMBaA
# FMsR6MrStBZYAck3LjMWFrlMmgofMFYGA1UdHwRPME0wS6BJoEeGRWh0dHA6Ly9j
# cmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1Y3RzL01pY0NvZFNpZ1BDQV8w
# OC0zMS0yMDEwLmNybDBaBggrBgEFBQcBAQROMEwwSgYIKwYBBQUHMAKGPmh0dHA6
# Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljQ29kU2lnUENBXzA4LTMx
# LTIwMTAuY3J0MA0GCSqGSIb3DQEBBQUAA4IBAQAx124qElczgdWdxuv5OtRETQie
# 7l7falu3ec8CnLx2aJ6QoZwLw3+ijPFNupU5+w3g4Zv0XSQPG42IFTp8263Os8ls
# ujksRX0kEVQmMA0N/0fqAwfl5GZdLHudHakQ+hywdPJPaWueqSSE2u2WoN9zpO9q
# GqxLYp7xfMAUf0jNTbJE+fA8k21C2Oh85hegm2hoCSj5ApfvEQO6Z1Ktwemzc6bS
# Y81K4j7k8079/6HguwITO10g3lU/o66QQDE4dSheBKlGbeb1enlAvR/N6EXVruJd
# PvV1x+ZmY2DM1ZqEh40kMPfvNNBjHbFCZ0oOS786Du+2lTqnOOQlkgimiGaCMIIF
# vDCCA6SgAwIBAgIKYTMmGgAAAAAAMTANBgkqhkiG9w0BAQUFADBfMRMwEQYKCZIm
# iZPyLGQBGRYDY29tMRkwFwYKCZImiZPyLGQBGRYJbWljcm9zb2Z0MS0wKwYDVQQD
# EyRNaWNyb3NvZnQgUm9vdCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkwHhcNMTAwODMx
# MjIxOTMyWhcNMjAwODMxMjIyOTMyWjB5MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
# V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0
# IENvcnBvcmF0aW9uMSMwIQYDVQQDExpNaWNyb3NvZnQgQ29kZSBTaWduaW5nIFBD
# QTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBALJyWVwZMGS/HZpgICBC
# mXZTbD4b1m/My/Hqa/6XFhDg3zp0gxq3L6Ay7P/ewkJOI9VyANs1VwqJyq4gSfTw
# aKxNS42lvXlLcZtHB9r9Jd+ddYjPqnNEf9eB2/O98jakyVxF3K+tPeAoaJcap6Vy
# c1bxF5Tk/TWUcqDWdl8ed0WDhTgW0HNbBbpnUo2lsmkv2hkL/pJ0KeJ2L1TdFDBZ
# +NKNYv3LyV9GMVC5JxPkQDDPcikQKCLHN049oDI9kM2hOAaFXE5WgigqBTK3S9dP
# Y+fSLWLxRT3nrAgA9kahntFbjCZT6HqqSvJGzzc8OJ60d1ylF56NyxGPVjzBrAlf
# A9MCAwEAAaOCAV4wggFaMA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFMsR6MrS
# tBZYAck3LjMWFrlMmgofMAsGA1UdDwQEAwIBhjASBgkrBgEEAYI3FQEEBQIDAQAB
# MCMGCSsGAQQBgjcVAgQWBBT90TFO0yaKleGYYDuoMW+mPLzYLTAZBgkrBgEEAYI3
# FAIEDB4KAFMAdQBiAEMAQTAfBgNVHSMEGDAWgBQOrIJgQFYnl+UlE/wq4QpTlVnk
# pDBQBgNVHR8ESTBHMEWgQ6BBhj9odHRwOi8vY3JsLm1pY3Jvc29mdC5jb20vcGtp
# L2NybC9wcm9kdWN0cy9taWNyb3NvZnRyb290Y2VydC5jcmwwVAYIKwYBBQUHAQEE
# SDBGMEQGCCsGAQUFBzAChjhodHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpL2Nl
# cnRzL01pY3Jvc29mdFJvb3RDZXJ0LmNydDANBgkqhkiG9w0BAQUFAAOCAgEAWTk+
# fyZGr+tvQLEytWrrDi9uqEn361917Uw7LddDrQv+y+ktMaMjzHxQmIAhXaw9L0y6
# oqhWnONwu7i0+Hm1SXL3PupBf8rhDBdpy6WcIC36C1DEVs0t40rSvHDnqA2iA6VW
# 4LiKS1fylUKc8fPv7uOGHzQ8uFaa8FMjhSqkghyT4pQHHfLiTviMocroE6WRTsgb
# 0o9ylSpxbZsa+BzwU9ZnzCL/XB3Nooy9J7J5Y1ZEolHN+emjWFbdmwJFRC9f9Nqu
# 1IIybvyklRPk62nnqaIsvsgrEA5ljpnb9aL6EiYJZTiU8XofSrvR4Vbo0HiWGFzJ
# NRZf3ZMdSY4tvq00RBzuEBUaAF3dNVshzpjHCe6FDoxPbQ4TTj18KUicctHzbMrB
# 7HCjV5JXfZSNoBtIA1r3z6NnCnSlNu0tLxfI5nI3EvRvsTxngvlSso0zFmUeDord
# EN5k9G/ORtTTF+l5xAS00/ss3x+KnqwK+xMnQK3k+eGpf0a7B2BHZWBATrBC7E7t
# s3Z52Ao0CW0cgDEf4g5U3eWh++VHEK1kmP9QFi58vwUheuKVQSdpw5OPlcmN2Jsh
# rg1cnPCiroZogwxqLbt2awAdlq3yFnv2FoMkuYjPaqhHMS+a3ONxPdcAfmJH0c6I
# ybgY+g5yjcGjPa8CQGr/aZuW4hCoELQ3UAjWwz0wggYHMIID76ADAgECAgphFmg0
# AAAAAAAcMA0GCSqGSIb3DQEBBQUAMF8xEzARBgoJkiaJk/IsZAEZFgNjb20xGTAX
# BgoJkiaJk/IsZAEZFgltaWNyb3NvZnQxLTArBgNVBAMTJE1pY3Jvc29mdCBSb290
# IENlcnRpZmljYXRlIEF1dGhvcml0eTAeFw0wNzA0MDMxMjUzMDlaFw0yMTA0MDMx
# MzAzMDlaMHcxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYD
# VQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xITAf
# BgNVBAMTGE1pY3Jvc29mdCBUaW1lLVN0YW1wIFBDQTCCASIwDQYJKoZIhvcNAQEB
# BQADggEPADCCAQoCggEBAJ+hbLHf20iSKnxrLhnhveLjxZlRI1Ctzt0YTiQP7tGn
# 0UytdDAgEesH1VSVFUmUG0KSrphcMCbaAGvoe73siQcP9w4EmPCJzB/LMySHnfL0
# Zxws/HvniB3q506jocEjU8qN+kXPCdBer9CwQgSi+aZsk2fXKNxGU7CG0OUoRi4n
# rIZPVVIM5AMs+2qQkDBuh/NZMJ36ftaXs+ghl3740hPzCLdTbVK0RZCfSABKR2YR
# JylmqJfk0waBSqL5hKcRRxQJgp+E7VV4/gGaHVAIhQAQMEbtt94jRrvELVSfrx54
# QTF3zJvfO4OToWECtR0Nsfz3m7IBziJLVP/5BcPCIAsCAwEAAaOCAaswggGnMA8G
# A1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFCM0+NlSRnAK7UD7dvuzK7DDNbMPMAsG
# A1UdDwQEAwIBhjAQBgkrBgEEAYI3FQEEAwIBADCBmAYDVR0jBIGQMIGNgBQOrIJg
# QFYnl+UlE/wq4QpTlVnkpKFjpGEwXzETMBEGCgmSJomT8ixkARkWA2NvbTEZMBcG
# CgmSJomT8ixkARkWCW1pY3Jvc29mdDEtMCsGA1UEAxMkTWljcm9zb2Z0IFJvb3Qg
# Q2VydGlmaWNhdGUgQXV0aG9yaXR5ghB5rRahSqClrUxzWPQHEy5lMFAGA1UdHwRJ
# MEcwRaBDoEGGP2h0dHA6Ly9jcmwubWljcm9zb2Z0LmNvbS9wa2kvY3JsL3Byb2R1
# Y3RzL21pY3Jvc29mdHJvb3RjZXJ0LmNybDBUBggrBgEFBQcBAQRIMEYwRAYIKwYB
# BQUHMAKGOGh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2VydHMvTWljcm9z
# b2Z0Um9vdENlcnQuY3J0MBMGA1UdJQQMMAoGCCsGAQUFBwMIMA0GCSqGSIb3DQEB
# BQUAA4ICAQAQl4rDXANENt3ptK132855UU0BsS50cVttDBOrzr57j7gu1BKijG1i
# uFcCy04gE1CZ3XpA4le7r1iaHOEdAYasu3jyi9DsOwHu4r6PCgXIjUji8FMV3U+r
# kuTnjWrVgMHmlPIGL4UD6ZEqJCJw+/b85HiZLg33B+JwvBhOnY5rCnKVuKE5nGct
# xVEO6mJcPxaYiyA/4gcaMvnMMUp2MT0rcgvI6nA9/4UKE9/CCmGO8Ne4F+tOi3/F
# NSteo7/rvH0LQnvUU3Ih7jDKu3hlXFsBFwoUDtLaFJj1PLlmWLMtL+f5hYbMUVbo
# nXCUbKw5TNT2eb+qGHpiKe+imyk0BncaYsk9Hm0fgvALxyy7z0Oz5fnsfbXjpKh0
# NbhOxXEjEiZ2CzxSjHFaRkMUvLOzsE1nyJ9C/4B5IYCeFTBm6EISXhrIniIh0EPp
# K+m79EjMLNTYMoBMJipIJF9a6lbvpt6Znco6b72BJ3QGEe52Ib+bgsEnVLaxaj2J
# oXZhtG6hE6a/qkfwEm/9ijJssv7fUciMI8lmvZ0dhxJkAj0tr1mPuOQh5bWwymO0
# eFQF1EEuUKyUsKV4q7OglnUa2ZKHE3UiLzKoCG6gW4wlv6DvhMoh1useT8ma7kng
# 9wFlb4kLfchpyOZu6qeXzjEp/w7FW1zYTRuh2Povnj8uVRZryROj/TGCBKcwggSj
# AgEBMIGQMHkxCzAJBgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYD
# VQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xIzAh
# BgNVBAMTGk1pY3Jvc29mdCBDb2RlIFNpZ25pbmcgUENBAhMzAAAAsBGvCovQO5/d
# AAEAAACwMAkGBSsOAwIaBQCggcAwGQYJKoZIhvcNAQkDMQwGCisGAQQBgjcCAQQw
# HAYKKwYBBAGCNwIBCzEOMAwGCisGAQQBgjcCARUwIwYJKoZIhvcNAQkEMRYEFN1x
# 1gKpdJq2TFdrEkeYAlhTFsOyMGAGCisGAQQBgjcCAQwxUjBQoCqAKABFAG4AdABl
# AHIAcAByAGkAcwBlACAATABpAGIAcgBhAHIAeQAgADahIoAgaHR0cDovL21zZG4u
# bWljcm9zb2Z0LmNvbS9lbnRsaWIwDQYJKoZIhvcNAQEBBQAEggEAmcz6Iqn7T+Jz
# 0gMfCNX74CaBFJkdwlnofzzKz0aum+nn+bHNbdErIkHYNcu2ByQX60zL8agA6GV2
# LnnbzWAeTfe20F+SfZg/ZEC8K0rPQhMzgPOLdN9Foc862qFDGusN3lwR0WJmX51D
# hnc1JTsMHhH6vH2BwPQFuaVBsudR75/LhqKngFUTPo5TLY5UnD7xFjWQPyHenx1L
# bmd6AEOVfTS1rHAPDjBuKTqlnlMTF2nTBwqqmyxXgiQwUflhywv2I20fnhuDPUmC
# fv29gT5xaY3YGT4dJAkQXkPWDrRKcreaZYz8D3QHLVBauckh6MWqLgsvh00Cg6Ni
# pHnRA3BGHqGCAigwggIkBgkqhkiG9w0BCQYxggIVMIICEQIBATCBjjB3MQswCQYD
# VQQGEwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEe
# MBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSEwHwYDVQQDExhNaWNyb3Nv
# ZnQgVGltZS1TdGFtcCBQQ0ECEzMAAAAz5SeGow5KKoAAAAAAADMwCQYFKw4DAhoF
# AKBdMBgGCSqGSIb3DQEJAzELBgkqhkiG9w0BBwEwHAYJKoZIhvcNAQkFMQ8XDTEz
# MDQyNTE4MzkwNFowIwYJKoZIhvcNAQkEMRYEFPEBPpBhhBOBe8gOjfcjE9du/OZ7
# MA0GCSqGSIb3DQEBBQUABIIBAFtkYwlfkXwHDX9tmnfGwTqT2brRQja3h2Aa5WL2
# z6eunJBIhT8+qPomfkecqsGhKIIjJCPcqVCuoCR9sj+ub5TT3kcT3W2lyVSIm/Jy
# IPGMqrK5l7Gqb7EjXAOXFj8QxYO5njYKVdK9q3+Lx/9tfZxUV1dS8dj7ft7keJyt
# Ge8BKc46BjJVjPYVj8SvKpGETjuwv/rpQ042fB2sk3/KaXzKkTGKjWJnv2chHBAl
# a8Tkx0puim8j7xxkIUaFqultlyw0e/+pES0qu2PMZT9zDSv3rnvC8xB0P7RTOWQQ
# +jwdXdD5EUNq1eeVj4G92abncbjPjT0r4/beno0wDrlM7A4=
# SIG # End signature block
