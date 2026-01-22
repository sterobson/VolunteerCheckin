#!/usr/bin/env pwsh
# Unified Deployment Script for Volunteer Check-in
# Deploys Frontend and/or Backend to Local or Testing environments

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("local", "testing", "production")]
    [string]$Environment,

    [Parameter(Mandatory=$false)]
    [switch]$Frontend,

    [Parameter(Mandatory=$false)]
    [switch]$Backend
)

# Color functions for better output
function Write-Info($message) { Write-Host $message -ForegroundColor Cyan }
function Write-Success($message) { Write-Host $message -ForegroundColor Green }
function Write-Warning($message) { Write-Host $message -ForegroundColor Yellow }
function Write-ErrorMessage($message) { Write-Host $message -ForegroundColor Red }
function Write-Gray($message) { Write-Host $message -ForegroundColor Gray }

# Function to get local network IP address
function Get-LocalIPAddress {
    $networkIP = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
        $_.InterfaceAlias -notmatch 'Loopback' -and
        $_.IPAddress -notmatch '^169\.254\.' -and
        $_.IPAddress -ne '127.0.0.1'
    } | Select-Object -First 1).IPAddress
    return $networkIP
}

# Function to kill process on a specific port
function Stop-ProcessOnPort {
    param(
        [int]$Port,
        [string]$ServiceName
    )

    $connection = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($connection) {
        Write-Warning "$ServiceName is running on port $Port. Shutting it down..."
        $processId = $connection.OwningProcess
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Success "$ServiceName stopped"
        return $true
    }
    return $false
}

# Configuration
$config = @{
    local = @{
        Frontend = @{
            DeployPath = "/"
            ApiUrl = "http://localhost:7071/api"
        }
        Backend = @{
            Url = "http://localhost:7071/api"
        }
    }
    testing = @{
        Frontend = @{
            DeployPath = "/VolunteerCheckin/testing/"
            DestinationDir = "testing"
            ApiUrl = "https://sterobson-volunteercheckin-testing.azurewebsites.net/api"
            FrontendUrl = "https://sterobson.github.io/VolunteerCheckin/testing"
        }
        Backend = @{
            AppName = "sterobson-volunteercheckin-testing"
            ResourceGroup = "VolunteerCheckIn"
            Url = "https://sterobson-volunteercheckin-testing.azurewebsites.net/api"
        }
    }
    production = @{
        Frontend = @{
            DeployPath = "/"
            ApiUrl = "https://sterobson-volunteercheckin-testing.azurewebsites.net/api"
            FrontendUrl = "https://onthedayapp.com"
            StaticWebAppName = "OnTheDayUi"
            ResourceGroup = "VolunteerCheckIn"
        }
    }
}

# Banner
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Volunteer Check-in Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ask for environment if not provided
if (-not $Environment) {
    Write-Warning "Select deployment environment:"
    Write-Host "  1. Local (Start local development servers)" -ForegroundColor White
    Write-Host "  2. Testing (GitHub Pages + Azure Functions)" -ForegroundColor White
    Write-Host "  3. Production (Azure Static Web Apps)" -ForegroundColor White
    Write-Host ""

    $choice = Read-Host "Enter choice (1, 2, or 3)"

    switch ($choice) {
        "1" { $Environment = "local" }
        "2" { $Environment = "testing" }
        "3" { $Environment = "production" }
        default {
            Write-ErrorMessage "Invalid choice. Please select 1, 2, or 3."
            exit 1
        }
    }
}

