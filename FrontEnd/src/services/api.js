import axios from 'axios';
import { API_BASE_URL } from '../config';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Auth API
export const authApi = {
  requestMagicLink: (email) => api.post('/auth/request-magic-link', { email }),
  validateToken: (token) => api.post('/auth/validate-token', { token }),
  createAdmin: (email) => api.post('/auth/create-admin', { email }),
};

// Events API
export const eventsApi = {
  create: (data) => api.post('/events', data),
  getAll: () => api.get('/events'),
  getById: (id) => api.get(`/events/${id}`),
  update: (id, data) => api.put(`/events/${id}`, data),
  delete: (id) => api.delete(`/events/${id}`),
};

// Locations API
export const locationsApi = {
  create: (data) => api.post('/locations', data),
  getById: (eventId, locationId) => api.get(`/locations/${eventId}/${locationId}`),
  getByEvent: (eventId) => api.get(`/locations/event/${eventId}`),
  update: (eventId, locationId, data) => api.put(`/locations/${eventId}/${locationId}`, data),
  delete: (eventId, locationId) => api.delete(`/locations/${eventId}/${locationId}`),
};

// Assignments API
export const assignmentsApi = {
  create: (data) => api.post('/assignments', data),
  getById: (eventId, assignmentId) => api.get(`/assignments/${eventId}/${assignmentId}`),
  getByEvent: (eventId) => api.get(`/assignments/event/${eventId}`),
  delete: (eventId, assignmentId) => api.delete(`/assignments/${eventId}/${assignmentId}`),
  getEventStatus: (eventId) => api.get(`/assignments/status/${eventId}`),
};

// Check-in API
export const checkInApi = {
  checkIn: (data) => api.post('/checkin', data),
  adminCheckIn: (assignmentId) => api.post(`/checkin/admin/${assignmentId}`),
};

export default api;
