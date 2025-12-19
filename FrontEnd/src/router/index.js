import { createRouter, createWebHistory } from 'vue-router';
import Home from '../views/Home.vue';
import AdminLogin from '../views/AdminLogin.vue';
import AdminDashboard from '../views/AdminDashboard.vue';
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
    path: '/admin/dashboard',
    name: 'AdminDashboard',
    component: AdminDashboard,
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
];

const router = createRouter({
  history: createWebHistory(),
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