# ============================================================================
# Git Status Check - Warn about uncommitted/unpushed changes (remote deploys only)
# ============================================================================
if ($Environment -ne "local") {
    $hasUncommitted = $false
    $hasUnpushed = $false
    $currentBranch = git branch --show-current 2>$null

    # Check for uncommitted changes (staged + unstaged + untracked)
    $status = git status --porcelain 2>$null
    if ($status) {
        $hasUncommitted = $true
        $changedFiles = ($status | Measure-Object).Count
    }

    # Check for unpushed commits
    $unpushed = git log --oneline "@{u}.." 2>$null
    if ($LASTEXITCODE -eq 0 -and $unpushed) {
        $hasUnpushed = $true
        $unpushedCount = ($unpushed | Measure-Object).Count
    }

    if ($hasUncommitted -or $hasUnpushed) {
        Write-Host ""
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host "!!                                                          !!" -ForegroundColor Red
        Write-Host "!!                        WARNING                           !!" -ForegroundColor Red
        Write-Host "!!                                                          !!" -ForegroundColor Red
        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host ""

        if ($hasUncommitted) {
            Write-Host "  *** You have $changedFiles UNCOMMITTED change(s)! ***" -ForegroundColor Yellow
        }
        if ($hasUnpushed) {
            Write-Host "  *** You have $unpushedCount UNPUSHED commit(s)! ***" -ForegroundColor Yellow
        }
        if ($currentBranch -and $currentBranch -ne "main" -and $currentBranch -ne "master") {
            Write-Host "  *** You are on branch '$currentBranch', not main! ***" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "  You are about to deploy code that is NOT fully in source control!" -ForegroundColor Yellow
        Write-Host ""

        # Show the actual changes
        if ($hasUncommitted) {
            Write-Host "  Uncommitted changes:" -ForegroundColor Gray
            git status --short | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
            Write-Host ""
        }

        if ($hasUnpushed) {
            Write-Host "  Unpushed commits:" -ForegroundColor Gray
            git log --oneline "@{u}.." | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
            Write-Host ""
        }

        Write-Host "!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!" -ForegroundColor Red
        Write-Host ""

        # Offer to fix it
        if ($hasUncommitted) {
            Write-Host "Would you like to commit and push your changes now?" -ForegroundColor Cyan
            Write-Host "  1. Yes, commit and push for me" -ForegroundColor White
            Write-Host "  2. No, deploy anyway (not recommended)" -ForegroundColor White
            Write-Host "  3. Cancel deployment" -ForegroundColor White
            Write-Host ""

            $gitChoice = Read-Host "Enter choice (1, 2, or 3)"

            switch ($gitChoice) {
                "1" {
                    Write-Host ""
                    $commitMessage = Read-Host "Enter commit message"

                    if ([string]::IsNullOrWhiteSpace($commitMessage)) {
                        Write-ErrorMessage "Commit message cannot be empty."
                        exit 1
                    }

                    Write-Host ""
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
                    $hasUnpushed = $true  # Now we definitely have unpushed commits
                }
                "2" {
                    Write-Host ""
                    Write-Warning "Proceeding with deployment despite uncommitted changes..."
                    Write-Host ""
                }
                "3" {
                    Write-Host ""
                    Write-Info "Deployment cancelled."
                    exit 0
                }
                default {
                    Write-ErrorMessage "Invalid choice."
                    exit 1
                }
            }
        }

        # Check again for unpushed (either from before or from the commit we just made)
        $unpushed = git log --oneline "@{u}.." 2>$null
        if ($LASTEXITCODE -eq 0 -and $unpushed) {
            $unpushedCount = ($unpushed | Measure-Object).Count

            if (-not $hasUncommitted -or $gitChoice -eq "1") {
                # Either we just committed, or there were only unpushed commits
                Write-Host ""
                Write-Host "You have $unpushedCount unpushed commit(s):" -ForegroundColor Yellow
                git log --oneline "@{u}.." | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
                Write-Host ""

                Write-Host "Would you like to push now?" -ForegroundColor Cyan
                Write-Host "  1. Yes, push to origin" -ForegroundColor White
                Write-Host "  2. No, deploy anyway" -ForegroundColor White
                Write-Host "  3. Cancel deployment" -ForegroundColor White
                Write-Host ""

                $pushChoice = Read-Host "Enter choice (1, 2, or 3)"

                switch ($pushChoice) {
                    "1" {
                        Write-Host ""
                        Write-Info "Pushing to origin..."

                        git push
                        if ($LASTEXITCODE -ne 0) {
                            Write-ErrorMessage "Failed to push. You may need to pull first or resolve conflicts."
                            exit 1
                        }

                        Write-Success "Changes pushed to origin."
                        Write-Host ""
                    }
                    "2" {
                        Write-Host ""
                        Write-Warning "Proceeding with deployment despite unpushed commits..."
                        Write-Host ""
                    }
                    "3" {
                        Write-Host ""
                        Write-Info "Deployment cancelled."
                        exit 0
                    }
                    default {
                        Write-ErrorMessage "Invalid choice."
                        exit 1
                    }
                }
            }
        }
    }
}

# Ask what to deploy if not specified
if (-not $Frontend -and -not $Backend) {
    Write-Host ""

    # Production only supports frontend deployment
    if ($Environment -eq "production") {
        Write-Info "Production environment only supports frontend deployment."
        $Frontend = $true
    } else {
        Write-Warning "What would you like to deploy?"
        Write-Host "  1. Frontend" -ForegroundColor White
        Write-Host "  2. Backend (Azure Functions)" -ForegroundColor White
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
}

# Warn if backend is requested for production
if ($Environment -eq "production" -and $Backend) {
    Write-Warning "Backend deployment is not configured for production environment."
    Write-Warning "Only frontend will be deployed."
    $Backend = $false
}

Write-Host ""
if ($Environment -eq "local") {
    Write-Info "Starting local development environment"
} elseif ($Environment -eq "testing") {
    Write-Info "Deploying to: Testing (GitHub Pages + Azure Functions)"
} elseif ($Environment -eq "production") {
    Write-Info "Deploying to: Production (Azure Static Web Apps)"
}
if ($Frontend) { Write-Gray "  - Frontend" }
if ($Backend) { Write-Gray "  - Backend (Azure Functions)" }
Write-Host ""

$selectedConfig = $config[$Environment]
$deploymentSuccess = $true

# ============================================================================
# Local Backend - Start Azure Functions
# ============================================================================
if ($Environment -eq "local" -and $Backend) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Starting Azure Functions"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Check if already running and stop it
    $wasRunning = Stop-ProcessOnPort -Port 7071 -ServiceName "Azure Functions"

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
    $backendPath = Join-Path $PSScriptRoot "Backend"
    Push-Location $backendPath
    try {
        dotnet build --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Backend build failed"
            $deploymentSuccess = $false
        } else {
            Write-Success "Backend build completed"
        }
    } finally {
        Pop-Location
    }

    if ($deploymentSuccess) {
        # Check CORS settings if frontend is being deployed too
        if ($Frontend) {
            Write-Info "Checking CORS configuration..."
            $localSettingsPath = Join-Path $backendPath "local.settings.json"

            if (Test-Path $localSettingsPath) {
                $localSettings = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
                $frontendPort = "5174"
                $frontendUrl = "http://localhost:$frontendPort"
                $needsUpdate = $false

                # Check CORS setting
                if ($localSettings.Host.CORS) {
                    $corsUrls = @($localSettings.Host.CORS -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
                    if ($corsUrls -notcontains $frontendUrl) {
                        Write-Warning "CORS does not include $frontendUrl"
                        $needsUpdate = $true
                    }
                }

                # Check FRONTEND_URL setting
                if ($localSettings.Values.FRONTEND_URL -and $localSettings.Values.FRONTEND_URL -ne $frontendUrl) {
                    Write-Warning "FRONTEND_URL is set to $($localSettings.Values.FRONTEND_URL) instead of $frontendUrl"
                    $needsUpdate = $true
                }

                if ($needsUpdate) {
                    Write-Host ""
                    $response = Read-Host "Update local.settings.json to include port $frontendPort? (Y/n)"

                    if ($response -eq "" -or $response -eq "Y" -or $response -eq "y") {
                        # Update CORS
                        if ($localSettings.Host.CORS) {
                            $corsUrls = @($localSettings.Host.CORS -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -ne "http://localhost:5173" })
                            if ($corsUrls -notcontains $frontendUrl) {
                                $corsUrls += $frontendUrl
                            }
                            $localSettings.Host.CORS = $corsUrls -join ','
                        } else {
                            if (-not $localSettings.Host) {
                                $localSettings | Add-Member -MemberType NoteProperty -Name "Host" -Value @{}
                            }
                            $localSettings.Host | Add-Member -MemberType NoteProperty -Name "CORS" -Value $frontendUrl -Force
                        }

                        # Update FRONTEND_URL
                        $localSettings.Values.FRONTEND_URL = $frontendUrl

                        # Save the file with proper formatting
                        $jsonContent = $localSettings | ConvertTo-Json -Depth 10
                        $jsonContent | Set-Content $localSettingsPath -Encoding UTF8
                        Write-Success "local.settings.json updated"
                        Write-Warning "Please restart Azure Functions for CORS changes to take effect!"
                    } else {
                        Write-Warning "Skipping CORS update. You may experience CORS errors."
                    }
                    Write-Host ""
                } else {
                    Write-Success "CORS configuration is correct"
                }
            }
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
    }
}

# ============================================================================
# Local Frontend - Start Dev Server
# ============================================================================
if ($Environment -eq "local" -and $Frontend) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Starting Frontend Dev Server"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Check if already running and stop it
    $wasRunning = Stop-ProcessOnPort -Port 5174 -ServiceName "Frontend dev server"

    $frontendPath = Join-Path $PSScriptRoot "FrontEnd"

    # Check if node_modules exists
    $nodeModules = Join-Path $frontendPath "node_modules"
    if (-not (Test-Path $nodeModules)) {
        Write-Info "Installing frontend dependencies..."
        Push-Location $frontendPath
        try {
            npm install --silent
            if ($LASTEXITCODE -ne 0) {
                Write-ErrorMessage "Failed to install dependencies"
                $deploymentSuccess = $false
            } else {
                Write-Success "Dependencies installed"
            }
        } finally {
            Pop-Location
        }
    }

    if ($deploymentSuccess) {
        Write-Info "Starting frontend dev server..."
        Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "cd '$frontendPath'; npm run dev -- --port 5174" -WindowStyle Normal
        Start-Sleep -Seconds 3

        $viteRunning = Get-NetTCPConnection -LocalPort 5174 -State Listen -ErrorAction SilentlyContinue
        if ($viteRunning) {
            Write-Success "Frontend started on http://localhost:5174"
            $localIP = Get-LocalIPAddress
            if ($localIP) {
                Write-Success "Network access:    http://${localIP}:5174"
            }
        } else {
            Write-Warning "Frontend may still be starting. Check the console window."
        }

        # Ask about subpath server for testing
        Write-Host ""
        Write-Host "Would you like to also start a subpath server for testing?" -ForegroundColor Cyan
        Write-Gray "  This simulates the GitHub Pages deployment path (/VolunteerCheckin/testing/)"
        Write-Gray "  Useful for testing that magic links include the correct path."
        Write-Host ""
        Write-Host "  1. Yes, start subpath server on port 5175" -ForegroundColor White
        Write-Host "  2. No, just use the root server" -ForegroundColor White
        Write-Host ""

        $subpathChoice = Read-Host "Enter choice (1 or 2)"

        if ($subpathChoice -eq "1") {
            # Stop any existing server on 5175
            Stop-ProcessOnPort -Port 5175 -ServiceName "Frontend subpath server" | Out-Null

            Write-Info "Starting subpath dev server..."
            $subpathCommand = "`$env:VITE_BASE_PATH='/VolunteerCheckin/testing/'; npm run dev -- --port 5175"
            Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "cd '$frontendPath'; $subpathCommand" -WindowStyle Normal
            Start-Sleep -Seconds 3

            $subpathRunning = Get-NetTCPConnection -LocalPort 5175 -State Listen -ErrorAction SilentlyContinue
            if ($subpathRunning) {
                Write-Success "Subpath server started on http://localhost:5175/VolunteerCheckin/testing/"
            } else {
                Write-Warning "Subpath server may still be starting. Check the console window."
            }

            # Track that we started subpath server for summary
            $script:StartedSubpathServer = $true
        }
    }
}

