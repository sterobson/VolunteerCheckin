# Authentication & Authorization System

## Overview

The Volunteer Check-in application uses a passwordless authentication system with two distinct authentication methods:

1. **Email Magic Link** - For admins and area leads (expires in 24 hours)
2. **Marshal Magic Code** - For event marshals (6-digit code, never expires)

The key architectural principle is that **authentication method determines permissions**, not just role assignment. This allows admins who authenticate via magic link to also act as marshals in the same session, while marshals who only use their magic code cannot access admin functions.

## Core Concepts

### 1. Person-Centric Identity

Every user in the system has a **PersonEntity** that represents their core identity across all events:

```csharp
PersonEntity
├── PersonId (GUID) - Unique identifier
├── Email
├── Name
├── Phone
└── IsSystemAdmin - Cross-event superuser flag
```

### 2. Authentication Methods

#### Email Magic Link (Secure Email Link)
- **Who**: Admins, area leads, area admins
- **Flow**: Request link → Email sent → Click link → Verify token → Create session
- **Session Duration**: 24 hours
- **Scope**: Cross-event (can access multiple events)
- **Permissions**: Full elevated permissions (admin/lead functions)

#### Marshal Magic Code
- **Who**: Event marshals
- **Flow**: Enter 6-digit code → Create session
- **Session Duration**: Never expires
- **Scope**: Locked to single event
- **Permissions**: Marshal functions only (cannot use admin/lead features)

### 3. Sessions

Sessions are created after successful authentication and stored in **AuthSessionEntity**:

```csharp
AuthSessionEntity
├── SessionId (GUID)
├── SessionTokenHash (SHA256 hashed)
├── PersonId
├── EventId (null for cross-event, set for marshal sessions)
├── AuthMethod ("MarshalMagicCode" or "SecureEmailLink")
├── CreatedAt
├── ExpiresAt (null for marshal sessions)
├── LastAccessedAt
├── IsRevoked
└── IpAddress (audit trail)
```

**Key Points:**
- Session tokens are 64-byte cryptographically secure random values
- Only the SHA256 hash is stored (protects against database compromise)
- Sessions are updated with `LastAccessedAt` on each request
- Marshal sessions (magic code auth) never expire
- Admin sessions (email link auth) expire after 24 hours

### 4. Claims & Permissions

On each request, **ClaimsService** computes a **UserClaims** object from the session token:

```csharp
UserClaims
├── PersonId
├── PersonName
├── PersonEmail
├── IsSystemAdmin
├── EventId (null for cross-event sessions)
├── AuthMethod (gates permissions!)
├── MarshalId (if they are a marshal in this event)
└── EventRoles (list of EventRoleInfo)
```

**Permission Gating:**
```csharp
// Can use admin/lead features?
claims.CanUseElevatedPermissions
  → true if AuthMethod == "SecureEmailLink"

// Can act as marshal (check in, complete tasks)?
claims.CanActAsMarshal
  → true if MarshalId != null

// Can manage event?
claims.IsEventAdmin
  → true if has "EventAdmin" role

// Can manage specific area?
claims.IsAreaAdmin(areaId)
  → true if has "EventAreaAdmin" role for that area

// Can view area marshal tasks?
claims.IsAreaLead(areaId)
  → true if has "EventAreaLead" role for that area
```

## Role Hierarchy

### Event Roles (stored in EventRoleEntity)

| Role | Description | Scope |
|------|-------------|-------|
| **EventAdmin** | Full control over event | Event-wide |
| **EventAreaAdmin** | Manage checkpoints in assigned areas | Area-specific |
| **EventAreaLead** | View marshal tasks/PII in assigned areas | Area-specific |

**Important:**
- A person can have multiple roles across different events
- A person can be promoted from marshal → lead → admin while keeping marshal duties
- Roles are stored per-person-per-event
- Area-specific roles have `AreaIdsJson` containing list of area IDs

### System Admin

- `PersonEntity.IsSystemAdmin = true`
- Cross-event superuser
- Can access any event without explicit role assignment

## Data Model

### Entity Relationship

```
PersonEntity (1) ─────┬───── (many) EventRoleEntity
                      │
                      ├───── (many) AuthSessionEntity
                      │
                      ├───── (many) AuthTokenEntity
                      │
                      └───── (many) MarshalEntity
                                     (via PersonId)
```

### Table Storage Structure

#### People Table
- **PartitionKey**: "PERSON" (all people in one partition)
- **RowKey**: PersonId (GUID)

#### EventRoles Table
- **PartitionKey**: PersonId (efficient "what roles does this person have?" queries)
- **RowKey**: {EventId}_{RoleId}

