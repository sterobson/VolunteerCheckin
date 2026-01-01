# Permissions System

## Overview

The Volunteer Check-in application uses a layered permission system that determines what actions users can perform and what data they can access. Permissions are computed from:

1. **Authentication Method** - How the user logged in
2. **System Admin Flag** - Global superuser status
3. **Event Roles** - Event-specific role assignments
4. **Marshal Status** - Whether the user is a marshal in the event
5. **Area Assignments** - Which areas/checkpoints the user is associated with

## Authentication-Based Permissions

The authentication method gates access to elevated features. This is the first permission check.

| Auth Method | Description | Elevated Features | Marshal Features |
|-------------|-------------|:-----------------:|:----------------:|
| `SecureEmailLink` | Magic link sent to email | ✓ | ✓ (if marshal) |
| `MarshalMagicCode` | 6-digit event code | ✗ | ✓ |

**Key Principle**: A user authenticated via magic code can NEVER access admin features, even if they have admin roles assigned. They must re-authenticate via email magic link.

```csharp
// Check if user can use admin/lead features
if (!claims.CanUseElevatedPermissions)
{
    return new ForbidResult(); // Must authenticate via email
}
```

## Role Hierarchy

### System Admin

- Stored in `PersonEntity.IsSystemAdmin`
- Cross-event superuser with full access to everything
- Bypasses all event-specific permission checks

### Event Roles

Stored in `EventRoleEntity` table, scoped per-person per-event:

| Role | Scope | Permissions |
|------|-------|-------------|
| **EventAdmin** | Event-wide | Full control over all event settings, marshals, checkpoints, areas |
| **EventAreaAdmin** | Area-specific | Manage checkpoints and assignments in specified areas |
| **EventAreaLead** | Area-specific | View marshal tasks, PII, and completion status in specified areas |

**Area-Specific Roles:**
- If `AreaIds` is empty: Role applies to ALL areas
- If `AreaIds` contains values: Role only applies to those specific areas

## Contact Permissions

The `ContactPermissionService` determines who can view and modify marshal contact details (email, phone, notes).

### Permission Levels

| Role | View Own | View Area Leads | View Marshals in Area | View All | Modify Own | Modify All |
|------|:--------:|:---------------:|:---------------------:|:--------:|:----------:|:----------:|
| Marshal | ✓ | ✓ | ✗ | ✗ | ✓ | ✗ |
| Area Lead | ✓ | ✓ | ✓ | ✗ | ✓ | ✗ |
| Event Admin | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| System Admin | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

### View Permission Details

**Marshals can view:**
- Their own contact details
- Contact details of area leads for areas they're assigned to
- Event emergency contacts

**Area Leads can view:**
- Their own contact details
- Contact details of all marshals assigned to checkpoints in their lead areas
- Contact details of other area leads

**Event Admins/System Admins can view:**
- All marshal contact details in the event

### Modify Permission Details

**Marshals can modify:**
- Only their own name, email, and phone number

**Event Admins/System Admins can modify:**
- Any marshal's details in the event

### Implementation

```csharp
// Get permissions for current user
ContactPermissions permissions = await _contactPermissionService
    .GetContactPermissionsAsync(claims, eventId);

// Check if can view specific marshal
if (!_contactPermissionService.CanViewContactDetails(permissions, marshalId))
{
    // Redact PII from response
    response.Email = null;
    response.PhoneNumber = null;
    response.Notes = null;
}

// Check if can modify
if (!_contactPermissionService.CanModifyMarshal(permissions, marshalId))
{
    return new ForbidResult();
}
```

### Response DTOs

The API uses `MarshalWithPermissionsResponse` to indicate permission status:

```json
{
  "Id": "marshal-guid",
  "Name": "John Smith",
  "Email": null,              // null if can't view
  "PhoneNumber": null,        // null if can't view
  "Notes": null,              // null if can't view
  "CanViewContactDetails": false,
  "CanModify": false
}
```

## Scope-Based Visibility

Checklists and Notes use a scope system to determine which items are visible to which marshals. See [Checklists API](../API/Checklists.md) and [Notes API](../API/Notes.md) for full details.

### Available Scopes

| Scope | Description | Use Case |
|-------|-------------|----------|
| `EveryoneInAreas` | All marshals in specified areas | Area-wide announcements |
| `EveryoneAtCheckpoints` | All marshals at specified checkpoints | Checkpoint-specific tasks |
| `SpecificPeople` | Specific marshals by ID | Individual assignments |
| `EveryAreaLead` | Each area lead | Lead responsibilities |
| `OnePerCheckpoint` | Shared per checkpoint | Team tasks (checklists only) |
| `OnePerArea` | Shared per area | Area tasks (checklists only) |
| `OneLeadPerArea` | One lead per area | Lead team tasks (checklists only) |

### Who Can Create/Modify Scoped Items

| Action | EventAdmin | AreaAdmin | AreaLead | Marshal |
|--------|:----------:|:---------:|:--------:|:-------:|
| Create checklist items | ✓ | ✓ (own areas) | ✗ | ✗ |
| Modify checklist items | ✓ | ✓ (own areas) | ✗ | ✗ |
| Delete checklist items | ✓ | ✓ (own areas) | ✗ | ✗ |
| Complete checklist items | ✓ | ✓ | ✓ (own items) | ✓ (own items) |
| Create notes | ✓ | ✓ (own areas) | ✗ | ✗ |
| Modify notes | ✓ | ✓ (own areas) | ✗ | ✗ |
| Delete notes | ✓ | ✓ (own areas) | ✗ | ✗ |

## Endpoint Permission Matrix

