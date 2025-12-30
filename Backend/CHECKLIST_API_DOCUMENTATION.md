# Checklist API Documentation

## Table of Contents
- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [Data Models](#data-models)
- [Checklist Scopes Explained](#checklist-scopes-explained)
- [Advanced Scope Configuration Examples](#advanced-scope-configuration-examples)
- [API Endpoints](#api-endpoints)
  - [Admin: Checklist Management](#admin-checklist-management)
  - [Marshal: Checklist Usage](#marshal-checklist-usage)
  - [Admin: Area Lead Management](#admin-area-lead-management)
  - [Admin: Reporting](#admin-reporting)
- [Business Rules](#business-rules)
- [Common Workflows](#common-workflows)
- [Frontend Implementation Guide](#frontend-implementation-guide)
- [Error Handling](#error-handling)

---

## Overview

The Checklist system allows event administrators to define checklist items that marshals must complete. Items can be:
- **Personal** - Each marshal completes individually (e.g., "Collected hi-viz vest")
- **Shared** - One person completes per checkpoint/area (e.g., "Spoken to traffic management")
- **Role-based** - Only area leads can complete (e.g., "Collected all equipment for my area")

### Key Features
- ✅ 7 different scope types for flexible targeting
- ✅ Area lead system for hierarchical permissions
- ✅ Time-based visibility (show/hide by date)
- ✅ Completion tracking with timestamps and audit trail
- ✅ Uncomplete/correction functionality
- ✅ Comprehensive reporting

---

## Core Concepts

### 1. Checklist Item
A template/definition of something that needs to be completed. Contains:
- **Text**: What needs to be done (e.g., "Collected radio")
- **Scope Configurations**: An array of flexible scope configs defining who sees/completes it
- **Visibility**: When it should appear/disappear
- **Display Order**: Sorting in the UI

**Important**: One checklist item definition can apply to multiple marshals. It's NOT duplicated per marshal.

### 2. Checklist Completion
A record that someone completed a checklist item. Contains:
- **Who** completed it (marshal ID and name)
- **When** it was completed (timestamp)
- **Context**: What context (personal, checkpoint, or area)
- **Matched Scope**: Which scope configuration matched for this completion

**Important**: Completions are only created when someone checks something off. No pre-population.

### 3. Scope Configuration
Determines who can see and complete a checklist item. Each item can have MULTIPLE configurations in an array, allowing complex targeting scenarios. See [Flexible Scope System & Most Specific Wins](#flexible-scope-system--most-specific-wins) and [Checklist Scopes Explained](#checklist-scopes-explained) for details.

### 4. Flexible Scope System & Most Specific Wins

Each checklist item can have MULTIPLE scope configurations in an array. This allows complex targeting like:
- "Everyone in area-north + checkpoint-42 + John Doe personally"

When a marshal matches multiple configurations, the **Most Specific Wins** rule determines the completion context:

**Specificity Hierarchy** (most to least specific):
1. **Marshal ID** (specificity = 1) - Explicitly listed marshal
2. **Checkpoint ID** (specificity = 2) - Assigned to specific checkpoint
3. **Area ID** (specificity = 3) - Assigned to checkpoint in specific area
4. **Everyone** (specificity = 4) - No filter

**Example**: Item has configs:
```json
[
  {
    "scope": "EveryoneInAreas",
    "itemType": "Area",
    "ids": ["area-1"]
  },
  {
    "scope": "SpecificPeople",
    "itemType": "Marshal",
    "ids": ["john-doe"]
  }
]
```
- Regular marshal in area-1: Matches area config (specificity 3), Personal context
- John Doe in area-1: Matches BOTH, but Marshal config wins (specificity 1), Personal context

### 5. Completion Context
For shared items (OnePerCheckpoint, OnePerArea), the context identifies which checkpoint/area was completed:
- **Personal**: Context = Marshal ID (for Everyone, EveryoneInAreas, etc.)
- **Checkpoint**: Context = Location ID (for OnePerCheckpoint)
- **Area**: Context = Area ID (for OnePerArea, AreaLead)

### 6. Area Lead
A marshal designated as a "lead" for a specific area. Area leads have special permissions:
- Can complete checklist items for checkpoints in their area
- Can complete area-scoped items
- Can see and manage area-specific tasks

**Important**: Multiple marshals can be area leads for the same area.

---

## Data Models

### ScopeConfiguration
```typescript
interface ScopeConfiguration {
  scope: string;         // "Everyone", "OnePerCheckpoint", etc.
  itemType: string | null;  // "Marshal", "Checkpoint", "Area", or null for Everyone
  ids: string[];         // IDs to match (marshal IDs, checkpoint IDs, or area IDs)
}
```

### ChecklistItemResponse
```typescript
interface ChecklistItemResponse {
  itemId: string;              // Unique ID
  eventId: string;             // Event this belongs to
  text: string;                // "Collected hi-viz vest"
  scopeConfigurations: ScopeConfiguration[];  // Array of scope configs for flexibility
  displayOrder: number;        // For sorting in UI
  isRequired: boolean;         // Is this mandatory?
  visibleFrom?: string | null; // ISO 8601 date - when to show
  visibleUntil?: string | null;// ISO 8601 date - when to hide
  mustCompleteBy?: string | null; // ISO 8601 date - deadline
  createdByAdminEmail: string; // Who created it
  createdDate: string;         // ISO 8601 date
  lastModifiedDate?: string | null;
  lastModifiedByAdminEmail: string;
}
```

### ChecklistItemWithStatus
```typescript
interface ChecklistItemWithStatus {
  // All fields from ChecklistItemResponse, plus:
  isCompleted: boolean;        // Has this been completed in this context?
  canBeCompletedByMe: boolean; // Can I complete this?
  completedByName?: string | null;  // Who completed it (if completed)
  completedAt?: string | null;      // When completed (ISO 8601)
  completionContextType: string;    // "Personal", "Checkpoint", or "Area"
  completionContextId: string;      // ID of the context
  matchedScope: string;         // Which scope configuration matched for this marshal
}
```

### ChecklistCompletionResponse
```typescript
interface ChecklistCompletionResponse {
  completionId: string;
  eventId: string;
  checklistItemId: string;
  completedByMarshalId: string;
  completedByMarshalName: string;
  completionContextType: string;  // "Personal", "Checkpoint", or "Area"
  completionContextId: string;    // Marshal ID, Location ID, or Area ID
  completedAt: string;            // ISO 8601 date
  isDeleted: boolean;             // Has this been uncompleted?
}
```

### AreaLeadResponse
```typescript
interface AreaLeadResponse {
  marshalId: string;
  marshalName: string;
  email: string;
}
```

---

## Checklist Scopes Explained

### 1. Everyone
**Who sees it**: Every marshal in the event
**Who can complete**: The marshal themselves
**Context**: Personal (per marshal)
**Use case**: "Collected hi-viz vest", "Attended safety briefing"

```json
{
  "scopeConfigurations": [
    {
      "scope": "Everyone",
      "itemType": null,
      "ids": []
    }
  ]
}
```

**Example**:
- 50 marshals in event
- All 50 see this item
- Each completes it individually
- 50 separate completions possible

---

### 2. EveryoneInAreas
**Who sees it**: Marshals assigned to checkpoints in specified areas
**Who can complete**: The marshal themselves
**Context**: Personal (per marshal)
**Use case**: "Checked area map", "Reviewed area-specific instructions"

```json
{
  "scopeConfigurations": [
    {
      "scope": "EveryoneInAreas",
      "itemType": "Area",
      "ids": ["area-north", "area-east"]
    }
  ]
}
```

**Example**:
- 20 marshals assigned to North area
- 15 marshals assigned to East area
- 35 marshals see this item
- Each completes it individually

**Important**: A marshal is "in an area" if ANY of their assigned checkpoints are in that area.

---

### 3. EveryoneAtCheckpoints
**Who sees it**: Marshals assigned to specified checkpoints
**Who can complete**: The marshal themselves
**Context**: Personal (per marshal)
**Use case**: "Read checkpoint-specific instructions", "Collected checkpoint sign"

```json
{
  "scopeConfigurations": [
    {
      "scope": "EveryoneAtCheckpoints",
      "itemType": "Checkpoint",
      "ids": ["checkpoint-5", "checkpoint-7"]
    }
  ]
}
```

**Example**:
- Checkpoint 5 has 3 marshals
- Checkpoint 7 has 2 marshals
- 5 marshals see this item
- Each completes it individually

**Important**: If a marshal is assigned to multiple checkpoints in the list, they still only see the item ONCE and complete it ONCE.

---

### 4. SpecificPeople
**Who sees it**: Only the specified marshals
**Who can complete**: Those specific marshals
**Context**: Personal (per marshal)
**Use case**: "Submit incident report (John)", "Call coordinator (Sarah)"

```json
{
  "scopeConfigurations": [
    {
      "scope": "SpecificPeople",
      "itemType": "Marshal",
      "ids": ["marshal-123", "marshal-456"]
    }
  ]
}
```

**Example**:
- Only marshals 123 and 456 see this item
- Each completes it individually
- Max 2 completions possible

---

### 5. OnePerCheckpoint
**Who sees it**:
- Marshals assigned to specified checkpoints
- Area leads for areas containing those checkpoints

**Who can complete**:
- Any marshal at the checkpoint
- Any area lead for that checkpoint's area

**Context**: Checkpoint (per location)
**Use case**: "Spoken to traffic management", "Set up checkpoint signage", "Verified route markings"

```json
{
  "scopeConfigurations": [
    {
      "scope": "OnePerCheckpoint",
      "itemType": "Checkpoint",
      "ids": ["checkpoint-5", "checkpoint-7"]
    }
  ]
}
```

**Example**:
- Checkpoint 5 has 3 marshals assigned
- All 3 see this item
- Any ONE of them can complete it
- Once completed, shows as "✓ Completed by Sarah" to all 3
- Only 1 completion possible per checkpoint

**Multi-area checkpoint handling**:
```
Checkpoint 5 is in BOTH "North" and "East" areas
Item: "Spoken to traffic management" (OnePerCheckpoint: [checkpoint-5])
Context: "Checkpoint:checkpoint-5"

If marshal in North completes it → Marked complete for checkpoint-5
If marshal in East views their checklist → Also shows as complete
```

**Area lead permissions**:
```
Sarah is area lead for "North" area
Checkpoint 5 is in "North" area
Sarah is NOT assigned to checkpoint 5
Sarah STILL sees this item and can complete it (because she's area lead)
```

---

### 6. OnePerArea
**Who sees it**:
- Marshals assigned to checkpoints in specified areas
- Area leads for those areas

**Who can complete**:
- Any marshal in the area
- Any area lead for the area

**Context**: Area (per area)
**Use case**: "Submitted area incident log", "Verified all checkpoints in area"

```json
{
  "scopeConfigurations": [
    {
      "scope": "OnePerArea",
      "itemType": "Area",
      "ids": ["area-north", "area-east"]
    }
  ]
}
```

**Example**:
- North area has 20 marshals
- All 20 see this item
- Any ONE of them can complete it
- Once completed, shows as "✓ Completed by John" to all 20
- One completion for North, one for East (2 total)

**Important**: If the same marshal is in BOTH North and East areas, they see the item TWICE (once per area context) and can complete it twice (once per area).

---

### 7. AreaLead
**Who sees it**: Area leads for specified areas
**Who can complete**: Area leads only
**Context**: Area (per area)
**Use case**: "Submitted area lead report", "Collected all radios for area", "Approved area completion"

```json
{
  "scopeConfigurations": [
    {
      "scope": "AreaLead",
      "itemType": "Area",
      "ids": ["area-north"]
    }
  ]
}
```

**Example**:
- North area has 3 area leads (Sarah, John, Mike)
- All 3 see this item
- Any ONE of them can complete it
- Regular marshals in North area do NOT see it

---

## Advanced Scope Configuration Examples

### Scenario 1: Everyone in an Area + Specific Checkpoint Outside Area
```json
{
  "text": "Review safety procedures",
  "scopeConfigurations": [
    {
      "scope": "EveryoneInAreas",
      "itemType": "Area",
      "ids": ["area-north"]
    },
    {
      "scope": "EveryoneAtCheckpoints",
      "itemType": "Checkpoint",
      "ids": ["checkpoint-special"]
    }
  ]
}
```
**Who sees it**:
- All marshals in North area
- Marshals at checkpoint-special (even if not in North area)

**Completion context**:
- Personal (each marshal completes individually)
- Most specific match wins: If a marshal is both in North and at checkpoint-special, the Checkpoint match (specificity 2) wins

---

### Scenario 2: Mixed Personal and Shared Scopes
```json
{
  "text": "Equipment check",
  "scopeConfigurations": [
    {
      "scope": "OnePerCheckpoint",
      "itemType": "Checkpoint",
      "ids": ["checkpoint-5", "checkpoint-7"]
    },
    {
      "scope": "SpecificPeople",
      "itemType": "Marshal",
      "ids": ["supervisor-jane"]
    }
  ]
}
```
**Who sees it**:
- All marshals at checkpoints 5 and 7 (shared completion per checkpoint)
- Supervisor Jane (personal completion)

**Completion context**:
- Marshals at checkpoint-5: Most specific match = Checkpoint (specificity 2) → Context: Checkpoint:checkpoint-5
- Supervisor Jane at checkpoint-5: Most specific match = Marshal (specificity 1) → Context: Personal:jane
- Jane's completion is SEPARATE from the checkpoint completion

**Important**: Jane always sees this item as her personal task, even if she's at a checkpoint where others completed it. The "Most Specific Wins" rule ensures her explicit personal assignment takes precedence over any checkpoint assignment.

---

### Scenario 3: Area Lead + Checkpoint Staff
```json
{
  "text": "Final safety sign-off",
  "scopeConfigurations": [
    {
      "scope": "OnePerCheckpoint",
      "itemType": "Checkpoint",
      "ids": ["checkpoint-5"]
    },
    {
      "scope": "AreaLead",
      "itemType": "Area",
      "ids": ["area-north"]
    }
  ]
}
```
**Who sees it**:
- All marshals at checkpoint 5 (shared completion)
- All area leads for North area (separate completion)

**Completion context**:
- Regular marshal at checkpoint-5: Matches OnePerCheckpoint → Context: Checkpoint:checkpoint-5
- Area lead for North at checkpoint-5: Most specific match = Checkpoint (specificity 2) → Context: Checkpoint:checkpoint-5
- Area lead for North NOT at checkpoint-5: Matches AreaLead → Context: Area:area-north

---

### Scenario 4: Complex Multi-Target Item
```json
{
  "text": "Checkpoint specific tasks",
  "scopeConfigurations": [
    {
      "scope": "EveryoneInAreas",
      "itemType": "Area",
      "ids": ["area-north"]
    },
    {
      "scope": "EveryoneAtCheckpoints",
      "itemType": "Checkpoint",
      "ids": ["checkpoint-42"]
    },
    {
      "scope": "SpecificPeople",
      "itemType": "Marshal",
      "ids": ["john-doe"]
    }
  ]
}
```
**Who sees it**:
- All marshals in area-north
- Marshals at checkpoint-42 (may overlap with area-north)
- John Doe specifically

**Completion context by marshal**:
- Regular marshal in area-north: Matches area config (specificity 3) → Context: Personal
- Marshal at checkpoint-42 (not in area-north): Matches checkpoint config (specificity 2) → Context: Personal
- John Doe in area-north: All 3 match, Marshal config wins (specificity 1) → Context: Personal
- John Doe at checkpoint-42: All 3 match, Marshal config wins (specificity 1) → Context: Personal
- John Doe elsewhere: Matches Marshal config (specificity 1) → Context: Personal

**Key insight**: John Doe's item appears in all his checkpoints AND everywhere else because he's explicitly listed, and explicit assignment (Marshal ID) has highest specificity.

---

## API Endpoints

### Admin: Checklist Management

#### Create Checklist Item
```http
POST /api/events/{eventId}/checklist-items
Content-Type: application/json
X-Admin-Email: admin@example.com
```

**Request Body**:
```json
{
  "text": "Collected hi-viz vest",
  "scopeConfigurations": [
    {
      "scope": "Everyone",
      "itemType": null,
      "ids": []
    }
  ],
  "displayOrder": 1,
  "isRequired": true,
  "visibleFrom": null,
  "visibleUntil": null,
  "mustCompleteBy": null
}
```

**Response** (200 OK):
```json
{
  "itemId": "item-abc123",
  "eventId": "event-456",
  "text": "Collected hi-viz vest",
  "scopeConfigurations": [
    {
      "scope": "Everyone",
      "itemType": null,
      "ids": []
    }
  ],
  "displayOrder": 1,
  "isRequired": true,
  "visibleFrom": null,
  "visibleUntil": null,
  "mustCompleteBy": null,
  "createdByAdminEmail": "admin@example.com",
  "createdDate": "2025-01-15T10:30:00Z",
  "lastModifiedDate": null,
  "lastModifiedByAdminEmail": ""
}
```

**Validation Rules**:
- `text` is required and max 2000 characters
- `scopeConfigurations` must not be empty (at least one configuration)
- Each configuration must have valid `scope`, `itemType`, and `ids` fields
- `scope` must be one of: "Everyone", "EveryoneInAreas", "EveryoneAtCheckpoints", "SpecificPeople", "OnePerArea", "OnePerCheckpoint", "AreaLead"
- `itemType` must match the scope type:
  - "Everyone" → itemType: null
  - "EveryoneInAreas", "OnePerArea", "AreaLead" → itemType: "Area"
  - "EveryoneAtCheckpoints", "OnePerCheckpoint" → itemType: "Checkpoint"
  - "SpecificPeople" → itemType: "Marshal"
- If itemType is not null, `ids` array must not be empty
- HTML/script tags are stripped from `text` for security
- Multiple scope configurations allow flexible, complex targeting

---

#### List All Checklist Items
```http
GET /api/events/{eventId}/checklist-items
```

**Response** (200 OK):
```json
[
  {
    "itemId": "item-abc123",
    "eventId": "event-456",
    "text": "Collected hi-viz vest",
    "scopeConfigurations": [
      {
        "scope": "Everyone",
        "itemType": null,
        "ids": []
      }
    ]
    // ... full ChecklistItemResponse
  },
  {
    "itemId": "item-def456",
    "eventId": "event-456",
    "text": "Spoken to traffic management",
    "scopeConfigurations": [
      {
        "scope": "OnePerCheckpoint",
        "itemType": "Checkpoint",
        "ids": ["checkpoint-5"]
      }
    ]
    // ... full ChecklistItemResponse
  }
]
```

**Notes**:
- Returns ALL checklist items for the event (not filtered by scope)
- Sorted by `displayOrder`, then by `text`
- Use this for admin dashboard / management interface
- Each item includes its scopeConfigurations array for multi-config support

---

#### Get Single Checklist Item
```http
GET /api/checklist-items/{eventId}/{itemId}
```

**Response** (200 OK):
```json
{
  "itemId": "item-abc123",
  "eventId": "event-456",
  "text": "Collected hi-viz vest",
  "scopeConfigurations": [
    {
      "scope": "Everyone",
      "itemType": null,
      "ids": []
    }
  ]
  // ... full ChecklistItemResponse
}
```

**Response** (404 Not Found):
```json
{
  "message": "Checklist item not found"
}
```

---

#### Update Checklist Item
```http
PUT /api/checklist-items/{eventId}/{itemId}
Content-Type: application/json
X-Admin-Email: admin@example.com
```

**Request Body**: Same as create (all fields with scopeConfigurations array)

**Response** (200 OK):
```json
{
  "itemId": "item-abc123",
  "eventId": "event-456",
  "text": "Collected hi-viz vest and ID badge",
  "scopeConfigurations": [
    {
      "scope": "Everyone",
      "itemType": null,
      "ids": []
    }
  ]
  // ... updated fields
  "lastModifiedDate": "2025-01-15T11:45:00Z",
  "lastModifiedByAdminEmail": "admin@example.com"
}
```

**Important Notes**:
- Changing `scopeConfigurations` after completions exist can cause confusion
- Frontend should warn: "23 completions exist. Changing scopeConfigurations may affect their validity."
- Consider creating a new item instead of changing scope drastically
- Use the "Most Specific Wins" rule when adding new configurations to ensure expected behavior

---

#### Delete Checklist Item
```http
DELETE /api/checklist-items/{eventId}/{itemId}
```

**Response** (204 No Content)

**Important**: This also deletes ALL completions for this item. Frontend should confirm:
```
"Delete 'Collected hi-viz vest'? This will remove 47 completions and cannot be undone."
```

---

### Marshal: Checklist Usage

#### Get Marshal's Checklist
```http
GET /api/events/{eventId}/marshals/{marshalId}/checklist
```

**Response** (200 OK):
```json
[
  {
    "itemId": "item-abc123",
    "eventId": "event-456",
    "text": "Collected hi-viz vest",
    "scopeConfigurations": [
      {
        "scope": "Everyone",
        "itemType": null,
        "ids": []
      }
    ],
    "displayOrder": 1,
    "isRequired": true,
    "visibleFrom": null,
    "visibleUntil": null,
    "mustCompleteBy": null,
    "isCompleted": false,
    "canBeCompletedByMe": true,
    "completedByName": null,
    "completedAt": null,
    "completionContextType": "Personal",
    "completionContextId": "marshal-123",
    "matchedScope": "Everyone"
  },
  {
    "itemId": "item-def456",
    "eventId": "event-456",
    "text": "Spoken to traffic management",
    "scopeConfigurations": [
      {
        "scope": "OnePerCheckpoint",
        "itemType": "Checkpoint",
        "ids": ["checkpoint-5"]
      }
    ],
    "displayOrder": 2,
    "isRequired": true,
    "isCompleted": true,
    "canBeCompletedByMe": false,
    "completedByName": "Sarah Johnson",
    "completedAt": "2025-01-15T09:30:00Z",
    "completionContextType": "Checkpoint",
    "completionContextId": "checkpoint-5",
    "matchedScope": "OnePerCheckpoint"
  }
]
```

**Key Fields for Frontend**:
- `isCompleted`: Show checkmark ✓ or empty checkbox ☐
- `canBeCompletedByMe`: Enable/disable the checkbox
- `completedByName`: Show "Completed by Sarah Johnson" if shared item
- `completedAt`: Show timestamp "Completed at 9:30 AM"
- `isRequired`: Show "(Optional)" if false

**Filtering Logic**:
This endpoint automatically filters based on:
1. Marshal's assigned checkpoints
2. Areas those checkpoints are in
3. Whether marshal is an area lead
4. Time-based visibility (visibleFrom/visibleUntil)

Only items relevant to THIS marshal are returned.

**Example UI**:
```
Your Checklist (3/5 completed)

✓ Collected hi-viz vest
  Completed at 9:15 AM

✓ Spoken to traffic management (Checkpoint 5)
  Completed by Sarah Johnson at 9:30 AM

☐ Submitted incident log (Area North)
  Can be completed by any marshal in North area

☐ Collected radio (Required)

☐ Area lead report (Area North)
  Only area leads can complete
```

---

#### Complete Checklist Item
```http
POST /api/checklist-items/{eventId}/{itemId}/complete
Content-Type: application/json
```

**Request Body**:
```json
{
  "marshalId": "marshal-123",
  "contextType": null,
  "contextId": null
}
```

**Notes on contextType/contextId**:
- Usually leave as `null` - backend auto-determines the context
- Only override if you need to specify which checkpoint/area for multi-context scenarios
- Example: Marshal is in BOTH North and East areas, completing "OnePerArea" item for East specifically:
  ```json
  {
    "marshalId": "marshal-123",
    "contextType": "Area",
    "contextId": "area-east"
  }
  ```

**Response** (200 OK):
```json
{
  "completionId": "completion-xyz789",
  "eventId": "event-456",
  "checklistItemId": "item-abc123",
  "completedByMarshalId": "marshal-123",
  "completedByMarshalName": "John Smith",
  "completionContextType": "Personal",
  "completionContextId": "marshal-123",
  "completedAt": "2025-01-15T10:45:00Z",
  "isDeleted": false
}
```

**Error Responses**:

**403 Forbidden** (No permission):
```json
{
  "message": "You don't have permission to complete this item"
}
```
Reasons:
- Item is scoped to different area/checkpoint
- Item is for area leads only, but marshal is not an area lead
- Item is for specific people, but marshal is not in that list

**400 Bad Request** (Already completed):
```json
{
  "message": "This checklist item has already been completed"
}
```
Reason: For shared items (OnePerCheckpoint, OnePerArea), someone already completed it in this context.

**404 Not Found**:
```json
{
  "message": "Checklist item not found"
}
```
or
```json
{
  "message": "Marshal not found"
}
```

---

#### Uncomplete Checklist Item (Admin Only)
```http
POST /api/checklist-items/{eventId}/{itemId}/uncomplete
Content-Type: application/json
X-Admin-Email: admin@example.com
```

**Request Body**:
```json
{
  "marshalId": "marshal-123",
  "contextType": null,
  "contextId": null
}
```

**Use case**: Marshal accidentally checked off an item, admin needs to uncomplete it.

**Response** (204 No Content)

**Important**: This is a SOFT DELETE. The completion record is marked as `isDeleted: true` with audit trail, but not physically deleted from the database.

**Who can uncomplete**:
- Only admins (requires X-Admin-Email header)
- Cannot be undone by marshals themselves

---

### Admin: Area Lead Management

#### Add Area Lead
```http
POST /api/areas/{eventId}/{areaId}/leads
Content-Type: application/json
```

**Request Body**:
```json
{
  "marshalId": "marshal-123"
}
```

**Response** (200 OK):
```json
{
  "marshalId": "marshal-123",
  "marshalName": "Sarah Johnson",
  "email": "sarah@example.com"
}
```

**Error Responses**:

**404 Not Found** (Area doesn't exist):
```json
{
  "message": "Area not found"
}
```

**404 Not Found** (Marshal doesn't exist):
```json
{
  "message": "Marshal not found"
}
```

**400 Bad Request** (Already an area lead):
```json
{
  "message": "Marshal is already an area lead for this area"
}
```

---

#### Remove Area Lead
```http
DELETE /api/areas/{eventId}/{areaId}/leads/{marshalId}
```

**Response** (204 No Content)

**Error Responses**:

**404 Not Found** (Not an area lead):
```json
{
  "message": "Marshal is not an area lead for this area"
}
```

---

#### Get Area Leads
```http
GET /api/areas/{eventId}/{areaId}/leads
```

**Response** (200 OK):
```json
[
  {
    "marshalId": "marshal-123",
    "marshalName": "Sarah Johnson",
    "email": "sarah@example.com"
  },
  {
    "marshalId": "marshal-456",
    "marshalName": "John Smith",
    "email": "john@example.com"
  }
]
```

**Empty array if no area leads**:
```json
[]
```

---

### Admin: Reporting

#### Get Completion Report
```http
GET /api/events/{eventId}/checklist-report
```

**Response** (200 OK):
```json
{
  "totalItems": 5,
  "totalCompletions": 127,
  "completionsByItem": [
    {
      "itemId": "item-abc123",
      "text": "Collected hi-viz vest",
      "scope": "Everyone",
      "completionCount": 47,
      "isRequired": true
    },
    {
      "itemId": "item-def456",
      "text": "Spoken to traffic management",
      "scope": "OnePerCheckpoint",
      "completionCount": 10,
      "isRequired": true
    }
  ],
  "completionsByMarshal": [
    {
      "marshalId": "marshal-123",
      "marshalName": "Sarah Johnson",
      "completionCount": 4
    },
    {
      "marshalId": "marshal-456",
      "marshalName": "John Smith",
      "completionCount": 3
    }
  ]
}
```

**Use cases**:
- Admin dashboard: "127 total checklist completions"
- Item-level: "Collected hi-viz vest: 47/50 marshals completed"
- Marshal-level: "Sarah has completed 4/5 required items"
- Identify who hasn't completed required items

**Frontend calculations**:
```typescript
// Calculate completion percentage for an item
const everyoneItems = report.completionsByItem.filter(i => i.scope === 'Everyone');
const vestItem = everyoneItems.find(i => i.text === 'Collected hi-viz vest');
const totalMarshals = 50; // From marshals API
const percentComplete = (vestItem.completionCount / totalMarshals) * 100;
// "94% of marshals collected vest"

// Identify missing marshals
const completedMarshalIds = report.completionsByMarshal.map(m => m.marshalId);
const allMarshalIds = /* from marshals API */;
const missingMarshalIds = allMarshalIds.filter(id => !completedMarshalIds.includes(id));
// "5 marshals haven't completed any items"
```

---

## Business Rules

### Completion Rules

#### Personal Items (Everyone, EveryoneInAreas, EveryoneAtCheckpoints, SpecificPeople)
- Each marshal completes individually
- Completion tied to marshal ID
- Marshal can only complete for themselves
- Can complete multiple times if they see the item in different contexts (rare edge case)

#### Shared Items (OnePerCheckpoint, OnePerArea)
- Only ONE completion allowed per context (checkpoint or area)
- First person to complete "claims" it
- Others see "Completed by [Name]"
- Area leads can complete even if not assigned to that checkpoint/area
- Once completed, checkbox becomes view-only for others

#### Area Lead Items (AreaLead)
- Only area leads can see and complete
- One completion per area
- Regular marshals in that area cannot see it

### Permission Matrix

| Scope | Regular Marshal | Area Lead (for that area) |
|-------|----------------|---------------------------|
| Everyone | ✓ Can complete | ✓ Can complete |
| EveryoneInAreas | ✓ If in area | ✓ Can complete |
| EveryoneAtCheckpoints | ✓ If at checkpoint | ✓ Can complete |
| SpecificPeople | ✓ If in list | ✓ If in list |
| OnePerCheckpoint | ✓ If at checkpoint | ✓ For checkpoints in their area |
| OnePerArea | ✓ If in area | ✓ For their area |
| AreaLead | ✗ Cannot see | ✓ Can complete |

### Time-Based Visibility

**visibleFrom**:
- Item hidden until this date/time
- Use for: "Show briefing checklist 1 hour before event"

**visibleUntil**:
- Item hidden after this date/time
- Use for: "Hide setup tasks after event starts"

**mustCompleteBy**:
- Deadline for completion (visual indicator only, not enforced)
- Use for: "Must submit report by 5 PM"

**Current time comparison**: Server uses UTC. Frontend should convert to event timezone for display.

### Validation Rules

**On Create/Update**:
1. Text cannot be empty or > 2000 chars
2. Scope must be valid enum value
3. Scope-specific validations:
   - EveryoneInAreas: areaIds not empty
   - EveryoneAtCheckpoints: locationIds not empty
   - SpecificPeople: marshalIds not empty
   - OnePerArea: areaIds not empty
   - OnePerCheckpoint: locationIds not empty
   - AreaLead: areaIds not empty
4. HTML/scripts stripped from text
5. X-Admin-Email header required

**On Complete**:
1. Marshal must exist in event
2. Checklist item must exist
3. Marshal must have permission (based on scope)
4. Item must not already be completed (for shared items)
5. Item must be visible (time-based check)

---

## Common Workflows

### Workflow 1: Admin Creates "Everyone" Checklist
```
1. Admin navigates to event checklist management
2. Admin clicks "Add Item"
3. Admin fills form:
   - Text: "Collected hi-viz vest"
   - Scope: "Everyone"
   - Required: Yes
   - Display Order: 1
4. Frontend POSTs to /api/events/{eventId}/checklist-items
5. Backend creates item
6. Frontend refreshes item list
7. All marshals now see this item in their checklist
```

### Workflow 2: Marshal Completes Personal Item
```
1. Marshal opens their checklist: GET /api/events/{eventId}/marshals/{marshalId}/checklist
2. Marshal sees:
   ☐ Collected hi-viz vest
   isCompleted: false
   canBeCompletedByMe: true
3. Marshal clicks checkbox
4. Frontend POSTs to /api/checklist-items/{eventId}/{itemId}/complete
   Body: { "marshalId": "marshal-123" }
5. Backend creates completion record
6. Frontend updates UI:
   ✓ Collected hi-viz vest
   Completed at 9:15 AM
```

### Workflow 3: First Marshal Completes Shared Item
```
1. Marshal Sarah at Checkpoint 5 opens checklist
2. Sees:
   ☐ Spoken to traffic management (Checkpoint 5)
   isCompleted: false
   canBeCompletedByMe: true
3. Sarah clicks checkbox
4. Frontend POSTs to /api/checklist-items/{eventId}/{itemId}/complete
5. Backend creates completion with context:
   - completionContextType: "Checkpoint"
   - completionContextId: "checkpoint-5"
   - completedByMarshalName: "Sarah Johnson"
6. Frontend updates Sarah's UI to checked

Meanwhile:
7. Marshal John at Checkpoint 5 refreshes his checklist
8. Backend returns:
   ✓ Spoken to traffic management (Checkpoint 5)
   isCompleted: true
   canBeCompletedByMe: false (already completed)
   completedByName: "Sarah Johnson"
   completedAt: "9:30 AM"
9. Frontend shows:
   ✓ Spoken to traffic management (Checkpoint 5)
   Completed by Sarah Johnson at 9:30 AM
   [Checkbox is view-only/disabled]
```

### Workflow 4: Admin Sets Up Area Lead Checklist
```
1. Admin creates area lead item:
   POST /api/events/{eventId}/checklist-items
   {
     "text": "Submitted area incident log",
     "scope": "AreaLead",
     "areaIds": ["area-north"],
     ...
   }

2. Admin designates Sarah as area lead:
   POST /api/areas/{eventId}/area-north/leads
   { "marshalId": "marshal-sarah" }

3. Sarah opens her checklist
   - Sees area lead item (because she's an area lead for North)
   - Can complete it

4. John (regular marshal in North) opens his checklist
   - Does NOT see area lead item
   - Only area leads can see it
```

### Workflow 5: Multi-Area Marshal Scenario
```
Setup:
- Marshal Mike is assigned to Checkpoint 3 (in North area) and Checkpoint 7 (in East area)
- Item: "Area patrol complete" (OnePerArea: ["area-north", "area-east"])

Mike's checklist shows:
☐ Area patrol complete (Area North)
  Context: Area:area-north
☐ Area patrol complete (Area East)
  Context: Area:area-east

Mike completes the North one:
1. Clicks checkbox for North
2. Frontend POSTs with contextType: "Area", contextId: "area-north"
3. Backend creates completion for North

Mike's checklist now shows:
✓ Area patrol complete (Area North)
  Completed by Mike at 10:00 AM
☐ Area patrol complete (Area East)
  Still needs completing

Mike completes East:
4. Clicks checkbox for East
5. Frontend POSTs with contextType: "Area", contextId: "area-east"
6. Two separate completions exist (one per area)
```

### Workflow 6: Admin Uncompletes Item (Mistake Correction)
```
Scenario: Marshal accidentally checked wrong item

1. Admin opens completion report
2. Sees: "John Smith completed 'Collected radio' at 9:45 AM"
3. Admin clicks "Uncomplete" button
4. Frontend POSTs to /api/checklist-items/{eventId}/{itemId}/uncomplete
   Body: { "marshalId": "marshal-john" }
   Headers: { "X-Admin-Email": "admin@example.com" }
5. Backend soft-deletes the completion:
   - Sets isDeleted: true
   - Records uncompletedAt: now
   - Records uncompletedByAdminEmail: "admin@example.com"
6. John's checklist now shows item as unchecked again
7. John can re-complete it correctly
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

interface ChecklistItemResponse {
  itemId: string;
  eventId: string;
  text: string;
  scopeConfigurations: ScopeConfiguration[];
  displayOrder: number;
  isRequired: boolean;
  visibleFrom?: string | null;
  visibleUntil?: string | null;
  mustCompleteBy?: string | null;
  createdByAdminEmail: string;
  createdDate: string;
  lastModifiedDate?: string | null;
  lastModifiedByAdminEmail: string;
}

interface ChecklistItemWithStatus extends ChecklistItemResponse {
  isCompleted: boolean;
  canBeCompletedByMe: boolean;
  completedByName?: string | null;
  completedAt?: string | null;
  completionContextType: string;
  completionContextId: string;
  matchedScope: string; // Which scope configuration matched for this marshal
}
```

### Recommended State Management

```typescript
// Store checklist items with status
interface ChecklistState {
  items: ChecklistItemWithStatus[];
  loading: boolean;
  error: string | null;
  lastRefreshed: Date | null;
}

// Actions
async function loadChecklist(eventId: string, marshalId: string) {
  const response = await fetch(`/api/events/${eventId}/marshals/${marshalId}/checklist`);
  return await response.json();
}

async function completeItem(eventId: string, itemId: string, marshalId: string) {
  const response = await fetch(`/api/checklist-items/${eventId}/${itemId}/complete`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ marshalId })
  });
  return await response.json();
}
```

### UI Components

#### Marshal Checklist View
```tsx
function MarshalChecklist({ eventId, marshalId }) {
  const [items, setItems] = useState<ChecklistItemWithStatus[]>([]);

  useEffect(() => {
    loadChecklist(eventId, marshalId).then(setItems);
  }, [eventId, marshalId]);

  const handleCheck = async (item: ChecklistItemWithStatus) => {
    if (!item.canBeCompletedByMe) return;

    try {
      await completeItem(eventId, item.itemId, marshalId);
      // Refresh checklist
      const updated = await loadChecklist(eventId, marshalId);
      setItems(updated);
    } catch (error) {
      // Handle error (show toast, etc.)
    }
  };

  return (
    <div className="checklist">
      <h2>Your Checklist ({completedCount}/{items.length})</h2>
      {items.map(item => (
        <ChecklistItem
          key={item.itemId}
          item={item}
          onCheck={handleCheck}
        />
      ))}
    </div>
  );
}

function ChecklistItem({ item, onCheck }) {
  const isPastDeadline = item.mustCompleteBy &&
    new Date(item.mustCompleteBy) < new Date();

  return (
    <div className={`checklist-item ${item.isCompleted ? 'completed' : ''}`}>
      <input
        type="checkbox"
        checked={item.isCompleted}
        disabled={!item.canBeCompletedByMe}
        onChange={() => onCheck(item)}
      />
      <div className="item-content">
        <div className="item-text">
          {item.text}
          {!item.isRequired && <span className="optional">(Optional)</span>}
          {item.isRequired && isPastDeadline && <span className="overdue">⚠️ Overdue</span>}
        </div>

        {item.isCompleted && (
          <div className="completion-info">
            {item.completedByName && item.completedByName !== currentMarshalName && (
              <span>Completed by {item.completedByName}</span>
            )}
            <span className="timestamp">
              {formatTimestamp(item.completedAt)}
            </span>
          </div>
        )}

        {!item.canBeCompletedByMe && !item.isCompleted && (
          <div className="permission-info">
            {getScopeDescription(item)}
          </div>
        )}
      </div>
    </div>
  );
}

function getScopeDescription(item: ChecklistItemWithStatus): string {
  switch (item.scope) {
    case 'OnePerCheckpoint':
      return 'Can be completed by any marshal at this checkpoint';
    case 'OnePerArea':
      return 'Can be completed by any marshal in this area';
    case 'AreaLead':
      return 'Area leads only';
    default:
      return '';
  }
}
```

#### Admin Checklist Management
```tsx
function AdminChecklistManagement({ eventId }) {
  const [items, setItems] = useState<ChecklistItemResponse[]>([]);
  const [showCreateDialog, setShowCreateDialog] = useState(false);

  const loadItems = async () => {
    const response = await fetch(`/api/events/${eventId}/checklist-items`);
    setItems(await response.json());
  };

  useEffect(() => { loadItems(); }, [eventId]);

  return (
    <div className="admin-checklist">
      <div className="header">
        <h2>Checklist Items</h2>
        <button onClick={() => setShowCreateDialog(true)}>
          Add Item
        </button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Order</th>
            <th>Text</th>
            <th>Scope</th>
            <th>Required</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {items.map(item => (
            <tr key={item.itemId}>
              <td>{item.displayOrder}</td>
              <td>{item.text}</td>
              <td>{item.scope}</td>
              <td>{item.isRequired ? 'Yes' : 'No'}</td>
              <td>
                <button onClick={() => editItem(item)}>Edit</button>
                <button onClick={() => deleteItem(item)}>Delete</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {showCreateDialog && (
        <CreateChecklistItemDialog
          eventId={eventId}
          onClose={() => setShowCreateDialog(false)}
          onSaved={loadItems}
        />
      )}
    </div>
  );
}
```

#### Scope Selector Component
```tsx
function ScopeSelector({ value, onChange, eventId }) {
  const [areas, setAreas] = useState([]);
  const [checkpoints, setCheckpoints] = useState([]);
  const [marshals, setMarshals] = useState([]);

  // Load dependent data based on scope
  useEffect(() => {
    if (['EveryoneInAreas', 'OnePerArea', 'AreaLead'].includes(value)) {
      loadAreas(eventId).then(setAreas);
    }
    if (['EveryoneAtCheckpoints', 'OnePerCheckpoint'].includes(value)) {
      loadCheckpoints(eventId).then(setCheckpoints);
    }
    if (value === 'SpecificPeople') {
      loadMarshals(eventId).then(setMarshals);
    }
  }, [value, eventId]);

  return (
    <div className="scope-selector">
      <label>Who should complete this?</label>
      <select value={value} onChange={e => onChange(e.target.value)}>
        <option value="Everyone">Everyone</option>
        <option value="EveryoneInAreas">Everyone in specific areas</option>
        <option value="EveryoneAtCheckpoints">Everyone at specific checkpoints</option>
        <option value="SpecificPeople">Specific people</option>
        <option value="OnePerCheckpoint">One person per checkpoint</option>
        <option value="OnePerArea">One person per area</option>
        <option value="AreaLead">Area leads only</option>
      </select>

      {value === 'EveryoneInAreas' && (
        <MultiSelect
          label="Select Areas"
          options={areas}
          onChange={selectedAreaIds => /* update form */}
        />
      )}

      {value === 'EveryoneAtCheckpoints' && (
        <MultiSelect
          label="Select Checkpoints"
          options={checkpoints}
          onChange={selectedLocationIds => /* update form */}
        />
      )}

      {value === 'SpecificPeople' && (
        <MultiSelect
          label="Select Marshals"
          options={marshals}
          onChange={selectedMarshalIds => /* update form */}
        />
      )}

      {/* Similar for other scopes */}
    </div>
  );
}
```

### Real-Time Updates

**Polling Strategy** (Recommended):
```typescript
// Poll for updates every 30 seconds while checklist is visible
function useChecklistPolling(eventId: string, marshalId: string, intervalMs = 30000) {
  const [items, setItems] = useState<ChecklistItemWithStatus[]>([]);

  useEffect(() => {
    const loadData = () => {
      loadChecklist(eventId, marshalId).then(setItems);
    };

    loadData(); // Initial load
    const interval = setInterval(loadData, intervalMs);

    return () => clearInterval(interval);
  }, [eventId, marshalId, intervalMs]);

  return items;
}
```

**Use case**: When Sarah completes a shared item at Checkpoint 5, John (also at Checkpoint 5) will see the update within 30 seconds.

**Alternative**: WebSocket/SignalR for instant updates (more complex).

### Error Handling

```typescript
async function completeItem(eventId: string, itemId: string, marshalId: string) {
  try {
    const response = await fetch(`/api/checklist-items/${eventId}/${itemId}/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ marshalId })
    });

    if (!response.ok) {
      const error = await response.json();

      switch (response.status) {
        case 400:
          if (error.message === 'This checklist item has already been completed') {
            showToast('Someone else just completed this item', 'info');
            // Refresh checklist to show updated state
            return await loadChecklist(eventId, marshalId);
          }
          break;

        case 403:
          showToast('You don\'t have permission to complete this item', 'error');
          break;

        case 404:
          showToast('Item not found', 'error');
          break;

        default:
          showToast('An error occurred. Please try again.', 'error');
      }

      throw new Error(error.message);
    }

    return await response.json();
  } catch (error) {
    console.error('Error completing item:', error);
    throw error;
  }
}
```

### Progress Indicators

```typescript
function calculateProgress(items: ChecklistItemWithStatus[]) {
  const required = items.filter(i => i.isRequired);
  const completed = required.filter(i => i.isCompleted);

  return {
    completedCount: completed.length,
    totalCount: required.length,
    percentage: (completed.length / required.length) * 100,
    allComplete: completed.length === required.length
  };
}

function ProgressBar({ items }: { items: ChecklistItemWithStatus[] }) {
  const progress = calculateProgress(items);

  return (
    <div className="progress-container">
      <div className="progress-text">
        {progress.completedCount} of {progress.totalCount} completed
      </div>
      <div className="progress-bar">
        <div
          className="progress-fill"
          style={{ width: `${progress.percentage}%` }}
        />
      </div>
      {progress.allComplete && (
        <div className="completion-badge">✓ All required items complete!</div>
      )}
    </div>
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
| 400 | Bad Request | Validation failed, item already completed |
| 403 | Forbidden | No permission to complete item |
| 404 | Not Found | Item/marshal/area doesn't exist |
| 500 | Server Error | Database error, unexpected exception |

### Common Error Scenarios

#### "Already Completed" Race Condition
```
Scenario: Two marshals at same checkpoint try to complete OnePerCheckpoint item simultaneously

Marshal A clicks checkbox → Request sent
Marshal B clicks checkbox → Request sent
Marshal A's request completes → Creates completion record
Marshal B's request arrives → Sees item already completed → Returns 400

Frontend should:
1. Show toast: "Someone else just completed this item"
2. Refresh checklist to show updated state
3. Disable checkbox and show "Completed by Marshal A"
```

#### "Permission Denied" for Area Lead Items
```
Scenario: Regular marshal tries to complete area lead item (shouldn't happen if UI is correct)

Frontend should:
1. Never show checkbox as enabled for items where canBeCompletedByMe = false
2. If error occurs: Show "Only area leads can complete this item"
3. Log error to help debug UI bugs
```

#### "Item Not Found" After Deletion
```
Scenario: Marshal has checklist open, admin deletes an item, marshal tries to complete it

Frontend should:
1. Handle 404 gracefully
2. Show toast: "This item was removed by an administrator"
3. Refresh checklist to get current items
```

### Validation Errors

When creating/updating items, backend validates and may return 400 with details:

```json
{
  "message": "Invalid request"
}
```

Frontend should validate BEFORE sending:
- Text not empty
- Scope selected
- Filters provided for scope (areas for EveryoneInAreas, etc.)
- Display order is a number
- Dates are valid ISO 8601 if provided

---

## Testing Checklist for Frontend Developers

### Scope Testing

- [ ] **Everyone scope**
  - [ ] All marshals see the item
  - [ ] Each can complete independently
  - [ ] Completion doesn't affect other marshals

- [ ] **EveryoneInAreas scope**
  - [ ] Only marshals in selected areas see it
  - [ ] Marshals in other areas don't see it
  - [ ] Marshal in multiple areas (one selected, one not) sees it

- [ ] **OnePerCheckpoint scope**
  - [ ] All marshals at checkpoint see it
  - [ ] First to complete locks it for others
  - [ ] Others see "Completed by [Name]"
  - [ ] Area leads for that checkpoint's area can complete
  - [ ] Area leads for OTHER areas cannot complete

- [ ] **AreaLead scope**
  - [ ] Only area leads see it
  - [ ] Regular marshals don't see it
  - [ ] Can be completed by any area lead for that area

### Multi-Area/Checkpoint Testing

- [ ] Marshal assigned to Checkpoint 3 (North) and Checkpoint 5 (East)
  - [ ] OnePerCheckpoint item for Checkpoint 3: Marshal sees it
  - [ ] OnePerArea item for North and East: Marshal sees BOTH (one per area)

- [ ] Checkpoint 5 is in BOTH North and East areas
  - [ ] OnePerCheckpoint item: Only 1 completion possible
  - [ ] Completion shows to marshals in both areas

### Permission Testing

- [ ] Regular marshal cannot complete AreaLead items (checkbox disabled)
- [ ] Area lead CAN complete items for checkpoints in their area (even if not assigned)
- [ ] Area lead CANNOT complete items for checkpoints outside their area
- [ ] Trying to complete without permission returns 403

### Time-Based Testing

- [ ] Item with visibleFrom in future: Not shown in checklist
- [ ] Item with visibleUntil in past: Not shown in checklist
- [ ] Item with mustCompleteBy in past: Shown with "Overdue" indicator

### Real-Time Testing

- [ ] Marshal A completes shared item
- [ ] Marshal B (at same checkpoint) refreshes: Sees completion
- [ ] Marshal B's checkbox is disabled
- [ ] Marshal B sees "Completed by Marshal A"

### Error Handling Testing

- [ ] Network error: Show appropriate error message
- [ ] 400 Already Completed: Show toast, refresh checklist
- [ ] 403 Forbidden: Show "No permission" message
- [ ] 404 Not Found: Show "Item removed" message
- [ ] Try to complete item twice rapidly: Second attempt fails gracefully

---

## Summary

This API provides a flexible, powerful checklist system with:
- **7 scope types** for targeting specific groups
- **Area lead hierarchy** for management structure
- **Real-time collaboration** with shared items
- **Audit trail** for accountability
- **Time-based controls** for scheduling

Key principles:
1. **No duplication** - One item definition, completions only when checked
2. **Context-aware** - Personal vs shared completion tracking
3. **Permission-based** - Enforced at backend, reflected in `canBeCompletedByMe`
4. **Flexible** - Scope system covers wide range of use cases

Frontend implementation should:
1. Respect `canBeCompletedByMe` flag
2. Handle race conditions gracefully (already completed errors)
3. Show completion context (who/when) for shared items
4. Poll or use WebSockets for real-time updates
5. Validate before sending to backend

The system is production-ready and handles edge cases like:
- Multi-area checkpoints
- Multiple area leads
- Race conditions for shared items
- Corrections via uncomplete
- Flexible reporting

Questions? Check the error handling section and common workflows above.