#### AuthTokens Table
- **PartitionKey**: "AUTHTOKEN" (all tokens in one partition)
- **RowKey**: TokenId (GUID)
- **Purpose**: One-time use magic link tokens

#### AuthSessions Table
- **PartitionKey**: "SESSION" (all sessions in one partition)
- **RowKey**: SessionId (GUID)
- **Purpose**: Long-lived authenticated sessions

#### Marshals Table (updated)
- **PartitionKey**: EventId
- **RowKey**: MarshalId (GUID)
- **New Fields**:
  - `PersonId` - Links to PersonEntity
  - `MagicCode` - 6-digit alphanumeric code (A-Z, 0-9)

## API Endpoints

### 0. Instant Login (Development Only)

**Endpoint**: `POST /api/auth/instant-login`

**⚠️ WARNING**: This endpoint bypasses email verification and should **ONLY be used in development**. Disable in production!

**Request:**
```json
{
  "Email": "admin@example.com"
}
```

**Response:**
```json
{
  "Success": true,
  "Email": "admin@example.com",
  "Message": "Login successful"
}
```

**Flow:**
1. Get or create PersonEntity for email (no verification)
2. Create AuthSession with `AuthMethod = "SecureEmailLink"`
3. Set session cookie and return

**Use Case:** Development when you don't have email (SMTP) configured. Allows you to log in instantly with any email address.

**Side Effects:**
- Sets `session` cookie (HttpOnly, Secure, SameSite=Strict, 24h expiry)
- Creates PersonEntity if doesn't exist
- Creates AuthSession with full admin permissions

---

### 1. Request Magic Link

**Endpoint**: `POST /api/auth/request-login`

**Request:**
```json
{
  "Email": "admin@example.com"
}
```

**Response:**
```json
{
  "Success": true,
  "Message": "Magic link sent to your email"
}
```

**Flow:**
1. Get or create PersonEntity for email
2. Generate cryptographically secure random token
3. Hash token with SHA256 and store in AuthTokenEntity
4. Send email with link: `https://yourapp.com/auth/verify?token={token}`
5. Token expires in 15 minutes

### 2. Verify Magic Link

**Endpoint**: `POST /api/auth/verify-token`

**Request:**
```json
{
  "Token": "base64-encoded-token-from-email-link"
}
```

**Response:**
```json
{
  "Success": true,
  "SessionToken": "base64-encoded-session-token",
  "Person": {
    "PersonId": "guid",
    "Name": "John Admin",
    "Email": "admin@example.com",
    "Phone": "555-1234",
    "IsSystemAdmin": false
  },
  "Message": "Login successful"
}
```

**Side Effects:**
- Sets `session` cookie (HttpOnly, Secure, SameSite=Strict, 24h expiry)
- Marks AuthToken as used (prevents reuse)
- Creates AuthSession with `AuthMethod = "SecureEmailLink"`
- Session is **cross-event** (EventId = null)

### 3. Marshal Login

**Endpoint**: `POST /api/auth/marshal-login`

**Request:**
```json
{
  "EventId": "event-guid",
  "MagicCode": "ABC123"
}
```

**Response:**
```json
{
  "Success": true,
  "SessionToken": "base64-encoded-session-token",
  "Person": {
    "PersonId": "guid",
    "Name": "Marshal Name",
    "Email": "marshal@example.com",
    "Phone": "555-5678",
    "IsSystemAdmin": false
  },
  "MarshalId": "marshal-guid",
  "Message": "Login successful"
}
```

**Side Effects:**
- Sets `session` cookie (HttpOnly, Secure, SameSite=Strict, no expiry)
- Creates or gets PersonEntity from marshal details
- Creates AuthSession with `AuthMethod = "MarshalMagicCode"`
- Session is **event-locked** (EventId = specified event)

### 4. Get Current User Claims

**Endpoint**: `GET /api/auth/me?eventId={eventId}`

**Headers:**
- `Cookie: session={token}` OR
- `Authorization: Bearer {token}`

**Query Parameters:**
- `eventId` (optional) - For cross-event admins to specify which event they're accessing

**Response:**
```json
{
  "PersonId": "guid",
  "PersonName": "John Admin",
  "PersonEmail": "admin@example.com",
  "IsSystemAdmin": false,
  "EventId": "event-guid",
  "AuthMethod": "SecureEmailLink",
  "MarshalId": "marshal-guid",
  "EventRoles": [
    {
      "Role": "EventAdmin",
      "AreaIds": []
    },
    {
      "Role": "EventAreaLead",
      "AreaIds": ["area-guid-1", "area-guid-2"]
    }
  ]
}
```

