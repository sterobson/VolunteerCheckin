import { createRouter, createWebHashHistory, createWebHistory } from 'vue-router';
import { getSession, hasSessions, getMagicCode, migrateLegacyStorage, cleanupOldSessions } from '../services/marshalSessionService';
import { setAuthContext } from '../services/api';

// Run migration and cleanup once on app load
let initialized = false;
function initializeMarshalSessions() {
  if (initialized) return;
  initialized = true;
  migrateLegacyStorage();
  cleanupOldSessions();
}
import Home from '../views/Home.vue';
import AdminLogin from '../views/AdminLogin.vue';
import AdminVerify from '../views/AdminVerify.vue';
import AdminEventManage from '../views/AdminEventManage.vue';
import MarshalView from '../views/MarshalView.vue';
import SessionsView from '../views/SessionsView.vue';

const routes = [
  {
    path: '/',
    name: 'Home',
    component: Home,
  },
  {
    path: '/admin/login',
    name: 'AdminLogin',
    component: AdminLogin,
  },
  {
    path: '/admin/verify',
    name: 'AdminVerify',
    component: AdminVerify,
  },
  {
    path: '/sessions',
    name: 'Sessions',
    component: SessionsView,
  },
  {
    path: '/admin/dashboard',
    redirect: '/sessions',
  },
  {
    path: '/admin/profile',
    redirect: '/sessions',
  },
  {
    path: '/admin/event/:eventId',
    name: 'AdminEventManage',
    component: AdminEventManage,
    meta: { requiresAuth: true },
  },
  {
    path: '/marshal',
    redirect: '/sessions',
  },
  {
    path: '/event/:eventId',
    name: 'MarshalView',
    component: MarshalView,
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
];

// Use hash routing for local dev and GitHub Pages (static file hosting without server config)
// Use history routing for Azure Static Web Apps and other hosts with proper routing support
const useHashRouting = import.meta.env.VITE_USE_HASH_ROUTING === 'true';

const router = createRouter({
  history: useHashRouting
    ? createWebHashHistory(import.meta.env.BASE_URL)
    : createWebHistory(import.meta.env.BASE_URL),
  routes,
});

router.beforeEach((to, from, next) => {
  // Ensure marshal sessions are migrated and cleaned up
  initializeMarshalSessions();

  const adminEmail = localStorage.getItem('adminEmail');

  // Admin route protection
  if (to.meta.requiresAuth && !adminEmail) {
    next({ name: 'AdminLogin' });
    return;
  }

  // Set auth context based on destination route
  if (to.meta.requiresAuth) {
    // Admin routes
    setAuthContext('admin');
  } else if (to.name !== 'MarshalView' && to.name !== 'Sessions') {
    // Non-auth routes (Home, login pages, etc.) - clear context
    setAuthContext(null);
  }
  // Note: MarshalView sets its own context in useMarshalAuth

  // Marshal view navigation logic
  if (to.name === 'MarshalView') {
    const eventId = to.params.eventId;
    const hasMagicCode = !!to.query.code;
    const session = getSession(eventId);
    // Get magic code using marshalId from session (if available)
    const storedMagicCode = session?.marshalId ? getMagicCode(eventId, session.marshalId) : null;

    // If we have a magic code in URL, let the view handle authentication
    if (hasMagicCode) {
      next();
      return;
    }

    // If we have a valid session token, continue to the view
    if (session?.token) {
      next();
      return;
    }

    // If we have a stored magic code (but no session token), let the view re-authenticate
    if (storedMagicCode) {
      next();
      return;
    }

    // No session and no magic code - redirect to sessions if we have other sessions
    // Otherwise show the "no session" message in the view
    if (hasSessions()) {
      next({ name: 'Sessions' });
      return;
    }
  }

  next();
});

export default router;
