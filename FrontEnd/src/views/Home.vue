<template>
  <div class="home">
    <!-- Hero section with animation and overlaid title -->
    <section class="hero-section">
      <HeroHeader />
      <p class="subtitle">Manage your event's marshals and volunteers with ease</p>
      <div class="hero-buttons">
        <button class="btn btn-primary" @click="handleLoginClick">
          Join us or login
        </button>
        <button
          class="btn btn-secondary"
          @click="handleTryItOut"
          :disabled="isCreatingSample"
        >
          <span v-if="isCreatingSample">Creating demo...</span>
          <span v-else>Try it out for free</span>
        </button>
      </div>
      <p v-if="sampleError" class="sample-error">{{ sampleError }}</p>
    </section>

    <!-- Main content -->
    <section class="content-section">
      <div class="container">

        <!-- Features - Grid on wide screens, Carousel on narrow -->
        <div class="features-grid">
          <div class="feature-card">
            <h3>For admins</h3>
            <ul>
              <li>Create and manage events</li>
              <li>Define marshal locations</li>
              <li>Real-time check-in monitoring</li>
              <li>Map view of everything in your event</li>
              <li>Manage incidents</li>
            </ul>
          </div>

          <div class="feature-card">
            <h3>For marshals</h3>
            <ul>
              <li>View the entire route</li>
              <li>View your assigned location</li>
              <li>Check in with GPS verification</li>
              <li>Access emergency contact info</li>
              <li>Record incidents</li>
            </ul>
          </div>
        </div>

        <!-- Carousel for narrow screens -->
        <div class="carousel">
          <div class="carousel-track" :style="{ transform: `translateX(-${activeIndex * 100}%)` }">
            <div class="feature-card">
              <h3>For admins</h3>
              <ul>
                <li>Create and manage events</li>
                <li>Define marshal locations</li>
                <li>Real-time check-in monitoring</li>
                <li>Map view of everything in your event</li>
                <li>Manage incidents</li>
              </ul>
            </div>

            <div class="feature-card">
              <h3>For marshals</h3>
              <ul>
                <li>View the entire route</li>
                <li>View your assigned location</li>
                <li>Check in with GPS verification</li>
                <li>Access emergency contact info</li>
                <li>Record incidents</li>
              </ul>
            </div>
          </div>

          <div class="carousel-indicators">
            <button
              v-for="(_, index) in 2"
              :key="index"
              :class="['indicator', { active: activeIndex === index }]"
              @click="goToSlide(index)"
            ></button>
          </div>
        </div>
      </div>
    </section>

    <!-- Login Modal -->
    <LoginModal
      :show="showLoginModal"
      @close="closeModal"
    />

    <!-- Sample Event Created Modal -->
    <SampleEventCreatedModal
      :show="showSampleEventModal"
      :event-id="sampleEventResult?.eventId || ''"
      :admin-code="sampleEventResult?.adminCode || ''"
      :lifetime-hours="sampleEventResult?.lifetimeHours || 4"
      @close="closeSampleEventModal"
      @navigate="navigateToSampleEvent"
    />
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import HeroHeader from '../components/HeroHeader.vue';
import LoginModal from '../components/LoginModal.vue';
import SampleEventCreatedModal from '../components/SampleEventCreatedModal.vue';
import { hasSessions } from '../services/marshalSessionService';
import { sampleEventsApi } from '../services/api';

const route = useRoute();
const router = useRouter();

// Check if user has any active sessions (admin or marshal)
function hasAnySessions() {
  const hasAdminSession = !!localStorage.getItem('adminEmail');
  const hasMarshalSessions = hasSessions();
  return hasAdminSession || hasMarshalSessions;
}

function handleLoginClick() {
  if (hasAnySessions()) {
    router.push('/myevents');
  } else {
    showLoginModal.value = true;
  }
}

// Carousel state
const activeIndex = ref(0);
let intervalId = null;

