#!/usr/bin/env pwsh
# Red/Green Deployment Script for Volunteer Check-in
# Supports multiple backend slots for zero-downtime deployments

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("local", "production")]
    [string]$Environment,

    [Parameter(Mandatory=$false)]
    [switch]$Frontend,

    [Parameter(Mandatory=$false)]
    [switch]$Backend
)

# ============================================================================
# Configuration
# ============================================================================
$script:ScriptRoot = $PSScriptRoot
$script:StateFilePath = Join-Path $PSScriptRoot ".deployment\state.json"
$script:SecretsDir = Join-Path $env:USERPROFILE ".onthedayapp"
$script:SecretsFilePath = Join-Path $script:SecretsDir "secrets.json"

# Required environment variables for backend
$script:RequiredEnvVars = @(
    "AzureWebJobsStorage",
    "DEPLOYMENT_STORAGE_CONNECTION_STRING",
    "FROM_EMAIL",
    "FROM_NAME",
    "FRONTEND_URL",
    "SMTP_HOST",
    "SMTP_PASSWORD",
    "SMTP_USERNAME"
)

# ============================================================================
# Output Functions
# ============================================================================
function Write-Info($message) { Write-Host $message -ForegroundColor Cyan }
function Write-Success($message) { Write-Host $message -ForegroundColor Green }
function Write-Warning($message) { Write-Host $message -ForegroundColor Yellow }
function Write-ErrorMessage($message) { Write-Host $message -ForegroundColor Red }
function Write-Gray($message) { Write-Host $message -ForegroundColor Gray }

# ============================================================================
# State File Management
# ============================================================================
function Get-DeploymentState {
    if (-not (Test-Path $script:StateFilePath)) {
        Write-ErrorMessage "Deployment state file not found at: $script:StateFilePath"
        Write-ErrorMessage "Please ensure .deployment/state.json exists."
        exit 1
    }
    return Get-Content $script:StateFilePath -Raw | ConvertFrom-Json
}

function Save-DeploymentState($state) {
    $state | ConvertTo-Json -Depth 10 | Set-Content $script:StateFilePath -Encoding UTF8
}

function Get-EnvironmentConfig($envName) {
    $state = Get-DeploymentState
    $envConfig = $state.environments.$envName
    if (-not $envConfig) {
        Write-ErrorMessage "Environment '$envName' not found in deployment state."
        exit 1
    }
    return $envConfig
}

# ============================================================================
# Secrets Management (DPAPI)
# ============================================================================
function Ensure-SecretsDir {
    if (-not (Test-Path $script:SecretsDir)) {
        New-Item -ItemType Directory -Path $script:SecretsDir -Force | Out-Null
    }
}

function Get-StoredSecrets {
    Ensure-SecretsDir
    if (-not (Test-Path $script:SecretsFilePath)) {
        return @{}
    }

    try {
        $encrypted = Get-Content $script:SecretsFilePath -Raw | ConvertFrom-Json
        $decrypted = @{}

        foreach ($prop in $encrypted.PSObject.Properties) {
            $key = $prop.Name
            $encValue = $prop.Value

            if ($encValue) {
                try {
                    $secureString = ConvertTo-SecureString $encValue -ErrorAction Stop
                    $credential = New-Object System.Management.Automation.PSCredential("dummy", $secureString)
                    $decrypted[$key] = $credential.GetNetworkCredential().Password
                } catch {
                    Write-Warning "Failed to decrypt secret '$key'. It may have been created by a different user."
                    $decrypted[$key] = $null
                }
            }
        }

        return $decrypted
    } catch {
        Write-Warning "Failed to read secrets file: $_"
        return @{}
    }
}

function Save-StoredSecrets($secrets) {
    Ensure-SecretsDir

    $encrypted = @{}
    foreach ($key in $secrets.Keys) {
        $value = $secrets[$key]
        if ($value) {
            $secureString = ConvertTo-SecureString $value -AsPlainText -Force
            $encrypted[$key] = ConvertFrom-SecureString $secureString
        }
    }

    $encrypted | ConvertTo-Json -Depth 10 | Set-Content $script:SecretsFilePath -Encoding UTF8
}

function Get-SecretKey($slotName, $varName) {
    return "$slotName`:$varName"
}

function Get-SlotSecret($slotName, $varName) {
    $secrets = Get-StoredSecrets
    $key = Get-SecretKey $slotName $varName
    return $secrets[$key]
}

function Set-SlotSecret($slotName, $varName, $value) {
    $secrets = Get-StoredSecrets
    $key = Get-SecretKey $slotName $varName
    $secrets[$key] = $value
    Save-StoredSecrets $secrets
}

function Get-SwaDeploymentToken {
    $secrets = Get-StoredSecrets
    return $secrets["swa-deployment-token"]
}

function Set-SwaDeploymentToken($token) {
    $secrets = Get-StoredSecrets
    $secrets["swa-deployment-token"] = $token
    Save-StoredSecrets $secrets
}

# ============================================================================
# Azure CLI Helpers
# ============================================================================
function Test-AzureCliInstalled {
    $azCommand = Get-Command az -ErrorAction SilentlyContinue
    return $null -ne $azCommand
}

function Test-AzureCliLoggedIn {
    if (-not (Test-AzureCliInstalled)) {
        return $false
    }

    $ErrorActionPreference = "Continue"
    $null = az account show 2>&1
    $result = $LASTEXITCODE -eq 0
    $ErrorActionPreference = "Stop"

    return $result
}

