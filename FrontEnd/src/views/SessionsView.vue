<template>
  <div class="sessions-view">
    <div class="sessions-container">
      <h1 class="page-title">My events</h1>

      <div v-if="loading" class="loading-state">
        <p>Loading sessions...</p>
      </div>

      <div v-else-if="sessions.length === 0 && adminEvents.length === 0 && demoEvents.length === 0" class="empty-state">
        <p>No active sessions.</p>
        <p class="hint">Sign in to access your events, or use a login link from your event organiser.</p>
        <div class="empty-state-buttons">
          <button class="btn btn-primary" @click="showLoginModal = true">
            Sign in
          </button>
          <button
            class="btn btn-secondary"
            @click="handleCreateSampleEvent"
            :disabled="isCreatingSample"
          >
            <span v-if="isCreatingSample">Creating...</span>
            <span v-else>Try a demo</span>
          </button>
        </div>
        <p v-if="sampleError" class="sample-error">{{ sampleError }}</p>
      </div>

      <div v-else class="sessions-list">
        <!-- Sign in and demo buttons when no admin session -->
        <div v-if="!hasAdminSession" class="sign-in-section">
          <button class="btn btn-primary" @click="showLoginModal = true">
            Sign in
          </button>
          <button
            v-if="showCreateSampleButton"
            class="btn btn-secondary"
            @click="handleCreateSampleEvent"
            :disabled="isCreatingSample"
          >
            <span v-if="isCreatingSample">Creating...</span>
            <span v-else>Try a demo</span>
          </button>
          <p v-if="sampleError" class="sample-error">{{ sampleError }}</p>
        </div>

        <!-- Demo events section -->
        <template v-if="demoEvents.length > 0">
          <div class="section-header">
            <span class="section-label">Demo events</span>
          </div>

          <div
            v-for="demo in demoEvents"
            :key="'demo-' + demo.eventId"
            class="session-card demo-card"
            @click="goToDemoEvent(demo)"
          >
            <div class="card-icon demo-icon">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z"/>
              </svg>
            </div>
            <div class="session-info">
              <div class="event-name">Demo event</div>
              <div class="event-date demo-expiry">
                Expires: {{ formatDemoExpiry(demo.expiresAt) }}
              </div>
              <div class="user-name">Demo admin</div>
            </div>
            <button
              class="btn-logout"
              @click.stop="confirmRemoveDemoEvent(demo)"
              title="Remove demo event"
            >
              &times;
            </button>
          </div>
        </template>

        <!-- Admin section -->
        <template v-if="hasAdminSession">
          <div class="section-header">
            <span class="section-label">
              Admin events
              <span v-if="adminEventsLoading" class="loading-indicator"></span>
            </span>
            <div class="section-actions">
              <button class="btn btn-small btn-primary" @click="goToProfile">
                Profile
              </button>
              <button class="btn btn-small btn-primary" @click="showCreateEvent = true">
                Create event
              </button>
            </div>
          </div>

          <!-- Admin event cards -->
          <div
            v-for="event in adminEvents"
            :key="'admin-' + event.id"
            class="session-card admin-card"
            @click="goToAdminEvent(event.id)"
          >
            <div class="card-icon admin-icon">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm0 10.99h7c-.53 4.12-3.28 7.79-7 8.94V12H5V6.3l7-3.11v8.8z"/>
              </svg>
            </div>
            <div class="session-info">
              <div class="event-name">{{ event.name }}</div>
              <div v-if="event.eventDate" class="event-date">
                {{ formatDateTime(event.eventDate) }}
              </div>
              <div class="user-name">Admin</div>
              <div v-if="getAdminLastAccessed(event.id)" class="last-accessed">
                Last accessed: {{ formatDateTime(getAdminLastAccessed(event.id)) }}
              </div>
            </div>
            <button
              class="btn-logout"
              @click.stop="confirmAdminLogout"
              title="Log out as admin"
            >
              &times;
            </button>
          </div>

          <!-- Show demo button when admin has no events -->
          <div v-if="showCreateSampleButton" class="no-admin-events-hint">
            <p>No events yet? Try a demo to explore the features.</p>
            <button
              class="btn btn-secondary"
              @click="handleCreateSampleEvent"
              :disabled="isCreatingSample"
            >
              <span v-if="isCreatingSample">Creating...</span>
              <span v-else>Try a demo</span>
            </button>
            <p v-if="sampleError" class="sample-error">{{ sampleError }}</p>
          </div>
        </template>

        <!-- Marshal section header -->
        <div v-if="sessions.length > 0" class="section-header">
          <span class="section-label">Marshal sessions</span>
        </div>

        <!-- Marshal sessions -->
        <div
          v-for="session in sessions"
          :key="session.sessionKey"
          class="session-card"
          @click="selectSession(session)"
        >
          <div class="card-icon marshal-icon">
            <svg viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
            </svg>
          </div>
          <div class="session-info">
            <div class="event-name">{{ session.eventName || 'Event' }}</div>
            <div v-if="session.eventDate" class="event-date">
              {{ formatDateTime(session.eventDate) }}
            </div>
            <div class="user-name">{{ session.marshalName || 'Marshal' }}</div>
            <div v-if="session.lastAccessed" class="last-accessed">
              Last accessed: {{ formatDateTime(session.lastAccessed) }}
            </div>
          </div>
          <button
            class="btn-logout"
            @click.stop="confirmRemoveSession(session)"
            title="Log out from this session"
          >
            &times;
          </button>
        </div>
      </div>

    </div>

    <!-- Login Modal -->
    <LoginModal
      :show="showLoginModal"
      @close="showLoginModal = false"
      @success="handleLoginSuccess"
    />

    <!-- Profile Modal -->
    <EditProfileModal
      :show="showProfileModal"
      :profile="profileStore.profile"
      :show-logout="true"
      @close="showProfileModal = false"
      @submit="handleProfileSave"
      @logout="handleProfileLogout"
    />

    <!-- Create Event Modal -->
    <EventFormModal
      :show="showCreateEvent"
      :event-data="eventForm"
      :is-editing="false"
      @close="closeCreateEventModal"
      @submit="handleCreateEvent"
    />

    <!-- Confirm admin logout modal -->
    <Teleport to="body">
      <Transition name="modal">
        <div v-if="showAdminLogoutModal" class="modal-overlay" @click.self="showAdminLogoutModal = false">
          <div class="modal-card">
            <h3>Log out as admin?</h3>
            <p>Logging out <strong>{{ adminEmail }}</strong> from all admin events.</p>
            <p class="hint">You can log back in using the sign-in button.</p>
            <div class="modal-actions">
              <button class="btn btn-secondary" @click="showAdminLogoutModal = false">Cancel</button>
              <button class="btn btn-danger" @click="adminLogoutConfirmed">Log out</button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>

    <!-- Confirm remove marshal session modal -->
    <Teleport to="body">
      <Transition name="modal">
        <div v-if="showConfirmModal" class="modal-overlay" @click.self="showConfirmModal = false">
          <div class="modal-card">
            <h3>Log out from session?</h3>
            <p>Logging out <strong>{{ sessionToRemove?.marshalName || 'Marshal' }}</strong> from <strong>{{ sessionToRemove?.eventName || 'this event' }}</strong>.</p>
            <p class="hint">You can log back in using the link from your event organiser.</p>
            <div class="modal-actions">
              <button class="btn btn-secondary" @click="showConfirmModal = false">Cancel</button>
              <button class="btn btn-danger" @click="removeSessionConfirmed">Log out</button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>

    <!-- Confirm remove demo event modal -->
    <Teleport to="body">
      <Transition name="modal">
        <div v-if="showDemoRemoveModal" class="modal-overlay" @click.self="showDemoRemoveModal = false">
          <div class="modal-card">
            <h3>Remove demo event?</h3>
            <p>This will remove the demo event from your list.</p>
            <p class="hint">You can create a new demo event from the home page.</p>
            <div class="modal-actions">
              <button class="btn btn-secondary" @click="showDemoRemoveModal = false">Cancel</button>
              <button class="btn btn-danger" @click="removeDemoEventConfirmed">Remove</button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import LoginModal from '../components/LoginModal.vue';
