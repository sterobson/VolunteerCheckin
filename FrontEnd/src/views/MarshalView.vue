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
        <div class="welcome-card">
          <h2>Welcome, {{ currentPerson?.name || 'Marshal' }}!</h2>
          <p v-if="currentPerson?.email" class="email-info">{{ currentPerson.email }}</p>
        </div>

        <!-- Assignment Info -->
        <div v-if="assignment" class="assignment-card">
          <h3>Your Assignment</h3>
          <div class="location-info">
            <strong>{{ assignedLocation?.name }}</strong>
            <p v-if="assignedLocation?.description">{{ assignedLocation.description }}</p>
            <p v-if="assignedLocation?.startTime || assignedLocation?.endTime" class="time-range">
              {{ formatTimeRange(assignedLocation.startTime, assignedLocation.endTime) }}
            </p>
          </div>

          <div v-if="assignment.isCheckedIn" class="checked-in-status">
            <span class="check-icon">✓</span>
            <div>
              <strong>Checked In</strong>
              <p class="check-time">{{ formatTime(assignment.checkInTime) }}</p>
              <p class="check-method">Method: {{ assignment.checkInMethod }}</p>
            </div>
          </div>

          <div v-else class="check-in-actions">
            <button
              @click="handleCheckIn"
              class="btn btn-primary btn-large"
              :disabled="checkingIn"
            >
              {{ checkingIn ? 'Checking in...' : 'Check In with GPS' }}
            </button>

            <button
              @click="handleManualCheckIn"
              class="btn btn-secondary"
              :disabled="checkingIn"
            >
              Manual Check-In
            </button>

            <div v-if="checkInError" class="error">{{ checkInError }}</div>
          </div>
        </div>

        <div v-else class="assignment-card">
          <h3>Your Assignment</h3>
          <p class="no-assignment">You don't have a checkpoint assignment yet.</p>
        </div>

        <!-- Personal Checklist -->
        <div class="checklist-card">
          <h3>Your Checklist</h3>
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

        <!-- Area Lead Section (if user is an area lead) -->
        <AreaLeadSection
          v-if="isAreaLead"
          :event-id="route.params.eventId"
          :area-ids="areaLeadAreaIds"
          :marshal-id="currentMarshalId"
          @checklist-updated="loadChecklist"
        />

        <!-- Map View -->
        <div class="map-card">
          <h3>
            Your Location
            <span v-if="userLocation" class="location-status active">GPS Active</span>
            <span v-else class="location-status">GPS Inactive</span>
          </h3>
          <MapView
            :locations="allLocations"
            :center="mapCenter"
            :zoom="15"
            :user-location="userLocation"
            :highlight-location-id="assignment?.locationId"
            :marshal-mode="true"
          />
        </div>
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
import { authApi, checkInApi, checklistApi, eventsApi, assignmentsApi, locationsApi } from '../services/api';
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

// Event data
const event = ref(null);
const allLocations = ref([]);
const assignment = ref(null);
const loading = ref(true);

// Check-in state
const checkingIn = ref(false);
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

const assignedLocation = computed(() => {
  if (!assignment.value || !allLocations.value.length) return null;
  return allLocations.value.find(loc => loc.id === assignment.value.locationId);
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

  // Load locations with assignments using event status endpoint
  try {
    console.log('Fetching event status (locations + assignments)...');
    const statusResponse = await assignmentsApi.getEventStatus(eventId);
    allLocations.value = statusResponse.data?.locations || [];
    console.log('Locations loaded:', allLocations.value.length);

    // Find current marshal's assignment from all locations
    if (currentMarshalId.value) {
      for (const location of allLocations.value) {
        const myAssignment = location.assignments?.find(a => a.marshalId === currentMarshalId.value);
        if (myAssignment) {
          assignment.value = myAssignment;
          console.log('My assignment found:', assignment.value, 'at location:', location.name);
          break;
        }
      }
      if (!assignment.value) {
        console.log('No assignment found for marshal:', currentMarshalId.value);
      }
    }
  } catch (error) {
    console.error('Failed to load event status:', error.response?.status, error.response?.data);
  }

  // Load checklist
  await loadChecklist();
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

const handleCheckIn = async () => {
  checkingIn.value = true;
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
      assignmentId: assignment.value.id,
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      manualCheckIn: false,
    });

    assignment.value = response.data;
  } catch (error) {
    if (error.response?.data?.message) {
      checkInError.value = error.response.data.message;
    } else if (error.message) {
      checkInError.value = error.message;
    } else {
      checkInError.value = 'Failed to check in. Please try manual check-in.';
    }
  } finally {
    checkingIn.value = false;
  }
};

const handleManualCheckIn = () => {
  confirmModalTitle.value = 'Manual Check-In';
  confirmModalMessage.value = 'Are you sure you want to check in manually? This should only be used if GPS is not available.';
  confirmModalCallback.value = async () => {
    checkingIn.value = true;
    checkInError.value = null;

    try {
      const response = await checkInApi.checkIn({
        eventId: route.params.eventId,
        assignmentId: assignment.value.id,
        latitude: null,
        longitude: null,
        manualCheckIn: true,
      });

      assignment.value = response.data;
    } catch (error) {
      checkInError.value = 'Failed to check in manually. Please contact the admin.';
    } finally {
      checkingIn.value = false;
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
  assignment.value = null;
  checklistItems.value = [];

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
.map-card h3 {
  margin: 0 0 1rem 0;
  color: #333;
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

.email-info {
  color: #666;
  margin: 0;
  font-size: 0.9rem;
}

.welcome-card {
  text-align: center;
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
  flex-direction: column;
  gap: 1rem;
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
  .map-card {
    padding: 1.5rem;
  }

  .map-card :deep(.map-container) {
    height: 300px;
  }
}
</style>