function Ensure-AzureCliLoggedIn {
    if (-not (Test-AzureCliInstalled)) {
        Write-ErrorMessage "Azure CLI not found. Please install it from https://aka.ms/installazurecliwindows"
        exit 1
    }

    if (-not (Test-AzureCliLoggedIn)) {
        Write-Warning "Not logged in to Azure CLI. Opening login..."
        az login
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Azure login failed."
            exit 1
        }
    }
}

function Get-AzureFunctionAppSettings($resourceGroup, $appName) {
    $ErrorActionPreference = "Continue"
    $result = az functionapp config appsettings list --resource-group $resourceGroup --name $appName 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"

    if ($exitCode -ne 0) {
        Write-ErrorMessage "Failed to get app settings for $appName"
        Write-Gray $result
        return $null
    }

    return $result | ConvertFrom-Json
}

function Set-AzureFunctionAppSetting($resourceGroup, $appName, $name, $value) {
    $ErrorActionPreference = "Continue"
    $result = az functionapp config appsettings set --resource-group $resourceGroup --name $appName --settings "$name=$value" 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"

    return $exitCode -eq 0
}

function Set-AzureFunctionAppCors($resourceGroup, $appName, $origins) {
    Write-Gray "Setting CORS for $appName..."

    # Clear existing CORS
    $ErrorActionPreference = "Continue"
    $null = az functionapp cors remove --resource-group $resourceGroup --name $appName --allowed-origins "*" 2>&1
    $ErrorActionPreference = "Stop"

    # Add each origin
    foreach ($origin in $origins) {
        $ErrorActionPreference = "Continue"
        $null = az functionapp cors add --resource-group $resourceGroup --name $appName --allowed-origins $origin 2>&1
        $ErrorActionPreference = "Stop"
    }

    Write-Success "CORS configured for $appName"
}

# ============================================================================
# Environment Variable Sync
# ============================================================================
function Sync-EnvironmentVariables($resourceGroup, $slotName) {
    Write-Info "Checking environment variables for $slotName..."

    $azureSettings = Get-AzureFunctionAppSettings $resourceGroup $slotName
    if (-not $azureSettings) {
        Write-ErrorMessage "Could not fetch Azure settings. Aborting."
        exit 1
    }

    # Convert Azure settings to hashtable
    $azureVars = @{}
    foreach ($setting in $azureSettings) {
        $azureVars[$setting.name] = $setting.value
    }

    $needsUpdate = $false
    $updates = @{}

    foreach ($varName in $script:RequiredEnvVars) {
        $localValue = Get-SlotSecret $slotName $varName
        $azureValue = $azureVars[$varName]

        $hasLocal = -not [string]::IsNullOrEmpty($localValue)
        $hasAzure = -not [string]::IsNullOrEmpty($azureValue)

        if (-not $hasLocal -and -not $hasAzure) {
            # Neither has it - need to get from user
            Write-Warning "  $varName - NOT SET anywhere"
            Write-Host ""
            $newValue = Read-Host "  Enter value for $varName"

            if ([string]::IsNullOrWhiteSpace($newValue)) {
                Write-ErrorMessage "  $varName cannot be empty."
                exit 1
            }

            Set-SlotSecret $slotName $varName $newValue
            $updates[$varName] = $newValue
            $needsUpdate = $true
            Write-Success "  $varName - saved locally and will be set in Azure"

        } elseif (-not $hasLocal -and $hasAzure) {
            # Azure has it, store locally
            Set-SlotSecret $slotName $varName $azureValue
            Write-Success "  $varName - fetched from Azure and stored locally"

        } elseif ($hasLocal -and -not $hasAzure) {
            # Local has it, push to Azure
            $updates[$varName] = $localValue
            $needsUpdate = $true
            Write-Warning "  $varName - missing in Azure, will be set from local"

        } elseif ($localValue -ne $azureValue) {
            # Both have it but different
            Write-Warning "  $varName - VALUES DIFFER"
            Write-Gray "    Local:  $(if ($localValue.Length -gt 20) { $localValue.Substring(0, 20) + '...' } else { $localValue })"
            Write-Gray "    Azure:  $(if ($azureValue.Length -gt 20) { $azureValue.Substring(0, 20) + '...' } else { $azureValue })"
            Write-Host ""
            Write-Host "  Which value should be used?" -ForegroundColor Cyan
            Write-Host "    1. Keep LOCAL value (update Azure)" -ForegroundColor White
            Write-Host "    2. Keep AZURE value (update local)" -ForegroundColor White
            Write-Host "    3. Enter NEW value" -ForegroundColor White
            Write-Host ""

            $choice = Read-Host "  Enter choice (1, 2, or 3)"

            switch ($choice) {
                "1" {
                    $updates[$varName] = $localValue
                    $needsUpdate = $true
                    Write-Success "  Using local value for $varName"
                }
                "2" {
                    Set-SlotSecret $slotName $varName $azureValue
                    Write-Success "  Using Azure value for $varName"
                }
                "3" {
                    $newValue = Read-Host "  Enter new value for $varName"
                    if ([string]::IsNullOrWhiteSpace($newValue)) {
                        Write-ErrorMessage "  $varName cannot be empty."
                        exit 1
                    }
                    Set-SlotSecret $slotName $varName $newValue
                    $updates[$varName] = $newValue
                    $needsUpdate = $true
                    Write-Success "  $varName updated"
                }
                default {
                    Write-ErrorMessage "Invalid choice."
                    exit 1
                }
            }
        } else {
            # Both have same value
            Write-Success "  $varName - OK"
        }
    }

    Write-Host ""

    # Ask if user wants to review/change any variables
    Write-Host "Would you like to review or change any environment variables?" -ForegroundColor Cyan
    Write-Host "  1. No, continue with deployment" -ForegroundColor White
    Write-Host "  2. Yes, let me review/change variables" -ForegroundColor White
    Write-Host ""

    $reviewChoice = Read-Host "Enter choice (1 or 2)"

    if ($reviewChoice -eq "2") {
        Write-Host ""
        Write-Info "Current values (stored locally):"

        foreach ($varName in $script:RequiredEnvVars) {
            $value = Get-SlotSecret $slotName $varName
            $displayValue = if ($value.Length -gt 30) { $value.Substring(0, 30) + '...' } else { $value }
            Write-Host "  $varName = $displayValue" -ForegroundColor Gray
        }

        Write-Host ""
        Write-Host "Enter variable name to change (or press Enter to continue):" -ForegroundColor Cyan

        while ($true) {
            $varToChange = Read-Host "Variable name"

            if ([string]::IsNullOrWhiteSpace($varToChange)) {
                break
            }

            if ($script:RequiredEnvVars -notcontains $varToChange) {
                Write-Warning "Unknown variable: $varToChange"
                Write-Gray "Valid variables: $($script:RequiredEnvVars -join ', ')"
                continue
            }

            $newValue = Read-Host "New value for $varToChange"

            if (-not [string]::IsNullOrWhiteSpace($newValue)) {
                Set-SlotSecret $slotName $varToChange $newValue
                $updates[$varToChange] = $newValue
                $needsUpdate = $true
                Write-Success "$varToChange updated"
            }

            Write-Host ""
            Write-Host "Enter another variable name to change (or press Enter to continue):" -ForegroundColor Cyan
        }
    }

    # Apply updates to Azure
    if ($needsUpdate -and $updates.Count -gt 0) {
        Write-Host ""
        Write-Info "Applying $($updates.Count) setting(s) to Azure..."

        foreach ($key in $updates.Keys) {
            Write-Gray "  Setting $key..."
            $success = Set-AzureFunctionAppSetting $resourceGroup $slotName $key $updates[$key]
            if (-not $success) {
                Write-ErrorMessage "Failed to set $key"
                exit 1
            }
        }

        Write-Success "All settings applied to Azure"
    }

    Write-Host ""
}

