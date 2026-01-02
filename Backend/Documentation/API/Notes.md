# Notes API Documentation

## Table of Contents
- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [Available Scope Types](#available-scope-types)
- [Data Models](#data-models)
- [API Endpoints](#api-endpoints)
  - [Admin: Note Management](#admin-note-management)
  - [Marshal: Viewing Notes](#marshal-viewing-notes)
- [Business Rules](#business-rules)
- [Common Workflows](#common-workflows)
- [Frontend Implementation Guide](#frontend-implementation-guide)
- [Error Handling](#error-handling)

---

## Overview

The Notes system allows event administrators and area leads to create informational notes that are displayed to marshals based on scope configurations. Notes are **read-only** for marshals; only admins and area leads can create, edit, or delete them.

Unlike checklists, notes:
- Don't have completion tracking
- Don't support "One per Area/Checkpoint" scopes (since there's no completion)
- Are purely informational

### Key Features
- Scope-based visibility using the same system as checklists
- Priority levels (Urgent, High, Normal, Low)
- Pinned notes for important information
- Categories for organization
- Markdown support in content

---

## Core Concepts

### 1. Note
An informational message displayed to marshals. Contains:
- **Title**: Short, prominent heading
- **Content**: Body text (supports markdown)
- **Scope Configurations**: Array defining who sees the note
- **Priority**: Visual importance level (Urgent, High, Normal, Low)
- **Category**: Optional grouping
- **IsPinned**: Whether to display at top

### 2. Scope Configuration
Determines who can see the note. Uses the same scope system as checklists for consistency, but only supports personal scopes:

| Scope | Who Sees It |
|-------|------------|
| EveryoneInAreas | Marshals assigned to checkpoints in specified areas |
| EveryoneAtCheckpoints | Marshals assigned to specified checkpoints |
| SpecificPeople | Only the specified marshals |
| EveryAreaLead | Area leads for specified areas |

**Note**: "One per" scopes (OnePerArea, OnePerCheckpoint, OneLeadPerArea) are **not** supported for notes since there's no completion tracking.

### 3. Priority Levels

| Priority | Use Case |
|----------|----------|
| Urgent | Critical safety information, immediate action required |
| High | Important reminders, time-sensitive info |
| Normal | General information |
| Low | Background info, nice-to-know |

---

## Available Scope Types

### 1. EveryoneInAreas
- **Who sees it**: Marshals assigned to checkpoints in specified areas
- **Filter**: `itemType: "Area"`, `ids: [area IDs]`
- **Use case**: Area-specific information like "North area: Watch for cyclists on the shared path"

### 2. EveryoneAtCheckpoints
- **Who sees it**: Marshals assigned to specified checkpoints
- **Filter**: `itemType: "Checkpoint"`, `ids: [checkpoint IDs]`
- **Use case**: Checkpoint-specific info like "Checkpoint 5: Road closure expected at 2 PM"

### 3. SpecificPeople
- **Who sees it**: Only the specified marshals
- **Filter**: `itemType: "Marshal"`, `ids: [marshal IDs]` or `ids: ["ALL_MARSHALS"]`
- **Use case**: Personal messages or event-wide announcements

### 4. EveryAreaLead
- **Who sees it**: Area leads for specified areas
- **Filter**: `itemType: "Area"`, `ids: [area IDs]`
- **Use case**: Area lead-specific instructions like "Area leads: Ensure all checkpoints have first aid kits"

---

## Data Models

### ScopeConfiguration
```typescript
interface ScopeConfiguration {
  scope: string;         // "EveryoneInAreas", "EveryoneAtCheckpoints", "SpecificPeople", "EveryAreaLead"
  itemType: string | null;  // "Marshal", "Checkpoint", "Area"
  ids: string[];         // IDs to match (can include sentinel values like "ALL_MARSHALS", "ALL_AREAS", "ALL_CHECKPOINTS")
}
```

### CreateNoteRequest
```typescript
interface CreateNoteRequest {
  title: string;                          // Required, max 200 characters
  content: string;                        // Optional, supports markdown
  scopeConfigurations: ScopeConfiguration[];  // At least one required
  displayOrder?: number;                  // Default: 0
  priority?: string;                      // Default: "Normal"
  category?: string;                      // Optional grouping
  isPinned?: boolean;                     // Default: false
}
```

### UpdateNoteRequest
```typescript
interface UpdateNoteRequest {
  title: string;
  content: string;
  scopeConfigurations: ScopeConfiguration[];
  displayOrder: number;
  priority: string;
  category?: string;
  isPinned: boolean;
}
```

### NoteResponse (Admin View)
```typescript
interface NoteResponse {
  noteId: string;
  eventId: string;
  title: string;
  content: string;
  scopeConfigurations: ScopeConfiguration[];
  displayOrder: number;
  priority: string;
  category?: string;
  isPinned: boolean;
  createdByPersonId: string;
  createdByName: string;
  createdAt: string;             // ISO 8601
  updatedByPersonId?: string;
  updatedByName?: string;
  updatedAt?: string;            // ISO 8601
}
```

### NoteForMarshalResponse (Marshal View)
```typescript
interface NoteForMarshalResponse {
  noteId: string;
  eventId: string;
  title: string;
  content: string;
  priority: string;
  category?: string;
  isPinned: boolean;
  createdAt: string;
  createdByName: string;
  matchedScope: string;          // Which scope configuration matched
}
```

---

## API Endpoints

### Admin: Note Management

#### Create Note
```http
POST /api/events/{eventId}/notes
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**:
```json
{
  "title": "Road Closure Notice",
  "content": "Main Street will be closed between 10 AM and 2 PM. Please inform all visitors.",
  "scopeConfigurations": [
    {
      "scope": "EveryoneInAreas",
      "itemType": "Area",
      "ids": ["area-north", "area-east"]
    }
  ],
  "displayOrder": 1,
  "priority": "High",
  "category": "Traffic",
  "isPinned": true
}
```

**Response** (200 OK):
```json
{
  "noteId": "note-abc123",
  "eventId": "event-456",
  "title": "Road Closure Notice",
  "content": "Main Street will be closed between 10 AM and 2 PM. Please inform all visitors.",
  "scopeConfigurations": [
    {
      "scope": "EveryoneInAreas",
      "itemType": "Area",
      "ids": ["area-north", "area-east"]
    }
  ],
  "displayOrder": 1,
  "priority": "High",
  "category": "Traffic",
  "isPinned": true,
  "createdByPersonId": "person-123",
  "createdByName": "Admin User",
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedByPersonId": null,
  "updatedByName": null,
  "updatedAt": null
}
```

**Error Responses**:

**400 Bad Request** (Invalid scope):
```json
{
  "message": "Notes cannot use 'One per' scopes as they don't have completion tracking"
}
```

**401 Unauthorized** (Not authorized):
```json
{
  "message": "Not authorized to manage notes"
}
```

---

#### List All Notes (Admin)
```http
GET /api/events/{eventId}/notes
Authorization: Bearer {sessionToken}
```

**Response** (200 OK):
```json
[
  {
    "noteId": "note-abc123",
    "eventId": "event-456",
    "title": "Road Closure Notice",
    "content": "...",
    "scopeConfigurations": [...],
    "displayOrder": 1,
    "priority": "High",
    "category": "Traffic",
    "isPinned": true,
    "createdByPersonId": "person-123",
    "createdByName": "Admin User",
    "createdAt": "2025-01-15T10:30:00Z",
    "updatedByPersonId": null,
    "updatedByName": null,
    "updatedAt": null
  }
]
```

**Notes**:
- Returns ALL notes for the event (not filtered by scope)
- Sorted by: pinned first, then display order, then creation date (newest first)
- Only event admins can access this endpoint

---

#### Get Single Note
```http
GET /api/events/{eventId}/notes/{noteId}
Authorization: Bearer {sessionToken}
```

**Response** (200 OK): Same as NoteResponse

**Response** (404 Not Found):
```json
{
  "message": "Note not found"
}
```

---

#### Update Note
```http
PUT /api/events/{eventId}/notes/{noteId}
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**: Same as CreateNoteRequest

**Response** (200 OK): Updated NoteResponse

---

#### Delete Note
```http
DELETE /api/events/{eventId}/notes/{noteId}
Authorization: Bearer {sessionToken}
```

**Response** (204 No Content)

**Note**: This is a soft delete. The note is marked as deleted but not permanently removed.

---

### Marshal: Viewing Notes

#### Get Notes for Marshal
```http
GET /api/events/{eventId}/marshals/{marshalId}/notes
```

**Response** (200 OK):
```json
[
  {
    "noteId": "note-abc123",
    "eventId": "event-456",
    "title": "Road Closure Notice",
    "content": "Main Street will be closed between 10 AM and 2 PM. Please inform all visitors.",
    "priority": "High",
    "category": "Traffic",
    "isPinned": true,
    "createdAt": "2025-01-15T10:30:00Z",
    "createdByName": "Admin User",
    "matchedScope": "EveryoneInAreas"
  }
]
```

**Sorting**:
1. Pinned notes first
2. By priority (Urgent > High > Normal > Low)
3. By display order
4. By creation date (newest first)

---

#### Get My Notes (Current User)
```http
GET /api/events/{eventId}/my-notes
Authorization: Bearer {sessionToken}
```

Same response format as GetNotesForMarshal.

**Notes**:
- Uses the authenticated user's marshal ID from the session
- If user is admin without marshal assignment, returns all notes
- Returns 400 if no marshal assignment and not admin

---

## Business Rules

### Who Can Manage Notes

| Role | Create | Edit | Delete | View (Admin Endpoint) |
|------|--------|------|--------|----------------------|
| Event Admin | ✓ | ✓ | ✓ | ✓ |
| Area Lead | ✓ | ✓ | ✗ | ✗ |
| Regular Marshal | ✗ | ✗ | ✗ | ✗ |

### Scope Restrictions

Notes **cannot** use:
- OnePerCheckpoint
- OnePerArea
- OneLeadPerArea

These scopes are for completion tracking which notes don't support.

### Visibility Rules

A marshal sees a note if ANY of the scope configurations match:
1. **EveryoneInAreas**: Marshal is assigned to a checkpoint in one of the specified areas
2. **EveryoneAtCheckpoints**: Marshal is assigned to one of the specified checkpoints
3. **SpecificPeople**: Marshal's ID is in the list (or ALL_MARSHALS is used)
4. **EveryAreaLead**: Marshal is an area lead for one of the specified areas

---

## Inline Creation with Marshals and Locations

When creating a new marshal or location, you can optionally include pending notes that will be automatically created and scoped to that specific entity. This is a convenience feature that allows admins to set up entity-specific information during entity creation.

### Creating Notes with a Marshal

When creating a new marshal, include the `pendingNewNotes` field:

```http
POST /api/events/{eventId}/marshals
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**:
```json
{
  "eventId": "event-123",
  "name": "John Smith",
  "email": "john@example.com",
  "phoneNumber": "555-1234",
  "notes": "Experienced marshal",
  "pendingNewNotes": [
    {
      "title": "Your Equipment",
      "content": "You have been assigned radio #42. Please return it at the end of the event."
    },
    {
      "title": "Special Assignment",
      "content": "You will be relieving checkpoint 3 marshals during their break at 2 PM."
    }
  ]
}
```

**What happens**:
1. Marshal "John Smith" is created
2. Two notes are created with `SpecificPeople` scope targeting this marshal's ID
3. Only John Smith will see these notes in their notes list
4. Notes are created with default settings (Normal priority, not pinned)

**Created notes**:
```json
[
  {
    "title": "Your Equipment",
    "content": "You have been assigned radio #42. Please return it at the end of the event.",
    "scopeConfigurations": [
      {
        "scope": "SpecificPeople",
        "itemType": "Marshal",
        "ids": ["<new-marshal-id>"]
      }
    ],
    "priority": "Normal",
    "isPinned": false
  }
]
```

### Creating Notes with a Location

When creating a new location/checkpoint, include the `pendingNewNotes` field:

```http
POST /api/events/{eventId}/locations
Content-Type: application/json
Authorization: Bearer {sessionToken}
```

**Request Body**:
```json
{
  "eventId": "event-123",
  "name": "Checkpoint 5",
  "description": "Main intersection",
  "latitude": 51.5074,
  "longitude": -0.1278,
  "requiredMarshals": 2,
  "pendingNewNotes": [
    {
      "title": "Local Hazards",
      "content": "Watch for loose gravel near the bend. Warn cyclists to slow down."
    },
    {
      "title": "Nearby Facilities",
      "content": "Public toilets are located 100m south. Water fountain at the park entrance."
    }
  ]
}
```

**What happens**:
1. Location "Checkpoint 5" is created
2. Two notes are created with `EveryoneAtCheckpoints` scope targeting this location's ID
3. All marshals assigned to Checkpoint 5 will see these notes
4. Notes are sorted by their position in the array (displayOrder)

**Created notes**:
```json
[
  {
    "title": "Local Hazards",
    "content": "Watch for loose gravel near the bend. Warn cyclists to slow down.",
    "scopeConfigurations": [
      {
        "scope": "EveryoneAtCheckpoints",
        "itemType": "Checkpoint",
        "ids": ["<new-location-id>"]
      }
    ],
    "priority": "Normal",
    "isPinned": false
  }
]
```

### Use Cases

**Marshal-specific notes**:
- Equipment assignments for this person
- Special responsibilities or schedule changes
- Personal reminders or instructions

**Checkpoint-specific notes**:
- Location-specific hazards or conditions
- Nearby facilities information
- Special procedures for this checkpoint

### Notes

- Pending notes with empty title or content are ignored
- Content is sanitized for security (HTML/scripts removed)
- Notes are created with `displayOrder` based on their position in the array
- To create notes with custom priority, pinning, or categories, use the standard Create Note endpoint after entity creation

---

## Common Workflows

### Workflow 1: Admin Creates Event-Wide Announcement
```
1. Admin navigates to Notes management
2. Admin clicks "Add Note"
3. Admin fills form:
   - Title: "Welcome to the Event!"
   - Content: "Thank you for volunteering today. Please check in at your assigned checkpoint."
   - Scope: SpecificPeople with ALL_MARSHALS
   - Priority: Normal
   - Pinned: Yes
4. All marshals see this note at the top of their notes list
```

### Workflow 2: Admin Creates Area-Specific Note
```
1. Admin creates note:
   - Title: "North Area: Road Work Today"
   - Content: "Expect heavy traffic near checkpoint 3. Direct visitors via alternative route."
   - Scope: EveryoneInAreas with ["area-north"]
   - Priority: High
   - Category: "Traffic"
2. Only marshals in North area see this note
3. Marshals in other areas don't see it
```

### Workflow 3: Area Lead Creates Checkpoint Note
```
1. Area lead (Sarah) for North area logs in
2. Sarah creates note:
   - Title: "Checkpoint 5: Water Station Location"
   - Content: "The water station is located 50m east of the checkpoint. Look for the blue tent."
   - Scope: EveryoneAtCheckpoints with ["checkpoint-5"]
   - Priority: Normal
3. Only marshals at checkpoint 5 see this note
4. Sarah can edit this note later if needed
```

### Workflow 4: Marshal Views Notes
```
1. Marshal opens their event dashboard
2. Calls GET /api/events/{eventId}/my-notes
3. Receives list of notes relevant to their assignments
4. Notes are sorted: pinned first, then by priority
5. Marshal can expand notes to read full content
```

---

## Frontend Implementation Guide

### TypeScript Interfaces

```typescript
interface ScopeConfiguration {
  scope: string;
  itemType: string | null;
  ids: string[];
}

interface NoteForMarshalResponse {
  noteId: string;
  eventId: string;
  title: string;
  content: string;
  priority: string;
  category?: string;
  isPinned: boolean;
  createdAt: string;
  createdByName: string;
  matchedScope: string;
}
```

### Example React Component

```tsx
function MarshalNotes({ eventId }: { eventId: string }) {
  const [notes, setNotes] = useState<NoteForMarshalResponse[]>([]);
  const [expandedNoteId, setExpandedNoteId] = useState<string | null>(null);

  useEffect(() => {
    fetch(`/api/events/${eventId}/my-notes`, {
      headers: { 'Authorization': `Bearer ${sessionToken}` }
    })
      .then(res => res.json())
      .then(setNotes);
  }, [eventId]);

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'Urgent': return 'red';
      case 'High': return 'orange';
      case 'Normal': return 'blue';
      case 'Low': return 'gray';
      default: return 'blue';
    }
  };

  return (
    <div className="notes-list">
      <h2>Notes ({notes.length})</h2>
      {notes.map(note => (
        <div
          key={note.noteId}
          className={`note-card ${note.isPinned ? 'pinned' : ''}`}
        >
          <div className="note-header">
            {note.isPinned && <span className="pin-icon">📌</span>}
            <span
              className="priority-badge"
              style={{ backgroundColor: getPriorityColor(note.priority) }}
            >
              {note.priority}
            </span>
            {note.category && (
              <span className="category-badge">{note.category}</span>
            )}
          </div>
          <h3>{note.title}</h3>
          {expandedNoteId === note.noteId ? (
            <div className="note-content">
              <ReactMarkdown>{note.content}</ReactMarkdown>
            </div>
          ) : (
            <button onClick={() => setExpandedNoteId(note.noteId)}>
              Read more
            </button>
          )}
          <div className="note-footer">
            <span>By {note.createdByName}</span>
            <span>{formatDate(note.createdAt)}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
```

### Admin Note Management

```tsx
function AdminNoteEditor({ eventId, noteId }: { eventId: string; noteId?: string }) {
  const [form, setForm] = useState({
    title: '',
    content: '',
    scopeConfigurations: [],
    priority: 'Normal',
    category: '',
    isPinned: false,
    displayOrder: 0
  });

  const handleSubmit = async () => {
    const url = noteId
      ? `/api/events/${eventId}/notes/${noteId}`
      : `/api/events/${eventId}/notes`;

    const response = await fetch(url, {
      method: noteId ? 'PUT' : 'POST',
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

  return (
    <form onSubmit={handleSubmit}>
      <input
        type="text"
        placeholder="Title"
        value={form.title}
        onChange={e => setForm({ ...form, title: e.target.value })}
        required
      />
      <textarea
        placeholder="Content (Markdown supported)"
        value={form.content}
        onChange={e => setForm({ ...form, content: e.target.value })}
      />
      <select
        value={form.priority}
        onChange={e => setForm({ ...form, priority: e.target.value })}
      >
        <option value="Urgent">Urgent</option>
        <option value="High">High</option>
        <option value="Normal">Normal</option>
        <option value="Low">Low</option>
      </select>
      <ScopeSelector
        value={form.scopeConfigurations}
        onChange={configs => setForm({ ...form, scopeConfigurations: configs })}
        excludeOnePerScopes={true}  // Notes don't support these
      />
      <label>
        <input
          type="checkbox"
          checked={form.isPinned}
          onChange={e => setForm({ ...form, isPinned: e.target.checked })}
        />
        Pin to top
      </label>
      <button type="submit">
        {noteId ? 'Update Note' : 'Create Note'}
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
| 400 | Bad Request | Invalid scope, missing title |
| 401 | Unauthorized | No session, invalid token |
| 404 | Not Found | Note doesn't exist |
| 500 | Server Error | Database error |

### Common Error Messages

| Error Message | Cause | Resolution |
|--------------|-------|------------|
| "Notes cannot use 'One per' scopes as they don't have completion tracking" | Using OnePerCheckpoint, OnePerArea, or OneLeadPerArea | Use EveryoneInAreas, EveryoneAtCheckpoints, SpecificPeople, or EveryAreaLead instead |
| "Not authorized to manage notes" | User is not admin or area lead | Only admins and area leads can create/edit notes |
| "Note not found" | Note ID doesn't exist or was deleted | Refresh the notes list |
| "Title is required" | Empty title field | Provide a title |

---

## Comparison with Checklists

| Feature | Checklists | Notes |
|---------|------------|-------|
| Purpose | Tasks to complete | Information to display |
| Completion tracking | Yes | No |
| "One per" scopes | Supported | Not supported |
| Personal scopes | Supported | Supported |
| Priority levels | No | Yes |
| Categories | No | Yes |
| Pinning | No | Yes |
| Markdown content | No | Yes |
| Who can create | Admins only | Admins + Area leads |

---

## Summary

The Notes API provides a simple, scope-based information distribution system:

- **Flexible visibility**: Same scope system as checklists
- **Priority-based sorting**: Important notes appear first
- **Pinning**: Critical information stays at top
- **Categories**: Optional organization
- **Markdown**: Rich content formatting

Key differences from checklists:
- No completion tracking
- Only personal scopes (no "One per" scopes)
- Area leads can create notes
- Priority and pinning features

Use notes for:
- Event announcements
- Area-specific information
- Checkpoint instructions
- Safety notices
- General reminders
