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

// Lazy-loaded views for code splitting
// Each view becomes a separate chunk, only downloaded when needed
const Home = () => import('../views/Home.vue');
const PricingView = () => import('../views/PricingView.vue');
const AboutView = () => import('../views/AboutView.vue');
const AdminLogin = () => import('../views/AdminLogin.vue');
const AdminVerify = () => import('../views/AdminVerify.vue');
const AdminEventManage = () => import('../views/AdminEventManage.vue');
const MarshalView = () => import('../views/MarshalView.vue');
const SessionsView = () => import('../views/SessionsView.vue');

const routes = [
  {
    path: '/',
    name: 'Home',
    component: Home,
  },
  {
    path: '/pricing',
    name: 'Pricing',
    component: PricingView,
  },
  {
    path: '/about',
    name: 'About',
    component: AboutView,
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
    path: '/myevents',
    name: 'MyEvents',
    component: SessionsView,
  },
  {
    path: '/sessions',
    redirect: '/myevents',
  },
  {
    path: '/admin/dashboard',
    redirect: '/myevents',
  },
  {
    path: '/admin/profile',
    redirect: '/myevents',
  },
  {
    path: '/admin/event/:eventId',
    name: 'AdminEventManage',
    component: AdminEventManage,
    meta: { requiresAuth: true },
  },
  {
    path: '/marshal',
    redirect: '/myevents',
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

// Use history routing for localhost and Azure Static Web Apps
// Use hash routing only when explicitly enabled (e.g., GitHub Pages)
const isLocalhost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
const useHashRouting = !isLocalhost && import.meta.env.VITE_USE_HASH_ROUTING === 'true';

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
  const sessionToken = localStorage.getItem('sessionToken');
  const hasSampleCode = !!to.query.sample;

  // Admin route protection
  // Allow access if: has adminEmail, has sessionToken, or has sample code in URL
  if (to.meta.requiresAuth && !adminEmail && !sessionToken && !hasSampleCode) {
    next({ name: 'AdminLogin' });
    return;
  }

  // Set auth context based on destination route
  if (to.meta.requiresAuth) {
    if (hasSampleCode) {
      // Sample event admin access
      setAuthContext('sample', to.params.eventId, to.query.sample);
    } else {
      // Regular admin access
      setAuthContext('admin');
    }
  } else if (to.name !== 'MarshalView' && to.name !== 'MyEvents') {
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

    // No session and no magic code - redirect to my events if we have other sessions
    // Otherwise show the "no session" message in the view
    if (hasSessions()) {
      next({ name: 'MyEvents' });
      return;
    }
  }

  next();
});

export default router;