# ============================================================================
# Slot Selection
# ============================================================================
function Select-DeploymentSlot($envConfig) {
    $slots = $envConfig.slots
    $lastSlot = $envConfig.lastDeployedSlot

    if ($slots.Count -eq 1) {
        return $slots[0]
    }

    # Suggest the "other" slot
    $suggestedSlot = $null
    if ($lastSlot) {
        $lastIndex = [array]::IndexOf($slots, $lastSlot)
        if ($lastIndex -ge 0) {
            $nextIndex = ($lastIndex + 1) % $slots.Count
            $suggestedSlot = $slots[$nextIndex]
        }
    }

    if (-not $suggestedSlot) {
        $suggestedSlot = $slots[0]
    }

    Write-Host ""
    Write-Info "Select deployment slot:"

    for ($i = 0; $i -lt $slots.Count; $i++) {
        $slot = $slots[$i]
        $marker = ""
        if ($slot -eq $suggestedSlot) {
            $marker = " (Recommended)"
        }
        if ($slot -eq $lastSlot) {
            $marker += " [Last deployed]"
        }
        Write-Host "  $($i + 1). $slot$marker" -ForegroundColor White
    }

    Write-Host ""
    $choice = Read-Host "Enter choice (1-$($slots.Count), default: 1)"

    if ([string]::IsNullOrWhiteSpace($choice)) {
        $choice = "1"
    }

    $index = [int]$choice - 1
    if ($index -lt 0 -or $index -ge $slots.Count) {
        Write-ErrorMessage "Invalid choice."
        exit 1
    }

    return $slots[$index]
}

# ============================================================================
# Version Verification
# ============================================================================
function Get-ExpectedVersion {
    $timestamp = Get-Date -Format "yyyy.MM.dd.HH.mm"
    $gitHash = git rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $gitHash) {
        $gitHash = "unknown"
    }
    return "$timestamp-$gitHash"
}

function Test-BackendVersion($slotName, $expectedVersion, $maxRetries = 10) {
    $url = "https://$slotName.azurewebsites.net/api/version"

    Write-Info "Verifying backend deployment..."
    Write-Gray "  Expected version: $expectedVersion"
    Write-Gray "  URL: $url"

    for ($i = 1; $i -le $maxRetries; $i++) {
        Write-Gray "  Attempt $i of $maxRetries..."

        try {
            $response = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 10
            $actualVersion = $response.version

            Write-Gray "  Actual version: $actualVersion"

            if ($actualVersion -eq $expectedVersion) {
                Write-Success "Backend version verified!"
                return $true
            }

            Write-Warning "  Version mismatch, waiting for deployment to complete..."
        } catch {
            Write-Gray "  Request failed: $($_.Exception.Message)"
        }

        if ($i -lt $maxRetries) {
            Start-Sleep -Seconds 10
        }
    }

    Write-ErrorMessage "Backend version verification failed after $maxRetries attempts."
    return $false
}

