<template>
  <div class="marshal-view">
    <header class="header">
      <h1 v-if="event">{{ event.name }}</h1>
      <div class="header-actions">
        <button v-if="isAuthenticated" @click="showEmergency = true" class="btn-emergency">
          Emergency Info
        </button>
        <button v-if="isAuthenticated" @click="handleLogout" class="btn-logout">
          Logout
        </button>
      </div>
    </header>

    <div class="container">
      <!-- Loading State -->
      <div v-if="loading" class="selection-card">
        <div class="loading">Loading...</div>
      </div>

      <!-- Login with Magic Code -->
      <div v-else-if="!isAuthenticated && !loginError" class="selection-card">
        <h2>Welcome</h2>
        <p class="instruction">Please use the login link sent to you to access your marshal dashboard.</p>
        <p v-if="authenticating" class="loading">Authenticating...</p>
      </div>

      <!-- Login Error -->
      <div v-else-if="loginError" class="selection-card error-card">
        <h2>Login Failed</h2>
        <p class="error">{{ loginError }}</p>
        <p class="instruction">Please contact the event organizer for a new login link.</p>
      </div>

      <!-- Marshal Dashboard -->
      <div v-else class="dashboard">
        <div class="welcome-bar">
          <div class="welcome-info">
            <span class="welcome-name">{{ currentPerson?.name || 'Marshal' }}</span>
            <span v-if="currentPerson?.email" class="welcome-email">{{ currentPerson.email }}</span>
          </div>
          <div v-if="canSwitchToAdmin" class="mode-switch">
            <button @click="switchToAdminMode" class="btn-mode-switch">
              Switch to Admin
            </button>
          </div>
          <div v-else-if="isAdminButNeedsReauth" class="mode-switch">
            <span class="reauth-hint" title="Login via admin flow to access admin features">
              Admin access requires re-login
            </span>
          </div>
        </div>

        <!-- Accordion Sections -->
        <div class="accordion">
          <!-- Assignments Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'assignments' }"
              @click="toggleSection('assignments')"
            >
              <span class="accordion-title">Your Assignments ({{ assignments.length }})</span>
              <span class="accordion-icon">{{ expandedSection === 'assignments' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'assignments'" class="accordion-content">
              <div v-if="assignments.length === 0" class="empty-state">
                <p>You don't have any checkpoint assignments yet.</p>
              </div>
              <div v-else class="assignments-list">
                <div v-for="assign in assignmentsWithDetails" :key="assign.id" class="assignment-item">
                  <div class="assignment-header">
                    <strong>{{ assign.location?.name || assign.locationName }}</strong>
                    <span v-if="assign.areaName" class="area-badge">{{ assign.areaName }}</span>
                  </div>
                  <p v-if="assign.location?.description" class="assignment-description">{{ assign.location.description }}</p>
                  <p v-if="assign.location?.startTime || assign.location?.endTime" class="time-range">
                    {{ formatTimeRange(assign.location.startTime, assign.location.endTime) }}
                  </p>

                  <!-- Marshals on this checkpoint -->
                  <div class="checkpoint-marshals">
                    <div class="marshals-label">Marshals:</div>
                    <div class="marshals-list">
                      <span
                        v-for="m in assign.allMarshals"
                        :key="m.marshalId"
                        class="marshal-tag"
                        :class="{ 'is-you': m.marshalId === currentMarshalId, 'checked-in': m.isCheckedIn }"
                      >
                        {{ m.marshalName }}{{ m.marshalId === currentMarshalId ? ' (you)' : '' }}
                        <span v-if="m.isCheckedIn" class="check-badge">✓</span>
                      </span>
                    </div>
                  </div>

                  <!-- Check-in status and actions -->
                  <div v-if="assign.isCheckedIn" class="checked-in-status">
                    <span class="check-icon">✓</span>
                    <div>
                      <strong>Checked In</strong>
                      <p class="check-time">{{ formatTime(assign.checkInTime) }}</p>
                    </div>
                  </div>
                  <div v-else class="check-in-actions">
                    <button
                      @click="handleCheckIn(assign)"
                      class="btn btn-primary"
                      :disabled="checkingIn === assign.id"
                    >
                      {{ checkingIn === assign.id ? 'Checking in...' : 'GPS Check-In' }}
                    </button>
                    <button
                      @click="handleManualCheckIn(assign)"
                      class="btn btn-secondary"
                      :disabled="checkingIn === assign.id"
                    >
                      Manual
                    </button>
                  </div>
                  <div v-if="checkInError && checkingInAssignment === assign.id" class="error">{{ checkInError }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Checklist Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'checklist' }"
              @click="toggleSection('checklist')"
            >
              <span class="accordion-title">Your Checklist ({{ checklistItems.length }})</span>
              <span class="accordion-icon">{{ expandedSection === 'checklist' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'checklist'" class="accordion-content">
              <div v-if="checklistLoading" class="loading">Loading checklist...</div>
              <div v-else-if="checklistError" class="error">{{ checklistError }}</div>
              <div v-else-if="checklistItems.length === 0" class="empty-state">
                <p>No checklist items for you.</p>
              </div>
              <div v-else class="checklist-items">
                <div
                  v-for="item in checklistItems"
                  :key="item.itemId"
                  class="checklist-item"
                  :class="{ 'item-completed': item.isCompleted }"
                >
                  <div class="item-checkbox">
                    <input
                      type="checkbox"
                      :checked="item.isCompleted"
                      :disabled="!item.canBeCompletedByMe || savingChecklist"
                      @change="handleToggleChecklist(item)"
                    />
                  </div>
                  <div class="item-content">
                    <div class="item-text">{{ item.text }}</div>
                    <div v-if="getContextName(item)" class="item-context">
                      {{ getContextName(item) }}
                    </div>
                    <div v-if="item.isCompleted" class="completion-info">
                      <span class="completion-text">
                        Completed by {{ item.completedByActorName || 'Unknown' }}
                      </span>
                      <span class="completion-time">
                        {{ formatDateTime(item.completedAt) }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Area Contacts Section -->
          <div v-if="areaContactsByArea.length > 0" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'areaContacts' }"
              @click="toggleSection('areaContacts')"
            >
              <span class="accordion-title">Area Contacts ({{ totalAreaContacts }})</span>
              <span class="accordion-icon">{{ expandedSection === 'areaContacts' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'areaContacts'" class="accordion-content">
              <div v-for="area in areaContactsByArea" :key="area.areaId" class="area-contacts-group">
                <h4 class="area-group-title">{{ area.areaName }}</h4>
                <div class="contact-list">
                  <div v-for="contact in area.contacts" :key="contact.marshalId" class="contact-item">
                    <div class="contact-info">
                      <div class="contact-name">{{ contact.marshalName }}</div>
                      <div v-if="contact.role" class="contact-role">{{ contact.role }}</div>
                      <div v-if="contact.phone" class="contact-detail">{{ contact.phone }}</div>
                      <div v-if="contact.email" class="contact-detail">{{ contact.email }}</div>
                    </div>
                    <div class="contact-actions">
                      <a v-if="contact.phone" :href="`tel:${contact.phone}`" class="contact-link">
                        <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                          <path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"/>
                        </svg>
                        Call
                      </a>
                      <a v-if="contact.phone" :href="`sms:${contact.phone}`" class="contact-link">
                        <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                          <path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/>
                        </svg>
                        Text
                      </a>
                      <a v-if="contact.email" :href="`mailto:${contact.email}`" class="contact-link">
                        <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                          <path d="M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z"/>
                        </svg>
                        Email
                      </a>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Event Contacts Section -->
          <div v-if="eventContacts.length > 0" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'eventContacts' }"
              @click="toggleSection('eventContacts')"
            >
              <span class="accordion-title">Event Contacts ({{ eventContacts.length }})</span>
              <span class="accordion-icon">{{ expandedSection === 'eventContacts' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'eventContacts'" class="accordion-content">
              <div class="contact-list">
                <div v-for="(contact, index) in eventContacts" :key="index" class="contact-item">
                  <div class="contact-info">
                    <div class="contact-name">{{ contact.name }}</div>
                    <div v-if="contact.role" class="contact-role">{{ contact.role }}</div>
                  </div>
                  <div class="contact-actions">
                    <a v-if="contact.phone" :href="`tel:${contact.phone}`" class="contact-link">
                      <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"/>
                      </svg>
                      Call
                    </a>
                    <a v-if="contact.phone" :href="`sms:${contact.phone}`" class="contact-link">
                      <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/>
                      </svg>
                      Text
                    </a>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Map Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'map' }"
              @click="toggleSection('map')"
            >
              <span class="accordion-title">
                Map
                <span v-if="userLocation" class="location-status active">GPS Active</span>
                <span v-else class="location-status">GPS Inactive</span>
              </span>
              <span class="accordion-icon">{{ expandedSection === 'map' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'map'" class="accordion-content map-content">
              <MapView
                :locations="allLocations"
                :center="mapCenter"
                :zoom="15"
                :user-location="userLocation"
                :highlight-location-id="primaryAssignment?.locationId"
                :marshal-mode="true"
              />
            </div>
          </div>
        </div>

        <!-- Area Lead Section (if user is an area lead) -->
        <AreaLeadSection
          v-if="isAreaLead"
          :event-id="route.params.eventId"
          :area-ids="areaLeadAreaIds"
          :marshal-id="currentMarshalId"
          @checklist-updated="loadChecklist"
        />
      </div>
    </div>

    <!-- Emergency Contact Modal -->
    <EmergencyContactModal
      :show="showEmergency"
      :contacts="event?.emergencyContacts || []"
      @close="showEmergency = false"
    />

    <ConfirmModal
      :show="showConfirmModal"
      :title="confirmModalTitle"
      :message="confirmModalMessage"
      @confirm="handleConfirmModalConfirm"
      @cancel="handleConfirmModalCancel"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { authApi, checkInApi, checklistApi, eventsApi, assignmentsApi, locationsApi, areasApi } from '../services/api';
import MapView from '../components/MapView.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import EmergencyContactModal from '../components/event-manage/modals/EmergencyContactModal.vue';
import AreaLeadSection from '../components/AreaLeadSection.vue';

const route = useRoute();
const router = useRouter();

// Auth state
const isAuthenticated = ref(false);
const authenticating = ref(false);
const loginError = ref(null);
const currentPerson = ref(null);
const currentMarshalId = ref(null);
const userClaims = ref(null);

// Area lead state
const areaLeadAreaIds = computed(() => {
  if (!userClaims.value?.eventRoles) return [];
  const areaLeadRoles = userClaims.value.eventRoles.filter(r => r.role === 'EventAreaLead');
  return areaLeadRoles.flatMap(r => r.areaIds || []);
});

const isAreaLead = computed(() => areaLeadAreaIds.value.length > 0);

// Check if user is an event admin
const isEventAdmin = computed(() => {
  if (!userClaims.value?.eventRoles) return false;
  return userClaims.value.eventRoles.some(r => r.role === 'EventAdmin');
});

// Check if user can switch to admin mode (logged in via SecureEmailLink AND is admin)
const canSwitchToAdmin = computed(() => {
  return userClaims.value?.authMethod === 'SecureEmailLink' && isEventAdmin.value;
});

// Check if user is admin but logged in via magic code (needs re-auth for admin)
const isAdminButNeedsReauth = computed(() => {
  return userClaims.value?.authMethod === 'MarshalMagicCode' && isEventAdmin.value;
});

// Switch to admin mode
const switchToAdminMode = () => {
  router.push(`/admin/events/${route.params.eventId}`);
};

// Event data
const event = ref(null);
const allLocations = ref([]);
const assignments = ref([]);
const areas = ref([]);
const loading = ref(true);

// Accordion state
const expandedSection = ref('assignments');

// Check-in state
const checkingIn = ref(null); // Now stores the assignment ID being checked in
const checkingInAssignment = ref(null);
const checkInError = ref(null);

// Checklist state
const checklistItems = ref([]);
const checklistLoading = ref(false);
const checklistError = ref(null);
const savingChecklist = ref(false);

// UI state
const showEmergency = ref(false);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

// GPS tracking
const userLocation = ref(null);
let locationWatchId = null;

// Toggle accordion section
const toggleSection = (section) => {
  expandedSection.value = expandedSection.value === section ? null : section;
};

// Assignments with details (location info, area name, all marshals on checkpoint)
const assignmentsWithDetails = computed(() => {
  return assignments.value.map(assign => {
    const location = allLocations.value.find(loc => loc.id === assign.locationId);

    // Get area name from the location's area IDs
    const areaIds = location?.areaIds || location?.AreaIds || [];
    let areaName = null;
    if (areaIds.length > 0 && areas.value.length > 0) {
      const area = areas.value.find(a => areaIds.includes(a.id));
      areaName = area?.name;
    }

    // Get all marshals assigned to this checkpoint
    const allMarshals = location?.assignments || [];

    return {
      ...assign,
      location,
      areaName,
      allMarshals,
      locationName: location?.name || 'Unknown Location',
    };
  });
});

// Primary assignment (first one) for map highlighting
const primaryAssignment = computed(() => {
  return assignments.value.length > 0 ? assignments.value[0] : null;
});

const assignedLocation = computed(() => {
  if (!primaryAssignment.value || !allLocations.value.length) return null;
  return allLocations.value.find(loc => loc.id === primaryAssignment.value.locationId);
});

// Event contacts (emergency contacts from event)
const eventContacts = computed(() => {
  return event.value?.emergencyContacts || [];
});

// Area contacts - loaded from API, grouped by area
const areaContactsRaw = ref([]);

// Area contacts grouped by area
const areaContactsByArea = computed(() => {
  if (areaContactsRaw.value.length === 0) return [];

  // Group by area
  const grouped = {};
  for (const contact of areaContactsRaw.value) {
    const key = contact.areaId || 'unknown';
    if (!grouped[key]) {
      grouped[key] = {
        areaId: contact.areaId,
        areaName: contact.areaName || 'Unknown Area',
        contacts: [],
      };
    }
    grouped[key].contacts.push(contact);
  }

  return Object.values(grouped);
});

// Total area contacts count
const totalAreaContacts = computed(() => {
  return areaContactsRaw.value.length;
});

const mapCenter = computed(() => {
  if (assignedLocation.value) {
    return {
      lat: assignedLocation.value.latitude,
      lng: assignedLocation.value.longitude,
    };
  }
  if (userLocation.value) {
    return userLocation.value;
  }
  if (allLocations.value.length > 0) {
    return {
      lat: allLocations.value[0].latitude,
      lng: allLocations.value[0].longitude,
    };
  }
  return { lat: 51.505, lng: -0.09 };
});

const authenticateWithMagicCode = async (eventId, code) => {
  authenticating.value = true;
  loginError.value = null;
  console.log('Authenticating with magic code:', { eventId, code });

  try {
    const response = await authApi.marshalLogin(eventId, code);
    console.log('Auth response:', response.data);

    if (response.data.success) {
      // Store session token
      localStorage.setItem('sessionToken', response.data.sessionToken);
      localStorage.setItem(`marshal_${eventId}`, response.data.marshalId);

      currentPerson.value = response.data.person;
      currentMarshalId.value = response.data.marshalId;
      isAuthenticated.value = true;
      console.log('Authenticated as marshal:', currentMarshalId.value, currentPerson.value?.name);

      // Fetch full claims to get role information
      try {
        const claimsResponse = await authApi.getMe(eventId);
        userClaims.value = claimsResponse.data;
        console.log('User claims:', userClaims.value);
      } catch (claimsError) {
        console.warn('Failed to fetch claims:', claimsError);
      }

      // Clear the code from URL to prevent re-authentication attempts
      router.replace({ path: route.path, query: {} });

      // Load data after authentication
      await loadEventData();
    } else {
      loginError.value = response.data.message || 'Authentication failed';
    }
  } catch (error) {
    console.error('Authentication failed:', error);
    loginError.value = error.response?.data?.message || 'Invalid or expired login link';
  } finally {
    authenticating.value = false;
  }
};

const checkExistingSession = async (eventId) => {
  const sessionToken = localStorage.getItem('sessionToken');
  const marshalId = localStorage.getItem(`marshal_${eventId}`);

  if (!sessionToken || !marshalId) {
    return false;
  }

  try {
    const response = await authApi.getMe(eventId);

    if (response.data && response.data.marshalId) {
      userClaims.value = response.data;
      currentPerson.value = {
        personId: response.data.personId,
        name: response.data.personName,
        email: response.data.personEmail,
      };
      currentMarshalId.value = response.data.marshalId;
      isAuthenticated.value = true;
      return true;
    }
  } catch (error) {
    // Session invalid, clear it
    localStorage.removeItem('sessionToken');
    localStorage.removeItem(`marshal_${eventId}`);
  }

  return false;
};

const loadEventData = async () => {
  const eventId = route.params.eventId;
  console.log('Loading event data for:', { eventId, marshalId: currentMarshalId.value });

  // Load event details
  try {
    console.log('Fetching event...');
    const eventResponse = await eventsApi.getById(eventId);
    event.value = eventResponse.data;
    console.log('Event loaded:', event.value?.name);
  } catch (error) {
    console.error('Failed to load event:', error.response?.status, error.response?.data);
  }

  // Load areas for the event
  try {
    console.log('Fetching areas...');
    const areasResponse = await areasApi.getByEvent(eventId);
    areas.value = areasResponse.data || [];
    console.log('Areas loaded:', areas.value.length);
  } catch (error) {
    console.error('Failed to load areas:', error.response?.status, error.response?.data);
  }

  // Load locations with assignments using event status endpoint
  try {
    console.log('Fetching event status (locations + assignments)...');
    const statusResponse = await assignmentsApi.getEventStatus(eventId);
    allLocations.value = statusResponse.data?.locations || [];
    console.log('Locations loaded:', allLocations.value.length);

    // Find ALL of current marshal's assignments from all locations
    if (currentMarshalId.value) {
      const myAssignments = [];
      for (const location of allLocations.value) {
        const myAssignment = location.assignments?.find(a => a.marshalId === currentMarshalId.value);
        if (myAssignment) {
          myAssignments.push(myAssignment);
          console.log('Assignment found:', myAssignment.id, 'at location:', location.name);
        }
      }
      assignments.value = myAssignments;
      console.log('Total assignments found:', assignments.value.length);
    }
  } catch (error) {
    console.error('Failed to load event status:', error.response?.status, error.response?.data);
  }

  // Load checklist
  await loadChecklist();

  // Load area contacts for the checkpoint's areas
  await loadAreaContacts();
};

const loadAreaContacts = async () => {
  // Get unique area IDs from all assignments
  const uniqueAreaIds = new Set();
  for (const assign of assignments.value) {
    const location = allLocations.value.find(loc => loc.id === assign.locationId);
    const areaIds = location?.areaIds || location?.AreaIds || [];
    areaIds.forEach(id => uniqueAreaIds.add(id));
  }

  if (uniqueAreaIds.size === 0) {
    console.log('No area IDs for any checkpoint');
    return;
  }
  console.log('Loading area contacts for areas:', [...uniqueAreaIds]);

  const eventId = route.params.eventId;
  const allContacts = [];

  for (const areaId of uniqueAreaIds) {
    try {
      // Fetch the full area details which includes contacts
      const response = await areasApi.getById(eventId, areaId);
      const area = response.data;
      // Handle both camelCase and PascalCase
      const contacts = area?.contacts || area?.Contacts || [];
      console.log(`Area ${area?.name || areaId} has ${contacts.length} contacts`);

      if (contacts.length > 0) {
        allContacts.push(...contacts.map(contact => ({
          marshalId: contact.marshalId || contact.MarshalId,
          marshalName: contact.marshalName || contact.MarshalName,
          role: contact.role || contact.Role,
          phone: contact.phone || contact.Phone,
          email: contact.email || contact.Email,
          areaId: areaId,
          areaName: area?.name || area?.Name,
        })));
      }
    } catch (error) {
      console.warn(`Failed to load area contacts for area ${areaId}:`, error);
    }
  }

  areaContactsRaw.value = allContacts;
  console.log('Area contacts loaded:', areaContactsRaw.value.length);
};

const loadChecklist = async () => {
  if (!currentMarshalId.value) {
    console.warn('No marshal ID, skipping checklist load');
    return;
  }

  checklistLoading.value = true;
  checklistError.value = null;

  try {
    console.log('Fetching checklist for marshal:', currentMarshalId.value);
    const response = await checklistApi.getMarshalChecklist(route.params.eventId, currentMarshalId.value);
    checklistItems.value = response.data || [];
    console.log('Checklist loaded:', checklistItems.value.length, 'items');
  } catch (error) {
    console.error('Failed to load checklist:', error.response?.status, error.response?.data);
    checklistError.value = error.response?.data?.message || 'Failed to load checklist';
  } finally {
    checklistLoading.value = false;
  }
};

const handleToggleChecklist = async (item) => {
  if (savingChecklist.value) return;

  savingChecklist.value = true;

  try {
    if (item.isCompleted) {
      // Uncomplete
      await checklistApi.uncomplete(route.params.eventId, item.itemId, {
        marshalId: currentMarshalId.value,
        contextType: item.completionContextType,
        contextId: item.completionContextId,
      });
    } else {
      // Complete
      await checklistApi.complete(route.params.eventId, item.itemId, {
        marshalId: currentMarshalId.value,
        contextType: item.completionContextType,
        contextId: item.completionContextId,
      });
    }

    // Reload checklist to get updated state
    await loadChecklist();
  } catch (error) {
    console.error('Failed to toggle checklist item:', error);
    // Show error temporarily
    checklistError.value = error.response?.data?.message || 'Failed to update checklist';
    setTimeout(() => {
      checklistError.value = null;
    }, 3000);
  } finally {
    savingChecklist.value = false;
  }
};

const getContextName = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return null;
  }

  if (item.completionContextType === 'Checkpoint') {
    const location = allLocations.value.find(l => l.id === item.completionContextId);
    if (!location) return null;
    return `at ${location.name}`;
  }

  if (item.completionContextType === 'Personal') {
    return 'personal item';
  }

  return null;
};

const startLocationTracking = () => {
  if (!('geolocation' in navigator)) {
    console.warn('Geolocation not supported');
    return;
  }

  console.log('Starting location tracking...');

  locationWatchId = navigator.geolocation.watchPosition(
    (position) => {
      userLocation.value = {
        lat: position.coords.latitude,
        lng: position.coords.longitude,
      };
      console.log('Location updated:', userLocation.value);
    },
    (error) => {
      // Provide helpful error messages
      const errorMessages = {
        1: 'Location permission denied. Please allow location access in your browser settings.',
        2: 'Location unavailable. Make sure GPS is enabled on your device.',
        3: 'Location request timed out. Please try again.',
      };
      console.warn('Geolocation error:', errorMessages[error.code] || error.message);
    },
    {
      enableHighAccuracy: true,
      timeout: 15000,
      maximumAge: 10000,
    }
  );
};

const stopLocationTracking = () => {
  if (locationWatchId !== null) {
    navigator.geolocation.clearWatch(locationWatchId);
    locationWatchId = null;
  }
};

const handleCheckIn = async (assign) => {
  checkingIn.value = assign.id;
  checkingInAssignment.value = assign.id;
  checkInError.value = null;

  try {
    if (!('geolocation' in navigator)) {
      throw new Error('Geolocation is not supported by your browser');
    }

    const position = await new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 10000,
      });
    });

    const response = await checkInApi.checkIn({
      eventId: route.params.eventId,
      assignmentId: assign.id,
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      manualCheckIn: false,
    });

    // Update the assignment in the list
    const index = assignments.value.findIndex(a => a.id === assign.id);
    if (index !== -1) {
      assignments.value[index] = response.data;
    }
  } catch (error) {
    if (error.response?.data?.message) {
      checkInError.value = error.response.data.message;
    } else if (error.message) {
      checkInError.value = error.message;
    } else {
      checkInError.value = 'Failed to check in. Please try manual check-in.';
    }
  } finally {
    checkingIn.value = null;
    checkingInAssignment.value = null;
  }
};

const handleManualCheckIn = (assign) => {
  confirmModalTitle.value = 'Manual Check-In';
  confirmModalMessage.value = 'Are you sure you want to check in manually? This should only be used if GPS is not available.';
  confirmModalCallback.value = async () => {
    checkingIn.value = assign.id;
    checkingInAssignment.value = assign.id;
    checkInError.value = null;

    try {
      const response = await checkInApi.checkIn({
        eventId: route.params.eventId,
        assignmentId: assign.id,
        latitude: null,
        longitude: null,
        manualCheckIn: true,
      });

      // Update the assignment in the list
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = response.data;
      }
    } catch (error) {
      checkInError.value = 'Failed to check in manually. Please contact the admin.';
    } finally {
      checkingIn.value = null;
      checkingInAssignment.value = null;
    }
  };
  showConfirmModal.value = true;
};

const handleConfirmModalConfirm = () => {
  showConfirmModal.value = false;
  if (confirmModalCallback.value) {
    confirmModalCallback.value();
    confirmModalCallback.value = null;
  }
};

const handleConfirmModalCancel = () => {
  showConfirmModal.value = false;
  confirmModalCallback.value = null;
};

const handleLogout = async () => {
  try {
    await authApi.logout();
  } catch (error) {
    // Ignore logout errors
  }

  localStorage.removeItem('sessionToken');
  localStorage.removeItem(`marshal_${route.params.eventId}`);

  isAuthenticated.value = false;
  currentPerson.value = null;
  currentMarshalId.value = null;
  assignments.value = [];
  checklistItems.value = [];
  areaContactsRaw.value = [];

  // Redirect to home
  router.push('/');
};

const formatTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString();
};

