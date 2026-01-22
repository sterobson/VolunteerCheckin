import { ref, computed } from 'vue';
import { incidentsApi } from '../services/api';
import { denormalizeIncidentsList } from '../utils/denormalize';

/**
 * Composable for managing marshal incidents functionality
 */
export function useMarshalIncidents({
  eventId,
  sectionLastLoadedAt,
  showMessage,
}) {
  // State
  const myIncidents = ref([]);
  const incidentsLoading = ref(false);
  const selectedIncident = ref(null);
  const showIncidentDetail = ref(false);
  const showAddIncidentNoteModal = ref(false);
  const incidentNoteText = ref('');
  const submittingIncidentNote = ref(false);
  const showReportIncident = ref(false);
  const reportingIncident = ref(false);

  // Count of open incidents for badge
  const openIncidentsCount = computed(() => {
    return myIncidents.value.filter(i =>
      i.status === 'open' || i.status === 'acknowledged' || i.status === 'in_progress'
    ).length;
  });

  // Load incidents for "Your incidents" section
  // The backend filters incidents based on user role:
  // - Admins see all incidents
  // - Area leads see incidents they reported, at their checkpoints, or in their areas
  // - Marshals see incidents they reported or at their checkpoints
  const loadMyIncidents = async () => {
    const evtId = eventId.value;
    incidentsLoading.value = true;

    try {
      const response = await incidentsApi.getAll(evtId);
      const denormalized = denormalizeIncidentsList(response.data);
      const incidents = denormalized.incidents || [];

      myIncidents.value = incidents.sort((a, b) => {
        // Sort by date, most recent first
        return new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt);
      });

      if (sectionLastLoadedAt?.value) {
        sectionLastLoadedAt.value.incidents = Date.now();
      }
    } catch (error) {
      console.error('Failed to load incidents:', error);
    } finally {
      incidentsLoading.value = false;
    }
  };

  // Incident reporting
  const handleReportIncident = async (incidentData) => {
    reportingIncident.value = true;
    try {
      await incidentsApi.create(eventId.value, incidentData);
      showReportIncident.value = false;
      // Reload incidents to show the new one
      loadMyIncidents();
      if (showMessage) {
        showMessage('Incident Reported', 'An administrator will review it shortly.');
      }
    } catch (error) {
      console.error('Failed to report incident:', error);
      if (showMessage) {
        showMessage('Error', error.response?.data?.message || 'Failed to report incident. Please try again.');
      }
    } finally {
      reportingIncident.value = false;
    }
  };

  // Incident detail handlers
  const openIncidentDetail = (incident) => {
    selectedIncident.value = incident;
    showIncidentDetail.value = true;
  };

  const closeIncidentDetail = () => {
    showIncidentDetail.value = false;
    selectedIncident.value = null;
  };

  const handleIncidentStatusChange = async ({ incidentId, status }) => {
    try {
      await incidentsApi.updateStatus(eventId.value, incidentId, { status });
      // Update local state
      const incident = myIncidents.value.find(i => i.incidentId === incidentId);
      if (incident) {
        incident.status = status;
      }
      if (selectedIncident.value?.incidentId === incidentId) {
        selectedIncident.value.status = status;
      }
    } catch (error) {
      console.error('Failed to update incident status:', error);
      if (showMessage) {
        showMessage('Error', 'Failed to update status. Please try again.');
      }
    }
  };

  const openAddIncidentNoteModal = () => {
    incidentNoteText.value = '';
    showAddIncidentNoteModal.value = true;
  };

  const closeAddIncidentNoteModal = () => {
    showAddIncidentNoteModal.value = false;
    incidentNoteText.value = '';
  };

  const submitIncidentNote = async () => {
    if (!incidentNoteText.value.trim() || !selectedIncident.value) return;

    submittingIncidentNote.value = true;
    try {
      await incidentsApi.addNote(eventId.value, selectedIncident.value.incidentId, incidentNoteText.value.trim());
      // Close the modal
      closeAddIncidentNoteModal();
      // Reload to get the updated incident with new note
      loadMyIncidents();
      // Also reload the selected incident
      const response = await incidentsApi.get(eventId.value, selectedIncident.value.incidentId);
      selectedIncident.value = response.data;
    } catch (error) {
      console.error('Failed to add note:', error);
      if (showMessage) {
        showMessage('Error', 'Failed to add note. Please try again.');
      }
    } finally {
      submittingIncidentNote.value = false;
    }
  };

  return {
    // State
    myIncidents,
    incidentsLoading,
    selectedIncident,
    showIncidentDetail,
    showAddIncidentNoteModal,
    incidentNoteText,
    submittingIncidentNote,
    showReportIncident,
    reportingIncident,

    // Computed
    openIncidentsCount,

    // Functions
    loadMyIncidents,
    handleReportIncident,
    openIncidentDetail,
    closeIncidentDetail,
    handleIncidentStatusChange,
    openAddIncidentNoteModal,
    closeAddIncidentNoteModal,
    submitIncidentNote,
  };
}