# ============================================================================
# Git Status Check
# ============================================================================
function Test-GitClean {
    $status = git status --porcelain 2>$null
    return [string]::IsNullOrWhiteSpace($status)
}

function Get-GitCommitHash {
    return git rev-parse --short HEAD 2>$null
}

function Ensure-GitClean {
    if (-not (Test-GitClean)) {
        Write-Host ""
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host "!!                                                          !!" -ForegroundColor Red
        Write-Host "!!                        WARNING                           !!" -ForegroundColor Red
        Write-Host "!!                                                          !!" -ForegroundColor Red
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host ""
        Write-Warning "You have uncommitted changes. Production deployments require a clean git state"
        Write-Warning "so the version number includes the correct commit hash."
        Write-Host ""

        git status --short | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

        Write-Host ""
        Write-Host "Would you like to commit and push your changes now?" -ForegroundColor Cyan
        Write-Host "  1. Yes, commit and push for me" -ForegroundColor White
        Write-Host "  2. Cancel deployment" -ForegroundColor White
        Write-Host ""

        $choice = Read-Host "Enter choice (1 or 2)"

        switch ($choice) {
            "1" {
                Write-Host ""
                $commitMessage = Read-Host "Enter commit message"

                if ([string]::IsNullOrWhiteSpace($commitMessage)) {
                    Write-ErrorMessage "Commit message cannot be empty."
                    exit 1
                }

                Write-Info "Committing changes..."
                git add -A
                if ($LASTEXITCODE -ne 0) {
                    Write-ErrorMessage "Failed to stage changes."
                    exit 1
                }

                git commit -m "$commitMessage"
                if ($LASTEXITCODE -ne 0) {
                    Write-ErrorMessage "Failed to commit changes."
                    exit 1
                }

                Write-Success "Changes committed."

                Write-Info "Pushing to origin..."
                git push
                if ($LASTEXITCODE -ne 0) {
                    Write-ErrorMessage "Failed to push. You may need to pull first or resolve conflicts."
                    exit 1
                }

                Write-Success "Changes pushed to origin."
            }
            default {
                Write-Info "Deployment cancelled."
                exit 0
            }
        }
    }
}

# ============================================================================
# Local Backend - Start Azure Functions
# ============================================================================
function Start-LocalBackend {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Starting Azure Functions"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Check if already running and stop it
    $connection = Get-NetTCPConnection -LocalPort 7071 -State Listen -ErrorAction SilentlyContinue
    if ($connection) {
        Write-Warning "Azure Functions is running on port 7071. Shutting it down..."
        $processId = $connection.OwningProcess
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Success "Azure Functions stopped"
    }

    # Check for Azurite
    Write-Info "Checking Azurite (Azure Storage Emulator)..."
    $azuriteRunning = Get-NetTCPConnection -LocalPort 10000 -State Listen -ErrorAction SilentlyContinue

    if (-not $azuriteRunning) {
        Write-Warning "Azurite not running. Starting Azurite..."
        try {
            Start-Process -FilePath "azurite" -ArgumentList "--silent" -WindowStyle Hidden -ErrorAction Stop
            Start-Sleep -Seconds 2
            Write-Success "Azurite started"
        } catch {
            Write-Warning "Could not start Azurite. Install with: npm install -g azurite"
        }
    } else {
        Write-Success "Azurite already running"
    }

    # Build the backend
    Write-Info "Building Azure Functions..."
    $backendPath = Join-Path $script:ScriptRoot "Backend"
    Push-Location $backendPath
    try {
        dotnet build --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Backend build failed"
            return $false
        }
        Write-Success "Backend build completed"
    } finally {
        Pop-Location
    }

    Write-Info "Starting Azure Functions..."
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "cd '$backendPath'; func start" -WindowStyle Normal
    Start-Sleep -Seconds 5

    $funcRunning = Get-NetTCPConnection -LocalPort 7071 -State Listen -ErrorAction SilentlyContinue
    if ($funcRunning) {
        Write-Success "Azure Functions started on http://localhost:7071"
    } else {
        Write-Warning "Azure Functions may still be starting. Check the console window."
    }

    return $true
}

# ============================================================================
# Local Frontend - Start Dev Server
# ============================================================================
function Start-LocalFrontend {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Starting Frontend Dev Server"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Check if already running and stop it
    $connection = Get-NetTCPConnection -LocalPort 5174 -State Listen -ErrorAction SilentlyContinue
    if ($connection) {
        Write-Warning "Frontend dev server is running on port 5174. Shutting it down..."
        $processId = $connection.OwningProcess
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Success "Frontend dev server stopped"
    }

    $frontendPath = Join-Path $script:ScriptRoot "FrontEnd"

    # Check if node_modules exists
    $nodeModules = Join-Path $frontendPath "node_modules"
    if (-not (Test-Path $nodeModules)) {
        Write-Info "Installing frontend dependencies..."
        Push-Location $frontendPath
        try {
            npm install --silent
            if ($LASTEXITCODE -ne 0) {
                Write-ErrorMessage "Failed to install dependencies"
                return $false
            }
            Write-Success "Dependencies installed"
        } finally {
            Pop-Location
        }
    }

    Write-Info "Starting frontend dev server..."
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "cd '$frontendPath'; npm run dev -- --port 5174" -WindowStyle Normal
    Start-Sleep -Seconds 3

    $viteRunning = Get-NetTCPConnection -LocalPort 5174 -State Listen -ErrorAction SilentlyContinue
    if ($viteRunning) {
        Write-Success "Frontend started on http://localhost:5174"
    } else {
        Write-Warning "Frontend may still be starting. Check the console window."
    }

    return $true
}

