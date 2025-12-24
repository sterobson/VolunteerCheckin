# Azure Function App Configuration Guide

This guide shows you how to manually configure your Azure Function App settings for the testing environment.

## Prerequisites

- Azure Function App already created: `sterobson-volunteercheckin-testing`
- Azure Storage Account in the `VolunteerCheckIn` resource group

## Step 1: Get Your Storage Connection String

1. Go to **Azure Portal** (https://portal.azure.com)
2. Navigate to your **Storage Account** in the `VolunteerCheckIn` resource group
3. In the left menu, go to **Security + networking** → **Access keys**
4. Click **Show** next to `key1`
5. Copy the **Connection string** (not the key itself)

## Step 2: Configure Function App Settings

1. In Azure Portal, navigate to your **Function App**: `sterobson-volunteercheckin-testing`
2. In the left menu, go to **Settings** → **Environment variables**
3. Click on the **App settings** tab
4. Click **+ Add** for each setting below:

### Required Settings

| Name | Value | Notes |
|------|-------|-------|
| `AzureWebJobsStorage` | (paste your connection string from Step 1) | Required for Functions runtime |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` | Required for .NET isolated Functions |
| `FRONTEND_URL` | `https://sterobson.github.io/VolunteerCheckin/testing/` | Used for CORS and email links |

### Optional Settings (for email functionality)

| Name | Value | Notes |
|------|-------|-------|
| `SMTP_HOST` | `smtp.gmail.com` | Your SMTP server |
| `SMTP_PORT` | `587` | SMTP port (usually 587 for TLS) |
| `SMTP_USERNAME` | (your email address) | Email account for sending |
| `SMTP_PASSWORD` | (your app password) | App-specific password (not your regular password) |
| `FROM_EMAIL` | `noreply@volunteercheckin.com` | From address in emails |
| `FROM_NAME` | `Volunteer Check-in` | From name in emails |

5. Click **Apply** at the bottom
6. Click **Confirm** when prompted (this will restart your Function App)

## Step 3: Configure CORS

1. Still in your Function App, go to **API** → **CORS**
2. Add these allowed origins:
   - `https://sterobson.github.io`
   - `http://localhost:5173` (for local development)
3. **Uncheck** "Enable Access-Control-Allow-Credentials"
4. Click **Save**

## Step 4: Verify Configuration

1. Wait about 1-2 minutes for the Function App to restart
2. Visit your testing frontend: https://sterobson.github.io/VolunteerCheckin/testing/
3. Try to create an event or login - it should now work!

## Troubleshooting

### "Storage account not found" errors
- Check that `AzureWebJobsStorage` is set correctly
- Ensure the connection string is complete (should start with `DefaultEndpointsProtocol=https...`)

### CORS errors in browser console
- Check that you added the correct origins in CORS settings
- Make sure to include `https://` (not `http://`)
- Clear browser cache and try again

### Emails not sending
- If using Gmail, you need an "App Password", not your regular password
  - Go to Google Account → Security → 2-Step Verification → App passwords
  - Generate a new app password for "Mail"
  - Use that password in `SMTP_PASSWORD`

## Need to Update Settings Later?

Just go back to **Settings** → **Environment variables** → **App settings** and edit the values. Don't forget to click **Apply** and **Confirm** to restart the app.
