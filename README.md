# Volunteer Check-in System

A comprehensive web application for managing race marshals and volunteers with real-time check-in tracking, GPS verification, and admin management.

## Features

### For Admins
- 🎯 Create and manage events
- 📍 Define marshal locations with GPS coordinates
- 👥 Assign marshals to specific locations
- 🗺️ Real-time map view of all locations
- ✓ Live check-in monitoring with SignalR
- 🔒 Secure email-based authentication (magic links)
- 📱 Emergency contact management

### For Marshals
- 📱 Mobile-friendly check-in interface
- 📍 GPS-based location verification
- 🗺️ View all marshal locations on a map
- 🆘 Quick access to emergency contacts
- ✋ Manual check-in option (if GPS unavailable)

## Architecture

### Backend
- **Framework**: C# Azure Functions (.NET 10)
- **Storage**: Azure Table Storage
- **Real-time**: SignalR for live updates
- **Email**: MailKit for magic link authentication
- **Hosting**: Azure Functions (consumption plan)

### Frontend
- **Framework**: Vue.js 3 with Vite
- **State Management**: Pinia
- **Routing**: Vue Router
- **Maps**: Leaflet
- **Real-time**: SignalR client
- **HTTP Client**: Axios

## Getting Started

### Prerequisites
- Node.js 20+ (for frontend)
- .NET 10 SDK (for backend)
- Azure Storage Emulator or Azure Storage Account
- SMTP server credentials (for email)

### Backend Setup

1. Navigate to the Backend folder:
   ```bash
   cd Backend
   ```

2. Install dependencies:
   ```bash
   dotnet restore
   ```

3. Configure `local.settings.json`:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "SMTP_HOST": "smtp.gmail.com",
       "SMTP_PORT": "587",
       "SMTP_USERNAME": "your-email@gmail.com",
       "SMTP_PASSWORD": "your-app-password",
       "FROM_EMAIL": "noreply@volunteercheckin.com",
       "FROM_NAME": "Volunteer Check-in",
       "FRONTEND_URL": "http://localhost:5173"
     }
   }
   ```

4. Start Azure Storage Emulator (if using local development):
   ```bash
   # Windows
   AzureStorageEmulator.exe start

   # Or use Azurite
   azurite
   ```

5. Run the backend:
   ```bash
   func start
   ```

   The API will be available at `http://localhost:7071`

### Frontend Setup

1. Navigate to the FrontEnd folder:
   ```bash
   cd FrontEnd
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Configure `.env`:
   ```
   VITE_API_BASE_URL=http://localhost:7071/api
   ```

4. Run the development server:
   ```bash
   npm run dev
   ```

   The app will be available at `http://localhost:5173`

### Creating Your First Admin User

To create an admin user, call the CreateAdmin endpoint (this endpoint requires Function-level authentication):

```bash
curl -X POST http://localhost:7071/api/auth/create-admin \
  -H "Content-Type: application/json" \
  -d '{"email": "your-admin-email@example.com"}'
```

Or use the Azure Functions Core Tools:
```bash
func run CreateAdmin --data '{"email": "your-admin-email@example.com"}'
```

## Usage Guide

### For Admins

1. **Login**:
   - Go to `/admin/login`
   - Enter your admin email
   - Check your email for the magic link
   - Click the link to log in (valid for 15 minutes)

2. **Create an Event**:
   - Click "Create New Event"
   - Fill in event details including emergency contacts
   - Save the event

3. **Add Locations**:
   - Open your event
   - Click "Add Location"
   - Click on the map to set GPS coordinates or enter manually
   - Specify how many marshals are needed
   - Save the location

4. **Assign Marshals**:
   - Select a location
   - Click "Assign Marshal"
   - Enter the marshal's name
   - Save the assignment

5. **Share with Marshals**:
   - Click "Share Marshal Link"
   - Copy the link and share it with your marshals

6. **Monitor Check-ins**:
   - View the map to see real-time check-in status
   - Green markers = all marshals checked in
   - Orange markers = some marshals checked in
   - Red markers = no marshals checked in
   - Manually check in marshals if needed

