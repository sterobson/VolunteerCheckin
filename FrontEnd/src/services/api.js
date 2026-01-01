import axios from 'axios';
import { API_BASE_URL } from '../config';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Add interceptor to include auth headers
api.interceptors.request.use((config) => {
  const adminEmail = localStorage.getItem('adminEmail');
  if (adminEmail) {
    config.headers['X-Admin-Email'] = adminEmail;
  }
  const sessionToken = localStorage.getItem('sessionToken');
  if (sessionToken) {
    config.headers['Authorization'] = `Bearer ${sessionToken}`;
  }
  return config;
});

// Auth API
export const authApi = {
  requestLogin: (email) => api.post('/auth/request-login', { email }),
  verifyToken: (token) => api.post('/auth/verify-token', { token }),
  instantLogin: (email) => api.post('/auth/instant-login', { email }),
  createAdmin: (email) => api.post('/auth/create-admin', { email }),
  marshalLogin: (eventId, magicCode) => api.post('/auth/marshal-login', { eventId, magicCode }),
  getMe: (eventId) => api.get('/auth/me', { params: { eventId } }),
  logout: () => api.post('/auth/logout'),
};

// Profile API
export const profileApi = {
  getProfile: () => api.get('/auth/profile'),
  updateProfile: (data) => api.put('/auth/profile', data),
  getPerson: (personId, eventId) => api.get(`/people/${personId}`, { params: { eventId } }),
  updatePerson: (personId, eventId, data) => api.put(`/people/${personId}`, data, { params: { eventId } }),
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
  getMagicLink: (eventId, marshalId) => api.get(`/marshals/${eventId}/${marshalId}/magic-link`),
  sendMagicLink: (eventId, marshalId) => api.post(`/marshals/${eventId}/${marshalId}/send-magic-link`),
  importCsv: (eventId, file) => {
    const formData = new FormData();
    formData.append('csv', file);
    return api.post(`/marshals/import/${eventId}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Areas API
export const areasApi = {
  create: (data) => api.post('/areas', data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/areas`),
  getById: (eventId, areaId) => api.get(`/areas/${eventId}/${areaId}`),
  update: (eventId, areaId, data) => api.put(`/areas/${eventId}/${areaId}`, data),
  delete: (eventId, areaId) => api.delete(`/areas/${eventId}/${areaId}`),
  recalculate: (eventId) => api.post(`/areas/recalculate/${eventId}`),
  getCheckpoints: (eventId, areaId) => api.get(`/areas/${eventId}/${areaId}/locations`),
  getAreaLeads: (eventId, areaId) => api.get(`/areas/${eventId}/${areaId}/leads`),
};

// Checklist API
export const checklistApi = {
  create: (eventId, data) => api.post(`/events/${eventId}/checklist-items`, data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/checklist-items`),
  getById: (eventId, itemId) => api.get(`/checklist-items/${eventId}/${itemId}`),
  update: (eventId, itemId, data) => api.put(`/checklist-items/${eventId}/${itemId}`, data),
  delete: (eventId, itemId) => api.delete(`/checklist-items/${eventId}/${itemId}`),
  getReport: (eventId) => api.get(`/events/${eventId}/checklist-report`),
  getMarshalChecklist: (eventId, marshalId) => api.get(`/events/${eventId}/marshals/${marshalId}/checklist`),
  getCheckpointChecklist: (eventId, locationId) => api.get(`/events/${eventId}/locations/${locationId}/checklist`),
  getAreaChecklist: (eventId, areaId) => api.get(`/events/${eventId}/areas/${areaId}/checklist`),
  complete: (eventId, itemId, data) => api.post(`/checklist-items/${eventId}/${itemId}/complete`, data),
  uncomplete: (eventId, itemId, data) => api.post(`/checklist-items/${eventId}/${itemId}/uncomplete`, data),
};

// Notes API
export const notesApi = {
  create: (eventId, data) => api.post(`/events/${eventId}/notes`, data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/notes`),
  getById: (eventId, noteId) => api.get(`/events/${eventId}/notes/${noteId}`),
  update: (eventId, noteId, data) => api.put(`/events/${eventId}/notes/${noteId}`, data),
  delete: (eventId, noteId) => api.delete(`/events/${eventId}/notes/${noteId}`),
  getMarshalNotes: (eventId, marshalId) => api.get(`/events/${eventId}/marshals/${marshalId}/notes`),
  getMyNotes: (eventId) => api.get(`/events/${eventId}/my-notes`),
};

export default api;