# ============================================================================
# Deploy Frontend to Testing (GitHub Pages)
# ============================================================================
if ($Environment -eq "testing" -and $Frontend) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Frontend to GitHub Pages"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $frontendPath = Join-Path $PSScriptRoot "FrontEnd"

    if (-not (Test-Path $frontendPath)) {
        Write-ErrorMessage "Frontend directory not found at: $frontendPath"
        $deploymentSuccess = $false
    } else {
        Push-Location $frontendPath

        try {
            # Install dependencies
            Write-Info "Installing dependencies..."
            npm install --silent

            if ($LASTEXITCODE -ne 0) {
                Write-ErrorMessage "Failed to install dependencies"
                $deploymentSuccess = $false
            } else {
                Write-Success "Dependencies installed"

                # Build
                Write-Host ""
                Write-Info "Building frontend..."

                $env:VITE_API_BASE_URL = $selectedConfig.Frontend.ApiUrl
                $env:VITE_BASE_PATH = $selectedConfig.Frontend.DeployPath
                $env:VITE_FRONTEND_URL = $selectedConfig.Frontend.FrontendUrl
                $env:VITE_USE_HASH_ROUTING = "true"

                npm run build

                if ($LASTEXITCODE -ne 0) {
                    Write-ErrorMessage "Frontend build failed"
                    $deploymentSuccess = $false
                } else {
                    Write-Success "Frontend build completed"

                    $distPath = Join-Path $frontendPath "dist"

                    if (-not (Test-Path $distPath)) {
                        Write-ErrorMessage "Build output not found at: $distPath"
                        $deploymentSuccess = $false
                    } else {
                        # Deploy to GitHub Pages using git worktree
                        Write-Host ""
                        Write-Info "Deploying to GitHub Pages..."
                        Write-Gray "Setting up deployment worktree..."

                        # Create temp directory for gh-pages worktree
                        $worktreePath = Join-Path $env:TEMP "gh-pages-deploy-$(Get-Date -Format 'yyyyMMddHHmmss')"

                        try {
                            # Fetch latest gh-pages
                            $ErrorActionPreference = "Continue"
                            git fetch origin gh-pages:gh-pages 2>&1 | Out-Null
                            $ErrorActionPreference = "Stop"

                            # Create worktree for gh-pages branch
                            $ErrorActionPreference = "Continue"
                            git worktree add $worktreePath gh-pages 2>&1 | Out-Null
                            $worktreeResult = $LASTEXITCODE
                            $ErrorActionPreference = "Stop"

                            if ($worktreeResult -ne 0) {
                                # gh-pages branch doesn't exist, create orphan branch
                                Write-Warning "gh-pages branch doesn't exist, creating it..."
                                $ErrorActionPreference = "Continue"
                                git worktree add --detach $worktreePath 2>&1 | Out-Null
                                $ErrorActionPreference = "Stop"

                                Push-Location $worktreePath
                                try {
                                    $ErrorActionPreference = "Continue"
                                    git checkout --orphan gh-pages 2>&1 | Out-Null
                                    git rm -rf . 2>&1 | Out-Null
                                    $ErrorActionPreference = "Stop"
                                } finally {
                                    Pop-Location
                                }
                            }

                            Push-Location $worktreePath

                            try {
                                # Clean the worktree (but preserve .git and testing directory if it exists)
                                Write-Gray "Cleaning deployment directory..."

                                # Only remove files we're about to replace
                                if ($selectedConfig.Frontend.DestinationDir) {
                                    $targetDir = Join-Path $worktreePath $selectedConfig.Frontend.DestinationDir
                                    if (Test-Path $targetDir) {
                                        Remove-Item -Path $targetDir -Recurse -Force -ErrorAction SilentlyContinue
                                    }
                                    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
                                } else {
                                    # For root deployment, clean everything except .git
                                    Get-ChildItem -Path $worktreePath -Exclude ".git" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
                                    $targetDir = $worktreePath
                                }

                                # Copy built files to worktree
                                Write-Gray "Copying built files to deployment directory..."
                                Copy-Item -Path "$distPath\*" -Destination $targetDir -Recurse -Force

                                # Add .nojekyll file
                                $nojekyll = Join-Path $worktreePath ".nojekyll"
                                if (-not (Test-Path $nojekyll)) {
                                    New-Item -ItemType File -Path $nojekyll -Force | Out-Null
                                }

                                # Commit and push
                                $ErrorActionPreference = "Continue"
                                git add -A 2>&1 | Out-Null
                                $ErrorActionPreference = "Stop"

                                $commitMessage = "Deploy $Environment frontend - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

                                $ErrorActionPreference = "Continue"
                                git commit -m $commitMessage 2>&1 | Out-Null
                                $commitResult = $LASTEXITCODE
                                $ErrorActionPreference = "Stop"

                                if ($commitResult -eq 0) {
                                    Write-Gray "Pushing to GitHub..."

                                    $ErrorActionPreference = "Continue"
                                    git push origin gh-pages 2>&1 | Write-Host
                                    $pushResult = $LASTEXITCODE
                                    $ErrorActionPreference = "Stop"

                                    if ($pushResult -eq 0) {
                                        Write-Success "Frontend deployed to GitHub Pages!"

                                        $pageUrl = if ($selectedConfig.Frontend.DestinationDir) {
                                            "https://sterobson.github.io/VolunteerCheckin/$($selectedConfig.Frontend.DestinationDir)/"
                                        } else {
                                            "https://sterobson.github.io/VolunteerCheckin/"
                                        }
                                        Write-Gray "URL: $pageUrl"
                                    } else {
                                        Write-ErrorMessage "Failed to push to GitHub. Make sure you're authenticated."
                                        $deploymentSuccess = $false
                                    }
                                } else {
                                    Write-Warning "No changes to commit"
                                }
                            } finally {
                                Pop-Location
                            }
                        } finally {
                            # Remove the worktree
                            if (Test-Path $worktreePath) {
                                $ErrorActionPreference = "Continue"
                                git worktree remove $worktreePath --force 2>&1 | Out-Null
                                $ErrorActionPreference = "Stop"
                            }
                        }
                    }
                }
            }
        } finally {
            Pop-Location

            # Clean up environment variables
            Remove-Item Env:\VITE_API_BASE_URL -ErrorAction SilentlyContinue
            Remove-Item Env:\VITE_BASE_PATH -ErrorAction SilentlyContinue
            Remove-Item Env:\VITE_FRONTEND_URL -ErrorAction SilentlyContinue
            Remove-Item Env:\VITE_USE_HASH_ROUTING -ErrorAction SilentlyContinue
        }
    }
}