# ============================================================================
# Production Backend Deployment
# ============================================================================
function Deploy-ProductionBackend($envConfig, $slotName) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Backend to $slotName"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $resourceGroup = $envConfig.resourceGroup
    $backendPath = Join-Path $script:ScriptRoot "Backend"

    # Sync environment variables
    Sync-EnvironmentVariables $resourceGroup $slotName

    # Get expected version before deployment
    $expectedVersion = Get-ExpectedVersion

    # Build the backend
    Write-Info "Building backend..."
    Push-Location $backendPath
    try {
        dotnet build --configuration Release --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Backend build failed"
            return $false
        }
        Write-Success "Backend build completed"
    } finally {
        Pop-Location
    }

    # Start CORS configuration in background
    Write-Info "Configuring CORS (parallel)..."
    $corsJob = Start-Job -ScriptBlock {
        param($resourceGroup, $slotName, $origins)

        foreach ($origin in $origins) {
            az functionapp cors add --resource-group $resourceGroup --name $slotName --allowed-origins $origin 2>&1 | Out-Null
        }
    } -ArgumentList $resourceGroup, $slotName, $envConfig.cors

    # Deploy to Azure
    Write-Host ""
    Write-Info "Deploying to Azure Functions..."
    Write-Gray "This may take a few minutes..."
    Write-Host ""

    Push-Location $backendPath
    try {
        $ErrorActionPreference = "Continue"
        func azure functionapp publish $slotName 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) {
                if ($_.Exception.Message -and $_.Exception.Message.Trim()) {
                    Write-Host $_.Exception.Message
                }
            } else {
                Write-Host $_
            }
        }
        $funcResult = $LASTEXITCODE
        $ErrorActionPreference = "Stop"

        if ($funcResult -ne 0) {
            Write-ErrorMessage "Backend deployment failed"
            return $false
        }
    } finally {
        Pop-Location
    }

    # Set deployment version
    Write-Info "Setting deployment version..."
    $success = Set-AzureFunctionAppSetting $resourceGroup $slotName "DEPLOYMENT_VERSION" $expectedVersion
    if (-not $success) {
        Write-ErrorMessage "Failed to set DEPLOYMENT_VERSION"
        return $false
    }
    Write-Success "DEPLOYMENT_VERSION set to $expectedVersion"

    # Wait for CORS job to complete
    Write-Info "Waiting for CORS configuration to complete..."
    $null = Wait-Job $corsJob -Timeout 60
    Remove-Job $corsJob -Force -ErrorAction SilentlyContinue
    Write-Success "CORS configuration completed"

    # Verify deployment
    Write-Host ""
    $verified = Test-BackendVersion $slotName $expectedVersion
    if (-not $verified) {
        Write-ErrorMessage "Backend verification failed. The deployment may have issues."
        Write-Host ""
        Write-Host "Would you like to continue anyway?" -ForegroundColor Cyan
        Write-Host "  1. Yes, continue" -ForegroundColor White
        Write-Host "  2. No, abort" -ForegroundColor White
        Write-Host ""

        $choice = Read-Host "Enter choice (1 or 2)"
        if ($choice -ne "1") {
            return $false
        }
    }

    # Update state with last deployed slot
    $state = Get-DeploymentState
    $state.environments.production.lastDeployedSlot = $slotName
    Save-DeploymentState $state

    Write-Host ""
    Write-Success "Backend deployed to $slotName!"
    Write-Gray "URL: https://$slotName.azurewebsites.net/api"

    return $true
}

