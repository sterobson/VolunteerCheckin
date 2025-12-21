import { defineStore } from 'pinia';
import { ref } from 'vue';
import { authApi } from '../services/api';

export const useAuthStore = defineStore('auth', () => {
  const adminEmail = ref(localStorage.getItem('adminEmail') || null);
  const isAuthenticated = ref(!!adminEmail.value);

  const instantLogin = async (email) => {
    const response = await authApi.instantLogin(email);
    if (response.data.success) {
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
    instantLogin,
    logout,
  };
});
