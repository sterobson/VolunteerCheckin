# Sample Event Feature Implementation Plan

## Overview
Allow anonymous users to try the system by clicking "Try it out for free" on the home page. A pre-seeded template event gets cloned, giving them a fully functional sample event that auto-deletes after 4 hours.

## Backend Changes

### 1. EventEntity Changes (EventEntity.cs)
Add as direct entity properties (not in payload, since we need to query on them):
- `IsSampleEvent` (bool) - marks this as a sample event
- `ExpiresAt` (DateTime?) - when the sample event should be deleted

### 2. New SampleEventFunctions.cs
Create endpoint:
```
POST /api/sample-event
```
- No authentication required (anonymous)
- Rate limiting: one per day per device (via device fingerprint header)
- Clones the template event (identified by a special row key like "TEMPLATE_EVENT")
- Returns the new event ID and admin magic code

### 3. New SampleEventService.cs
Programmatically generate sample event data (no template cloning):
- Create EventEntity with:
  - Name: "Sample Event"
  - Date: 10am next Sunday (or Sunday after if today is Sunday)
  - IsSampleEvent=true, ExpiresAt=now+4hours
- Create Areas (e.g., "Start/Finish", "North Section", "South Section")
- Create Checkpoints (e.g., 6-8 checkpoints spread across areas)
- Create Marshals: 2x checkpoint count, names generated via Humanizer package (MIT license)
- Create Assignments: randomly assign 2 marshals per checkpoint
- Create ChecklistItems: sample tasks for checkpoints (e.g., "Check radio", "Set up signage")
- Create EventContacts: sample emergency contacts
- Generate MagicCode for admin access

### 4. Add to MaintenanceFunctions.cs
Timer-triggered cleanup function (every 15 minutes):
```csharp
[Function("CleanupExpiredSampleEvents")]
public async Task CleanupExpiredSampleEvents([TimerTrigger("0 */15 * * * *")] TimerInfo timer)
```
- Query all events where IsSampleEvent=true and ExpiresAt < now
- Delete the event and ALL related entities (cascade delete)

### 5. Rate Limiting
Use device fingerprint passed in header (e.g., `X-Device-Fingerprint`)
- Store in Table Storage: fingerprint -> last creation timestamp
- Reject if created sample in last 24 hours
- Return 429 Too Many Requests with appropriate message

## Frontend Changes

### 1. Home.vue
Add prominent "Try it out for free" button:
- Styled to stand out (primary CTA)
- Generates device fingerprint (using browser fingerprinting library or localStorage UUID)
- Calls POST /api/sample-event
- On success: stores admin magic code in localStorage, redirects to admin view
- On rate limit error: shows message "You've already created a sample today. Please try again tomorrow."

### 2. New DemoBanner.vue Component
Floating banner at top of page showing:
- "Demo mode - expires in X hours Y minutes"
- Countdown timer (updates every minute)
- "Sign up to keep your event" CTA button
- Styled to be noticeable but not intrusive

### 3. Update Admin Views
- Check if current event has IsSampleEvent=true and ExpiresAt set
- If so, show DemoBanner at top of page
- Pass expiration time to banner component

### 4. Update MarshalView.vue
- Same DemoBanner logic for marshal mode
- Marshals should see they're in a demo event

### 5. Rate Limiting (localStorage)
Store last sample creation timestamp:
- Key: `sampleEventLastCreated`
- Check before calling API (fail fast)
- Update on successful creation

## Dependencies
- Add `Humanizer` NuGet package (MIT license) for generating realistic marshal names

## Files to Create/Modify

### New Files:
- `Backend/Functions/SampleEventFunctions.cs`
- `Backend/Services/SampleEventService.cs`
- `FrontEnd/src/components/DemoBanner.vue`

### Modified Files:
- `Backend/Models/EventEntity.cs` - add IsSampleEvent, ExpiresAt as direct properties
- `Backend/Functions/MaintenanceFunctions.cs` - add cleanup timer
- `Backend/Program.cs` - register new services
- `FrontEnd/src/views/Home.vue` - add "Try it out" button
- `FrontEnd/src/views/AdminEventManage.vue` - show demo banner
- `FrontEnd/src/views/MarshalView.vue` - show demo banner
- `FrontEnd/src/services/api.js` - add createSampleEvent method

## Implementation Order
1. Add Humanizer NuGet package
2. Backend: EventEntity changes (add IsSampleEvent, ExpiresAt)
3. Backend: SampleEventService (generation logic)
4. Backend: SampleEventFunctions (API endpoint)
5. Backend: Cleanup timer function
6. Frontend: DemoBanner component
7. Frontend: Home.vue "Try it out" button
8. Frontend: Admin/Marshal views to show banner
9. Testing
