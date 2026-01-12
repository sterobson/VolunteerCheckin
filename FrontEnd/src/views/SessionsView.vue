<template>
  <div class="sessions-view">
    <div class="sessions-container">
      <!-- Hero animation with title overlay -->
      <HeroHeader title="Your sessions" />

      <div v-if="loading" class="loading-state">
        <p>Loading sessions...</p>
      </div>

      <div v-else-if="sessions.length === 0 && adminEvents.length === 0" class="empty-state">
        <p>No active sessions.</p>
        <p class="hint">Sign in to access your events, or use a login link from your event organiser.</p>
        <button class="btn btn-primary" @click="showLoginModal = true">
          Sign in
        </button>
      </div>

      <div v-else class="sessions-list">
        <!-- Admin section: show header and cards if admin, otherwise show sign-in button -->
        <template v-if="hasAdminSession">
          <div class="section-header">
            <span class="section-label">Admin events</span>
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
              <div class="role-name">Admin</div>
              <div v-if="event.eventDate" class="event-date">
                {{ formatDate(event.eventDate) }}
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
        </template>

        <!-- Sign in button when no admin session -->
        <div v-else class="sign-in-section">
          <button class="btn btn-primary" @click="showLoginModal = true">
            Sign in
          </button>
        </div>

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
            <div class="role-name">{{ session.marshalName || 'Marshal' }}</div>
            <div v-if="session.eventDate" class="event-date">
              {{ formatDate(session.eventDate) }}
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

      <!-- Back to home button -->
      <button class="btn-back-home" @click="goToHome" title="Back to home">
        &times;
      </button>
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
            <p>This will log you out from all admin events.</p>
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
            <p>This will log you out from <strong>{{ sessionToRemove?.eventName || 'this event' }}</strong>.</p>
            <p class="hint">You can log back in using the link from your event organiser.</p>
            <div class="modal-actions">
              <button class="btn btn-secondary" @click="showConfirmModal = false">Cancel</button>
              <button class="btn btn-danger" @click="removeSessionConfirmed">Log out</button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import HeroHeader from '../components/HeroHeader.vue';
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
import { setAuthContext, eventsApi } from '../services/api';
import { useAuthStore } from '../stores/auth';
import { useEventsStore } from '../stores/events';
import { useProfileStore } from '../stores/profile';

const router = useRouter();
const authStore = useAuthStore();
const eventsStore = useEventsStore();
const profileStore = useProfileStore();

const sessions = ref([]);
const adminEvents = ref([]);
const loading = ref(true);
const showLoginModal = ref(false);
const showProfileModal = ref(false);
const showConfirmModal = ref(false);
const showAdminLogoutModal = ref(false);
const sessionToRemove = ref(null);

// Create event modal state
const showCreateEvent = ref(false);
const eventForm = ref({
  name: '',
  description: '',
  eventDate: '',
  timeZoneId: 'UTC',
  emergencyContacts: [],
});

const adminEmail = computed(() => localStorage.getItem('adminEmail'));
const hasAdminSession = computed(() => !!adminEmail.value);

const loadSessions = () => {
  sessions.value = getSessionsForDisplay();
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
  adminEvents.value = [];
  showProfileModal.value = false;
};

const formatDate = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  });
};

const loadAdminEvents = async () => {
  if (!hasAdminSession.value) return;

  try {
    setAuthContext('admin');
    const response = await eventsApi.getAll();
    adminEvents.value = response.data || [];
  } catch (error) {
    console.error('Failed to load admin events:', error);
    adminEvents.value = [];
  }
};

const goToAdminEvent = (eventId) => {
  router.push(`/admin/event/${eventId}`);
};

const goToHome = () => {
  router.push('/');
};

const handleLoginSuccess = () => {
  // Login email sent - modal will show confirmation
  // User will receive email and verify via AdminVerify which redirects here
};

// Create event handlers
const closeCreateEventModal = () => {
  showCreateEvent.value = false;
  eventForm.value = {
    name: '',
    description: '',
    eventDate: '',
    timeZoneId: 'UTC',
    emergencyContacts: [],
  };
};

const handleCreateEvent = async (formData) => {
  try {
    const eventData = {
      ...formData,
      adminEmail: authStore.adminEmail,
    };
    await eventsStore.createEvent(eventData);
    closeCreateEventModal();
    // Refresh admin events list
    await loadAdminEvents();
  } catch (error) {
    console.error('Failed to create event:', error);
    alert('Failed to create event. Please try again.');
  }
};

onMounted(async () => {
  document.title = 'OnTheDay App - Sessions';

  // Migrate any legacy storage format
  migrateLegacyStorage();

  // Clean up old sessions
  cleanupOldSessions();

  // Load sessions
  loadSessions();

  // Load admin events if logged in as admin
  await loadAdminEvents();

  loading.value = false;
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
  padding-top: 2rem;
  overflow-y: auto;
}

.sessions-container {
  max-width: 400px;
  width: 100%;
  text-align: center;
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
}

.section-label {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #888;
  font-weight: 600;
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

.role-name {
  font-size: 0.9rem;
  color: #aaa;
}

.event-date {
  font-size: 0.8rem;
  color: #667eea;
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

.btn-back-home {
  position: fixed;
  top: 0;
  left: 0;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-left: none;
  border-top: none;
  border-radius: 0 0 8px 0;
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  z-index: 100;
  font-size: 1.75rem;
  line-height: 1;
  font-weight: 300;
  color: white;
}

.btn-back-home:hover {
  background: rgba(255, 255, 255, 0.2);
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
