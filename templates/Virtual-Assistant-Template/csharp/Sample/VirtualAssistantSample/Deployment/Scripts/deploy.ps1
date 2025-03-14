#Requires -Version 6

Param(
    [string] $name,
	[string] $resourceGroup,
    [string] $location,
	[string] $appId,
    [string] $appPassword,
    [string] $luisAuthoringKey,
	[string] $luisAuthoringRegion,
    [string] $parametersFile,
	[string] $languages = "en-us",
	[string] $projDir = $(Get-Location),
	[string] $logFile = $(Join-Path $PSScriptRoot .. "deploy_log.txt")
)

# Reset log file
if (Test-Path $logFile) {
	Clear-Content $logFile -Force | Out-Null
}
else {
	New-Item -Path $logFile | Out-Null
}

if (-not (Test-Path (Join-Path $projDir 'appsettings.json')))
{
	Write-Host "! Could not find an 'appsettings.json' file in the current directory." -ForegroundColor DarkRed
	Write-Host "+ Please re-run this script from your project directory." -ForegroundColor Magenta
	Break
}

# Get mandatory parameters
if (-not $name) {
    $name = Read-Host "? Bot Name (used as default name for resource group and deployed resources)"
}

if(-not($name -match '^[a-zA-Z0-9_-]{4,42}$')){
	Write-Host "! Bot name must be between 4 and 42 characters and can only have the following characters -, a-z, A-Z, 0-9, and _" -ForegroundColor DarkRed
	Break
}

if (-not $resourceGroup) {
	$resourceGroup = $name
}

if (-not $location) {
    $location = Read-Host "? Azure resource group region"
}

if (-not $appPassword) {
    $appPassword = Read-Host "? Password for MSA app registration (must be at least 16 characters long, contain at least 1 special character, and contain at least 1 numeric character)"
}

if(-not(($appPassword -match '^.{16,}$') -and ($appPassword -match '[!@#$%^&*(),.?:|<>\/_\-\+]{1,}') -and ($appPassword -match '[0-9]{1,}'))){
	Write-Host "! Password for MSA app registration (must be at least 16 characters long, contain at least 1 special character (!@#$%^&*(),.?:|<>), and contain at least 1 numeric character)" -ForegroundColor DarkRed
	Break
}

if (-not $luisAuthoringRegion) {
    $luisAuthoringRegion = Read-Host "? LUIS Authoring Region (westus, westeurope, or australiaeast)"
}

if (-not $luisAuthoringKey) {
	Switch ($luisAuthoringRegion) {
		"westus" { 
			$luisAuthoringKey = Read-Host "? LUIS Authoring Key (found at https://luis.ai/user/settings)"
			Break
		}
		"westeurope" {
		    $luisAuthoringKey = Read-Host "? LUIS Authoring Key (found at https://eu.luis.ai/user/settings)"
			Break
		}
		"australiaeast" {
			$luisAuthoringKey = Read-Host "? LUIS Authoring Key (found at https://au.luis.ai/user/settings)"
			Break
		}
		default {
			Write-Host "! $($luisAuthoringRegion) is not a valid LUIS authoring region." -ForegroundColor DarkRed
			Break
		}
	}

	if (-not $luisAuthoringKey) {
		Break
	}
}

if (-not $appId) {
	# Create app registration
	$app = (az ad app create `
		--display-name $name `
		--password $appPassword `
		--available-to-other-tenants `
		--reply-urls 'https://token.botframework.com/.auth/web/redirect')

	# Retrieve AppId
	if ($app) {
		$appId = ($app | ConvertFrom-Json) | Select-Object -ExpandProperty appId
	}

	if(-not $appId) {
		Write-Host "! Could not provision Microsoft App Registration automatically. Review the log for more information." -ForegroundColor DarkRed
		Write-Host "! Log: $($logFile)" -ForegroundColor DarkRed
		Write-Host "+ Provision an app manually in the Azure Portal, then try again providing the -appId and -appPassword arguments. See https://aka.ms/vamanualappcreation for more information." -ForegroundColor Magenta
		Break
	}
}

# Get timestamp
$timestamp = Get-Date -f MMddyyyyHHmmss

# Create resource group
Write-Host "> Creating resource group ..."
(az group create --name $resourceGroup --location $location) 2>> $logFile | Out-Null