const formatTimeRange = (startTime, endTime) => {
  if (!startTime && !endTime) return '';
  const start = startTime ? formatTime(startTime) : '?';
  const end = endTime ? formatTime(endTime) : '?';
  return `${start} - ${end}`;
};

onMounted(async () => {
  const eventId = route.params.eventId;
  const magicCode = route.query.code;

  loading.value = true;

  try {
    // Check for magic code in URL
    if (magicCode) {
      await authenticateWithMagicCode(eventId, magicCode);
    } else {
      // Check for existing session
      const hasSession = await checkExistingSession(eventId);

      if (hasSession) {
        await loadEventData();
      }
    }
  } finally {
    loading.value = false;
  }

  // Start location tracking if authenticated
  if (isAuthenticated.value) {
    startLocationTracking();
  }
});

// Watch for authentication changes to start/stop location tracking
watch(isAuthenticated, (authenticated) => {
  if (authenticated) {
    startLocationTracking();
  } else {
    stopLocationTracking();
  }
});

onUnmounted(() => {
  stopLocationTracking();
});
</script>

<style scoped>
.marshal-view {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.header {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  padding: 1.5rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: white;
}

.header h1 {
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 0.75rem;
}

.btn-emergency {
  background: #ff4444;
  color: white;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-emergency:hover {
  background: #cc0000;
}

.btn-logout {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.3);
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-logout:hover {
  background: rgba(255, 255, 255, 0.3);
}

.container {
  padding: 2rem;
  max-width: 800px;
  margin: 0 auto;
}

.selection-card,
.welcome-card,
.assignment-card,
.checklist-card,
.contacts-card,
.map-card {
  background: white;
  border-radius: 12px;
  padding: 2rem;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
  margin-bottom: 2rem;
}

.error-card {
  border: 2px solid #ff4444;
}

.selection-card h2,
.welcome-card h2,
.assignment-card h3,
.checklist-card h3,
.contacts-card h3,
.map-card h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

/* Contact list styles */
.contact-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.contact-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background: #f5f7fa;
  border-radius: 8px;
  gap: 1rem;
}

.contact-name {
  font-weight: 500;
  color: #333;
}

.contact-role {
  font-weight: 400;
  color: #666;
}

.contact-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.contact-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.5rem 0.75rem;
  background: #667eea;
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  transition: background 0.2s;
}