**Helper Methods on UserClaims:**
```csharp
claims.IsEventAdmin                    // true if has EventAdmin role
claims.IsAreaAdmin("area-id")          // true if can manage this area
claims.IsAreaLead("area-id")           // true if is lead for this area
claims.CanUseElevatedPermissions       // true if auth via email link
claims.CanActAsMarshal                 // true if has marshal ID
claims.HasRole("EventAreaAdmin")       // check for any role
```

### 5. Logout

**Endpoint**: `POST /api/auth/logout`

**Headers:**
- `Cookie: session={token}` OR
- `Authorization: Bearer {token}`

**Response:**
```json
{
  "Success": true,
  "Message": "Logged out successfully"
}
```

**Side Effects:**
- Revokes session (sets `IsRevoked = true`)
- Clears `session` cookie

## Security Features

### 1. Token Hashing
- All tokens (session tokens and magic link tokens) are hashed with SHA256 before storage
- If database is compromised, sessions cannot be hijacked

### 2. One-Time Magic Links
- Magic link tokens can only be used once
- `AuthTokenEntity.UsedAt` is set on first use
- Tokens expire after 15 minutes

### 3. Session Tracking
- IP address logged on session creation
- Last accessed time updated on each request
- Audit trail for security investigations

### 4. Secure Cookies
- HttpOnly (prevents XSS attacks)
- Secure (HTTPS only)
- SameSite=Strict (prevents CSRF attacks)

### 5. Rate Limiting Constants
```csharp
MaxMagicLinkRequestsPerEmailPerHour = 5
MaxMarshalCodeAttemptsPerIpPerMinute = 10
MaxMarshalCodeAttemptsPerEventPerHour = 100
```

*Note: Rate limiting service is not yet implemented but constants are defined*

### 6. Marshal Magic Code Security
- 6 characters: A-Z and 0-9
- 36^6 = 2.2 billion possible combinations
- Event-scoped (code only works for one event)
- Can be regenerated by admin to revoke access

## Common Scenarios

### Scenario 1: Admin Signs In and Acts as Marshal

1. Admin requests magic link for their email
2. Admin clicks link, verifies token
3. Session created with `AuthMethod = "SecureEmailLink"`, `EventId = null`
4. Admin navigates to event page, specifies `eventId` in `/api/auth/me?eventId=X`
5. ClaimsService looks up:
   - EventRoles for this person in this event
   - MarshalEntity for this person in this event
6. Claims returned with:
   - `CanUseElevatedPermissions = true` (can use admin features)
   - `MarshalId = "guid"` (can also act as marshal)
   - `IsEventAdmin = true`

**Result:** Admin can complete tasks, check in, AND manage event settings

### Scenario 2: Marshal Signs In

1. Marshal enters 6-digit magic code for event
2. Session created with `AuthMethod = "MarshalMagicCode"`, `EventId = "event-guid"`
3. Claims returned with:
   - `CanUseElevatedPermissions = false` (CANNOT use admin features)
   - `MarshalId = "guid"` (CAN act as marshal)
   - `EventRoles = []` (no elevated roles)

**Result:** Marshal can complete tasks and check in, but cannot access admin panel

### Scenario 3: Marshal Promoted to Area Lead

1. Event admin assigns "EventAreaLead" role to marshal's PersonId
2. EventRoleEntity created with:
   - `PersonId = marshal's person ID`
   - `EventId = event ID`
   - `Role = "EventAreaLead"`
   - `AreaIdsJson = ["area-1", "area-2"]`
3. Marshal's magic code still works for marshal functions
4. But to use lead features, marshal must:
   - Request magic link via email
   - Click link to create session with `AuthMethod = "SecureEmailLink"`
   - Now `CanUseElevatedPermissions = true`

**Result:** Marshal can still use magic code for daily work, but uses email link for lead responsibilities

### Scenario 4: System Admin Accessing Any Event

1. System admin (PersonEntity.IsSystemAdmin = true) signs in via email
2. Session created with `AuthMethod = "SecureEmailLink"`, `EventId = null`
3. System admin navigates to any event
4. ClaimsService checks `IsSystemAdmin = true`
5. Full access granted regardless of EventRoleEntity assignments

**Result:** System admin has god-mode access to all events

## Implementation Notes

### Generating Marshal Magic Codes

```csharp
string magicCode = AuthService.GenerateMagicCode();
// Returns: "A1B2C3" (6 chars, A-Z 0-9)

// When creating a marshal:
MarshalEntity marshal = new()
{
    MarshalId = Guid.NewGuid().ToString(),
    PersonId = person.PersonId,
    MagicCode = magicCode,
    Name = "Marshal Name",
    Email = "marshal@example.com",
    // ...
};
```

