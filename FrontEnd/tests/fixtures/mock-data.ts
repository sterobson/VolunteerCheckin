/**
 * Mock data for MarshalView visual regression tests
 * This data simulates realistic API responses for different test scenarios
 */

export const TEST_EVENT_ID = 'test-event-123';
export const TEST_MARSHAL_ID = 'marshal-001';
export const TEST_MAGIC_CODE = 'testcode123';

// Mock event with branding
export const mockEvent = {
  id: TEST_EVENT_ID,
  name: 'Summer Trail Run 2024',
  eventDate: '2024-07-15T08:00:00Z',
  description: 'A scenic trail run through the mountains with multiple checkpoints.',
  terminology: {
    checkpoint: 'Checkpoint',
    checkpoints: 'Checkpoints',
    area: 'Zone',
    areas: 'Zones',
    person: 'Marshal',
    people: 'Marshals',
    course: 'Route',
    checklists: 'Tasks',
  },
  branding: {
    logoUrl: null,
    logoPosition: 'left',
    headerBackgroundColor: '#1a365d',
    headerTextColor: '#ffffff',
    pageBackgroundColor: '#f7fafc',
  },
};

// Mock locations/checkpoints
export const mockLocations = [
  {
    id: 'loc-001',
    eventId: TEST_EVENT_ID,
    name: 'Start Line',
    description: 'Main start/finish area',
    latitude: 51.5074,
    longitude: -0.1278,
    order: 1,
    areaIds: ['area-001'],
    expectedFirstArrival: '2024-07-15T08:00:00Z',
    expectedLastArrival: '2024-07-15T08:30:00Z',
    iconShape: 'circle',
    iconColor: '#22c55e',
  },
  {
    id: 'loc-002',
    eventId: TEST_EVENT_ID,
    name: '5K Water Station',
    description: 'First water station at 5K mark',
    latitude: 51.5100,
    longitude: -0.1300,
    order: 2,
    areaIds: ['area-001'],
    expectedFirstArrival: '2024-07-15T08:25:00Z',
    expectedLastArrival: '2024-07-15T09:00:00Z',
    iconShape: 'square',
    iconColor: '#3b82f6',
  },
  {
    id: 'loc-003',
    eventId: TEST_EVENT_ID,
    name: '10K Turnaround',
    description: 'Turnaround point for 10K runners',
    latitude: 51.5150,
    longitude: -0.1350,
    order: 3,
    areaIds: ['area-002'],
    expectedFirstArrival: '2024-07-15T08:45:00Z',
    expectedLastArrival: '2024-07-15T09:30:00Z',
    iconShape: 'triangle',
    iconColor: '#f59e0b',
  },
];

// Mock areas/zones
export const mockAreas = [
  {
    id: 'area-001',
    eventId: TEST_EVENT_ID,
    name: 'Start Zone',
    color: '#22c55e',
    locationIds: ['loc-001', 'loc-002'],
  },
  {
    id: 'area-002',
    eventId: TEST_EVENT_ID,
    name: 'Mountain Section',
    color: '#f59e0b',
    locationIds: ['loc-003'],
  },
];

// Mock current marshal (authenticated user)
export const mockCurrentMarshal = {
  id: TEST_MARSHAL_ID,
  eventId: TEST_EVENT_ID,
  name: 'John Smith',
  email: 'john.smith@example.com',
  phone: '+44 7700 900123',
};

// Mock assignments for current marshal
export const mockAssignments = [
  {
    id: 'assign-001',
    eventId: TEST_EVENT_ID,
    marshalId: TEST_MARSHAL_ID,
    locationId: 'loc-001',
    isCheckedIn: true,
    checkInTime: new Date().toISOString(),
    isDynamic: false,
  },
  {
    id: 'assign-002',
    eventId: TEST_EVENT_ID,
    marshalId: TEST_MARSHAL_ID,
    locationId: 'loc-002',
    isCheckedIn: false,
    checkInTime: null,
    isDynamic: false,
  },
];

// Mock other marshals (for area lead view)
export const mockOtherMarshals = [
  {
    id: 'marshal-002',
    eventId: TEST_EVENT_ID,
    name: 'Jane Doe',
    email: 'jane.doe@example.com',
    phone: '+44 7700 900456',
  },
  {
    id: 'marshal-003',
    eventId: TEST_EVENT_ID,
    name: 'Bob Wilson',
    email: 'bob.wilson@example.com',
    phone: '+44 7700 900789',
  },
];

// Mock checklist items
export const mockChecklistItems = [
  {
    id: 'check-001',
    eventId: TEST_EVENT_ID,
    title: 'Set up safety barriers',
    description: 'Ensure all barriers are properly positioned',
    scope: 'checkpoint',
    locationId: 'loc-001',
    isCompleted: true,
    completedBy: TEST_MARSHAL_ID,
    completedAt: new Date().toISOString(),
  },
  {
    id: 'check-002',
    eventId: TEST_EVENT_ID,
    title: 'Check radio communications',
    description: 'Test radio and confirm channel',
    scope: 'checkpoint',
    locationId: 'loc-001',
    isCompleted: false,
    completedBy: null,
    completedAt: null,
  },
  {
    id: 'check-003',
    eventId: TEST_EVENT_ID,
    title: 'Verify water supplies',
    description: 'Ensure adequate water and cups available',
    scope: 'checkpoint',
    locationId: 'loc-002',
    isCompleted: false,
    completedBy: null,
    completedAt: null,
  },
];