.contact-link:hover {
  background: #5568d3;
}

.contact-icon {
  width: 1rem;
  height: 1rem;
}

.instruction {
  color: #666;
  margin-bottom: 1rem;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: #666;
}

/* Welcome bar - compact horizontal layout */
.welcome-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: white;
  border-radius: 10px;
  padding: 0.75rem 1.25rem;
  margin-bottom: 0.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.welcome-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.welcome-name {
  font-weight: 600;
  color: #333;
  font-size: 1rem;
}

.welcome-email {
  color: #666;
  font-size: 0.8rem;
}

.mode-switch {
  flex-shrink: 0;
}

.btn-mode-switch {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-mode-switch:hover {
  transform: translateY(-1px);
  box-shadow: 0 3px 10px rgba(102, 126, 234, 0.4);
}

.reauth-hint {
  font-size: 0.75rem;
  color: #999;
  font-style: italic;
  cursor: help;
}

.location-info {
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: #f5f7fa;
  border-radius: 8px;
}

.location-info strong {
  font-size: 1.125rem;
  color: #333;
}

.location-info p {
  margin: 0.5rem 0 0 0;
  color: #666;
}

.time-range {
  font-size: 0.9rem;
  color: #667eea;
  font-weight: 500;
}

.no-assignment {
  color: #666;
  font-style: italic;
}

.checked-in-status {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.5rem;
  background: #f1f8f4;
  border: 2px solid #4caf50;
  border-radius: 8px;
}

.check-icon {
  font-size: 2rem;
  color: #4caf50;
}

.checked-in-status strong {
  color: #4caf50;
  font-size: 1.125rem;
}

.check-time,
.check-method {
  margin: 0.25rem 0 0 0;
  color: #666;
  font-size: 0.875rem;
}

.check-in-actions {
  display: flex;
  flex-direction: row;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.btn {
  padding: 1rem 2rem;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 600;
  font-size: 1rem;
  transition: all 0.3s;
  text-decoration: none;
  display: inline-block;
  text-align: center;
}

.btn-large {
  font-size: 1.125rem;
  padding: 1.25rem 2rem;
}

.btn-primary {
  background: #667eea;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #5568d3;
  transform: translateY(-2px);
}

.btn-secondary {
  background: #e0e0e0;
  color: #333;
}

.btn-secondary:hover:not(:disabled) {
  background: #d0d0d0;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error {
  padding: 1rem;
  background: #fee;
  color: #c33;
  border-radius: 6px;
  font-size: 0.875rem;
}

/* Checklist styles */
.checklist-items {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.checklist-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  transition: all 0.2s;
}

.checklist-item.item-completed {
  background: #f1f8f4;
  border-color: #c8e6c9;
}

.item-checkbox {
  display: flex;
  align-items: flex-start;
  padding-top: 0.25rem;
}

.item-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1.2rem;
  height: 1.2rem;
  flex-shrink: 0;
}

.item-checkbox input[type="checkbox"]:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.item-text {
  font-size: 0.95rem;
  color: #333;
}

.item-context {
  font-size: 0.85rem;
  color: #667eea;
  font-weight: 500;
}

.completion-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: 0.8rem;
  color: #666;
}