import EditProfileModal from '../components/EditProfileModal.vue';
import EventFormModal from '../components/event-manage/modals/EventFormModal.vue';
import {
  getSessionsForDisplay,
  cleanupOldSessions,
  migrateLegacyStorage,
  removeSession,
  touchSession
} from '../services/marshalSessionService';
import { setAuthContext, eventsApi, sampleEventsApi } from '../services/api';
import { useAuthStore } from '../stores/auth';
import { useEventsStore } from '../stores/events';
import { useProfileStore } from '../stores/profile';
import { DEFAULT_TIME_ZONE } from '../constants/timeZones';

const router = useRouter();
const authStore = useAuthStore();
const eventsStore = useEventsStore();
const profileStore = useProfileStore();

const sessions = ref([]);
const adminEvents = ref([]);
const demoEvents = ref([]);
const loading = ref(true);
const adminEventsLoading = ref(false);
const showLoginModal = ref(false);
const showProfileModal = ref(false);
const showConfirmModal = ref(false);
const showAdminLogoutModal = ref(false);
const showDemoRemoveModal = ref(false);
const sessionToRemove = ref(null);
const demoToRemove = ref(null);

// Create event modal state
const showCreateEvent = ref(false);
const eventForm = ref({
  name: '',
  description: '',
  eventDate: '',
  timeZoneId: DEFAULT_TIME_ZONE,
  emergencyContacts: [],
});

