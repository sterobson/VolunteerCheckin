<template>
  <nav class="top-nav">
    <div class="nav-content">
      <router-link to="/" class="nav-brand">OnTheDay</router-link>
      <div class="nav-links">
        <router-link to="/" class="nav-link">Home</router-link>
        <router-link to="/pricing" class="nav-link">Pricing</router-link>
        <router-link to="/about" class="nav-link">About</router-link>
        <router-link v-if="hasAnySessions" to="/myevents" class="nav-link">My events</router-link>
      </div>
    </div>
  </nav>
</template>

<script setup>
import { computed } from 'vue';
import { hasSessions } from '../services/marshalSessionService';

// Show "My events" link if user has any sessions (admin or marshal)
const hasAnySessions = computed(() => {
  const hasAdminSession = !!localStorage.getItem('adminEmail');
  const hasMarshalSessions = hasSessions();
  return hasAdminSession || hasMarshalSessions;
});
</script>

<style scoped>
.top-nav {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 100;
  padding: 1rem 1.5rem;
}

.nav-content {
  max-width: 1200px;
  margin: 0 auto;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.nav-brand {
  font-size: 1.25rem;
  font-weight: 700;
  color: white;
  text-decoration: none;
  letter-spacing: -0.5px;
}

.nav-brand:hover {
  opacity: 0.9;
}

.nav-links {
  display: flex;
  gap: 1.5rem;
}

.nav-link {
  color: rgba(255, 255, 255, 0.85);
  text-decoration: none;
  font-size: 0.95rem;
  font-weight: 500;
  transition: color 0.2s;
}

.nav-link:hover {
  color: white;
}

.nav-link.router-link-exact-active {
  color: #667eea;
}

@media (max-width: 480px) {
  .top-nav {
    padding: 0.75rem 1rem;
  }

  .nav-brand {
    font-size: 1.1rem;
  }

  .nav-links {
    gap: 1rem;
  }

  .nav-link {
    font-size: 0.85rem;
  }
}
</style>
