#!/usr/bin/env pwsh
# Unified Deployment Script for Volunteer Check-in
# Deploys Frontend and/or Backend to Local or Testing environments

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("local", "testing")]
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
            DeployPath = "/testing/"
            DestinationDir = "testing"
            ApiUrl = "https://sterobson-volunteercheckin-testing.azurewebsites.net/api"
        }
        Backend = @{
            AppName = "sterobson-volunteercheckin-testing"
            ResourceGroup = "VolunteerCheckIn"
            Url = "https://sterobson-volunteercheckin-testing.azurewebsites.net/api"
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
    Write-Host "  2. Testing (Deploy to GitHub Pages + Azure)" -ForegroundColor White
    Write-Host ""

    $choice = Read-Host "Enter choice (1 or 2)"

    switch ($choice) {
        "1" { $Environment = "local" }
        "2" { $Environment = "testing" }
        default {
            Write-ErrorMessage "Invalid choice. Please select 1 or 2."
            exit 1
        }
    }
}

# Ask what to deploy if not specified
if (-not $Frontend -and -not $Backend) {
    Write-Host ""
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

Write-Host ""
if ($Environment -eq "local") {
    Write-Info "Starting local development environment"
} else {
    Write-Info "Deploying to: $Environment"
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

    # Check if already running
    $connection = Get-NetTCPConnection -LocalPort 7071 -State Listen -ErrorAction SilentlyContinue
    if ($connection) {
        Write-Success "Azure Functions already running on http://localhost:7071"
    } else {
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

        Write-Info "Starting Azure Functions..."
        $backendPath = Join-Path $PSScriptRoot "Backend"

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

    # Check if already running
    $connection = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
    if ($connection) {
        Write-Success "Frontend dev server already running on http://localhost:5173"
    } else {
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
                }
            } finally {
                Pop-Location
            }
        }

        if ($deploymentSuccess) {
            Write-Info "Starting frontend dev server..."
            Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "cd '$frontendPath'; npm run dev" -WindowStyle Normal
            Start-Sleep -Seconds 3

            $viteRunning = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
            if ($viteRunning) {
                Write-Success "Frontend started on http://localhost:5173"
            } else {
                Write-Warning "Frontend may still be starting. Check the console window."
            }
        }
    }
}

# ============================================================================
# Deploy Frontend to Testing
# ============================================================================
if ($Environment -eq "testing" -and $Frontend) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Info "  Deploying Frontend to Testing"
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
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($deploymentSuccess) {
    Write-Success "  Deployment Complete!"
} else {
    Write-ErrorMessage "  Deployment Failed!"
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
            Write-Host "  [OK] Frontend -> http://localhost:5173" -ForegroundColor Green
        }
    } else {
        Write-Host "Deployed to: $Environment" -ForegroundColor White
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
    }
    Write-Host ""

    exit 0
} else {
    Write-Host "Some deployments failed. Please check the errors above." -ForegroundColor Red
    Write-Host ""
    exit 1
}