.completion-text {
  color: #4caf50;
}

.completion-time {
  color: #999;
}

.empty-state {
  text-align: center;
  padding: 1.5rem;
  color: #666;
  font-style: italic;
}

/* Map card */
.map-card {
  height: auto;
}

.map-card h3 {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.location-status {
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  border-radius: 12px;
  background: #e0e0e0;
  color: #666;
  font-weight: 500;
}

.location-status.active {
  background: #e8f5e9;
  color: #4caf50;
}

.map-card :deep(.map-container) {
  height: 400px;
  margin-top: 1rem;
}

/* Accordion styles */
.accordion {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.accordion-section {
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  background: white;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: #333;
  transition: background 0.2s;
}

.accordion-header:hover {
  background: #f8f9fa;
}

.accordion-header.active {
  background: #f0f4ff;
  color: #667eea;
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: #667eea;
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid #e0e0e0;
}

.map-content {
  padding-bottom: 0;
}

.map-content :deep(.map-container) {
  height: 350px;
  border-radius: 0 0 12px 12px;
  margin: 0 -1.5rem -1.5rem;
}

/* Assignment list styles */
.assignments-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.assignment-item {
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 10px;
  padding: 1rem;
}

.assignment-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
  flex-wrap: wrap;
}

.assignment-header strong {
  font-size: 1.1rem;
  color: #333;
}

.assignment-description {
  color: #666;
  font-size: 0.9rem;
  margin: 0 0 0.5rem 0;
}

.area-badge {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  background: #667eea;
  color: white;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: 12px;
}

/* Checkpoint marshals list */
.checkpoint-marshals {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: #fff;
  border-radius: 8px;
}

.marshals-label {
  font-size: 0.8rem;
  color: #666;
  margin-bottom: 0.5rem;
  font-weight: 500;
}

.marshals-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.marshal-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.3rem 0.6rem;
  background: #e8e8e8;
  border-radius: 16px;
  font-size: 0.85rem;
  color: #555;
}

