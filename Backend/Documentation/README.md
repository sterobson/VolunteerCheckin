# Volunteer Check-in Backend Documentation

Welcome to the documentation for the Volunteer Check-in backend API. This guide provides comprehensive information about the system's architecture, API endpoints, and development practices.

## Table of Contents

### API Documentation

Detailed API documentation for each feature area:

| Document | Description |
|----------|-------------|
| [Checklists API](API/Checklists.md) | Complete guide to the checklist system including scope types, completion tracking, and area lead management |
| [Notes API](API/Notes.md) | Documentation for the notes system including scope-based visibility, priorities, and categories |

### Architecture

System architecture and design documentation:

| Document | Description |
|----------|-------------|
| [Authentication](Architecture/Authentication.md) | Authentication and authorization system including magic links, marshal codes, sessions, and claims |
| [Permissions](Architecture/Permissions.md) | Role-based permissions, contact visibility rules, scope-based access, and endpoint authorization |

### Development

Development guides and technical notes:

| Document | Description |
|----------|-------------|
| [Code Review](Development/CodeReview.md) | Code review notes, identified issues, and their resolutions |

---

## Quick Links

### Getting Started
- [Authentication Overview](Architecture/Authentication.md#overview)
- [API Endpoints Summary](#api-endpoints-summary)

### Key Concepts
- [Scope System](API/Checklists.md#available-scope-types) - How visibility and permissions work
- [Most Specific Wins](API/Checklists.md#flexible-scope-system--most-specific-wins) - How multiple scope configurations are evaluated
- [Sentinel Values](API/Checklists.md#scenario-5-sentinel-values---all_checkpoints-all_areas-and-all_marshals) - Using ALL_CHECKPOINTS, ALL_AREAS, ALL_MARSHALS

---

## API Endpoints Summary

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/request-login` | Request magic link email |
| POST | `/api/auth/verify-token` | Verify magic link token |
| POST | `/api/auth/marshal-login` | Login with marshal code |
| POST | `/api/auth/logout` | End session |

### Events
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events` | List events for current user |
| GET | `/api/events/{eventId}` | Get event details |
| POST | `/api/events` | Create new event |
| PUT | `/api/events/{eventId}` | Update event |

### Marshals
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/{eventId}/marshals` | List marshals |
| POST | `/api/events/{eventId}/marshals` | Create marshal |
| GET | `/api/events/{eventId}/marshals/{marshalId}` | Get marshal |
| PUT | `/api/events/{eventId}/marshals/{marshalId}` | Update marshal |
| DELETE | `/api/events/{eventId}/marshals/{marshalId}` | Delete marshal |

### Checkpoints (Locations)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/{eventId}/locations` | List checkpoints |
| POST | `/api/events/{eventId}/locations` | Create checkpoint |
| GET | `/api/events/{eventId}/locations/{locationId}` | Get checkpoint |
| PUT | `/api/events/{eventId}/locations/{locationId}` | Update checkpoint |
| DELETE | `/api/events/{eventId}/locations/{locationId}` | Delete checkpoint |

### Areas
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/{eventId}/areas` | List areas |
| POST | `/api/areas` | Create area |
| GET | `/api/areas/{eventId}/{areaId}` | Get area |
| PUT | `/api/areas/{eventId}/{areaId}` | Update area |
| DELETE | `/api/areas/{eventId}/{areaId}` | Delete area |
| GET | `/api/areas/{eventId}/{areaId}/leads` | Get area leads |
| POST | `/api/areas/{eventId}/{areaId}/leads` | Add area lead |
| DELETE | `/api/areas/{eventId}/{areaId}/leads/{marshalId}` | Remove area lead |

### Checklists
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/{eventId}/checklist-items` | List all items (admin) |
| POST | `/api/events/{eventId}/checklist-items` | Create item |
| GET | `/api/checklist-items/{eventId}/{itemId}` | Get item |
| PUT | `/api/checklist-items/{eventId}/{itemId}` | Update item |
| DELETE | `/api/checklist-items/{eventId}/{itemId}` | Delete item |
| GET | `/api/events/{eventId}/marshals/{marshalId}/checklist` | Get marshal's checklist |
| POST | `/api/checklist-items/{eventId}/{itemId}/complete` | Complete item |
| POST | `/api/checklist-items/{eventId}/{itemId}/uncomplete` | Uncomplete item (admin) |

### Notes
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/{eventId}/notes` | List all notes (admin) |
| POST | `/api/events/{eventId}/notes` | Create note |
| GET | `/api/events/{eventId}/notes/{noteId}` | Get note |
| PUT | `/api/events/{eventId}/notes/{noteId}` | Update note |
| DELETE | `/api/events/{eventId}/notes/{noteId}` | Delete note |
| GET | `/api/events/{eventId}/marshals/{marshalId}/notes` | Get marshal's notes |
| GET | `/api/events/{eventId}/my-notes` | Get current user's notes |

### Assignments
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/events/{eventId}/assignments` | List assignments |
| POST | `/api/assignments` | Create assignment |
| DELETE | `/api/assignments/{eventId}/{assignmentId}` | Delete assignment |
| POST | `/api/assignments/{eventId}/{assignmentId}/checkin` | Check in |

---

## Shared Concepts

### Scope System

Both Checklists and Notes use a shared scope evaluation system to determine visibility. The available scopes are:

| Scope | Description | Checklists | Notes |
|-------|-------------|:----------:|:-----:|
| EveryoneInAreas | Everyone in specified areas | ✓ | ✓ |
| EveryoneAtCheckpoints | Everyone at specified checkpoints | ✓ | ✓ |
| SpecificPeople | Specific marshals by ID | ✓ | ✓ |
| EveryAreaLead | Each area lead individually | ✓ | ✓ |
| OnePerCheckpoint | Shared completion per checkpoint | ✓ | ✗ |
| OnePerArea | Shared completion per area | ✓ | ✗ |
| OneLeadPerArea | One area lead per area | ✓ | ✗ |

Notes don't support "One per" scopes because they don't have completion tracking.

### Sentinel Values

For flexible targeting without hardcoding IDs:

| Sentinel | Description |
|----------|-------------|
| `ALL_CHECKPOINTS` | Matches all checkpoints in the event |
| `ALL_AREAS` | Matches all areas in the event |
| `ALL_MARSHALS` | Matches all marshals in the event |

---

## Technology Stack

- **Runtime**: .NET 10 / Azure Functions v4
- **Database**: Azure Table Storage
- **Authentication**: Passwordless (Magic Links + Marshal Codes)
- **Email**: SMTP (configurable)

---

## Contributing

When adding new documentation:

1. Place API documentation in `Documentation/API/`
2. Place architecture documentation in `Documentation/Architecture/`
3. Place development guides in `Documentation/Development/`
4. Update this README.md with links to new documents