### Getting Claims in Your Function

```csharp
public class MyFunction
{
    private readonly ClaimsService _claimsService;

    [Function("MyEndpoint")]
    public async Task<IActionResult> MyEndpoint(HttpRequest req)
    {
        // Get session token from cookie or header
        string? sessionToken = req.Cookies["session"]
            ?? req.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return new UnauthorizedResult();
        }

        // Get event ID from route or query
        string eventId = req.Query["eventId"];

        // Resolve claims
        UserClaims? claims = await _claimsService.GetClaimsAsync(sessionToken, eventId);

        if (claims == null)
        {
            return new UnauthorizedResult();
        }

        // Check permissions
        if (!claims.IsEventAdmin)
        {
            return new ForbidResult();
        }

        // Proceed with request...
    }
}
```

### Protecting Admin-Only Endpoints

```csharp
// Check if user authenticated via email (not magic code)
if (!claims.CanUseElevatedPermissions)
{
    return new ForbidObjectResult(new
    {
        Message = "This action requires admin authentication. Please sign in with your email."
    });
}

// Check if user has EventAdmin role
if (!claims.IsEventAdmin)
{
    return new ForbidObjectResult(new
    {
        Message = "You don't have permission to manage this event."
    });
}
```

### Protecting Area-Specific Endpoints

```csharp
// Get area ID from request
string areaId = req.Query["areaId"];

// Check if user can manage this area
if (!claims.IsAreaAdmin(areaId) && !claims.IsEventAdmin)
{
    return new ForbidObjectResult(new
    {
        Message = "You don't have permission to manage this area."
    });
}
```

## Database Migrations

When deploying this authentication system, the following tables will be automatically created:

- `People` - PersonEntity storage
- `EventRoles` - EventRoleEntity storage
- `AuthTokens` - AuthTokenEntity storage (magic link tokens)
- `AuthSessions` - AuthSessionEntity storage

Existing tables are updated with new fields:
- `Marshals` table gets `PersonId` and `MagicCode` fields

## Future Enhancements

### Planned but Not Yet Implemented

1. **Rate Limiting Service**
   - Enforce MaxMagicLinkRequestsPerEmailPerHour
   - Enforce MaxMarshalCodeAttemptsPerIpPerMinute
   - Prevent brute force attacks

2. **Email Verification**
   - Currently anyone can request magic link for any email
   - Should verify email ownership before granting access

3. **Session Management UI**
   - View active sessions
   - Revoke specific sessions
   - Force logout from all devices

4. **Audit Logging**
   - Track all authentication events
   - Failed login attempts
   - Permission changes

5. **Password Option**
   - Optional password authentication for users who prefer it
   - Would require additional PasswordEntity table

## Troubleshooting

### "Invalid or expired token"

- Magic link tokens expire after 15 minutes
- Tokens can only be used once
- Check that token parameter is correctly passed from email link

### "Not authenticated"

- Session cookie may have expired (24h for admin, never for marshal)
- Session may have been revoked
- Check that cookie is being sent with requests
- For API calls, ensure `Authorization: Bearer {token}` header is set

### "This action requires admin authentication"

- User authenticated with magic code (marshal auth)
- Need to request magic link via email instead
- Marshal codes cannot be used for admin functions

### "You don't have permission"

- User doesn't have required role (EventAdmin, EventAreaAdmin, etc.)
- EventRoleEntity may not exist for this person+event combination
- Check IsSystemAdmin flag if should have cross-event access

## Constants Reference

```csharp
// Auth Methods
Constants.AuthMethodMarshalMagicCode = "MarshalMagicCode"
Constants.AuthMethodSecureEmailLink = "SecureEmailLink"

// Roles
Constants.RoleEventAdmin = "EventAdmin"
Constants.RoleEventAreaAdmin = "EventAreaAdmin"
Constants.RoleEventAreaLead = "EventAreaLead"

// Timeouts
Constants.MagicLinkExpiryMinutes = 15
Constants.AdminSessionExpiryHours = 24
Constants.MagicCodeLength = 6

// Rate Limits (not yet enforced)
Constants.MaxMagicLinkRequestsPerEmailPerHour = 5
Constants.MaxMarshalCodeAttemptsPerIpPerMinute = 10
Constants.MaxMarshalCodeAttemptsPerEventPerHour = 100

// Actor Types (for checklist completion tracking)
Constants.ActorTypeMarshal = "Marshal"
Constants.ActorTypeEventAdmin = "EventAdmin"
Constants.ActorTypeAreaLead = "AreaLead"
```
