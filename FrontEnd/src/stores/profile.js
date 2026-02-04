import { defineStore } from 'pinia';
import { ref } from 'vue';
import { profileApi } from '../services/api';

export const useProfileStore = defineStore('profile', () => {
  const profile = ref(null);
  const loading = ref(false);
  const error = ref(null);

  const fetchProfile = async () => {
    loading.value = true;
    error.value = null;
    try {
      const response = await profileApi.getProfile();
      profile.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.response?.data?.message || 'Failed to load profile';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const updateProfile = async (data) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await profileApi.updateProfile(data);
      profile.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.response?.data?.message || 'Failed to update profile';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const fetchPerson = async (personId, eventId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await profileApi.getPerson(personId, eventId);
      return response.data;
    } catch (err) {
      error.value = err.response?.data?.message || 'Failed to load person';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const clearProfile = () => {
    profile.value = null;
    error.value = null;
  };

  return {
    profile,
    loading,
    error,
    fetchProfile,
    updateProfile,
    fetchPerson,
    clearProfile,
  };
});