# Deploy Azure services (deploys LUIS, QnA Maker, Content Moderator, CosmosDB)
if ($parametersFile) {
	Write-Host "> Validating Azure deployment ..."
	$validation = az group deployment validate `
		--resource-group $resourcegroup `
		--template-file "$(Join-Path $PSScriptRoot '..' 'Resources' 'template.json')" `
		--parameters "@$($parametersFile)" `
		--parameters name=$name microsoftAppId=$appId microsoftAppPassword="`"$($appPassword)`""

	if ($validation) {
		$validation = $validation | ConvertFrom-Json
	
		if (-not $validation.error) {
			Write-Host "> Deploying Azure services (this could take a while)..." -ForegroundColor Yellow
			$deployment = az group deployment create `
				--name $timestamp `
				--resource-group $resourceGroup `
				--template-file "$(Join-Path $PSScriptRoot '..' 'Resources' 'template.json')" `
				--parameters "@$($parametersFile)" `
				--parameters name=$name microsoftAppId=$appId microsoftAppPassword="`"$($appPassword)`""
		}
		else {
			Write-Host "! Template is not valid with provided parameters." -ForegroundColor DarkRed
			Write-Host "! Error: $($validation.error.message)"  -ForegroundColor DarkRed
			Write-Host "+ To delete this resource group, run 'az group delete -g $($resourceGroup) --no-wait'" -ForegroundColor Magenta
			Break
		}
	}
}
else {
	Write-Host "> Validating Azure deployment ..."
	$validation = az group deployment validate `
		--resource-group $resourcegroup `
		--template-file "$(Join-Path $PSScriptRoot '..' 'Resources' 'template.json')" `
		--parameters name=$name microsoftAppId=$appId microsoftAppPassword="`"$($appPassword)`""

	if ($validation) {
		$validation = $validation | ConvertFrom-Json

		if (-not $validation.error) {
			Write-Host "> Deploying Azure services (this could take a while)..." -ForegroundColor Yellow
			$deployment = az group deployment create `
				--name $timestamp `
				--resource-group $resourceGroup `
				--template-file "$(Join-Path $PSScriptRoot '..' 'Resources' 'template.json')" `
				--parameters name=$name microsoftAppId=$appId microsoftAppPassword="`"$($appPassword)`""
		}
		else {
			Write-Host "! Template is not valid with provided parameters." -ForegroundColor DarkRed
			Write-Host "! Error: $($validation.error.message)" -ForegroundColor DarkRed
			Write-Host "+ To delete this resource group, run 'az group delete -g $($resourceGroup) --no-wait'" -ForegroundColor Magenta
			Break
		}
	}
}

# Get deployment outputs
$outputs = (az group deployment show `
	--name $timestamp `
	--resource-group $resourceGroup `
	--query properties.outputs) 2>> $logFile

# If it succeeded then we perform the remainder of the steps
if ($outputs)
{
	# Log and convert to JSON
	$outputs >> $logFile
	$outputs = $outputs | ConvertFrom-Json

	# Update appsettings.json
	Write-Host "> Updating appsettings.json ..."
	if (Test-Path $(Join-Path $projDir appsettings.json)) {
		$settings = Get-Content $(Join-Path $projDir appsettings.json) | ConvertFrom-Json
	}
	else {
		$settings = New-Object PSObject
	}

	$settings | Add-Member -Type NoteProperty -Force -Name 'microsoftAppId' -Value $appId
	$settings | Add-Member -Type NoteProperty -Force -Name 'microsoftAppPassword' -Value $appPassword
	if ($outputs.ApplicationInsights) { $settings | Add-Member -Type NoteProperty -Force -Name 'ApplicationInsights' -Value $outputs.ApplicationInsights.value }
	if ($outputs.storage) { $settings | Add-Member -Type NoteProperty -Force -Name 'blobStorage' -Value $outputs.storage.value }
	if ($outputs.cosmosDb) { $settings | Add-Member -Type NoteProperty -Force -Name 'cosmosDb' -Value $outputs.cosmosDb.value }
	if ($outputs.contentModerator) { $settings | Add-Member -Type NoteProperty -Force -Name 'contentModerator' -Value $outputs.contentModerator.value }

	$settings | ConvertTo-Json -depth 100 | Out-File $(Join-Path $projDir appsettings.json)

	# Delay to let QnA Maker finish setting up
	Start-Sleep -s 30

	# Deploy cognitive models
	Invoke-Expression "$(Join-Path $PSScriptRoot 'deploy_cognitive_models.ps1') -name $($name) -luisAuthoringRegion $($luisAuthoringRegion) -luisAuthoringKey $($luisAuthoringKey) -luisAccountName $($outputs.luis.value.accountName) -luisSubscriptionKey $($outputs.luis.value.key) -resourceGroup $($resourceGroup) -qnaSubscriptionKey $($outputs.qnaMaker.value.key) -outFolder `"$($projDir)`" -languages `"$($languages)`""
	
	# Publish bot
	Invoke-Expression "$(Join-Path $PSScriptRoot 'publish.ps1') -name $($name) -resourceGroup $($resourceGroup) -projFolder `"$($projDir)`""

	Write-Host "> Done."
}
else
{
	# Check for failed deployments
	$operations = (az group deployment operation list -g $resourceGroup -n $timestamp) 2>> $logFile | Out-Null 
	
	if ($operations) {
		$operations = $operations | ConvertFrom-Json
		$failedOperations = $operations | Where { $_.properties.statusmessage.error -ne $null }
		if ($failedOperations) {
			foreach ($operation in $failedOperations) {
				switch ($operation.properties.statusmessage.error.code) {
					"MissingRegistrationForLocation" {
						Write-Host "! Deployment failed for resource of type $($operation.properties.targetResource.resourceType). This resource is not avaliable in the location provided." -ForegroundColor DarkRed
						Write-Host "+ Update the .\Deployment\Resources\parameters.template.json file with a valid region for this resource and provide the file path in the -parametersFile parameter." -ForegroundColor Magenta
					}
					default {
						Write-Host "! Deployment failed for resource of type $($operation.properties.targetResource.resourceType)."
						Write-Host "! Code: $($operation.properties.statusMessage.error.code)."
						Write-Host "! Message: $($operation.properties.statusMessage.error.message)."
					}
				}
			}
		}
	}
	else {
		Write-Host "! Deployment failed. Please refer to the log file for more information." -ForegroundColor DarkRed
		Write-Host "! Log: $($logFile)" -ForegroundColor DarkRed
	}
	
	Write-Host "+ To delete this resource group, run 'az group delete -g $($resourceGroup) --no-wait'" -ForegroundColor Magenta
	Break
}