### Event Management

| Endpoint | Required Permission |
|----------|---------------------|
| `GET /api/events` | Authenticated |
| `GET /api/events/{id}` | Authenticated + Event access |
| `POST /api/events` | System Admin |
| `PUT /api/events/{id}` | Event Admin |
| `DELETE /api/events/{id}` | Event Admin |

### Marshal Management

| Endpoint | Required Permission |
|----------|---------------------|
| `GET /api/events/{id}/marshals` | Authenticated + Event access |
| `POST /api/events/{id}/marshals` | Event Admin + Elevated |
| `PUT /api/events/{id}/marshals/{id}` | Event Admin + Elevated |
| `DELETE /api/events/{id}/marshals/{id}` | Event Admin + Elevated |
| `GET /api/events/{id}/marshals/{id}/magic-link` | Event Admin + Elevated |

### Area Management

| Endpoint | Required Permission |
|----------|---------------------|
| `GET /api/events/{id}/areas` | Authenticated + Event access |
| `POST /api/areas` | Event Admin + Elevated |
| `PUT /api/areas/{eventId}/{areaId}` | Event Admin + Elevated |
| `DELETE /api/areas/{eventId}/{areaId}` | Event Admin + Elevated |
| `POST /api/areas/{eventId}/{areaId}/leads` | Event Admin + Elevated |
| `DELETE /api/areas/{eventId}/{areaId}/leads/{id}` | Event Admin + Elevated |

### Checkpoint Management

| Endpoint | Required Permission |
|----------|---------------------|
| `GET /api/events/{id}/locations` | Authenticated + Event access |
| `POST /api/events/{id}/locations` | Event Admin + Elevated |
| `PUT /api/events/{id}/locations/{id}` | Event Admin + Elevated |
| `DELETE /api/events/{id}/locations/{id}` | Event Admin + Elevated |

### Checklist Items

| Endpoint | Required Permission |
|----------|---------------------|
| `GET /api/events/{id}/checklist-items` | Event Admin + Elevated |
| `POST /api/events/{id}/checklist-items` | Event Admin + Elevated |
| `PUT /api/checklist-items/{eventId}/{itemId}` | Event Admin + Elevated |
| `DELETE /api/checklist-items/{eventId}/{itemId}` | Event Admin + Elevated |
| `GET /api/events/{id}/marshals/{id}/checklist` | Marshal (own) or Admin |
| `POST /api/checklist-items/{eventId}/{itemId}/complete` | Marshal (own) or Admin |
| `POST /api/checklist-items/{eventId}/{itemId}/uncomplete` | Event Admin + Elevated |

### Notes

| Endpoint | Required Permission |
|----------|---------------------|
| `GET /api/events/{id}/notes` | Event Admin + Elevated |
| `POST /api/events/{id}/notes` | Event Admin + Elevated |
| `PUT /api/events/{id}/notes/{id}` | Event Admin + Elevated |
| `DELETE /api/events/{id}/notes/{id}` | Event Admin + Elevated |
| `GET /api/events/{id}/marshals/{id}/notes` | Marshal (own) or Admin |
| `GET /api/events/{id}/my-notes` | Authenticated + Event access |

## Common Permission Patterns

### Checking Event Admin

```csharp
if (!claims.IsEventAdmin && !claims.IsSystemAdmin)
{
    return new ForbidObjectResult(new ErrorResponse(
        "You don't have permission to manage this event."
    ));
}
```

### Checking Elevated Permissions

```csharp
if (!claims.CanUseElevatedPermissions)
{
    return new ForbidObjectResult(new ErrorResponse(
        "This action requires admin authentication. Please sign in with your email."
    ));
}
```

### Checking Area-Specific Access

```csharp
if (!claims.IsAreaAdmin(areaId) && !claims.IsEventAdmin && !claims.IsSystemAdmin)
{
    return new ForbidObjectResult(new ErrorResponse(
        "You don't have permission to manage this area."
    ));
}
```

### Checking Marshal Self-Access

```csharp
// Allow admins OR the marshal themselves
if (!claims.IsEventAdmin && claims.MarshalId != marshalId)
{
    return new ForbidObjectResult(new ErrorResponse(
        "You can only access your own data."
    ));
}
```

## Permission Constants

```csharp
// Auth Methods
Constants.AuthMethodMarshalMagicCode = "MarshalMagicCode"
Constants.AuthMethodSecureEmailLink = "SecureEmailLink"

// Roles
Constants.RoleEventAdmin = "EventAdmin"
Constants.RoleEventAreaAdmin = "EventAreaAdmin"
Constants.RoleEventAreaLead = "EventAreaLead"

// Scopes
Constants.ScopeEveryoneInAreas = "EveryoneInAreas"
Constants.ScopeEveryoneAtCheckpoints = "EveryoneAtCheckpoints"
Constants.ScopeSpecificPeople = "SpecificPeople"
Constants.ScopeEveryAreaLead = "EveryAreaLead"
Constants.ScopeOnePerCheckpoint = "OnePerCheckpoint"
Constants.ScopeOnePerArea = "OnePerArea"
Constants.ScopeOneLeadPerArea = "OneLeadPerArea"

// Sentinel Values
Constants.AllCheckpoints = "ALL_CHECKPOINTS"
Constants.AllAreas = "ALL_AREAS"
Constants.AllMarshals = "ALL_MARSHALS"
```

## Related Documentation

- [Authentication System](Authentication.md) - Login flows, sessions, tokens
- [Checklists API](../API/Checklists.md) - Scope-based checklist items
- [Notes API](../API/Notes.md) - Scope-based notes