function goToSlide(index) {
  activeIndex.value = index;
  resetCarouselInterval();
}

function nextSlide() {
  activeIndex.value = (activeIndex.value + 1) % 2;
}

function resetCarouselInterval() {
  if (intervalId) {
    clearInterval(intervalId);
  }
  intervalId = setInterval(nextSlide, 10000);
}

// Login modal state
const showLoginModal = ref(false);

function closeModal() {
  showLoginModal.value = false;
  // Remove login query param if present
  if (route.query.login) {
    router.replace({ query: {} });
  }
}

// Sample event state
const isCreatingSample = ref(false);
const sampleError = ref('');
const showSampleEventModal = ref(false);
const sampleEventResult = ref(null);

function getDeviceFingerprint() {
  // Use stored fingerprint if available, otherwise generate a new one
  let fingerprint = localStorage.getItem('deviceFingerprint');
  if (!fingerprint) {
    fingerprint = crypto.randomUUID();
    localStorage.setItem('deviceFingerprint', fingerprint);
  }
  return fingerprint;
}

function findExistingSampleEvent() {
  // Check localStorage for any existing unexpired sample events
  const prefix = 'sampleEvent_';
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key && key.startsWith(prefix)) {
      try {
        const data = JSON.parse(localStorage.getItem(key));
        if (data && data.adminCode && data.expiresAt) {
          const expiresAt = new Date(data.expiresAt);
          if (expiresAt > new Date()) {
            // Found a valid, unexpired sample event
            const eventId = key.substring(prefix.length);
            return { eventId, adminCode: data.adminCode, expiresAt: data.expiresAt, storageKey: key };
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
  return null;
}

async function handleTryItOut() {
  sampleError.value = '';
  isCreatingSample.value = true;

  // Check for an existing sample event first
  const existingEvent = findExistingSampleEvent();
  if (existingEvent) {
    try {
      // Validate with the backend that it still exists
      await sampleEventsApi.validate(existingEvent.adminCode);
      // Still valid, navigate to it
      router.push(`/admin/event/${existingEvent.eventId}?sample=${existingEvent.adminCode}`);
      isCreatingSample.value = false;
      return;
    } catch {
      // Event no longer exists on backend, remove from localStorage
      localStorage.removeItem(existingEvent.storageKey);
      // Fall through to create a new one
    }
  }

  const fingerprint = getDeviceFingerprint();

  try {
    const timeZoneId = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const response = await sampleEventsApi.create(fingerprint, timeZoneId);

    // Store the admin code and expiration for this sample event
    localStorage.setItem(`sampleEvent_${response.data.eventId}`, JSON.stringify({
      adminCode: response.data.adminCode,
      expiresAt: response.data.expiresAt
    }));

    // Store the result and show the modal
    sampleEventResult.value = response.data;
    showSampleEventModal.value = true;
  } catch (error) {
    console.error('Error creating sample event:', error);

    if (error.response?.status === 429) {
      // Rate limited - try to recover the existing sample event
      try {
        const recoveryResponse = await sampleEventsApi.recover(fingerprint);
        const { eventId, adminCode, expiresAt } = recoveryResponse.data;

        // Store the recovered credentials in localStorage
        localStorage.setItem(`sampleEvent_${eventId}`, JSON.stringify({
          adminCode,
          expiresAt
        }));

        // Navigate directly to the recovered event
        router.push(`/admin/event/${eventId}?sample=${adminCode}`);
        return;
      } catch (recoveryError) {
        // Recovery failed, show the rate limit error
        console.error('Failed to recover sample event:', recoveryError);
        sampleError.value = error.response.data?.message ||
          "You've already created a sample event today. Please try again tomorrow.";
      }
    } else {
      sampleError.value = 'Failed to create sample event. Please try again.';
    }
  } finally {
    isCreatingSample.value = false;
  }
}

function closeSampleEventModal() {
  showSampleEventModal.value = false;
}

function navigateToSampleEvent() {
  if (sampleEventResult.value) {
    const { eventId, adminCode } = sampleEventResult.value;
    showSampleEventModal.value = false;
    router.push(`/admin/event/${eventId}?sample=${adminCode}`);
  }
}

onMounted(() => {
  resetCarouselInterval();
  // Auto-open modal if login query param is present
  if (route.query.login) {
    if (hasAnySessions()) {
      router.replace('/myevents');
    } else {
      showLoginModal.value = true;
    }
  }
});

onUnmounted(() => {
  if (intervalId) {
    clearInterval(intervalId);
  }
});
</script>

<style scoped>
.home {
  min-height: 100vh;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  background: linear-gradient(180deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
  color: white;
}

/* Hero Section */
.hero-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;
  padding: 1rem;
  padding-top: 4rem;
  min-height: 0;
  gap: 0.5rem;
}

.hero-section .subtitle {
  font-size: 1rem;
  opacity: 0.85;
  color: #e0e0e0;
  margin: 0;
  text-align: center;
}

.hero-buttons {
  display: flex;
  gap: 1rem;
  margin-top: 0.5rem;
  flex-wrap: wrap;
  justify-content: center;
}

.sample-error {
  color: #fca5a5;
  font-size: 0.85rem;
  margin-top: 0.5rem;
  text-align: center;
}

/* Content Section */
.content-section {
  padding: 1rem 1.5rem 1.5rem;
  flex-shrink: 0;
}

.container {
  max-width: 900px;
  margin: 0 auto;
  text-align: center;
}

.btn {
  display: inline-block;
  padding: 0.75rem 2rem;
  font-size: 1rem;
  text-decoration: none;
  border-radius: 50px;
  transition: all 0.3s;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  font-weight: 600;
  box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(102, 126, 234, 0.5);
}

.btn-secondary {
  background: transparent;
  color: white;
  font-weight: 600;
  border: 2px solid rgba(255, 255, 255, 0.5);
}

.btn-secondary:hover:not(:disabled) {
  transform: translateY(-2px);
  border-color: rgba(255, 255, 255, 0.8);
  background: rgba(255, 255, 255, 0.1);
}

.btn-secondary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Features Grid (wide screens) */
.features-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
  margin-top: 1rem;
}

/* Carousel (narrow screens) */
.carousel {
  display: none;
  max-width: 350px;
  margin: 1rem auto 0;
  overflow: hidden;
}

@media (max-width: 700px) {
  .features-grid {
    display: none;
  }

  .carousel {
    display: block;
  }
}

.carousel-track {
  display: flex;
  transition: transform 0.6s ease-in-out;
}

.feature-card {
  flex: 0 0 100%;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  padding: 1rem 1.25rem;
  border-radius: 12px;
  text-align: left;
  box-sizing: border-box;
}

.feature-card h3 {
  margin-bottom: 0.5rem;
  font-size: 1rem;
  color: #667eea;
}

.feature-card ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.feature-card li {
  padding: 0.25rem 0;
  padding-left: 1.25rem;
  position: relative;
  color: #c0c0c0;
  font-size: 0.85rem;
}

.feature-card li:before {
  content: "";
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 6px;
  height: 6px;
  background: #667eea;
  border-radius: 50%;
}

/* Carousel Indicators */
.carousel-indicators {
  display: flex;
  justify-content: center;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  border: none;
  background: rgba(255, 255, 255, 0.3);
  cursor: pointer;
  transition: background 0.3s, transform 0.3s;
  padding: 0;
}

.indicator:hover {
  background: rgba(255, 255, 255, 0.5);
}

.indicator.active {
  background: #667eea;
  transform: scale(1.2);
}

/* Responsive */
@media (max-width: 480px) {
  .hero-section .subtitle {
    font-size: 0.9rem;
  }

  .carousel {
    max-width: 100%;
  }

  .feature-card {
    padding: 1rem;
  }
}
</style>
