import { defineStore } from 'pinia';
import { ref } from 'vue';
import { authApi } from '../services/api';

export const useAuthStore = defineStore('auth', () => {
  const adminEmail = ref(localStorage.getItem('adminEmail') || null);
  const isAuthenticated = ref(!!adminEmail.value);

  const requestMagicLink = async (email) => {
    const response = await authApi.requestMagicLink(email);
    return response.data;
  };

  const validateToken = async (token) => {
    const response = await authApi.validateToken(token);
    if (response.data.isValid) {
      adminEmail.value = response.data.email;
      isAuthenticated.value = true;
      localStorage.setItem('adminEmail', response.data.email);
    }
    return response.data;
  };

  const logout = () => {
    adminEmail.value = null;
    isAuthenticated.value = false;
    localStorage.removeItem('adminEmail');
  };

  return {
    adminEmail,
    isAuthenticated,
    requestMagicLink,
    validateToken,
    logout,
  };
});
