<template>
  <div class="home">
    <!-- Hero section with animation and overlaid title -->
    <section class="hero-section">
      <HeroHeader title="OnTheDay" />
      <p class="subtitle">Manage your event's marshals and volunteers with ease</p>
      <button class="btn btn-primary" @click="handleLoginClick">
        Join us or login
      </button>
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
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import HeroHeader from '../components/HeroHeader.vue';
import LoginModal from '../components/LoginModal.vue';
import { hasSessions } from '../services/marshalSessionService';

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
    router.push('/sessions');
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

onMounted(() => {
  resetCarouselInterval();
  // Auto-open modal if login query param is present
  if (route.query.login) {
    if (hasAnySessions()) {
      router.replace('/sessions');
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
  height: 100vh;
  overflow: hidden;
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
  padding-top: 2rem;
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

.hero-section > .btn {
  margin-top: 0.5rem;
}

/* Content Section */
.content-section {
  padding: 1rem 1.5rem 1.5rem;
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
