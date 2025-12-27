/**
 * Composable for event management
 * Centralized event-related operations
 */

import { ref, computed } from 'vue';
import api from '@/services/api';

export function useEventManagement() {
  const events = ref([]);
  const currentEvent = ref(null);
  const loading = ref(false);
  const error = ref(null);

  /**
   * Fetch all events
   */
  const fetchEvents = async () => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.getEvents();
      events.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch events';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Fetch a single event by ID
   * @param {number} eventId - The event ID
   */
  const fetchEvent = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.getEvent(eventId);
      currentEvent.value = response.data;
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to fetch event';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Create a new event
   * @param {Object} eventData - Event data
   */
  const createEvent = async (eventData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.createEvent(eventData);
      events.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to create event';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Update an existing event
   * @param {number} eventId - The event ID
   * @param {Object} eventData - Updated event data
   */
  const updateEvent = async (eventId, eventData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await api.updateEvent(eventId, eventData);

      // Update in events list
      const index = events.value.findIndex((e) => e.id === eventId);
      if (index !== -1) {
        events.value[index] = response.data;
      }

      // Update current event if it's the same
      if (currentEvent.value?.id === eventId) {
        currentEvent.value = response.data;
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to update event';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Delete an event
   * @param {number} eventId - The event ID
   */
  const deleteEvent = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      await api.deleteEvent(eventId);

      // Remove from events list
      events.value = events.value.filter((e) => e.id !== eventId);

      // Clear current event if it's the deleted one
      if (currentEvent.value?.id === eventId) {
        currentEvent.value = null;
      }
    } catch (err) {
      error.value = err.message || 'Failed to delete event';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Upload route file for an event
   * @param {number} eventId - The event ID
   * @param {File} file - GPX file
   */
  const uploadRoute = async (eventId, file) => {
    loading.value = true;
    error.value = null;
    try {
      const formData = new FormData();
      formData.append('file', file);
      const response = await api.uploadRoute(eventId, formData);

      // Update current event with new route
      if (currentEvent.value?.id === eventId) {
        currentEvent.value.route = response.data.route;
      }

      return response.data;
    } catch (err) {
      error.value = err.message || 'Failed to upload route';
      throw err;
    } finally {
      loading.value = false;
    }
  };

  /**
   * Get sorted events by date
   */
  const sortedEvents = computed(() => {
    return [...events.value].sort((a, b) => {
      return new Date(b.eventDate) - new Date(a.eventDate);
    });
  });

  /**
   * Get upcoming events
   */
  const upcomingEvents = computed(() => {
    const now = new Date();
    return events.value.filter((event) => new Date(event.eventDate) >= now);
  });

  /**
   * Get past events
   */
  const pastEvents = computed(() => {
    const now = new Date();
    return events.value.filter((event) => new Date(event.eventDate) < now);
  });

  return {
    // State
    events,
    currentEvent,
    loading,
    error,

    // Computed
    sortedEvents,
    upcomingEvents,
    pastEvents,

    // Methods
    fetchEvents,
    fetchEvent,
    createEvent,
    updateEvent,
    deleteEvent,
    uploadRoute,
  };
}