const adminEmail = computed(() => authStore.adminEmail);
const hasAdminSession = computed(() => !!adminEmail.value);

// Show create sample button when user has no admin events
const showCreateSampleButton = computed(() => {
  // Don't show if they already have a demo event
  if (demoEvents.value.length > 0) return false;
  // Show if not logged in as admin, or logged in but no events
  return !hasAdminSession.value || adminEvents.value.length === 0;
});

const isCreatingSample = ref(false);
const sampleError = ref('');

const getAdminLastAccessed = (eventId) => {
  const accessTimes = JSON.parse(localStorage.getItem('adminEventAccessTimes') || '{}');
  return accessTimes[eventId] || null;
};

const loadSessions = () => {
  sessions.value = getSessionsForDisplay();
};

const loadDemoEvents = async () => {
  const demos = [];
  const prefix = 'sampleEvent_';
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key && key.startsWith(prefix)) {
      try {
        const data = JSON.parse(localStorage.getItem(key));
        if (data && data.adminCode && data.expiresAt) {
          const expiresAt = new Date(data.expiresAt);
          if (expiresAt > new Date()) {
            // Valid, unexpired demo event
            const eventId = key.substring(prefix.length);
            demos.push({
              eventId,
              adminCode: data.adminCode,
              expiresAt: data.expiresAt,
              storageKey: key
            });
          } else {
            // Expired, clean it up
            localStorage.removeItem(key);
          }
        }
      } catch {
        // Invalid JSON, remove it
        localStorage.removeItem(key);
      }
    }
  }

  // If no demo events found but we have a device fingerprint, try to recover from backend
  if (demos.length === 0) {
    const fingerprint = localStorage.getItem('deviceFingerprint');
    if (fingerprint) {
      try {
        const response = await sampleEventsApi.recover(fingerprint);
        const { eventId, adminCode, expiresAt } = response.data;

        // Store the recovered credentials in localStorage
        const storageKey = `sampleEvent_${eventId}`;
        localStorage.setItem(storageKey, JSON.stringify({
          adminCode,
          expiresAt
        }));

        demos.push({
          eventId,
          adminCode,
          expiresAt,
          storageKey
        });
      } catch {
        // No demo event to recover, that's fine
      }
    }
  }

  demoEvents.value = demos;
};

const goToDemoEvent = (demo) => {
  router.push(`/admin/event/${demo.eventId}?sample=${demo.adminCode}`);
};

// Get or create device fingerprint for sample event rate limiting
const getDeviceFingerprint = () => {
  let fingerprint = localStorage.getItem('deviceFingerprint');
  if (!fingerprint) {
    // Generate a simple fingerprint based on available browser info
    const nav = navigator;
    const screen = window.screen;
    const data = [
      nav.userAgent,
      nav.language,
      screen.width,
      screen.height,
      screen.colorDepth,
      new Date().getTimezoneOffset(),
      nav.hardwareConcurrency || 'unknown',
      nav.deviceMemory || 'unknown'
    ].join('|');

    // Simple hash function
    let hash = 0;
    for (let i = 0; i < data.length; i++) {
      const char = data.charCodeAt(i);
      hash = ((hash << 5) - hash) + char;
      hash = hash & hash;
    }
    fingerprint = Math.abs(hash).toString(36);
    localStorage.setItem('deviceFingerprint', fingerprint);
  }
  return fingerprint;
};