.marshal-tag.is-you {
  background: #e3e9ff;
  color: #667eea;
  font-weight: 500;
}

.marshal-tag.checked-in {
  background: #e8f5e9;
  color: #4caf50;
}

.marshal-tag.is-you.checked-in {
  background: linear-gradient(135deg, #e3e9ff 50%, #e8f5e9 50%);
}

.check-badge {
  font-size: 0.75rem;
}

/* Area contacts group */
.area-contacts-group {
  margin-bottom: 1.5rem;
}

.area-contacts-group:last-child {
  margin-bottom: 0;
}

.area-group-title {
  margin: 0 0 0.75rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid #667eea;
  color: #333;
  font-size: 1rem;
}

.contact-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.contact-detail {
  font-size: 0.85rem;
  color: #555;
}

@media (max-width: 768px) {
  .header {
    flex-direction: column;
    gap: 1rem;
    text-align: center;
  }

  .header h1 {
    font-size: 1.25rem;
  }

  .container {
    padding: 1rem;
  }

  .selection-card,
  .welcome-card,
  .assignment-card,
  .checklist-card,
  .contacts-card,
  .map-card {
    padding: 1.5rem;
  }

  .map-card :deep(.map-container) {
    height: 300px;
  }

  .contact-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .contact-actions {
    width: 100%;
  }

  .contact-link {
    flex: 1;
    justify-content: center;
  }

  /* Accordion mobile adjustments */
  .accordion-header {
    padding: 1rem;
  }

  .accordion-content {
    padding: 1rem;
  }

  .map-content :deep(.map-container) {
    height: 300px;
    margin: 0 -1rem -1rem;
  }

  .assignment-item {
    padding: 0.75rem;
  }

  .check-in-actions {
    flex-direction: row;
    gap: 0.5rem;
  }

  .check-in-actions .btn {
    padding: 0.75rem 1rem;
    font-size: 0.9rem;
    flex: 1;
  }
}
</style>
