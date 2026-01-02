import { createRouter, createWebHashHistory } from 'vue-router';
import Home from '../views/Home.vue';
import AdminLogin from '../views/AdminLogin.vue';
import AdminVerify from '../views/AdminVerify.vue';
import AdminDashboard from '../views/AdminDashboard.vue';
import AdminProfile from '../views/AdminProfile.vue';
import AdminEventManage from '../views/AdminEventManage.vue';
import MarshalView from '../views/MarshalView.vue';

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
    path: '/admin/dashboard',
    name: 'AdminDashboard',
    component: AdminDashboard,
    meta: { requiresAuth: true },
  },
  {
    path: '/admin/profile',
    name: 'AdminProfile',
    component: AdminProfile,
    meta: { requiresAuth: true },
  },
  {
    path: '/admin/event/:eventId',
    name: 'AdminEventManage',
    component: AdminEventManage,
    meta: { requiresAuth: true },
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

const router = createRouter({
  history: createWebHashHistory(import.meta.env.BASE_URL),
  routes,
});

router.beforeEach((to, from, next) => {
  const adminEmail = localStorage.getItem('adminEmail');

  if (to.meta.requiresAuth && !adminEmail) {
    next({ name: 'AdminLogin' });
  } else {
    next();
  }
});

export default router;