const handleCreateSampleEvent = async () => {
  sampleError.value = '';
  isCreatingSample.value = true;

  const fingerprint = getDeviceFingerprint();

  try {
    const timeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const response = await sampleEventsApi.create(fingerprint, timeZoneId);

    const { eventId, adminCode, expiresAt } = response.data;

    // Store credentials in localStorage for recovery
    localStorage.setItem(`sampleEvent_${eventId}`, JSON.stringify({
      adminCode,
      expiresAt
    }));

    // Navigate to the sample event
    router.push(`/admin/event/${eventId}?sample=${adminCode}`);
  } catch (error) {
    console.error('Failed to create sample event:', error);
    if (error.response?.status === 429) {
      sampleError.value = error.response.data?.message || 'Rate limit exceeded. Please try again later.';
    } else {
      sampleError.value = 'Failed to create demo event. Please try again.';
    }
  } finally {
    isCreatingSample.value = false;
  }
};

const formatDemoExpiry = (expiresAt) => {
  const expiry = new Date(expiresAt);
  const now = new Date();
  const diffMs = expiry - now;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const remainingMins = diffMins % 60;

  if (diffHours > 0) {
    return `${diffHours}h ${remainingMins}m`;
  } else if (diffMins > 0) {
    return `${diffMins}m`;
  } else {
    return 'Soon';
  }
};

const confirmRemoveDemoEvent = (demo) => {
  demoToRemove.value = demo;
  showDemoRemoveModal.value = true;
};

const removeDemoEventConfirmed = () => {
  if (!demoToRemove.value) return;

  // Remove from localStorage
  localStorage.removeItem(demoToRemove.value.storageKey);

  // Refresh list
  loadDemoEvents();

  // Close modal
  showDemoRemoveModal.value = false;
  demoToRemove.value = null;
};

const selectSession = (session) => {
  // Touch this session to make it the most recent for this event
  touchSession(session.eventId, session.marshalId);
  router.push(`/event/${session.eventId}`);
};

const confirmRemoveSession = (session) => {
  sessionToRemove.value = session;
  showConfirmModal.value = true;
};

const removeSessionConfirmed = async () => {
  if (!sessionToRemove.value) return;

  const { sessionKey, eventId } = sessionToRemove.value;

  // Try to logout from backend
  try {
    setAuthContext('marshal', eventId);
    await authApi.logout();
  } catch (error) {
    // Ignore logout errors
  }

  // Remove session locally
  removeSession(sessionKey);

  // Clear auth context
  setAuthContext(null);

  // Refresh list
  loadSessions();

  // Close modal
  showConfirmModal.value = false;
  sessionToRemove.value = null;
};

const confirmAdminLogout = () => {
  showAdminLogoutModal.value = true;
};

const adminLogoutConfirmed = async () => {
  await authStore.logout();
  localStorage.removeItem('adminEventAccessTimes');
  localStorage.removeItem(ADMIN_EVENTS_CACHE_KEY);
  adminEvents.value = [];
  showAdminLogoutModal.value = false;
};

const goToProfile = async () => {
  showProfileModal.value = true;
  // Load profile if not already loaded
  if (!profileStore.profile) {
    try {
      await profileStore.fetchProfile();
    } catch (error) {
      console.error('Failed to load profile:', error);
    }
  }
};

const handleProfileSave = async (formData) => {
  try {
    await profileStore.updateProfile(formData);
    showProfileModal.value = false;
  } catch (error) {
    console.error('Failed to save profile:', error);
    alert('Failed to save profile. Please try again.');
  }
};

const handleProfileLogout = async () => {
  await authStore.logout();
  localStorage.removeItem('adminEventAccessTimes');
  localStorage.removeItem(ADMIN_EVENTS_CACHE_KEY);
  adminEvents.value = [];
  showProfileModal.value = false;
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit'
  });
};

const ADMIN_EVENTS_CACHE_KEY = 'cachedAdminEvents';