# ============================================================================
# Production Frontend Deployment
# ============================================================================
function Deploy-ProductionFrontend($envConfig, $backendSlot) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Frontend to Azure Static Web Apps"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $frontendPath = Join-Path $script:ScriptRoot "FrontEnd"
    $resourceGroup = $envConfig.resourceGroup
    $staticWebAppName = $envConfig.frontend.staticWebAppName
    $frontendUrl = $envConfig.frontend.url
    $apiUrl = "https://$backendSlot.azurewebsites.net/api"

    # Check SWA CLI
    Write-Info "Checking Azure Static Web Apps CLI..."
    $swaCommand = Get-Command swa -ErrorAction SilentlyContinue
    if (-not $swaCommand) {
        Write-Warning "Azure Static Web Apps CLI not found."
        Write-Host ""
        Write-Host "Would you like to install it now?" -ForegroundColor Cyan
        Write-Host "  1. Yes, install @azure/static-web-apps-cli globally" -ForegroundColor White
        Write-Host "  2. No, cancel deployment" -ForegroundColor White
        Write-Host ""

        $installChoice = Read-Host "Enter choice (1 or 2)"

        if ($installChoice -eq "1") {
            Write-Info "Installing Azure Static Web Apps CLI..."
            npm install -g @azure/static-web-apps-cli
            if ($LASTEXITCODE -ne 0) {
                Write-ErrorMessage "Failed to install SWA CLI"
                return $false
            }
            Write-Success "SWA CLI installed"
        } else {
            return $false
        }
    }

    # Get deployment token
    $deploymentToken = Get-SwaDeploymentToken

    if (-not $deploymentToken) {
        Write-Warning "Deployment token not found."
        Write-Host ""
        Write-Host "How would you like to provide the token?" -ForegroundColor Cyan
        Write-Host "  1. Fetch automatically from Azure (requires Azure CLI)" -ForegroundColor White
        Write-Host "  2. Enter manually" -ForegroundColor White
        Write-Host ""

        $tokenChoice = Read-Host "Enter choice (1 or 2)"

        switch ($tokenChoice) {
            "1" {
                Ensure-AzureCliLoggedIn

                Write-Info "Fetching deployment token from Azure..."
                $ErrorActionPreference = "Continue"
                $deploymentToken = az staticwebapp secrets list `
                    --name $staticWebAppName `
                    --resource-group $resourceGroup `
                    --query "properties.apiKey" -o tsv 2>&1
                $tokenResult = $LASTEXITCODE
                $ErrorActionPreference = "Stop"

                if ($tokenResult -ne 0 -or -not $deploymentToken -or $deploymentToken -like "*ERROR*") {
                    Write-ErrorMessage "Failed to fetch deployment token"
                    return $false
                }

                Write-Success "Deployment token retrieved"
            }
            "2" {
                Write-Host ""
                Write-Gray "Find the token in Azure Portal:"
                Write-Gray "  Static Web Apps > $staticWebAppName > Manage deployment token"
                Write-Host ""
                $deploymentToken = Read-Host "Enter deployment token"

                if ([string]::IsNullOrWhiteSpace($deploymentToken)) {
                    Write-ErrorMessage "No token provided"
                    return $false
                }
            }
            default {
                return $false
            }
        }

        # Save token
        Set-SwaDeploymentToken $deploymentToken
        Write-Success "Deployment token saved"
    }

    # Verify backend is healthy before deploying frontend
    Write-Info "Verifying backend is healthy..."
    $versionUrl = "https://$backendSlot.azurewebsites.net/api/version"

    try {
        $response = Invoke-RestMethod -Uri $versionUrl -Method Get -TimeoutSec 10
        Write-Success "Backend is healthy (version: $($response.version))"
    } catch {
        Write-ErrorMessage "Backend is not responding. Cannot deploy frontend."
        Write-Gray "URL: $versionUrl"
        Write-Gray "Error: $($_.Exception.Message)"
        return $false
    }

    # Build frontend
    Write-Info "Building frontend..."
    Write-Gray "  API URL: $apiUrl"
    Write-Gray "  Frontend URL: $frontendUrl"

    Push-Location $frontendPath
    try {
        # Install dependencies
        npm install --silent
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Failed to install dependencies"
            return $false
        }

        # Build with environment variables
        $env:VITE_API_BASE_URL = $apiUrl
        $env:VITE_BASE_PATH = "/"
        $env:VITE_FRONTEND_URL = $frontendUrl
        $env:VITE_USE_HASH_ROUTING = "false"

        npm run build

        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Frontend build failed"
            return $false
        }

        Write-Success "Frontend build completed"

        # Deploy
        $distPath = Join-Path $frontendPath "dist"

        Write-Host ""
        Write-Info "Deploying to Azure Static Web Apps..."

        $ErrorActionPreference = "Continue"
        swa deploy $distPath `
            --deployment-token $deploymentToken `
            --env production 2>&1 | ForEach-Object {
                if ($_ -is [System.Management.Automation.ErrorRecord]) {
                    if ($_.Exception.Message -and $_.Exception.Message.Trim()) {
                        Write-Host $_.Exception.Message
                    }
                } else {
                    Write-Host $_
                }
            }
        $swaResult = $LASTEXITCODE
        $ErrorActionPreference = "Stop"

        if ($swaResult -ne 0) {
            Write-ErrorMessage "Frontend deployment failed"
            return $false
        }

    } finally {
        Pop-Location

        # Clean up environment variables
        Remove-Item Env:\VITE_API_BASE_URL -ErrorAction SilentlyContinue
        Remove-Item Env:\VITE_BASE_PATH -ErrorAction SilentlyContinue
        Remove-Item Env:\VITE_FRONTEND_URL -ErrorAction SilentlyContinue
        Remove-Item Env:\VITE_USE_HASH_ROUTING -ErrorAction SilentlyContinue
    }

    Write-Host ""
    Write-Success "Frontend deployed to Azure Static Web Apps!"
    Write-Gray "URL: $frontendUrl"

    return $true
}

# ============================================================================
# Build Frontend in Background (for parallel "Both" deployment)
# ============================================================================
function Start-FrontendBuildJob($envConfig, $backendSlot) {
    $frontendPath = Join-Path $script:ScriptRoot "FrontEnd"
    $frontendUrl = $envConfig.frontend.url
    $apiUrl = "https://$backendSlot.azurewebsites.net/api"

    $job = Start-Job -ScriptBlock {
        param($frontendPath, $apiUrl, $frontendUrl)

        Set-Location $frontendPath

        # Install dependencies
        npm install --silent 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install dependencies"
        }

        # Build with environment variables
        $env:VITE_API_BASE_URL = $apiUrl
        $env:VITE_BASE_PATH = "/"
        $env:VITE_FRONTEND_URL = $frontendUrl
        $env:VITE_USE_HASH_ROUTING = "false"

        npm run build 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Frontend build failed"
        }

        return $true
    } -ArgumentList $frontendPath, $apiUrl, $frontendUrl

    return $job
}

function Deploy-FrontendFromBuildJob($envConfig, $backendSlot, $buildJob) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Frontend to Azure Static Web Apps"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $frontendPath = Join-Path $script:ScriptRoot "FrontEnd"
    $resourceGroup = $envConfig.resourceGroup
    $staticWebAppName = $envConfig.frontend.staticWebAppName
    $frontendUrl = $envConfig.frontend.url

    # Wait for build job to complete
    Write-Info "Waiting for frontend build to complete..."
    $null = Wait-Job $buildJob -Timeout 300
    $buildState = $buildJob.State

    if ($buildState -ne "Completed") {
        Write-ErrorMessage "Frontend build job failed or timed out (state: $buildState)"
        $jobOutput = Receive-Job $buildJob
        Write-Gray $jobOutput
        Remove-Job $buildJob -Force -ErrorAction SilentlyContinue
        return $false
    }

    try {
        $buildResult = Receive-Job $buildJob -ErrorAction Stop
        Write-Success "Frontend build completed"
    } catch {
        Write-ErrorMessage "Frontend build failed: $($_.Exception.Message)"
        Remove-Job $buildJob -Force -ErrorAction SilentlyContinue
        return $false
    }

    Remove-Job $buildJob -Force -ErrorAction SilentlyContinue

    # Get deployment token
    $deploymentToken = Get-SwaDeploymentToken

    if (-not $deploymentToken) {
        Write-Warning "Deployment token not found."
        Write-Host ""
        Write-Host "How would you like to provide the token?" -ForegroundColor Cyan
        Write-Host "  1. Fetch automatically from Azure" -ForegroundColor White
        Write-Host "  2. Enter manually" -ForegroundColor White
        Write-Host ""

        $tokenChoice = Read-Host "Enter choice (1 or 2)"

        switch ($tokenChoice) {
            "1" {
                Ensure-AzureCliLoggedIn

                Write-Info "Fetching deployment token from Azure..."
                $ErrorActionPreference = "Continue"
                $deploymentToken = az staticwebapp secrets list `
                    --name $staticWebAppName `
                    --resource-group $resourceGroup `
                    --query "properties.apiKey" -o tsv 2>&1
                $tokenResult = $LASTEXITCODE
                $ErrorActionPreference = "Stop"

                if ($tokenResult -ne 0 -or -not $deploymentToken -or $deploymentToken -like "*ERROR*") {
                    Write-ErrorMessage "Failed to fetch deployment token"
                    return $false
                }

                Write-Success "Deployment token retrieved"
            }
            "2" {
                Write-Host ""
                Write-Gray "Find the token in Azure Portal:"
                Write-Gray "  Static Web Apps > $staticWebAppName > Manage deployment token"
                Write-Host ""
                $deploymentToken = Read-Host "Enter deployment token"

                if ([string]::IsNullOrWhiteSpace($deploymentToken)) {
                    Write-ErrorMessage "No token provided"
                    return $false
                }
            }
            default {
                return $false
            }
        }

        Set-SwaDeploymentToken $deploymentToken
        Write-Success "Deployment token saved"
    }

    # Verify backend is healthy
    Write-Info "Verifying backend is healthy..."
    $versionUrl = "https://$backendSlot.azurewebsites.net/api/version"

    try {
        $response = Invoke-RestMethod -Uri $versionUrl -Method Get -TimeoutSec 10
        Write-Success "Backend is healthy (version: $($response.version))"
    } catch {
        Write-ErrorMessage "Backend is not responding. Cannot deploy frontend."
        return $false
    }

    # Deploy
    $distPath = Join-Path $frontendPath "dist"

    Write-Info "Deploying to Azure Static Web Apps..."

    $ErrorActionPreference = "Continue"
    swa deploy $distPath `
        --deployment-token $deploymentToken `
        --env production 2>&1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.ErrorRecord]) {
                if ($_.Exception.Message -and $_.Exception.Message.Trim()) {
                    Write-Host $_.Exception.Message
                }
            } else {
                Write-Host $_
            }
        }
    $swaResult = $LASTEXITCODE
    $ErrorActionPreference = "Stop"

    if ($swaResult -ne 0) {
        Write-ErrorMessage "Frontend deployment failed"
        return $false
    }

    Write-Host ""
    Write-Success "Frontend deployed to Azure Static Web Apps!"
    Write-Gray "URL: $frontendUrl"

    return $true
}

