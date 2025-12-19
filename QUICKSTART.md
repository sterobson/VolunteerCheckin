# Quick Start Guide

Get your Volunteer Check-in system running in 5 minutes!

## Step 1: Configure Email (Backend)

Edit `Backend/local.settings.json` and add your SMTP credentials:

```json
{
  "Values": {
    "SMTP_HOST": "smtp.gmail.com",
    "SMTP_PORT": "587",
    "SMTP_USERNAME": "your-email@gmail.com",
    "SMTP_PASSWORD": "your-app-password"
  }
}
```

**For Gmail**: Create an App Password at https://myaccount.google.com/apppasswords

## Step 2: Install Azure Storage Emulator

**Option A - Azurite (Recommended)**:
```bash
npm install -g azurite
azurite
```

**Option B - Azure Storage Emulator** (Windows):
Download and install from Microsoft's website, then run:
```bash
AzureStorageEmulator.exe start
```

## Step 3: Start the Backend

```bash
cd Backend
dotnet restore
func start
```

The backend will run on http://localhost:7071

## Step 4: Create Your Admin Account

Open `setup-admin.http` and replace `YOUR_EMAIL@example.com` with your email, then execute the request using VS Code REST Client extension, or use curl:

```bash
curl -X POST http://localhost:7071/api/auth/create-admin \
  -H "Content-Type: application/json" \
  -d '{"email": "your-email@example.com"}'
```

## Step 5: Start the Frontend

Open a new terminal:

```bash
cd FrontEnd
npm install
npm run dev
```

The frontend will run on http://localhost:5173

## Step 6: Test the System

1. Go to http://localhost:5173
2. Click "Admin Portal"
3. Enter your admin email
4. Check your email for the magic link
5. Click the link to log in
6. Create your first event!

## Common Issues

### "Storage emulator not running"
- Start Azurite or Azure Storage Emulator first
- Make sure it's running before starting the backend

### "Email not sending"
- Check your SMTP credentials
- For Gmail, use an App Password (not your regular password)
- Check spam folder
- Verify "SMTP_HOST" and "SMTP_PORT" are correct

### "Port already in use"
- Backend: Check if another app is using port 7071
- Frontend: Check if another app is using port 5173

### "Cannot find module errors"
- Backend: Run `dotnet restore` in the Backend folder
- Frontend: Run `npm install` in the FrontEnd folder

## Next Steps

Once logged in as admin:
1. Create an event
2. Add locations (click on map or enter coordinates)
3. Assign marshals to locations
4. Share the marshal link with your volunteers
5. Monitor check-ins in real-time!

## Tips

- 📱 The marshal view works great on mobile phones
- 🗺️ Click directly on the map to add locations quickly
- 📍 GPS check-in requires location permissions
- ✋ Manual check-in is available as a fallback
- 🔄 Changes update in real-time via SignalR

Enjoy your event! 🎉