### For Marshals

1. **Access the Event**:
   - Open the link provided by the admin
   - Select your name from the list

2. **Check In**:
   - **GPS Check-in**: Click "Check In with GPS"
     - Allow location permissions
     - Must be within 100 meters of assigned location
   - **Manual Check-in**: If GPS unavailable
     - Click "Manual Check-In"
     - Confirm the check-in

3. **Emergency Contacts**:
   - Click "Emergency Info" in the header
   - View contact details
   - Call directly from the app

## GPS Check-in Details

- **Verification Radius**: 100 meters (configurable)
- **Fallback**: Manual check-in available if GPS fails
- **Admin Override**: Admins can manually check in/out any marshal
- **Tracking**: Records check-in time, method (GPS/Manual/Admin), and coordinates

## Real-time Updates

The system uses SignalR for real-time updates:
- Admin dashboard updates automatically when marshals check in
- No manual refresh needed
- Falls back to polling if SignalR connection fails
- Minimal cost on Azure SignalR Service for small-scale usage

## Deployment

### Backend (Azure Functions)

1. Create an Azure Functions App:
   ```bash
   az functionapp create --resource-group YourResourceGroup \
     --consumption-plan-location YourRegion \
     --runtime dotnet-isolated \
     --functions-version 4 \
     --name your-app-name \
     --storage-account yourstorageaccount
   ```

2. Configure app settings:
   ```bash
   az functionapp config appsettings set --name your-app-name \
     --resource-group YourResourceGroup \
     --settings SMTP_HOST=smtp.gmail.com \
                 SMTP_PORT=587 \
                 SMTP_USERNAME=your-email \
                 SMTP_PASSWORD=your-password \
                 FROM_EMAIL=noreply@volunteercheckin.com \
                 FRONTEND_URL=https://your-frontend-url.com
   ```

3. Deploy:
   ```bash
   cd Backend
   func azure functionapp publish your-app-name
   ```

### Frontend (Azure Static Web Apps or any static hosting)

1. Build the frontend:
   ```bash
   cd FrontEnd
   npm run build
   ```

2. Deploy the `dist` folder to your hosting provider

3. Update environment variables with production API URL

## Cost Estimation

For a once-yearly event with 200 marshals:

- **Azure Functions**: ~$0 (within free tier for 4-6 hours usage)
- **Azure Table Storage**: ~$1-2/year (minimal data)
- **Azure SignalR**: ~$0 (self-hosted on Functions)
- **Email (SMTP)**: Depends on provider (Gmail free tier sufficient)
- **Static Hosting**: ~$0 (Azure Static Web Apps free tier or Netlify/Vercel)

**Total**: < $5/year for small events

## Security Considerations

- ✅ Admin authentication via email magic links (no passwords)
- ✅ Magic links expire after 15 minutes
- ✅ Tokens are single-use only
- ✅ No sensitive marshal data stored (names only)
- ✅ CORS properly configured
- ✅ Environment variables for secrets
- ⚠️ Consider adding rate limiting for production
- ⚠️ Use HTTPS in production

## Troubleshooting

### Backend won't start
- Ensure Azure Storage Emulator is running
- Check that all NuGet packages are restored
- Verify .NET 10 SDK is installed

### Frontend can't connect to API
- Check that backend is running on port 7071
- Verify VITE_API_BASE_URL in `.env`
- Check browser console for CORS errors

### Magic links not sending
- Verify SMTP credentials in `local.settings.json`
- Check spam folder
- Enable "Less secure app access" if using Gmail (or use App Passwords)

### GPS check-in failing
- Ensure location permissions are granted
- Try manual check-in as fallback
- Check GPS accuracy (outdoor vs indoor)

### SignalR not connecting
- Check browser console for connection errors
- Verify CORS settings
- Fallback to polling will work if SignalR fails

## Contributing

Feel free to submit issues and enhancement requests!

## License

MIT License - feel free to use this for your events!