# ============================================================================
# Deploy Frontend to Production (Azure Static Web Apps)
# ============================================================================
if ($Environment -eq "production" -and $Frontend) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Frontend to Azure Static Web Apps"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $frontendPath = Join-Path $PSScriptRoot "FrontEnd"

    if (-not (Test-Path $frontendPath)) {
        Write-ErrorMessage "Frontend directory not found at: $frontendPath"
        $deploymentSuccess = $false
    } else {
        # Check if SWA CLI is installed
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
                Write-Host ""
                Write-Info "Installing Azure Static Web Apps CLI..."
                npm install -g @azure/static-web-apps-cli

                if ($LASTEXITCODE -ne 0) {
                    Write-ErrorMessage "Failed to install SWA CLI"
                    $deploymentSuccess = $false
                } else {
                    Write-Success "SWA CLI installed successfully"
                }
            } else {
                Write-Info "Deployment cancelled."
                exit 0
            }
        }

        if ($deploymentSuccess) {
            $swaVersion = (swa --version 2>$null) -replace '\s.*', ''
            Write-Success "SWA CLI ready (v$swaVersion)"

            # Check for deployment token (stored securely with DPAPI)
            Write-Info "Checking deployment token..."

            $tokenFile = Join-Path $env:USERPROFILE ".swa-deploy-token"
            $deploymentToken = $null

            # Try to load existing token
            if (Test-Path $tokenFile) {
                try {
                    $encrypted = (Get-Content $tokenFile -Raw).Trim()
                    $secureToken = ConvertTo-SecureString $encrypted -ErrorAction Stop
                    $credential = New-Object System.Management.Automation.PSCredential("token", $secureToken)
                    $deploymentToken = $credential.GetNetworkCredential().Password
                    Write-Success "Deployment token loaded from secure storage"
                } catch {
                    Write-Warning "Failed to decrypt stored token. It may have been created by a different user."
                    Remove-Item $tokenFile -Force -ErrorAction SilentlyContinue
                }
            }

            if (-not $deploymentToken) {
                Write-Warning "Deployment token not found."
                Write-Host ""
                Write-Host "The deployment token is required to deploy to Azure Static Web Apps." -ForegroundColor Gray
                Write-Host ""
                Write-Host "How would you like to provide the token?" -ForegroundColor Cyan
                Write-Host "  1. Fetch automatically from Azure (requires Azure CLI)" -ForegroundColor White
                Write-Host "  2. Enter manually (copy from Azure Portal)" -ForegroundColor White
                Write-Host "  3. Cancel deployment" -ForegroundColor White
                Write-Host ""

                $tokenChoice = Read-Host "Enter choice (1, 2, or 3)"

                switch ($tokenChoice) {
                    "1" {
                        Write-Host ""
                        Write-Info "Fetching deployment token from Azure..."

                        # Check if Azure CLI is available
                        $azCommand = Get-Command az -ErrorAction SilentlyContinue
                        if (-not $azCommand) {
                            Write-ErrorMessage "Azure CLI not found. Please install it from https://aka.ms/installazurecliwindows"
                            $deploymentSuccess = $false
                        } else {
                            # Check if logged in
                            $ErrorActionPreference = "Continue"
                            $azAccount = az account show 2>&1
                            $azLoggedIn = $LASTEXITCODE -eq 0
                            $ErrorActionPreference = "Stop"

                            if (-not $azLoggedIn) {
                                Write-Warning "Not logged in to Azure CLI. Opening login..."
                                az login
                                if ($LASTEXITCODE -ne 0) {
                                    Write-ErrorMessage "Azure login failed"
                                    $deploymentSuccess = $false
                                }
                            }

                            if ($deploymentSuccess) {
                                $ErrorActionPreference = "Continue"
                                $deploymentToken = az staticwebapp secrets list `
                                    --name $selectedConfig.Frontend.StaticWebAppName `
                                    --resource-group $selectedConfig.Frontend.ResourceGroup `
                                    --query "properties.apiKey" -o tsv 2>&1
                                $tokenResult = $LASTEXITCODE
                                $ErrorActionPreference = "Stop"

                                if ($tokenResult -ne 0 -or -not $deploymentToken -or $deploymentToken -like "*ERROR*") {
                                    Write-ErrorMessage "Failed to fetch deployment token"
                                    Write-Gray "Error: $deploymentToken"
                                    $deploymentSuccess = $false
                                } else {
                                    Write-Success "Deployment token retrieved"
                                }
                            }
                        }
                    }
                    "2" {
                        Write-Host ""
                        Write-Gray "You can find the token in Azure Portal:"
                        Write-Gray "  Static Web Apps > $($selectedConfig.Frontend.StaticWebAppName) > Manage deployment token"
                        Write-Host ""
                        $deploymentToken = Read-Host "Enter deployment token"

                        if ([string]::IsNullOrWhiteSpace($deploymentToken)) {
                            Write-ErrorMessage "No token provided"
                            $deploymentSuccess = $false
                        }
                    }
                    default {
                        Write-Info "Deployment cancelled."
                        exit 0
                    }
                }

                # Save the token securely if we got one
                if ($deploymentSuccess -and $deploymentToken) {
                    Write-Host ""
                    Write-Host "Would you like to save this token securely for future deployments?" -ForegroundColor Cyan
                    Write-Gray "The token will be encrypted and stored in your user profile."
                    Write-Host "  1. Yes, save securely" -ForegroundColor White
                    Write-Host "  2. No, just use for this deployment" -ForegroundColor White
                    Write-Host ""

                    $saveChoice = Read-Host "Enter choice (1 or 2)"

                    if ($saveChoice -eq "1") {
                        try {
                            $secureToken = ConvertTo-SecureString $deploymentToken -AsPlainText -Force
                            $encrypted = ConvertFrom-SecureString $secureToken
                            $encrypted | Set-Content $tokenFile -Force -NoNewline
                            Write-Success "Token saved securely to $tokenFile"
                        } catch {
                            Write-Warning "Failed to save token: $_"
                        }
                    }
                }
            }

            # Set for SWA CLI to use
            if ($deploymentToken) {
                $env:SWA_CLI_DEPLOYMENT_TOKEN = $deploymentToken
            }
        }

        if ($deploymentSuccess) {
            Push-Location $frontendPath

            try {
                # Install dependencies
                Write-Info "Installing dependencies..."
                npm install --silent

                if ($LASTEXITCODE -ne 0) {
                    Write-ErrorMessage "Failed to install dependencies"
                    $deploymentSuccess = $false
                } else {
                    Write-Success "Dependencies installed"

                    # Build
                    Write-Host ""
                    Write-Info "Building frontend..."

                    $env:VITE_API_BASE_URL = $selectedConfig.Frontend.ApiUrl
                    $env:VITE_BASE_PATH = $selectedConfig.Frontend.DeployPath
                    $env:VITE_FRONTEND_URL = $selectedConfig.Frontend.FrontendUrl
                    $env:VITE_USE_HASH_ROUTING = "false"

                    npm run build

                    if ($LASTEXITCODE -ne 0) {
                        Write-ErrorMessage "Frontend build failed"
                        $deploymentSuccess = $false
                    } else {
                        Write-Success "Frontend build completed"

                        $distPath = Join-Path $frontendPath "dist"

                        if (-not (Test-Path $distPath)) {
                            Write-ErrorMessage "Build output not found at: $distPath"
                            $deploymentSuccess = $false
                        } else {
                            # Deploy to Azure Static Web Apps
                            Write-Host ""
                            Write-Info "Deploying to Azure Static Web Apps..."
                            Write-Gray "App: $($selectedConfig.Frontend.StaticWebAppName)"
                            Write-Host ""

                            $ErrorActionPreference = "Continue"
                            swa deploy $distPath `
                                --deployment-token $env:SWA_CLI_DEPLOYMENT_TOKEN `
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

                            if ($swaResult -eq 0) {
                                Write-Host ""
                                Write-Success "Frontend deployed to Azure Static Web Apps!"
                                Write-Gray "URL: $($selectedConfig.Frontend.FrontendUrl)"
                            } else {
                                Write-ErrorMessage "Static Web Apps deployment failed"
                                Write-Host ""
                                Write-Warning "Check the error messages above for details."
                                Write-Host ""
                                $deploymentSuccess = $false
                            }
                        }
                    }
                }
            } finally {
                Pop-Location

                # Clean up environment variables
                Remove-Item Env:\VITE_API_BASE_URL -ErrorAction SilentlyContinue
                Remove-Item Env:\VITE_BASE_PATH -ErrorAction SilentlyContinue
                Remove-Item Env:\VITE_FRONTEND_URL -ErrorAction SilentlyContinue
                Remove-Item Env:\VITE_USE_HASH_ROUTING -ErrorAction SilentlyContinue
            }
        }
    }
}

# ============================================================================
# Deploy Backend to Testing (Azure Functions)
# ============================================================================
if ($Environment -eq "testing" -and $Backend) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Backend to Azure"
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $backendPath = Join-Path $PSScriptRoot "Backend"

    if (-not (Test-Path $backendPath)) {
        Write-ErrorMessage "Backend directory not found at: $backendPath"
        $deploymentSuccess = $false
    } else {
        # Check if func tool is installed
        Write-Info "Checking Azure Functions Core Tools..."

        $funcVersion = func --version 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMessage "Azure Functions Core Tools not found!"
            Write-Host ""
            Write-Warning "Install Azure Functions Core Tools:"
            Write-Gray "  npm install -g azure-functions-core-tools@4 --unsafe-perm true"
            Write-Host ""
            $deploymentSuccess = $false
        } else {
            Write-Success "Azure Functions Core Tools found"

            # Check Azure authentication
            Write-Info "Checking Azure authentication..."

            $azCommand = Get-Command az -ErrorAction SilentlyContinue
            if (-not $azCommand) {
                Write-Warning "Azure CLI (az) not found - skipping authentication check"
                Write-Gray "  Install from: https://aka.ms/installazurecliwindows"
                Write-Success "Continuing with deployment (authentication will be checked during publish)"
            } else {
                $ErrorActionPreference = "Continue"
                $azResult = az account show 2>&1
                $azResultCode = $LASTEXITCODE
                $ErrorActionPreference = "Stop"

                if ($azResultCode -ne 0) {
                    Write-ErrorMessage "Not authenticated with Azure!"
                    Write-Host ""
                    Write-Warning "Please login to Azure:"
                    Write-Gray "  az login"
                    Write-Host ""
                    $deploymentSuccess = $false
                } else {
                    Write-Success "Authenticated with Azure"
                }
            }

            if ($deploymentSuccess) {
                Push-Location $backendPath

                try {
                    # Deploy
                    Write-Host ""
                    Write-Info "Deploying to Azure..."
                    Write-Gray "This may take a few minutes..."
                    Write-Host ""

                    $ErrorActionPreference = "Continue"
                    func azure functionapp publish $selectedConfig.Backend.AppName 2>&1 | ForEach-Object {
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

                    if ($funcResult -eq 0) {
                        Write-Host ""
                        Write-Success "Backend deployed to Azure!"
                        Write-Gray "URL: $($selectedConfig.Backend.Url)"
                    } else {
                        Write-ErrorMessage "Backend deployment failed"
                        $deploymentSuccess = $false
                    }
                } finally {
                    Pop-Location
                }
            }
        }
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
            $localIP = Get-LocalIPAddress
            if ($localIP) {
                Write-Host "       Network  -> http://${localIP}:5174" -ForegroundColor Green
            }
            if ($script:StartedSubpathServer) {
                Write-Host "  [OK] Subpath  -> http://localhost:5175/VolunteerCheckin/testing/" -ForegroundColor Green
            }
        }
    } elseif ($Environment -eq "testing") {
        Write-Host "Deployed to: Testing (GitHub Pages)" -ForegroundColor White
        if ($Frontend) {
            $pageUrl = if ($selectedConfig.Frontend.DestinationDir) {
                "https://sterobson.github.io/VolunteerCheckin/$($selectedConfig.Frontend.DestinationDir)/"
            } else {
                "https://sterobson.github.io/VolunteerCheckin/"
            }
            Write-Host "  [OK] Frontend -> $pageUrl" -ForegroundColor Green
        }
        if ($Backend) {
            Write-Host "  [OK] Backend  -> $($selectedConfig.Backend.Url)" -ForegroundColor Green
        }
    } elseif ($Environment -eq "production") {
        Write-Host "Deployed to: Production (Azure Static Web Apps)" -ForegroundColor White
        if ($Frontend) {
            Write-Host "  [OK] Frontend -> $($selectedConfig.Frontend.FrontendUrl)" -ForegroundColor Green
        }
    }
    Write-Host ""

    exit 0
} else {
    Write-Host "Some deployments failed. Please check the errors above." -ForegroundColor Red
    Write-Host ""
    exit 1
}
