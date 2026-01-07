<template>
  <div class="admin-profile">
    <header class="header">
      <h1>My profile</h1>
      <div class="header-actions">
        <span class="user-email">{{ authStore.adminEmail }}</span>
        <button @click="handleLogout" class="btn btn-secondary">Logout</button>
      </div>
    </header>

    <div class="container">
      <div class="section">
        <div class="section-header">
          <h2>Profile information</h2>
          <button @click="showEditModal = true" class="btn btn-primary">
            Edit profile
          </button>
        </div>

        <div v-if="profileStore.loading" class="loading">Loading profile...</div>

        <div v-else-if="profileStore.error" class="error">
          <p>{{ profileStore.error }}</p>
          <button @click="loadProfile" class="btn btn-secondary">Retry</button>
        </div>

        <div v-else-if="profileStore.profile" class="profile-details">
          <div class="profile-field">
            <label>Name</label>
            <div class="field-value">{{ profileStore.profile.name || '—' }}</div>
          </div>

          <div class="profile-field">
            <label>Email</label>
            <div class="field-value">{{ profileStore.profile.email || '—' }}</div>
          </div>

          <div class="profile-field">
            <label>Phone</label>
            <div class="field-value">{{ profileStore.profile.phone || '—' }}</div>
          </div>

          <div class="profile-field">
            <label>System admin</label>
            <div class="field-value">
              <span :class="['badge', profileStore.profile.isSystemAdmin ? 'badge-success' : 'badge-secondary']">
                {{ profileStore.profile.isSystemAdmin ? 'Yes' : 'No' }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <div class="section">
        <button @click="goToDashboard" class="btn btn-secondary">
          Back to Dashboard
        </button>
      </div>
    </div>

    <EditProfileModal
      :show="showEditModal"
      :profile="profileStore.profile"
      @close="showEditModal = false"
      @submit="handleSaveProfile"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useProfileStore } from '../stores/profile';
import EditProfileModal from '../components/EditProfileModal.vue';

const router = useRouter();
const authStore = useAuthStore();
const profileStore = useProfileStore();

const showEditModal = ref(false);

const loadProfile = async () => {
  try {
    await profileStore.fetchProfile();
  } catch (error) {
    console.error('Failed to load profile:', error);
  }
};

const handleSaveProfile = async (formData) => {
  try {
    await profileStore.updateProfile(formData);
    showEditModal.value = false;
  } catch (error) {
    console.error('Failed to save profile:', error);
    alert('Failed to save profile. Please try again.');
  }
};

const handleLogout = () => {
  authStore.logout();
  router.push({ name: 'Home' });
};

const goToDashboard = () => {
  router.push({ name: 'AdminDashboard' });
};

onMounted(() => {
  loadProfile();
});
</script>

<style scoped>
.admin-profile {
  min-height: 100vh;
  background: var(--bg-secondary);
}

.header {
  background: var(--card-bg);
  padding: 1.5rem 2rem;
  box-shadow: var(--shadow-sm);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header h1 {
  margin: 0;
  color: var(--text-dark);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.user-email {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.container {
  max-width: 800px;
  margin: 0 auto;
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.section {
  background: var(--card-bg);
  padding: 2rem;
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.section-header h2 {
  margin: 0;
  color: var(--text-dark);
}

.loading,
.error {
  text-align: center;
  padding: 3rem;
  color: var(--text-secondary);
}

.error p {
  color: var(--danger);
  margin-bottom: 1rem;
}

.profile-details {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.profile-field {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.profile-field label {
  font-weight: 600;
  color: var(--text-dark);
  font-size: 0.875rem;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.field-value {
  color: var(--text-secondary);
  font-size: 1rem;
  padding: 0.75rem;
  background: var(--bg-muted);
  border-radius: 6px;
}

.badge {
  display: inline-block;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.875rem;
  font-weight: 600;
}

.badge-success {
  background: var(--success-bg);
  color: var(--success-text);
}

.badge-secondary {
  background: var(--btn-cancel-bg);
  color: var(--text-secondary);
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--brand-primary-hover);
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover {
  background: var(--btn-cancel-hover);
}
</style>
