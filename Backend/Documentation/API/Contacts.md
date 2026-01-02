# Event Contacts API Documentation

## Table of Contents
- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [Available Scope Types](#available-scope-types)
- [Data Models](#data-models)
- [API Endpoints](#api-endpoints)
  - [Admin: Contact Management](#admin-contact-management)
  - [Marshal: Viewing Contacts](#marshal-viewing-contacts)
- [Business Rules](#business-rules)
- [Common Workflows](#common-workflows)
- [Frontend Implementation Guide](#frontend-implementation-guide)
- [Error Handling](#error-handling)

---

## Overview

The Event Contacts system allows event administrators to create contact information that is displayed to marshals based on scope configurations. Contacts can be linked to existing marshals in the system or be external contacts (not in the system).

### Key Features
- **Multiple contacts per event** with different roles
- **Scope-based visibility** using the same system as notes/checklists
- **Marshal linking** - contacts can optionally reference existing marshals
- **External contacts** - support for contacts not in the system
- **Built-in roles** with ability to add custom roles
- **Primary contact** designation per role

### Contact Types

| Type | Description |
|------|-------------|
| Linked Contact | References an existing marshal; can sync name/phone/email |
| External Contact | Standalone contact not in the marshal system |

---

## Core Concepts

### 1. Event Contact
A person's contact information displayed to marshals. Contains:
- **Name**: Contact's display name
- **Phone**: Primary phone number
- **Email**: Optional email address
- **Notes**: Optional description or instructions
- **Role**: What capacity they serve (EmergencyContact, EventDirector, etc.)
- **MarshalId**: Optional link to existing marshal
- **Scope Configurations**: Array defining who sees the contact
- **IsPrimary**: Whether this is the primary contact for their role

### 2. Contact Roles

Built-in roles (can be extended with custom roles):

| Role | Description |
|------|-------------|
| EmergencyContact | Emergency situations, safety concerns |
| EventDirector | Overall event coordination |
| MedicalLead | Medical emergencies, first aid |
| SafetyOfficer | Safety procedures, hazard reporting |
| Logistics | Equipment, supplies, transportation |

Custom roles can be added by simply using a new role name when creating a contact.

### 3. Scope Configuration
Determines who can see the contact. Uses the same scope system as notes for consistency:

| Scope | Who Sees It |
|-------|------------|
| EveryoneInAreas | Marshals assigned to checkpoints in specified areas |
| EveryoneAtCheckpoints | Marshals assigned to specified checkpoints |
| SpecificPeople | Only the specified marshals |
| EveryAreaLead | Area leads for specified areas |

**Note**: "One per" scopes (OnePerArea, OnePerCheckpoint, OneLeadPerArea) are **not** supported for contacts.

---

## Available Scope Types

### 1. EveryoneInAreas
- **Who sees it**: Marshals assigned to checkpoints in specified areas
- **Filter**: `itemType: "Area"`, `ids: [area IDs]`
- **Use case**: Area-specific emergency contact for that region

### 2. EveryoneAtCheckpoints
- **Who sees it**: Marshals assigned to specified checkpoints
- **Filter**: `itemType: "Checkpoint"`, `ids: [checkpoint IDs]`
- **Use case**: Checkpoint-specific coordinator contact

### 3. SpecificPeople
- **Who sees it**: Only the specified marshals
- **Filter**: `itemType: "Marshal"`, `ids: [marshal IDs]` or `ids: ["ALL_MARSHALS"]`
- **Use case**: Event-wide emergency contacts visible to everyone

### 4. EveryAreaLead
- **Who sees it**: Area leads for specified areas
- **Filter**: `itemType: "Area"`, `ids: [area IDs]`
- **Use case**: Contacts only area leads should have access to

### Default Scope
If no scope is specified when creating a contact, it defaults to visible to everyone:
```json
{
  "scope": "EveryoneInAreas",
  "itemType": "Area",
  "ids": ["ALL_AREAS"]
}
```

---

## Data Models

### ScopeConfiguration
```typescript
interface ScopeConfiguration {
  scope: string;         // "EveryoneInAreas", "EveryoneAtCheckpoints", "SpecificPeople", "EveryAreaLead"
  itemType: string | null;  // "Marshal", "Checkpoint", "Area"
  ids: string[];         // IDs to match (can include sentinel values)
}
```

### CreateEventContactRequest
```typescript
interface CreateEventContactRequest {
  role: string;                              // Required
  name: string;                              // Required
  phone: string;                             // Required
  email?: string;                            // Optional
  notes?: string;                            // Optional
  marshalId?: string;                        // Optional - link to existing marshal
  scopeConfigurations?: ScopeConfiguration[]; // Default: visible to everyone
  displayOrder?: number;                     // Default: 0
  isPrimary?: boolean;                       // Default: false
}
```

### UpdateEventContactRequest
```typescript
interface UpdateEventContactRequest {
  role: string;
  name: string;
  phone: string;
  email?: string;
  notes?: string;
  marshalId?: string;
  scopeConfigurations?: ScopeConfiguration[];
  displayOrder?: number;
  isPrimary?: boolean;
}
```

### EventContactResponse (Admin View)
```typescript
interface EventContactResponse {
  contactId: string;
  eventId: string;
  role: string;
  name: string;
  phone: string;
  email?: string;
  notes?: string;
  marshalId?: string;
  marshalName?: string;               // Resolved from marshal if linked
  scopeConfigurations: ScopeConfiguration[];
  displayOrder: number;
  isPrimary: boolean;
  createdAt: string;                  // ISO 8601
  updatedAt?: string;                 // ISO 8601
}
```

### EventContactForMarshalResponse (Marshal View)
```typescript
interface EventContactForMarshalResponse {
  contactId: string;
  role: string;
  name: string;
  phone: string;
  email?: string;
  notes?: string;
  isPrimary: boolean;
  matchedScope: string;               // Which scope configuration matched
}
```

### ContactRolesResponse
```typescript
interface ContactRolesResponse {
  builtInRoles: string[];             // ["EmergencyContact", "EventDirector", ...]
  customRoles: string[];              // Custom roles used in this event
}
```

---

## API Endpoints

### Admin: Contact Management

#### Create Contact
```http
POST /api/events/{eventId}/contacts
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**:
```json
{
  "role": "EmergencyContact",
  "name": "John Smith",
  "phone": "555-0100",
  "email": "john@example.com",
  "notes": "Available 24/7",
  "isPrimary": true,
  "scopeConfigurations": [
    {
      "scope": "EveryoneInAreas",
      "itemType": "Area",
      "ids": ["ALL_AREAS"]
    }
  ]
}
```

**Response** (200 OK):
```json
{
  "contactId": "contact-abc123",
  "eventId": "event-456",
  "role": "EmergencyContact",
  "name": "John Smith",
  "phone": "555-0100",
  "email": "john@example.com",
  "notes": "Available 24/7",
  "marshalId": null,
  "marshalName": null,
  "scopeConfigurations": [...],
  "displayOrder": 0,
  "isPrimary": true,
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": null
}
```

**Error Responses**:

**400 Bad Request** (Invalid marshal):
```json
{
  "message": "Marshal not found"
}
```

**401 Unauthorized**:
```json
{
  "message": "Not authorized to manage contacts"
}
```

---

#### Create Linked Contact (from Marshal)
```http
POST /api/events/{eventId}/contacts
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**:
```json
{
  "role": "EventDirector",
  "name": "Jane Doe",
  "phone": "555-0200",
  "marshalId": "marshal-123"
}
```

The `marshalId` links this contact to an existing marshal in the system.

---

#### List All Contacts (Admin)
```http
GET /api/events/{eventId}/contacts
Authorization: Bearer {sessionToken}
```

**Response** (200 OK):
```json
[
  {
    "contactId": "contact-abc123",
    "eventId": "event-456",
    "role": "EmergencyContact",
    "name": "John Smith",
    "phone": "555-0100",
    "email": "john@example.com",
    "notes": "Available 24/7",
    "marshalId": null,
    "marshalName": null,
    "scopeConfigurations": [...],
    "displayOrder": 0,
    "isPrimary": true,
    "createdAt": "2025-01-15T10:30:00Z",
    "updatedAt": null
  }
]
```

**Sorting**:
1. Primary contacts first
2. By display order
3. By role
4. By name

---

#### Get Single Contact
```http
GET /api/events/{eventId}/contacts/{contactId}
Authorization: Bearer {sessionToken}
```

**Response** (200 OK): Same as EventContactResponse

**Response** (404 Not Found):
```json
{
  "message": "Contact not found"
}
```

---

#### Update Contact
```http
PUT /api/events/{eventId}/contacts/{contactId}
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**: Same as CreateEventContactRequest

**Response** (200 OK): Updated EventContactResponse

---

#### Delete Contact
```http
DELETE /api/events/{eventId}/contacts/{contactId}
Authorization: Bearer {sessionToken}
```

**Response** (204 No Content)

**Note**: This is a soft delete. The contact is marked as deleted but not permanently removed.

---

#### Get Available Roles
```http
GET /api/events/{eventId}/contact-roles
Authorization: Bearer {sessionToken}
```

**Response** (200 OK):
```json
{
  "builtInRoles": [
    "EmergencyContact",
    "EventDirector",
    "MedicalLead",
    "SafetyOfficer",
    "Logistics"
  ],
  "customRoles": [
    "RadioOperator",
    "FirstAidStation"
  ]
}
```

Custom roles are automatically discovered from existing contacts in the event.

---

### Marshal: Viewing Contacts

#### Get Contacts for Marshal
```http
GET /api/events/{eventId}/marshals/{marshalId}/contacts
```

**Response** (200 OK):
```json
[
  {
    "contactId": "contact-abc123",
    "role": "EmergencyContact",
    "name": "John Smith",
    "phone": "555-0100",
    "email": "john@example.com",
    "notes": "Available 24/7",
    "isPrimary": true,
    "matchedScope": "EveryoneInAreas"
  }
]
```

**Sorting**:
1. Primary contacts first
2. By role
3. By display order

---

#### Get My Contacts (Current User)
```http
GET /api/events/{eventId}/my-contacts
Authorization: Bearer {sessionToken}
```

Same response format as GetContactsForMarshal.

**Notes**:
- Uses the authenticated user's marshal ID from the session
- If user is admin without marshal assignment, returns all contacts
- Returns 400 if no marshal assignment and not admin

---

## Business Rules

### Who Can Manage Contacts

| Role | Create | Edit | Delete | View (Admin Endpoint) |
|------|--------|------|--------|----------------------|
| Event Admin | Y | Y | Y | Y |
| Area Lead | N | N | N | N |
| Regular Marshal | N | N | N | N |

Only event admins can manage contacts (unlike notes where area leads can also create).

### Scope Restrictions

Contacts **cannot** use:
- OnePerCheckpoint
- OnePerArea
- OneLeadPerArea

These scopes are for completion tracking which contacts don't have.

### Visibility Rules

A marshal sees a contact if ANY of the scope configurations match:
1. **EveryoneInAreas**: Marshal is assigned to a checkpoint in one of the specified areas
2. **EveryoneAtCheckpoints**: Marshal is assigned to one of the specified checkpoints
3. **SpecificPeople**: Marshal's ID is in the list (or ALL_MARSHALS is used)
4. **EveryAreaLead**: Marshal is an area lead for one of the specified areas

### Primary Contact

Each role can have one primary contact. Primary contacts appear first in listings.

---

## Common Workflows

### Workflow 1: Admin Sets Up Emergency Contacts
```
1. Admin navigates to Event Settings > Contacts
2. Admin adds emergency contact:
   - Role: EmergencyContact
   - Name: "Event Safety Line"
   - Phone: "555-SAFE"
   - Scope: Everyone (ALL_AREAS)
   - Primary: Yes
3. Admin adds medical contact:
   - Role: MedicalLead
   - Name: "Dr. Jane Smith"
   - Phone: "555-MED1"
   - Scope: Everyone
4. All marshals can now see these contacts
```

### Workflow 2: Admin Creates Area-Specific Contact
```
1. Admin creates contact:
   - Role: SafetyOfficer
   - Name: "North Area Safety"
   - Phone: "555-NORTH"
   - Scope: EveryoneInAreas with ["area-north"]
2. Only marshals in North area see this contact
3. Marshals in other areas don't see it
```

### Workflow 3: Admin Links Marshal as Contact
```
1. Marshal "Sarah Jones" is already in the system
2. Admin creates contact:
   - Role: EventDirector
   - Name: "Sarah Jones"
   - Phone: "555-1234"
   - MarshalId: "marshal-sarah-123"
   - Primary: Yes
3. Contact is now linked to Sarah's marshal record
4. If Sarah's marshal record updates, admin can sync the contact
```

### Workflow 4: Marshal Views Contacts
```
1. Marshal opens their event dashboard
2. Calls GET /api/events/{eventId}/my-contacts
3. Receives list of contacts relevant to their assignments
4. Contacts are sorted: primary first, then by role
5. Marshal can tap phone numbers to call directly
```

---

## Frontend Implementation Guide

### TypeScript Interfaces

```typescript
interface EventContactForMarshalResponse {
  contactId: string;
  role: string;
  name: string;
  phone: string;
  email?: string;
  notes?: string;
  isPrimary: boolean;
  matchedScope: string;
}

interface ContactRolesResponse {
  builtInRoles: string[];
  customRoles: string[];
}
```

### Example React Component

```tsx
function MarshalContacts({ eventId }: { eventId: string }) {
  const [contacts, setContacts] = useState<EventContactForMarshalResponse[]>([]);

  useEffect(() => {
    fetch(`/api/events/${eventId}/my-contacts`, {
      headers: { 'Authorization': `Bearer ${sessionToken}` }
    })
      .then(res => res.json())
      .then(setContacts);
  }, [eventId]);

  const getRoleBadgeColor = (role: string) => {
    switch (role) {
      case 'EmergencyContact': return 'red';
      case 'MedicalLead': return 'green';
      case 'EventDirector': return 'blue';
      case 'SafetyOfficer': return 'orange';
      default: return 'gray';
    }
  };

  const groupedContacts = contacts.reduce((acc, contact) => {
    if (!acc[contact.role]) acc[contact.role] = [];
    acc[contact.role].push(contact);
    return acc;
  }, {} as Record<string, EventContactForMarshalResponse[]>);

  return (
    <div className="contacts-list">
      <h2>Event Contacts</h2>
      {Object.entries(groupedContacts).map(([role, roleContacts]) => (
        <div key={role} className="contact-group">
          <h3 style={{ color: getRoleBadgeColor(role) }}>{role}</h3>
          {roleContacts.map(contact => (
            <div
              key={contact.contactId}
              className={`contact-card ${contact.isPrimary ? 'primary' : ''}`}
            >
              {contact.isPrimary && <span className="primary-badge">Primary</span>}
              <div className="contact-name">{contact.name}</div>
              <a href={`tel:${contact.phone}`} className="contact-phone">
                {contact.phone}
              </a>
              {contact.email && (
                <a href={`mailto:${contact.email}`} className="contact-email">
                  {contact.email}
                </a>
              )}
              {contact.notes && (
                <div className="contact-notes">{contact.notes}</div>
              )}
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}
```

### Admin Contact Management

```tsx
function AdminContactEditor({ eventId, contactId }: { eventId: string; contactId?: string }) {
  const [roles, setRoles] = useState<ContactRolesResponse | null>(null);
  const [form, setForm] = useState({
    role: '',
    name: '',
    phone: '',
    email: '',
    notes: '',
    marshalId: '',
    scopeConfigurations: [],
    displayOrder: 0,
    isPrimary: false
  });

  useEffect(() => {
    // Load available roles
    fetch(`/api/events/${eventId}/contact-roles`, {
      headers: { 'Authorization': `Bearer ${sessionToken}` }
    })
      .then(res => res.json())
      .then(setRoles);
  }, [eventId]);

  const handleSubmit = async () => {
    const url = contactId
      ? `/api/events/${eventId}/contacts/${contactId}`
      : `/api/events/${eventId}/contacts`;

    const response = await fetch(url, {
      method: contactId ? 'PUT' : 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${sessionToken}`
      },
      body: JSON.stringify(form)
    });

    if (!response.ok) {
      const error = await response.json();
      alert(error.message);
      return;
    }

    // Success - redirect or refresh
  };

  const allRoles = roles
    ? [...roles.builtInRoles, ...roles.customRoles]
    : [];

  return (
    <form onSubmit={handleSubmit}>
      <select
        value={form.role}
        onChange={e => setForm({ ...form, role: e.target.value })}
        required
      >
        <option value="">Select Role...</option>
        {allRoles.map(role => (
          <option key={role} value={role}>{role}</option>
        ))}
        <option value="__custom__">+ Add Custom Role</option>
      </select>

      <input
        type="text"
        placeholder="Name"
        value={form.name}
        onChange={e => setForm({ ...form, name: e.target.value })}
        required
      />

      <input
        type="tel"
        placeholder="Phone"
        value={form.phone}
        onChange={e => setForm({ ...form, phone: e.target.value })}
        required
      />

      <input
        type="email"
        placeholder="Email (optional)"
        value={form.email}
        onChange={e => setForm({ ...form, email: e.target.value })}
      />

      <textarea
        placeholder="Notes (optional)"
        value={form.notes}
        onChange={e => setForm({ ...form, notes: e.target.value })}
      />

      <MarshalSelector
        eventId={eventId}
        value={form.marshalId}
        onChange={marshalId => setForm({ ...form, marshalId })}
        optional={true}
      />

      <ScopeSelector
        value={form.scopeConfigurations}
        onChange={configs => setForm({ ...form, scopeConfigurations: configs })}
        excludeOnePerScopes={true}
      />

      <label>
        <input
          type="checkbox"
          checked={form.isPrimary}
          onChange={e => setForm({ ...form, isPrimary: e.target.checked })}
        />
        Primary contact for this role
      </label>

      <button type="submit">
        {contactId ? 'Update Contact' : 'Create Contact'}
      </button>
    </form>
  );
}
```

---

## Error Handling

### HTTP Status Codes

| Code | Meaning | Common Causes |
|------|---------|--------------|
| 200 | Success | Request succeeded |
| 204 | No Content | Delete succeeded |
| 400 | Bad Request | Invalid scope, missing fields, invalid marshal ID |
| 401 | Unauthorized | No session, invalid token, not admin |
| 404 | Not Found | Contact doesn't exist |
| 500 | Server Error | Database error |

### Common Error Messages

| Error Message | Cause | Resolution |
|--------------|-------|------------|
| "Contacts cannot use 'One per' scopes" | Using OnePerCheckpoint, OnePerArea, or OneLeadPerArea | Use EveryoneInAreas, EveryoneAtCheckpoints, SpecificPeople, or EveryAreaLead |
| "Not authorized to manage contacts" | User is not event admin | Only event admins can manage contacts |
| "Contact not found" | Contact ID doesn't exist or was deleted | Refresh the contacts list |
| "Name and Role are required" | Empty name or role field | Provide both name and role |
| "Marshal not found" | Invalid marshalId provided | Verify the marshal exists in the event |

---

## Comparison with Notes

| Feature | Contacts | Notes |
|---------|----------|-------|
| Purpose | People to contact | Information to display |
| Linked to marshals | Optional | No |
| External entities | Yes | No |
| Role-based | Yes | No (categories instead) |
| Priority levels | No | Yes |
| Categories | No (use roles) | Yes |
| Pinning | No (use primary) | Yes |
| Markdown content | No | Yes |
| Who can create | Admins only | Admins + Area leads |
| Scope system | Same as notes | Same as contacts |

---

## Migration from EmergencyContactsJson

The Event Contacts system replaces the legacy `EmergencyContactsJson` field on the EventEntity. To migrate:

1. Read existing `EmergencyContactsJson` from the event
2. For each emergency contact, create an EventContact with:
   - Role: "EmergencyContact"
   - Scope: Everyone (ALL_AREAS)
   - IsPrimary: true for the first one
3. Clear the `EmergencyContactsJson` field (optional, for cleanup)

---

## Summary

The Event Contacts API provides a flexible contact management system:

- **Multiple contacts per event** with role-based organization
- **Flexible visibility** using the same scope system as notes/checklists
- **Marshal linking** for contacts who are also event volunteers
- **External contacts** for people outside the marshal system
- **Built-in + custom roles** for categorization
- **Primary designation** for key contacts

Use contacts for:
- Emergency contacts
- Event coordinators
- Medical personnel
- Safety officers
- Logistics support
- Area-specific contacts
