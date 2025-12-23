# Deployment Guide

This guide explains how to use the `deploy.ps1` script to run and deploy the Volunteer Check-in application.

## Prerequisites

### For Local Development
- **Node.js** (v18 or higher) - [Download](https://nodejs.org/)
- **Azure Functions Core Tools** - Install with: `npm install -g azure-functions-core-tools@4 --unsafe-perm true`
- **Azurite** (Azure Storage Emulator) - Install with: `npm install -g azurite`
- **.NET SDK** (v8.0 or higher) - [Download](https://dotnet.microsoft.com/download)

### For Testing Deployment
- **Azure CLI** - [Install Instructions](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- **Git** - [Download](https://git-scm.com/)
- **Azure Account** with access to the VolunteerCheckIn resource group
- **GitHub Account** with push access to the repository

## Initial Setup

1. **Copy the environment file:**
   ```powershell
   Copy-Item .env.local.example .env.local
   ```

2. **Edit `.env.local` and fill in your values:**
   - For **local development**, you don't need to fill anything in
   - For **testing deployment**, you need:
     - `AZURE_STORAGE_CONNECTION_STRING` - Get from Azure Portal → Storage Account → Access Keys
     - `SMTP_USERNAME` and `SMTP_PASSWORD` (optional, for email features)

## Running the Deployment Script

Simply run:
```powershell
.\deploy.ps1
```

You'll see a menu with options:

### Option 1: Deploy Locally

This option will:
- ✓ Start Azurite (Azure Storage Emulator) if not running
- ✓ Start the Azure Functions backend on `http://localhost:7071`
- ✓ Start the Vue.js frontend on `http://localhost:5173`

The script checks if each service is already running and only starts what's needed.

**Access your local app:**
- Frontend: http://localhost:5173
- Backend API: http://localhost:7071/api

### Option 2: Deploy to Testing

This option will:
- ✓ Build the frontend with base path `/testing/`
- ✓ Deploy frontend to GitHub Pages at: `https://sterobson.github.io/VolunteerCheckin/testing/`
- ✓ Update Azure Function app settings
- ✓ Publish backend to Azure: `https://sterobson-volunteercheckin-testing.azurewebsites.net`

**Requirements:**
- You must have filled in `.env.local` with Azure credentials
- You must be logged into Azure CLI (the script will prompt you if needed)
- You must have push access to the GitHub repository

## Configuration Files

### `.env.local` (Not committed to Git)
Contains secrets and environment-specific configuration. See `.env.local.example` for required fields.

### `Backend/local.settings.json`
Contains local development settings for Azure Functions. Already configured for local development with Azurite.

### `FrontEnd/src/config.js`
Contains frontend configuration. The API URL is automatically set based on environment:
- Local: `http://localhost:7071/api`
- Testing: Set via `VITE_API_BASE_URL` during build

## Troubleshooting

### "Azurite could not start"
Install Azurite: `npm install -g azurite`

### "Azure Functions could not start"
Install Azure Functions Core Tools: `npm install -g azure-functions-core-tools@4 --unsafe-perm true`

### "Missing required environment variables"
Make sure you've created `.env.local` and filled in the required values for your deployment target.

### "Failed to deploy to GitHub Pages"
Ensure you have push access to the repository and that you're authenticated with Git.

### "Azure deployment failed"
- Check you're logged into Azure CLI: `az login`
- Verify you have access to the resource group
- Check that the Function App name matches in `.env.local`

## What Gets Committed to Git

✅ **Committed:**
- Source code
- `.env.local.example` (template)
- `deploy.ps1` (deployment script)
- `Backend/local.settings.json` (local dev settings)

❌ **NOT Committed:**
- `.env.local` (contains secrets)
- `FrontEnd/dist/` (build output)
- `FrontEnd/node_modules/` (dependencies)

## Next Steps (Production Deployment)

Production deployment will be added to the script in a future update. It will:
- Deploy frontend to GitHub Pages at `/production/`
- Deploy backend to a separate Azure Function App for production