# ============================================================================
# Main Script
# ============================================================================

# Banner
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Volunteer Check-in Deployment v2" -ForegroundColor Cyan
Write-Host "  (Red/Green Deployment Strategy)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ask for environment if not provided
if (-not $Environment) {
    Write-Warning "Select deployment environment:"
    Write-Host "  1. Local (Start local development servers)" -ForegroundColor White
    Write-Host "  2. Production (Azure)" -ForegroundColor White
    Write-Host ""

    $choice = Read-Host "Enter choice (1 or 2)"

    switch ($choice) {
        "1" { $Environment = "local" }
        "2" { $Environment = "production" }
        default {
            Write-ErrorMessage "Invalid choice."
            exit 1
        }
    }
}

# Ask what to deploy if not specified
if (-not $Frontend -and -not $Backend) {
    Write-Host ""
    Write-Warning "What would you like to deploy?"
    Write-Host "  1. Frontend" -ForegroundColor White
    Write-Host "  2. Backend" -ForegroundColor White
    Write-Host "  3. Both" -ForegroundColor White
    Write-Host ""

    $choice = Read-Host "Enter choice (1, 2, or 3)"

    switch ($choice) {
        "1" { $Frontend = $true }
        "2" { $Backend = $true }
        "3" {
            $Frontend = $true
            $Backend = $true
        }
        default {
            Write-ErrorMessage "Invalid choice."
            exit 1
        }
    }
}

