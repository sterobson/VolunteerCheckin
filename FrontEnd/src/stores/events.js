import { defineStore } from 'pinia';
import { ref } from 'vue';
import { eventsApi, locationsApi, assignmentsApi } from '../services/api';
import { denormalizeEventStatus } from '../utils/denormalize';

export const useEventsStore = defineStore('events', () => {
  const events = ref([]);
  const currentEvent = ref(null);
  const locations = ref([]);
  const assignments = ref([]);
  const eventStatus = ref(null);
  const loading = ref(false);
  const error = ref(null);

  const fetchEvents = async () => {
    loading.value = true;
    error.value = null;
    try {
      const response = await eventsApi.getAll();
      events.value = response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const fetchEvent = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await eventsApi.getById(eventId);
      currentEvent.value = response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const createEvent = async (eventData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await eventsApi.create(eventData);
      events.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const updateEvent = async (eventId, eventData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await eventsApi.update(eventId, eventData);
      const index = events.value.findIndex((e) => e.id === eventId);
      if (index !== -1) {
        events.value[index] = response.data;
      }
      if (currentEvent.value?.id === eventId) {
        currentEvent.value = response.data;
      }
      return response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const deleteEvent = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      await eventsApi.delete(eventId);
      events.value = events.value.filter((e) => e.id !== eventId);
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const fetchLocations = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await locationsApi.getByEvent(eventId);
      locations.value = response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const createLocation = async (locationData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await locationsApi.create(locationData);
      locations.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const updateLocation = async (eventId, locationId, locationData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await locationsApi.update(eventId, locationId, locationData);
      const index = locations.value.findIndex((l) => l.id === locationId);
      if (index !== -1) {
        locations.value[index] = response.data;
      }
      return response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const deleteLocation = async (eventId, locationId) => {
    loading.value = true;
    error.value = null;
    try {
      await locationsApi.delete(eventId, locationId);
      locations.value = locations.value.filter((l) => l.id !== locationId);
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const fetchAssignments = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await assignmentsApi.getByEvent(eventId);
      assignments.value = response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const createAssignment = async (assignmentData) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await assignmentsApi.create(assignmentData);
      assignments.value.push(response.data);
      return response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const deleteAssignment = async (eventId, assignmentId) => {
    loading.value = true;
    error.value = null;
    try {
      await assignmentsApi.delete(eventId, assignmentId);
      assignments.value = assignments.value.filter((a) => a.id !== assignmentId);
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const fetchEventStatus = async (eventId) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await assignmentsApi.getEventStatus(eventId);
      const denormalized = denormalizeEventStatus(response.data);
      // Ensure eventId is set (may not be in normalized response)
      if (denormalized && !denormalized.eventId) {
        denormalized.eventId = eventId;
      }
      eventStatus.value = denormalized;
      return denormalized;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  const updateAssignmentCheckIn = (updatedAssignment) => {
    const index = assignments.value.findIndex((a) => a.id === updatedAssignment.id);
    if (index !== -1) {
      assignments.value[index] = updatedAssignment;
    }

    if (eventStatus.value) {
      eventStatus.value.locations.forEach((location) => {
        const assignmentIndex = location.assignments.findIndex(
          (a) => a.id === updatedAssignment.id
        );
        if (assignmentIndex !== -1) {
          location.assignments[assignmentIndex] = updatedAssignment;
          location.checkedInCount = location.assignments.filter((a) => a.isCheckedIn).length;
        }
      });
    }
  };

  const bulkUpdateLocationTimes = async (eventId, timeDelta) => {
    loading.value = true;
    error.value = null;
    try {
      const response = await locationsApi.bulkUpdateTimes(eventId, timeDelta);
      // Refresh locations after bulk update
      await fetchLocations(eventId);
      return response.data;
    } catch (err) {
      error.value = err.message;
      throw err;
    } finally {
      loading.value = false;
    }
  };

  return {
    events,
    currentEvent,
    locations,
    assignments,
    eventStatus,
    loading,
    error,
    fetchEvents,
    fetchEvent,
    createEvent,
    updateEvent,
    deleteEvent,
    fetchLocations,
    createLocation,
    updateLocation,
    deleteLocation,
    bulkUpdateLocationTimes,
    fetchAssignments,
    createAssignment,
    deleteAssignment,
    fetchEventStatus,
    updateAssignmentCheckIn,
  };
});
