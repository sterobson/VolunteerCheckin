import axios from 'axios';
import { API_BASE_URL } from '../config';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add interceptor to include admin email header
api.interceptors.request.use((config) => {
  const adminEmail = localStorage.getItem('adminEmail');
  if (adminEmail) {
    config.headers['X-Admin-Email'] = adminEmail;
  }
  return config;
});

// Auth API
export const authApi = {
  instantLogin: (email) => api.post('/auth/instant-login', { email }),
  createAdmin: (email) => api.post('/auth/create-admin', { email }),
};

// Events API
export const eventsApi = {
  create: (data) => api.post('/events', data),
  getAll: () => api.get('/events'),
  getById: (id) => api.get(`/events/${id}`),
  update: (id, data) => api.put(`/events/${id}`, data),
  delete: (id) => api.delete(`/events/${id}`),
  uploadGpx: (eventId, file) => {
    const formData = new FormData();
    formData.append('gpx', file);
    return api.post(`/events/${eventId}/upload-gpx`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Locations API
export const locationsApi = {
  create: (data) => api.post('/locations', data),
  getById: (eventId, locationId) => api.get(`/locations/${eventId}/${locationId}`),
  getByEvent: (eventId) => api.get(`/events/${eventId}/locations`),
  update: (eventId, locationId, data) => api.put(`/locations/${eventId}/${locationId}`, data),
  delete: (eventId, locationId) => api.delete(`/locations/${eventId}/${locationId}`),
  importCsv: (eventId, file, deleteExisting) => {
    const formData = new FormData();
    formData.append('csv', file);
    return api.post(`/locations/import/${eventId}?deleteExisting=${deleteExisting}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  bulkUpdateTimes: (eventId, timeDelta) => api.post(`/locations/bulk-update-times/${eventId}`, { timeDelta }),
};

// Assignments API
export const assignmentsApi = {
  create: (data) => api.post('/assignments', data),
  getById: (eventId, assignmentId) => api.get(`/assignments/${eventId}/${assignmentId}`),
  getByEvent: (eventId) => api.get(`/assignments/event/${eventId}`),
  delete: (eventId, assignmentId) => api.delete(`/assignments/${eventId}/${assignmentId}`),
  getEventStatus: (eventId) => api.get(`/events/${eventId}/status`),
};

// Check-in API
export const checkInApi = {
  checkIn: (data) => api.post('/checkin', data),
  adminCheckIn: (eventId, assignmentId) => api.post(`/checkin/admin/${eventId}/${assignmentId}`),
};

// Event Admins API
export const eventAdminsApi = {
  getAdmins: (eventId) => api.get(`/events/${eventId}/admins`),
  addAdmin: (eventId, userEmail) => api.post(`/events/${eventId}/admins`, { userEmail }),
  removeAdmin: (eventId, userEmail) => api.delete(`/events/${eventId}/admins/${userEmail}`),
};

// Marshals API
export const marshalsApi = {
  create: (data) => api.post('/marshals', data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/marshals`),
  getById: (eventId, marshalId) => api.get(`/marshals/${eventId}/${marshalId}`),
  update: (eventId, marshalId, data) => api.put(`/marshals/${eventId}/${marshalId}`, data),
  delete: (eventId, marshalId) => api.delete(`/marshals/${eventId}/${marshalId}`),
  importCsv: (eventId, file) => {
    const formData = new FormData();
    formData.append('csv', file);
    return api.post(`/marshals/import/${eventId}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

export default api;