Write-Host ""
Write-Info "Environment: $Environment"
if ($Frontend) { Write-Gray "  - Frontend" }
if ($Backend) { Write-Gray "  - Backend" }
Write-Host ""

$deploymentSuccess = $true

# ============================================================================
# Local Deployment
# ============================================================================
if ($Environment -eq "local") {
    if ($Backend) {
        $result = Start-LocalBackend
        if (-not $result) { $deploymentSuccess = $false }
    }

    if ($Frontend -and $deploymentSuccess) {
        $result = Start-LocalFrontend
        if (-not $result) { $deploymentSuccess = $false }
    }
}

# ============================================================================
# Production Deployment
# ============================================================================
if ($Environment -eq "production") {
    # Require clean git state
    Ensure-GitClean

    # Ensure Azure CLI is available and logged in
    Ensure-AzureCliLoggedIn

    # Load environment config
    $envConfig = Get-EnvironmentConfig "production"

    # Select deployment slot
    $selectedSlot = $null
    if ($Backend -or ($Frontend -and -not $Backend)) {
        # Need to know which slot to deploy to / which slot frontend should point to
        if ($Backend) {
            $selectedSlot = Select-DeploymentSlot $envConfig
        } else {
            # Frontend only - use last deployed slot or ask
            $selectedSlot = $envConfig.lastDeployedSlot
            if (-not $selectedSlot) {
                Write-Warning "No previous backend deployment found. Please select which backend the frontend should point to:"
                $selectedSlot = Select-DeploymentSlot $envConfig
            } else {
                Write-Info "Frontend will point to: $selectedSlot"
                Write-Host ""
                Write-Host "Is this correct?" -ForegroundColor Cyan
                Write-Host "  1. Yes" -ForegroundColor White
                Write-Host "  2. No, let me choose" -ForegroundColor White
                Write-Host ""

                $confirmChoice = Read-Host "Enter choice (1 or 2)"
                if ($confirmChoice -eq "2") {
                    $selectedSlot = Select-DeploymentSlot $envConfig
                }
            }
        }
    }

    Write-Host ""
    Write-Info "Deployment target: $selectedSlot"
    Write-Host ""

    if ($Backend -and $Frontend) {
        # Both - build frontend in background while deploying backend
        Write-Info "Starting frontend build in background..."
        $frontendBuildJob = Start-FrontendBuildJob $envConfig $selectedSlot

        # Deploy backend
        $backendResult = Deploy-ProductionBackend $envConfig $selectedSlot
        if (-not $backendResult) {
            $deploymentSuccess = $false
            # Cancel frontend job
            Stop-Job $frontendBuildJob -ErrorAction SilentlyContinue
            Remove-Job $frontendBuildJob -Force -ErrorAction SilentlyContinue
        } else {
            # Deploy frontend (using already-built files)
            $frontendResult = Deploy-FrontendFromBuildJob $envConfig $selectedSlot $frontendBuildJob
            if (-not $frontendResult) { $deploymentSuccess = $false }
        }

    } elseif ($Backend) {
        $result = Deploy-ProductionBackend $envConfig $selectedSlot
        if (-not $result) { $deploymentSuccess = $false }

    } elseif ($Frontend) {
        $result = Deploy-ProductionFrontend $envConfig $selectedSlot
        if (-not $result) { $deploymentSuccess = $false }
    }
}

# ============================================================================
# Summary
# ============================================================================
$completionTime = Get-Date -Format "HH:mm:ss on dd MMM yyyy"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($deploymentSuccess) {
    Write-Success "  Deployment Complete!"
    Write-Host "  $completionTime" -ForegroundColor Gray
} else {
    Write-ErrorMessage "  Deployment Failed!"
    Write-Host "  $completionTime" -ForegroundColor Gray
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($deploymentSuccess) {
    if ($Environment -eq "local") {
        Write-Host "Local development servers:" -ForegroundColor White
        if ($Backend) {
            Write-Host "  [OK] Backend  -> http://localhost:7071/api" -ForegroundColor Green
        }
        if ($Frontend) {
            Write-Host "  [OK] Frontend -> http://localhost:5174" -ForegroundColor Green
        }
    } elseif ($Environment -eq "production") {
        $envConfig = Get-EnvironmentConfig "production"
        $activeSlot = $envConfig.lastDeployedSlot

        Write-Host "Production deployment:" -ForegroundColor White
        if ($Backend) {
            Write-Host "  [OK] Backend  -> https://$activeSlot.azurewebsites.net/api" -ForegroundColor Green
        }
        if ($Frontend) {
            Write-Host "  [OK] Frontend -> $($envConfig.frontend.url)" -ForegroundColor Green
            Write-Gray "       (pointing to $activeSlot)"
        }
    }
    Write-Host ""
    exit 0
} else {
    Write-Host "Some deployments failed. Please check the errors above." -ForegroundColor Red
    Write-Host ""
    exit 1
}
