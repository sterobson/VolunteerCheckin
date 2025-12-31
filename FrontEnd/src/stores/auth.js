import { defineStore } from 'pinia';
import { ref } from 'vue';
import { authApi } from '../services/api';

export const useAuthStore = defineStore('auth', () => {
  const adminEmail = ref(localStorage.getItem('adminEmail') || null);
  const isAuthenticated = ref(!!adminEmail.value);
  const loginPending = ref(false);

  // Request a magic link to be sent to the email
  const requestLogin = async (email) => {
    const response = await authApi.requestLogin(email);
    if (response.data.success) {
      loginPending.value = true;
    }
    return response.data;
  };

  // Verify the magic link token and complete login
  const verifyToken = async (token) => {
    const response = await authApi.verifyToken(token);
    if (response.data.success) {
      adminEmail.value = response.data.person?.email;
      isAuthenticated.value = true;
      loginPending.value = false;
      localStorage.setItem('adminEmail', response.data.person?.email);
      localStorage.setItem('sessionToken', response.data.sessionToken);
    }
    return response.data;
  };

  // Dev-only instant login (bypasses email)
  const instantLogin = async (email) => {
    const response = await authApi.instantLogin(email);
    if (response.data.success) {
      adminEmail.value = response.data.email;
      isAuthenticated.value = true;
      localStorage.setItem('adminEmail', response.data.email);
      if (response.data.sessionToken) {
        localStorage.setItem('sessionToken', response.data.sessionToken);
      }
    }
    return response.data;
  };

  const logout = async () => {
    try {
      await authApi.logout();
    } catch (e) {
      // Ignore logout errors
    }
    adminEmail.value = null;
    isAuthenticated.value = false;
    loginPending.value = false;
    localStorage.removeItem('adminEmail');
    localStorage.removeItem('sessionToken');
  };

  return {
    adminEmail,
    isAuthenticated,
    loginPending,
    requestLogin,
    verifyToken,
    instantLogin,
    logout,
  };
});