const loadCachedAdminEvents = () => {
  if (!hasAdminSession.value) return;

  try {
    const cached = localStorage.getItem(ADMIN_EVENTS_CACHE_KEY);
    if (cached) {
      const { email, events } = JSON.parse(cached);
      // Only use cache if it's for the current admin
      if (email === adminEmail.value && Array.isArray(events)) {
        adminEvents.value = events;
      }
    }
  } catch (error) {
    // Ignore cache read errors
  }
};

const saveAdminEventsCache = (events) => {
  try {
    localStorage.setItem(ADMIN_EVENTS_CACHE_KEY, JSON.stringify({
      email: adminEmail.value,
      events
    }));
  } catch (error) {
    // Ignore cache write errors
  }
};

const loadAdminEvents = async () => {
  if (!hasAdminSession.value) return;

  adminEventsLoading.value = true;
  try {
    setAuthContext('admin');
    // Try summary endpoint first, fall back to full endpoint if not available
    let response;
    try {
      response = await eventsApi.getAllSummary();
    } catch (summaryError) {
      // Summary endpoint might not exist yet, fall back to full endpoint
      response = await eventsApi.getAll();
    }
    const events = response.data || [];
    adminEvents.value = events;
    saveAdminEventsCache(events);
  } catch (error) {
    console.error('Failed to load admin events:', error);
    // Keep cached events if fetch fails
    if (adminEvents.value.length === 0) {
      adminEvents.value = [];
    }
  } finally {
    adminEventsLoading.value = false;
  }
};

const goToAdminEvent = (eventId) => {
  // Update last accessed timestamp for this specific event
  const accessTimes = JSON.parse(localStorage.getItem('adminEventAccessTimes') || '{}');
  accessTimes[eventId] = new Date().toISOString();
  localStorage.setItem('adminEventAccessTimes', JSON.stringify(accessTimes));
  router.push(`/admin/event/${eventId}`);
};

const handleLoginSuccess = () => {
  // Login email sent - modal will show confirmation
  // User will receive email and verify via AdminVerify which redirects here
};

// Watch for admin session changes and load events when user logs in
watch(hasAdminSession, async (newValue, oldValue) => {
  if (newValue && !oldValue) {
    await loadAdminEvents();
  }
});

// Create event handlers
const closeCreateEventModal = () => {
  showCreateEvent.value = false;
  eventForm.value = {
    name: '',
    description: '',
    eventDate: '',
    timeZoneId: DEFAULT_TIME_ZONE,
    emergencyContacts: [],
  };
};

const handleCreateEvent = async (formData) => {
  try {
    await eventsStore.createEvent(formData);
    closeCreateEventModal();
    // Refresh admin events list
    await loadAdminEvents();
  } catch (error) {
    console.error('Failed to create event:', error);
    alert('Failed to create event. Please try again.');
  }
};

onMounted(async () => {
  document.title = 'OnTheDay App - My events';

  // Migrate any legacy storage format
  migrateLegacyStorage();

  // Clean up old sessions
  cleanupOldSessions();

  // Load cached data immediately for fast display
  loadSessions();
  loadCachedAdminEvents();

  // Show UI immediately with cached data
  loading.value = false;

  // Load demo events (may need to recover from backend)
  await loadDemoEvents();

  // Load fresh admin events in the background
  loadAdminEvents();
});
</script>

<style scoped>
.sessions-view {
  min-height: 100vh;
  background: linear-gradient(180deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
  color: white;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 1rem;
  padding-top: 4rem;
  overflow-y: auto;
}

.sessions-container {
  max-width: 400px;
  width: 100%;
  text-align: center;
}

.page-title {
  font-size: 1.75rem;
  margin-bottom: 1.5rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.loading-state,
.empty-state {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 2rem 1.5rem;
  margin-bottom: 1.5rem;
}

.empty-state p {
  margin: 0 0 0.5rem;
  color: #ccc;
}

.empty-state .hint {
  font-size: 0.85rem;
  color: #888;
  margin-bottom: 1.5rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin: 1rem 0 0.5rem;
  padding: 0 0.25rem;
}

.section-actions {
  display: flex;
  gap: 0.5rem;
}

.sign-in-section {
  margin-bottom: 1rem;
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  justify-content: center;
}

.empty-state-buttons {
  display: flex;
  gap: 0.75rem;
  justify-content: center;
  flex-wrap: wrap;
}

.sample-error {
  color: #ff6b6b;
  font-size: 0.85rem;
  margin-top: 0.75rem;
}

.no-admin-events-hint {
  background: rgba(255, 255, 255, 0.03);
  border: 1px dashed rgba(255, 255, 255, 0.15);
  border-radius: 12px;
  padding: 1.25rem;
  text-align: center;
}

.no-admin-events-hint p {
  color: #888;
  font-size: 0.9rem;
  margin: 0 0 1rem;
}

.section-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #888;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.loading-indicator {
  width: 12px;
  height: 12px;
  border: 2px solid rgba(102, 126, 234, 0.3);
  border-top-color: #667eea;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.sessions-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1.5rem;
}

.session-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 1rem 1.25rem;
  cursor: pointer;
  transition: all 0.2s;
}