// Mock notes
export const mockNotes = [
  {
    id: 'note-001',
    eventId: TEST_EVENT_ID,
    title: 'Weather Update',
    content: 'Expected rain at 10am - ensure runners have shelter options.',
    createdAt: new Date(Date.now() - 3600000).toISOString(),
    createdBy: 'Event Control',
    scope: 'event',
    locationId: null,
    areaId: null,
  },
  {
    id: 'note-002',
    eventId: TEST_EVENT_ID,
    title: 'First aid location',
    content: 'First aid tent is 50m behind the start line.',
    createdAt: new Date(Date.now() - 7200000).toISOString(),
    createdBy: 'Medical Team',
    scope: 'checkpoint',
    locationId: 'loc-001',
    areaId: null,
  },
];

// Mock contacts
export const mockContacts = [
  {
    id: 'contact-001',
    eventId: TEST_EVENT_ID,
    name: 'Event Control',
    role: 'Control Room',
    phone: '+44 7700 900000',
    email: 'control@event.com',
    isEmergency: true,
    order: 1,
  },
  {
    id: 'contact-002',
    eventId: TEST_EVENT_ID,
    name: 'Medical Lead',
    role: 'First Aid',
    phone: '+44 7700 900001',
    email: 'medical@event.com',
    isEmergency: true,
    order: 2,
  },
  {
    id: 'contact-003',
    eventId: TEST_EVENT_ID,
    name: 'Race Director',
    role: 'Director',
    phone: '+44 7700 900002',
    email: 'director@event.com',
    isEmergency: false,
    order: 3,
  },
];

// Mock incidents
export const mockIncidents = [
  {
    id: 'incident-001',
    eventId: TEST_EVENT_ID,
    title: 'Runner with minor injury',
    description: 'Runner twisted ankle near the 5K mark. Walking aid provided.',
    status: 'resolved',
    severity: 'minor',
    locationId: 'loc-002',
    reportedBy: TEST_MARSHAL_ID,
    reportedAt: new Date(Date.now() - 1800000).toISOString(),
    resolvedAt: new Date(Date.now() - 900000).toISOString(),
    notes: [
      {
        id: 'inc-note-001',
        note: 'Runner continued after brief rest.',
        createdAt: new Date(Date.now() - 1000000).toISOString(),
        createdBy: 'Medical Team',
      },
    ],
  },
];

// Mock route/GPX data
export const mockRoute = {
  id: 'route-001',
  eventId: TEST_EVENT_ID,
  name: 'Main Course',
  coordinates: [
    [51.5074, -0.1278],
    [51.5085, -0.1290],
    [51.5100, -0.1300],
    [51.5120, -0.1320],
    [51.5150, -0.1350],
  ],
};

// Mock auth/me response for authenticated marshal
export const mockAuthMe = {
  person: mockCurrentMarshal,
  marshalId: TEST_MARSHAL_ID,
  eventRoles: [
    {
      role: 'Marshal',
      eventId: TEST_EVENT_ID,
    },
  ],
  authMethod: 'MarshalMagicCode',
};

// Mock auth/me response for area lead
export const mockAuthMeAreaLead = {
  person: mockCurrentMarshal,
  marshalId: TEST_MARSHAL_ID,
  eventRoles: [
    {
      role: 'Marshal',
      eventId: TEST_EVENT_ID,
    },
    {
      role: 'EventAreaLead',
      eventId: TEST_EVENT_ID,
      areaIds: ['area-001'],
    },
  ],
  authMethod: 'MarshalMagicCode',
};

// Mock auth/me response for admin
export const mockAuthMeAdmin = {
  person: mockCurrentMarshal,
  marshalId: TEST_MARSHAL_ID,
  eventRoles: [
    {
      role: 'Marshal',
      eventId: TEST_EVENT_ID,
    },
    {
      role: 'EventAdmin',
      eventId: TEST_EVENT_ID,
    },
  ],
  authMethod: 'SecureEmailLink',
};

/**
 * Helper to set up all API mocks for a page
 */
export function getMockApiHandlers(scenario: 'marshal' | 'area-lead' | 'admin' = 'marshal') {
  const authResponse = scenario === 'area-lead'
    ? mockAuthMeAreaLead
    : scenario === 'admin'
      ? mockAuthMeAdmin
      : mockAuthMe;

  return {
    event: mockEvent,
    locations: mockLocations,
    areas: mockAreas,
    assignments: mockAssignments,
    marshals: [mockCurrentMarshal, ...mockOtherMarshals],
    checklistItems: mockChecklistItems,
    notes: mockNotes,
    contacts: mockContacts,
    incidents: mockIncidents,
    route: mockRoute,
    auth: authResponse,
  };
}
