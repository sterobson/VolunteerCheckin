<template>
  <div class="home">
    <!-- Hero section with animation and overlaid title -->
    <section class="hero-section">
      <div class="hero-content">
        <HeroMapAnimation />
        <div class="hero-overlay">
          <h1>OnTheDay</h1>
        </div>
      </div>
      <p class="subtitle">Manage your event's marshals and volunteers with ease</p>
      <button class="btn btn-primary" @click="showLoginModal = true">
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
    <Teleport to="body">
      <Transition name="modal">
        <div v-if="showLoginModal" class="modal-overlay" @click.self="closeModal">
          <div class="modal-card">
            <button class="modal-close" @click="closeModal">&times;</button>

            <!-- Email sent confirmation -->
            <div v-if="emailSent" class="email-sent">
              <div class="check-icon">&#10003;</div>
              <h3>Check your email!</h3>
              <p>We've sent a login link to <strong>{{ email }}</strong></p>
              <p class="hint">The link will expire in 15 minutes.</p>
              <button @click="resetForm" class="btn btn-secondary">
                Use a different email
              </button>
            </div>

            <!-- Login form -->
            <div v-else>
              <h2>Join us or login</h2>
              <p class="instruction">Enter your email to receive a login link</p>

              <form @submit.prevent="handleLogin">
                <div class="form-group">
                  <input
                    v-model="email"
                    type="email"
                    placeholder="you@example.com"
                    required
                    class="form-input"
                  />
                </div>

                <button type="submit" class="btn btn-primary btn-full" :disabled="loading">
                  {{ loading ? 'Sending...' : 'Send login link' }}
                </button>
              </form>

              <div v-if="error" class="error">{{ error }}</div>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import HeroMapAnimation from '../components/HeroMapAnimation.vue';
import { useAuthStore } from '../stores/auth';

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();

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
const email = ref('');
const loading = ref(false);
const error = ref(null);
const emailSent = ref(false);

async function handleLogin() {
  loading.value = true;
  error.value = null;

  try {
    const response = await authStore.requestLogin(email.value);
    if (response.success) {
      emailSent.value = true;
    } else {
      error.value = response.message || 'Failed to send login link. Please try again.';
    }
  } catch (err) {
    error.value = 'Failed to send login link. Please try again.';
  } finally {
    loading.value = false;
  }
}

function resetForm() {
  email.value = '';
  emailSent.value = false;
  error.value = null;
}

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
    showLoginModal.value = true;
  }
});

onUnmounted(() => {
  if (intervalId) {
    clearInterval(intervalId);
  }
});
</script>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Pacifico&display=swap');

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
  justify-content: center;
  padding: 1rem;
  min-height: 0;
  gap: 0.5rem;
}

.hero-content {
  position: relative;
  width: 100%;
  max-width: 400px;
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

.hero-overlay {
  position: absolute;
  top: 0%;
  left: 50%;
  transform: translate(-50%, 0) scale(0);
  text-align: center;
  z-index: 10;
  pointer-events: none;
  width: 90%;
  opacity: 0;
  animation: popIn 0.5s ease-out 1s forwards;
}

@keyframes popIn {
  0% {
    transform: translate(-50%, 0) scale(0);
    opacity: 0;
  }
  70% {
    transform: translate(-50%, 0) scale(1.1);
    opacity: 1;
  }
  100% {
    transform: translate(-50%, 0) scale(1);
    opacity: 1;
  }
}

.hero-overlay h1 {
  font-family: 'Pacifico', cursive;
  font-size: 2.5rem;
  font-weight: 400;
  margin: 0;
  text-shadow:
    0 4px 8px rgba(0, 0, 0, 0.8),
    0 2px 4px rgba(0, 0, 0, 0.8);
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
  padding: 2rem;
  border-radius: 16px;
  max-width: 380px;
  width: 100%;
  position: relative;
}

.modal-close {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  background: none;
  border: none;
  color: #888;
  font-size: 1.5rem;
  cursor: pointer;
  line-height: 1;
  padding: 0.25rem;
}

.modal-close:hover {
  color: white;
}

.modal-card h2 {
  margin: 0 0 0.5rem;
  font-size: 1.5rem;
  color: white;
}

.modal-card .instruction {
  color: #aaa;
  margin-bottom: 1.5rem;
  font-size: 0.9rem;
}

.modal-card .form-group {
  margin-bottom: 1rem;
}

.modal-card .form-input {
  width: 100%;
  padding: 0.75rem 1rem;
  font-size: 1rem;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  color: white;
  box-sizing: border-box;
}

.modal-card .form-input:focus {
  outline: none;
  border-color: #667eea;
}

.modal-card .form-input::placeholder {
  color: #666;
}

.btn-full {
  width: 100%;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.15);
}

.error {
  margin-top: 1rem;
  padding: 0.75rem;
  background: rgba(220, 53, 69, 0.2);
  color: #ff6b6b;
  border-radius: 6px;
  font-size: 0.85rem;
}

.email-sent {
  text-align: center;
}

.email-sent h3 {
  color: #4ade80;
  margin-bottom: 0.75rem;
  font-size: 1.25rem;
}

.email-sent p {
  color: #ccc;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
}

.email-sent .hint {
  font-size: 0.8rem;
  color: #888;
  margin-bottom: 1.5rem;
}

.check-icon {
  width: 50px;
  height: 50px;
  background: rgba(74, 222, 128, 0.2);
  color: #4ade80;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  margin: 0 auto 1rem;
}

/* Responsive */
@media (max-width: 480px) {
  .hero-overlay h1 {
    font-size: 2rem;
  }

  .hero-section .subtitle {
    font-size: 0.9rem;
  }

  .carousel {
    max-width: 100%;
  }

  .feature-card {
    padding: 1rem;
  }

  .modal-card {
    padding: 1.5rem;
  }
}
</style>