.card-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.card-icon svg {
  width: 28px;
  height: 28px;
}

.admin-icon {
  background: rgba(102, 126, 234, 0.2);
  color: #667eea;
}

.marshal-icon {
  background: rgba(74, 222, 128, 0.2);
  color: #4ade80;
}

.session-card:hover {
  background: rgba(255, 255, 255, 0.1);
  border-color: rgba(102, 126, 234, 0.5);
  transform: translateY(-1px);
}

.session-card.admin-card {
  text-decoration: none;
  color: inherit;
  border-color: rgba(102, 126, 234, 0.3);
  background: rgba(102, 126, 234, 0.1);
}

.session-card.admin-card:hover {
  border-color: rgba(102, 126, 234, 0.6);
  background: rgba(102, 126, 234, 0.15);
}

.session-card.demo-card {
  text-decoration: none;
  color: inherit;
  border-color: rgba(251, 191, 36, 0.3);
  background: rgba(251, 191, 36, 0.1);
}

.session-card.demo-card:hover {
  border-color: rgba(251, 191, 36, 0.6);
  background: rgba(251, 191, 36, 0.15);
}

.demo-icon {
  background: rgba(251, 191, 36, 0.2);
  color: #fbbf24;
}

.demo-expiry {
  color: #fbbf24 !important;
}

.session-info {
  flex: 1;
  text-align: left;
}

.event-name {
  font-weight: 600;
  font-size: 1rem;
  color: white;
  margin-bottom: 0.25rem;
}

.event-date {
  font-size: 0.85rem;
  color: #667eea;
}

.user-name {
  font-size: 0.9rem;
  color: #aaa;
  margin-top: 0.25rem;
}

.last-accessed {
  font-size: 0.75rem;
  color: #666;
  margin-top: 0.25rem;
}

.btn-logout {
  background: none;
  border: none;
  color: #888;
  font-size: 1.5rem;
  line-height: 1;
  padding: 0.25rem 0.5rem;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.btn-logout:hover {
  color: #ff6b6b;
  background: rgba(255, 107, 107, 0.1);
}

/* Buttons */
.btn {
  display: inline-block;
  padding: 0.75rem 1.5rem;
  font-size: 0.9rem;
  text-decoration: none;
  border-radius: 8px;
  border: none;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-small {
  padding: 0.5rem 1rem;
  font-size: 0.8rem;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  font-weight: 600;
}

.btn-primary:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.15);
}

.btn-danger {
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-card {
  background: #1e1e2e;
  border: 1px solid rgba(255, 255, 255, 0.1);
  padding: 1.5rem;
  border-radius: 12px;
  max-width: 350px;
  width: 100%;
  text-align: center;
}

.modal-card h3 {
  margin: 0 0 1rem;
  font-size: 1.25rem;
  color: white;
}

.modal-card p {
  color: #ccc;
  margin: 0 0 0.5rem;
  font-size: 0.9rem;
}

.modal-card .hint {
  color: #888;
  font-size: 0.8rem;
  margin-bottom: 1.5rem;
}

.modal-actions {
  display: flex;
  gap: 0.75rem;
  justify-content: center;
}

.modal-actions .btn {
  flex: 1;
}

/* Modal transitions */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.2s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-active .modal-card,
.modal-leave-active .modal-card {
  transition: transform 0.2s ease;
}

.modal-enter-from .modal-card,
.modal-leave-to .modal-card {
  transform: scale(0.95);
}
</style>
