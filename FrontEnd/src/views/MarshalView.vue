<template>
  <div class="marshal-view">
    <!-- Offline Indicator -->
    <OfflineIndicator />

    <header class="header">
      <div class="header-title">
        <h1 v-if="event">{{ event.name }}</h1>
        <div v-if="event?.eventDate" class="header-event-date">
          {{ formatEventDate(event.eventDate) }}
        </div>
      </div>
      <div class="header-actions">
        <button v-if="isAuthenticated" @click="showEmergency = true" class="btn-emergency">
          Emergency Info
        </button>
        <button v-if="isAuthenticated" @click="handleLogout" class="btn-logout">
          Logout {{ currentPerson?.name || '' }}
        </button>
      </div>
    </header>

    <div class="container">
      <!-- Loading State -->
      <div v-if="loading" class="selection-card">
        <div class="loading">Loading...</div>
      </div>

      <!-- Login with Magic Code -->
      <div v-else-if="!isAuthenticated && !loginError" class="selection-card">
        <h2>Welcome</h2>
        <p class="instruction">Please use the login link sent to you to access your marshal dashboard.</p>
        <p v-if="authenticating" class="loading">Authenticating...</p>
      </div>

      <!-- Login Error -->
      <div v-else-if="loginError" class="selection-card error-card">
        <h2>Login Failed</h2>
        <p class="error">{{ loginError }}</p>
        <p class="instruction">Please contact the event organizer for a new login link.</p>
      </div>

      <!-- Marshal Dashboard -->
      <div v-else class="dashboard">
        <div v-if="canSwitchToAdmin || isAdminButNeedsReauth" class="welcome-bar">
          <div class="welcome-info">
            <span v-if="currentPerson?.email" class="welcome-email">{{ currentPerson.email }}</span>
          </div>
          <div v-if="canSwitchToAdmin" class="mode-switch">
            <button @click="switchToAdminMode" class="btn-mode-switch">
              Switch to Admin
            </button>
          </div>
          <div v-else-if="isAdminButNeedsReauth" class="mode-switch">
            <span class="reauth-hint" title="Login via admin flow to access admin features">
              Admin access requires re-login
            </span>
          </div>
        </div>

        <!-- Accordion Sections -->
        <div class="accordion">
          <!-- Event Details Section -->
          <!-- Route/Map Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'map' }"
              @click="toggleSection('map')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('course')"></span>
                {{ terms.course }}
                <span v-if="userLocation" class="location-status active">GPS Active</span>
                <span v-else class="location-status">GPS Inactive</span>
              </span>
              <span class="accordion-icon">{{ expandedSection === 'map' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'map'" class="accordion-content map-content">
              <!-- Map selection mode banner -->
              <div v-if="selectingLocationOnMap" class="map-selection-banner">
                <span>Click on the map to set the new location for <strong>{{ updatingLocationFor?.location?.name }}</strong></span>
                <button @click="cancelMapLocationSelect" class="btn btn-secondary btn-sm">Cancel</button>
              </div>
              <MapView
                :locations="allLocations"
                :route="eventRoute"
                :center="mapCenter"
                :zoom="15"
                :user-location="userLocation"
                :highlight-location-ids="assignmentLocationIds"
                :marshal-mode="true"
                :clickable="selectingLocationOnMap || hasDynamicAssignment"
                :class="{ 'selecting-location': selectingLocationOnMap }"
                @map-click="handleMapClick"
                @location-click="handleLocationClick"
              />
            </div>
          </div>

          <!-- Assignments Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'assignments' }"
              @click="toggleSection('assignments')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('checkpoint')"></span>
                Your {{ termsLower.checkpoints }} ({{ assignments.length }})
              </span>
              <span class="accordion-icon">{{ expandedSection === 'assignments' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'assignments'" class="accordion-content assignments-accordion-content">
              <div v-if="assignments.length === 0" class="empty-state">
                <p>You don't have any {{ termsLower.checkpoint }} assignments yet.</p>
              </div>
              <div v-else class="checkpoint-accordion">
                <div v-for="assign in assignmentsWithDetails" :key="assign.id" class="checkpoint-accordion-section">
                  <button
                    class="checkpoint-accordion-header"
                    :class="{ active: expandedCheckpoint === assign.id, 'checked-in': assign.isCheckedIn }"
                    @click="toggleCheckpoint(assign.id)"
                  >
                    <div class="checkpoint-header-content">
                      <div class="checkpoint-title-row">
                        <span class="checkpoint-icon" v-html="getCheckpointIconSvg(assign.location)"></span>
                        <span v-if="assign.isCheckedIn" class="checkpoint-check-icon">✓</span>
                        <span class="checkpoint-name">{{ assign.location?.name || assign.locationName }}</span>
                        <span v-if="assign.areaName" class="area-badge">{{ assign.areaName }}</span>
                        <span v-if="assign.location?.startTime || assign.location?.endTime" class="checkpoint-time-badge">
                          {{ formatTimeRange(assign.location.startTime, assign.location.endTime) }}
                        </span>
                      </div>
                      <div v-if="assign.location?.description" class="checkpoint-description-preview">
                        {{ assign.location.description }}
                      </div>
                    </div>
                    <span class="accordion-icon">{{ expandedCheckpoint === assign.id ? '−' : '+' }}</span>
                  </button>
                  <div v-if="expandedCheckpoint === assign.id" class="checkpoint-accordion-content">
                    <!-- Mini-map for this checkpoint -->
                    <div v-if="assign.location" class="checkpoint-mini-map">
                      <MapView
                        :locations="allLocations"
                        :route="eventRoute"
                        :center="{ lat: assign.location.latitude, lng: assign.location.longitude }"
                        :zoom="16"
                        :user-location="userLocation"
                        :highlight-location-id="assign.locationId"
                        :marshal-mode="true"
                        :simplify-non-highlighted="true"
                        :clickable="hasDynamicAssignment"
                        @map-click="handleMapClick"
                        @location-click="handleLocationClick"
                      />
                    </div>

                    <!-- Marshals on this checkpoint -->
                    <div class="checkpoint-marshals">
                      <div class="marshals-label">{{ terms.people }}:</div>
                      <div class="marshals-list">
                        <span
                          v-for="m in assign.allMarshals"
                          :key="m.marshalId"
                          class="marshal-tag"
                          :class="{ 'is-you': m.marshalId === currentMarshalId, 'checked-in': m.isCheckedIn }"
                        >
                          {{ m.marshalName }}{{ m.marshalId === currentMarshalId ? ' (you)' : '' }}
                          <span v-if="m.isCheckedIn" class="check-badge">✓</span>
                        </span>
                      </div>
                    </div>

                    <!-- Area Contacts for this checkpoint -->
                    <div v-if="assign.areaContacts.length > 0" class="checkpoint-area-contacts">
                      <div class="marshals-label">{{ terms.area }} contacts:</div>
                      <div class="contact-list">
                        <div v-for="contact in assign.areaContacts" :key="contact.marshalId" class="contact-item">
                          <div class="contact-info">
                            <div class="contact-name">{{ contact.marshalName }}</div>
                            <div v-if="contact.role" class="contact-role">{{ contact.role }}</div>
                            <div v-if="contact.phone" class="contact-detail">{{ contact.phone }}</div>
                            <div v-if="contact.email" class="contact-detail">{{ contact.email }}</div>
                          </div>
                          <div class="contact-actions">
                            <a v-if="contact.phone" :href="`tel:${contact.phone}`" class="contact-link">
                              <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"/>
                              </svg>
                              Call
                            </a>
                            <a v-if="contact.phone" :href="`sms:${contact.phone}`" class="contact-link">
                              <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/>
                              </svg>
                              Text
                            </a>
                            <a v-if="contact.email" :href="`mailto:${contact.email}`" class="contact-link">
                              <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                                <path d="M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z"/>
                              </svg>
                              Email
                            </a>
                          </div>
                        </div>
                      </div>
                    </div>

                    <!-- Check-in status and actions -->
                    <div v-if="assign.isCheckedIn" class="checked-in-status">
                      <span class="check-icon">✓</span>
                      <div>
                        <strong>Checked In</strong>
                        <p class="check-time">{{ formatTime(assign.checkInTime) }}</p>
                      </div>
                    </div>
                    <div v-else class="check-in-actions">
                      <button
                        @click="handleCheckIn(assign)"
                        class="btn btn-primary"
                        :disabled="checkingIn === assign.id"
                      >
                        {{ checkingIn === assign.id ? 'Checking in...' : 'GPS Check-In' }}
                      </button>
                      <button
                        @click="handleManualCheckIn(assign)"
                        class="btn btn-secondary"
                        :disabled="checkingIn === assign.id"
                      >
                        Manual
                      </button>
                    </div>
                    <div v-if="checkInError && checkingInAssignment === assign.id" class="error">{{ checkInError }}</div>

                    <!-- Dynamic checkpoint location update -->
                    <div v-if="isDynamicCheckpoint(assign.location)" class="dynamic-location-section">
                      <div class="dynamic-location-header">
                        <span class="dynamic-badge">Dynamic {{ assign.location.resolvedCheckpointTerm || terms.checkpoint }}</span>
                        <span v-if="assign.location.lastLocationUpdate" class="last-update">
                          Last updated: {{ formatDateTime(assign.location.lastLocationUpdate) }}
                        </span>
                      </div>
                      <div class="dynamic-location-actions">
                        <button
                          @click="openLocationUpdateModal(assign)"
                          class="btn btn-update-location"
                          :disabled="updatingLocation"
                        >
                          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
                          </svg>
                          Update location
                        </button>
                        <button
                          @click="toggleAutoUpdate(assign)"
                          class="btn btn-auto-update"
                          :class="{ active: autoUpdateEnabled }"
                          :title="autoUpdateEnabled ? 'Stop auto-updating' : 'Auto-update every 60 seconds'"
                        >
                          <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/>
                          </svg>
                          {{ autoUpdateEnabled ? 'Stop' : 'Auto' }}
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Checklist Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'checklist' }"
              @click="toggleSection('checklist')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('checklist')"></span>
                Your {{ termsLower.checklists }} ({{ completedChecklistCount }} of {{ visibleChecklistItems.length }} complete)
              </span>
              <span class="accordion-icon">{{ expandedSection === 'checklist' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'checklist'" class="accordion-content">
              <div v-if="checklistLoading" class="loading">Loading checklist...</div>
              <div v-else-if="checklistError" class="error">{{ checklistError }}</div>
              <div v-else-if="visibleChecklistItems.length === 0" class="empty-state">
                <p>No {{ termsLower.checklists }} for you.</p>
              </div>
              <div v-else class="checklist-items">
                <div
                  v-for="item in visibleChecklistItems"
                  :key="item.itemId"
                  class="checklist-item"
                  :class="{ 'item-completed': item.isCompleted }"
                >
                  <div class="item-checkbox">
                    <input
                      type="checkbox"
                      :checked="item.isCompleted"
                      :disabled="!item.canBeCompletedByMe || savingChecklist"
                      @change="handleToggleChecklist(item)"
                    />
                  </div>
                  <div class="item-content">
                    <div class="item-text" :class="{ 'text-completed': item.isCompleted }">{{ item.text }}</div>
                    <div v-if="getContextName(item)" class="item-context">
                      {{ getContextName(item) }}
                    </div>
                    <div v-if="item.isCompleted" class="completion-info">
                      <span class="completion-text">
                        Completed by {{ item.completedByActorName || 'Unknown' }}
                      </span>
                      <span class="completion-time">
                        {{ formatDateTime(item.completedAt) }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Contacts Section -->
          <div v-if="eventContacts.length > 0 || contactsLoading" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'eventContacts' }"
              @click="toggleSection('eventContacts')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('contacts')"></span>
                Your contacts ({{ eventContacts.length }})
              </span>
              <span class="accordion-icon">{{ expandedSection === 'eventContacts' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'eventContacts'" class="accordion-content">
              <div v-if="contactsLoading" class="loading">Loading contacts...</div>
              <div v-else class="contact-list">
                <div v-for="contact in eventContacts" :key="contact.contactId" class="contact-item" :class="{ 'primary-contact': contact.isPrimary }">
                  <div class="contact-info">
                    <div class="contact-name-row">
                      <span v-if="contact.isPrimary" class="primary-badge">★</span>
                      <span class="contact-name">{{ contact.name }}</span>
                      <span v-if="contact.role" class="contact-role-badge">{{ formatRoleName(contact.role) }}</span>
                    </div>
                    <div v-if="contact.notes" class="contact-notes-text">{{ contact.notes }}</div>
                    <div v-if="contact.phone" class="contact-detail">{{ contact.phone }}</div>
                    <div v-if="contact.email" class="contact-detail">{{ contact.email }}</div>
                  </div>
                  <div class="contact-actions">
                    <a v-if="contact.phone" :href="`tel:${contact.phone}`" class="contact-link">
                      <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"/>
                      </svg>
                      Call
                    </a>
                    <a v-if="contact.phone" :href="`sms:${contact.phone}`" class="contact-link">
                      <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/>
                      </svg>
                      Text
                    </a>
                    <a v-if="contact.email" :href="`mailto:${contact.email}`" class="contact-link">
                      <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z"/>
                      </svg>
                      Email
                    </a>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Notes Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'notes' }"
              @click="toggleSection('notes')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('notes')"></span>
                Your notes ({{ notes.length }})
              </span>
              <span class="accordion-icon">{{ expandedSection === 'notes' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'notes'" class="accordion-content">
              <!-- Notes are pre-filtered by getMyNotes API, so don't pass marshal-id to avoid re-filtering -->
              <NotesView
                :event-id="route.params.eventId"
                :all-notes="notes"
                :locations="allLocations"
                :areas="areas"
                :assignments="assignments"
                :show-scope="false"
                @notes-changed="loadNotes"
              />
            </div>
          </div>

          <!-- Area Lead Section (if user is an area lead) -->
          <div v-if="isAreaLead" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'areaLead' }"
              @click="toggleSection('areaLead')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('people')"></span>
                Your {{ termsLower.people }}
                <span v-if="areaLeadRef?.areas?.length > 0" class="area-lead-badges">
                  <span
                    v-for="area in areaLeadRef.areas"
                    :key="area.areaId"
                    class="area-lead-badge"
                    :style="{ backgroundColor: area.color || '#667eea' }"
                  >
                    {{ area.name }}
                  </span>
                </span>
              </span>
              <span class="accordion-icon">{{ expandedSection === 'areaLead' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'areaLead'" class="accordion-content area-lead-accordion-content">
              <AreaLeadSection
                ref="areaLeadRef"
                :event-id="route.params.eventId"
                :area-ids="areaLeadAreaIds"
                :marshal-id="currentMarshalId"
                :route="eventRoute"
                @checklist-updated="loadChecklist"
              />
            </div>
          </div>

          <!-- Event Details Section (at the end) -->
          <div v-if="event?.eventDate || event?.description" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'eventDetails' }"
              @click="toggleSection('eventDetails')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('event')"></span>
                Event details
              </span>
              <span class="accordion-icon">{{ expandedSection === 'eventDetails' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'eventDetails'" class="accordion-content">
              <div class="event-details">
                <div v-if="event.eventDate" class="event-detail-row">
                  <span class="detail-label">Date and time</span>
                  <span class="detail-value">{{ formatEventDateTime(event.eventDate) }}</span>
                </div>
                <div v-if="event.description" class="event-description">
                  <span class="detail-label">Description</span>
                  <p class="detail-value">{{ event.description }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Emergency Contact Modal -->
    <EmergencyContactModal
      :show="showEmergency"
      :contacts="emergencyContacts"
      :notes="emergencyNotes"
      @close="showEmergency = false"
    />

    <ConfirmModal
      :show="showConfirmModal"
      :title="confirmModalTitle"
      :message="confirmModalMessage"
      @confirm="handleConfirmModalConfirm"
      @cancel="handleConfirmModalCancel"
    />

    <!-- Location Update Modal -->
    <div v-if="showLocationUpdateModal" class="modal-overlay" @click.self="closeLocationUpdateModal">
      <div class="modal-content location-update-modal">
        <div class="modal-header">
          <h3>Update {{ (updatingLocationFor?.location?.resolvedCheckpointTerm || terms.checkpoint).toLowerCase() }} location</h3>
          <button class="modal-close" @click="closeLocationUpdateModal">&times;</button>
        </div>

        <div class="modal-body">
          <div v-if="locationUpdateSuccess" class="success-message">
            {{ locationUpdateSuccess }}
          </div>
          <div v-else>
            <p class="modal-description">
              Update the position of <strong>{{ updatingLocationFor?.location?.name }}</strong>
            </p>

            <!-- Last update info -->
            <div v-if="updatingLocationFor?.location?.lastLocationUpdate" class="last-update-info">
              Last updated: {{ formatDateTime(updatingLocationFor.location.lastLocationUpdate) }}
            </div>

            <!-- GPS Status -->
            <div v-if="userLocation" class="gps-status active">
              <svg class="status-icon" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z"/>
              </svg>
              GPS active
            </div>

            <!-- GPS Option -->
            <div class="update-option">
              <button
                @click="updateLocationWithGps"
                class="btn btn-primary btn-full"
                :disabled="updatingLocation"
              >
                <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M12 8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4zm8.94 3c-.46-4.17-3.77-7.48-7.94-7.94V1h-2v2.06C6.83 3.52 3.52 6.83 3.06 11H1v2h2.06c.46 4.17 3.77 7.48 7.94 7.94V23h2v-2.06c4.17-.46 7.48-3.77 7.94-7.94H23v-2h-2.06zM12 19c-3.87 0-7-3.13-7-7s3.13-7 7-7 7 3.13 7 7-3.13 7-7 7z"/>
                </svg>
                {{ updatingLocation ? 'Updating...' : 'Use current GPS location' }}
              </button>
            </div>

            <!-- Auto-update Option -->
            <div class="update-option auto-update-option">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  :checked="autoUpdateEnabled"
                  @change="toggleAutoUpdate(updatingLocationFor)"
                />
                <span>Auto-update every 60 seconds</span>
              </label>
              <p class="option-hint">Automatically updates the {{ termsLower.checkpoint }} location using your GPS every minute.</p>
            </div>

            <!-- Select on Map Option -->
            <div class="update-option">
              <button
                @click="startMapLocationSelect"
                class="btn btn-secondary btn-full"
                :disabled="updatingLocation"
              >
                <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
                  <path d="M20.5 3l-.16.03L15 5.1 9 3 3.36 4.9c-.21.07-.36.25-.36.48V20.5c0 .28.22.5.5.5l.16-.03L9 18.9l6 2.1 5.64-1.9c.21-.07.36-.25.36-.48V3.5c0-.28-.22-.5-.5-.5zM15 19l-6-2.11V5l6 2.11V19z"/>
                </svg>
                Select location on map
              </button>
            </div>

            <!-- Copy from Checkpoint Option -->
            <div class="update-option">
              <label class="option-label">Or copy location from another {{ termsLower.checkpoint }}:</label>
              <div class="checkpoint-list" v-if="availableSourceCheckpoints.length > 0">
                <button
                  v-for="loc in availableSourceCheckpoints"
                  :key="loc.id"
                  @click="updateLocationFromCheckpoint(loc.id)"
                  class="btn btn-secondary btn-checkpoint-source"
                  :disabled="updatingLocation"
                >
                  {{ loc.name }}
                </button>
              </div>
              <p v-else class="no-checkpoints">
                No other {{ termsLower.checkpoints }} available to copy from.
              </p>
            </div>

            <div v-if="locationUpdateError" class="error">{{ locationUpdateError }}</div>
          </div>
        </div>

        <div class="modal-footer">
          <button @click="closeLocationUpdateModal" class="btn btn-secondary">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { authApi, checkInApi, checklistApi, eventsApi, assignmentsApi, locationsApi, areasApi, notesApi, contactsApi, queueOfflineAction, getOfflineMode } from '../services/api';
import MapView from '../components/MapView.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import EmergencyContactModal from '../components/event-manage/modals/EmergencyContactModal.vue';
import AreaLeadSection from '../components/AreaLeadSection.vue';
import NotesView from '../components/NotesView.vue';
import OfflineIndicator from '../components/OfflineIndicator.vue';
import { setTerminology, useTerminology } from '../composables/useTerminology';
import { getIcon } from '../utils/icons';
import { generateCheckpointSvg, getStatusColor } from '../constants/checkpointIcons';
import { useOffline } from '../composables/useOffline';
import { cacheEventData, getCachedEventData, updateCachedField } from '../services/offlineDb';

const { terms, termsLower } = useTerminology();

// Offline support
const {
  isFullyOnline,
  pendingActionsCount,
  updatePendingCount
} = useOffline();

const route = useRoute();
const router = useRouter();

// Auth state
const isAuthenticated = ref(false);
const authenticating = ref(false);
const loginError = ref(null);
const currentPerson = ref(null);
const currentMarshalId = ref(null);
const userClaims = ref(null);

// Area lead state
const areaLeadAreaIds = computed(() => {
  if (!userClaims.value?.eventRoles) return [];
  const areaLeadRoles = userClaims.value.eventRoles.filter(r => r.role === 'EventAreaLead');
  return areaLeadRoles.flatMap(r => r.areaIds || []);
});

const isAreaLead = computed(() => areaLeadAreaIds.value.length > 0);

// Check if user is an event admin
const isEventAdmin = computed(() => {
  if (!userClaims.value?.eventRoles) return false;
  return userClaims.value.eventRoles.some(r => r.role === 'EventAdmin');
});

// Check if user can switch to admin mode (logged in via SecureEmailLink AND is admin)
const canSwitchToAdmin = computed(() => {
  return userClaims.value?.authMethod === 'SecureEmailLink' && isEventAdmin.value;
});

// Check if user is admin but logged in via magic code (needs re-auth for admin)
const isAdminButNeedsReauth = computed(() => {
  return userClaims.value?.authMethod === 'MarshalMagicCode' && isEventAdmin.value;
});

// Switch to admin mode
const switchToAdminMode = () => {
  router.push(`/admin/events/${route.params.eventId}`);
};

// Event data
const event = ref(null);
const allLocations = ref([]);
const assignments = ref([]);
const areas = ref([]);
const loading = ref(true);

// Accordion state
const expandedSection = ref('assignments');

// Check-in state
const checkingIn = ref(null); // Now stores the assignment ID being checked in
const checkingInAssignment = ref(null);
const checkInError = ref(null);

// Checklist state
const checklistItems = ref([]);
const checklistLoading = ref(false);
const checklistError = ref(null);
const savingChecklist = ref(false);

// Notes state
const notes = ref([]);

// Dynamic checkpoint update state
const showLocationUpdateModal = ref(false);
const updatingLocationFor = ref(null); // Holds the assignment being updated
const locationUpdateError = ref(null);
const locationUpdateSuccess = ref(null);
const updatingLocation = ref(false);
const autoUpdateEnabled = ref(false);
let autoUpdateInterval = null;
let dynamicCheckpointPollInterval = null;

// Area Lead ref
const areaLeadRef = ref(null);

// UI state
const showEmergency = ref(false);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

// GPS tracking
const userLocation = ref(null);
const locationLastUpdated = ref(null); // Track when location was last updated
let locationWatchId = null;

// Toggle accordion section
const toggleSection = (section) => {
  expandedSection.value = expandedSection.value === section ? null : section;
};

// Track which checkpoint accordion is expanded (by assignment ID)
const expandedCheckpoint = ref(null);

const toggleCheckpoint = (assignId) => {
  expandedCheckpoint.value = expandedCheckpoint.value === assignId ? null : assignId;
};

// Auto-expand if there's only one assignment
watch(() => assignments.value, (newAssignments) => {
  if (newAssignments.length === 1 && expandedCheckpoint.value === null) {
    expandedCheckpoint.value = newAssignments[0].id;
  }
}, { immediate: true });

// Assignments with details (location info, area name, all marshals on checkpoint, area contacts)
const assignmentsWithDetails = computed(() => {
  return assignments.value.map(assign => {
    const location = allLocations.value.find(loc => loc.id === assign.locationId);

    // Get area IDs from the location
    const areaIds = location?.areaIds || location?.AreaIds || [];
    let areaName = null;
    if (areaIds.length > 0 && areas.value.length > 0) {
      const area = areas.value.find(a => areaIds.includes(a.id));
      areaName = area?.name;
    }

    // Get all marshals assigned to this checkpoint
    const allMarshals = location?.assignments || [];

    // Get area contacts for this checkpoint's areas
    const areaContacts = areaContactsRaw.value.filter(contact =>
      areaIds.includes(contact.areaId)
    );

    return {
      ...assign,
      location,
      areaName,
      areaIds,
      allMarshals,
      areaContacts,
      locationName: location?.name || 'Unknown Location',
    };
  });
});

// Primary assignment (first one) for map highlighting
const primaryAssignment = computed(() => {
  return assignments.value.length > 0 ? assignments.value[0] : null;
});

// All assignment location IDs for multi-checkpoint highlighting
const assignmentLocationIds = computed(() => {
  return assignments.value.map(a => a.locationId);
});

// Check if user has any dynamic checkpoint assignments
const hasDynamicAssignment = computed(() => {
  return assignmentsWithDetails.value.some(a => a.location?.isDynamic || a.location?.IsDynamic);
});

// Get the user's first dynamic assignment (for quick updates)
const firstDynamicAssignment = computed(() => {
  return assignmentsWithDetails.value.find(a => a.location?.isDynamic || a.location?.IsDynamic);
});

// Event route for map display
const eventRoute = computed(() => {
  if (!event.value?.route) return [];
  // Route can be stored as JSON string or array
  const route = typeof event.value.route === 'string'
    ? JSON.parse(event.value.route)
    : event.value.route;
  return route || [];
});

const assignedLocation = computed(() => {
  if (!primaryAssignment.value || !allLocations.value.length) return null;
  return allLocations.value.find(loc => loc.id === primaryAssignment.value.locationId);
});

// Filter out completed shared tasks that the marshal doesn't need to see
// Keep: incomplete tasks, personal tasks (completed or not), tasks completed by this marshal
const visibleChecklistItems = computed(() => {
  return checklistItems.value.filter(item => {
    // Always show incomplete tasks
    if (!item.isCompleted) return true;

    // Hide completed shared tasks (OnePerCheckpoint, OnePerArea, OneLeadPerArea)
    // These are tasks where "anyone" could complete them, so once done, no need to show
    const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];
    if (sharedScopes.includes(item.matchedScope)) {
      return false;
    }

    // Show all other completed tasks (personal tasks, everyone tasks, etc.)
    return true;
  });
});

// Checklist completion count (from visible items only)
const completedChecklistCount = computed(() => {
  return visibleChecklistItems.value.filter(item => item.isCompleted).length;
});

// Event contacts - loaded from new contacts API
const myContacts = ref([]);
const contactsLoading = ref(false);

// Legacy event contacts computed for backwards compatibility
const eventContacts = computed(() => {
  // Use new contacts API data, filtering to show only event-wide contacts
  // (those not specifically tied to areas/checkpoints in their scope)
  return myContacts.value;
});

// Emergency contacts - filter for specific roles that should be shown in emergency modal
const emergencyContacts = computed(() => {
  const emergencyRoles = ['EmergencyContact', 'EventDirector', 'MedicalLead', 'SafetyOfficer'];
  return myContacts.value.filter(contact => emergencyRoles.includes(contact.role));
});

// Emergency notes - filter for Emergency or Urgent priority notes
const emergencyNotes = computed(() => {
  return notes.value.filter(note => {
    const priority = note.priority || note.Priority;
    return priority === 'Emergency' || priority === 'Urgent';
  });
});

// Area contacts - loaded from API, grouped by area
const areaContactsRaw = ref([]);

// Area contacts grouped by area
const areaContactsByArea = computed(() => {
  if (areaContactsRaw.value.length === 0) return [];

  // Group by area
  const grouped = {};
  for (const contact of areaContactsRaw.value) {
    const key = contact.areaId || 'unknown';
    if (!grouped[key]) {
      grouped[key] = {
        areaId: contact.areaId,
        areaName: contact.areaName || 'Unknown Area',
        contacts: [],
      };
    }
    grouped[key].contacts.push(contact);
  }

  return Object.values(grouped);
});

// Total area contacts count
const totalAreaContacts = computed(() => {
  return areaContactsRaw.value.length;
});

// Available source checkpoints for copying location (excludes the one being updated)
// Natural sort comparator for strings with numbers (e.g., "Checkpoint 2" before "Checkpoint 10")
const naturalSort = (a, b) => {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' });
};

const availableSourceCheckpoints = computed(() => {
  if (!updatingLocationFor.value) return [];
  const currentId = updatingLocationFor.value.locationId;
  // Show all checkpoints with valid coordinates (excluding current one and those at 0,0)
  return allLocations.value.filter(l =>
    l.id !== currentId &&
    l.latitude !== 0 &&
    l.longitude !== 0
  ).sort((a, b) => naturalSort(a.name, b.name));
});

const mapCenter = computed(() => {
  if (assignedLocation.value) {
    return {
      lat: assignedLocation.value.latitude,
      lng: assignedLocation.value.longitude,
    };
  }
  if (userLocation.value) {
    return userLocation.value;
  }
  if (allLocations.value.length > 0) {
    return {
      lat: allLocations.value[0].latitude,
      lng: allLocations.value[0].longitude,
    };
  }
  return { lat: 51.505, lng: -0.09 };
});

const authenticateWithMagicCode = async (eventId, code) => {
  authenticating.value = true;
  loginError.value = null;
  console.log('Authenticating with magic code:', { eventId, code });

  try {
    const response = await authApi.marshalLogin(eventId, code);
    console.log('Auth response:', response.data);

    if (response.data.success) {
      // Store session token
      localStorage.setItem('sessionToken', response.data.sessionToken);
      localStorage.setItem(`marshal_${eventId}`, response.data.marshalId);

      currentPerson.value = response.data.person;
      currentMarshalId.value = response.data.marshalId;
      isAuthenticated.value = true;
      console.log('Authenticated as marshal:', currentMarshalId.value, currentPerson.value?.name);

      // Fetch full claims to get role information
      try {
        const claimsResponse = await authApi.getMe(eventId);
        userClaims.value = claimsResponse.data;
        console.log('User claims:', userClaims.value);
      } catch (claimsError) {
        console.warn('Failed to fetch claims:', claimsError);
      }

      // Clear the code from URL to prevent re-authentication attempts
      router.replace({ path: route.path, query: {} });

      // Start heartbeat to keep LastAccessedDate updated
      startHeartbeat();

      // Load data after authentication
      await loadEventData();
    } else {
      loginError.value = response.data.message || 'Authentication failed';
    }
  } catch (error) {
    console.error('Authentication failed:', error);
    loginError.value = error.response?.data?.message || 'Invalid or expired login link';
  } finally {
    authenticating.value = false;
  }
};

const checkExistingSession = async (eventId) => {
  const sessionToken = localStorage.getItem('sessionToken');
  const marshalId = localStorage.getItem(`marshal_${eventId}`);

  if (!sessionToken || !marshalId) {
    return false;
  }

  try {
    const response = await authApi.getMe(eventId);

    if (response.data && response.data.marshalId) {
      userClaims.value = response.data;
      currentPerson.value = {
        personId: response.data.personId,
        name: response.data.personName,
        email: response.data.personEmail,
      };
      currentMarshalId.value = response.data.marshalId;
      isAuthenticated.value = true;

      // Start heartbeat to keep LastAccessedDate updated
      startHeartbeat();

      return true;
    }
  } catch (error) {
    // Session invalid, clear it
    localStorage.removeItem('sessionToken');
    localStorage.removeItem(`marshal_${eventId}`);
  }

  return false;
};

const loadEventData = async () => {
  const eventId = route.params.eventId;
  console.log('Loading event data for:', { eventId, marshalId: currentMarshalId.value });

  // Check if we're offline and have cached data
  if (getOfflineMode()) {
    console.log('Offline mode detected, attempting to load from cache...');
    const cachedData = await getCachedEventData(eventId);
    if (cachedData) {
      console.log('Loaded cached data from:', cachedData.cachedAt);
      event.value = cachedData.event;
      areas.value = cachedData.areas || [];
      allLocations.value = cachedData.locations || [];
      checklistItems.value = cachedData.checklist || [];
      myContacts.value = cachedData.contacts || [];
      notes.value = cachedData.notes || [];

      // Apply terminology settings
      if (event.value) {
        setTerminology(event.value);
      }

      // Extract assignments for current marshal
      if (currentMarshalId.value) {
        const myAssignments = [];
        for (const location of allLocations.value) {
          const myAssignment = location.assignments?.find(a => a.marshalId === currentMarshalId.value);
          if (myAssignment) {
            myAssignments.push(myAssignment);
          }
        }
        assignments.value = myAssignments;
      }

      return;
    } else {
      console.warn('No cached data available while offline');
    }
  }

  // Load all data in parallel for better performance
  console.log('Fetching event data in parallel...');

  // Phase 1: Core event data (3 calls in parallel)
  const [eventResult, areasResult, statusResult] = await Promise.allSettled([
    eventsApi.getById(eventId),
    areasApi.getByEvent(eventId),
    assignmentsApi.getEventStatus(eventId)
  ]);

  // Process event result
  if (eventResult.status === 'fulfilled') {
    event.value = eventResult.value.data;
    console.log('Event loaded:', event.value?.name);
    if (event.value) {
      setTerminology(event.value);
    }
  } else {
    console.error('Failed to load event:', eventResult.reason);
  }

  // Process areas result
  if (areasResult.status === 'fulfilled') {
    areas.value = areasResult.value.data || [];
    console.log('Areas loaded:', areas.value.length);
  } else {
    console.error('Failed to load areas:', areasResult.reason);
  }

  // Process event status result
  if (statusResult.status === 'fulfilled') {
    allLocations.value = statusResult.value.data?.locations || [];
    console.log('Locations loaded:', allLocations.value.length);

    // Find ALL of current marshal's assignments from all locations
    if (currentMarshalId.value) {
      const myAssignments = [];
      for (const location of allLocations.value) {
        const myAssignment = location.assignments?.find(a => a.marshalId === currentMarshalId.value);
        if (myAssignment) {
          myAssignments.push(myAssignment);
        }
      }
      assignments.value = myAssignments;
      console.log('Total assignments found:', assignments.value.length);
    }
  } else {
    console.error('Failed to load event status:', statusResult.reason);
  }

  // Phase 2: Supplementary data (3 calls in parallel)
  await Promise.allSettled([
    loadChecklist(),
    loadContacts(),
    loadNotes()
  ]);

  // Cache all data for offline access
  // Use JSON.parse(JSON.stringify()) to convert Vue reactive proxies to plain objects
  try {
    const plainData = JSON.parse(JSON.stringify({
      event: event.value,
      areas: areas.value,
      locations: allLocations.value,
      checklist: checklistItems.value,
      contacts: myContacts.value,
      notes: notes.value,
      marshalId: currentMarshalId.value
    }));
    await cacheEventData(eventId, plainData);
    console.log('Event data cached for offline access');
  } catch (error) {
    console.warn('Failed to cache event data:', error);
  }

  // Start polling for dynamic checkpoint positions if any exist
  startDynamicCheckpointPolling();
};

/**
 * Background preloader - fetches additional data when the page is idle
 * This ensures all data needed for offline access is cached
 */
const preloadOfflineData = async () => {
  const eventId = route.params.eventId;

  // Don't preload if offline
  if (getOfflineMode()) {
    console.log('Skipping background preload: offline');
    return;
  }

  console.log('Starting background preload for offline data...');

  // Preload area lead dashboard (even if not currently an area lead, in case roles change)
  // This data is useful for area leads to see their checkpoints offline
  try {
    const dashboardResponse = await areasApi.getAreaLeadDashboard(eventId);
    if (dashboardResponse.data) {
      // Convert to plain object to avoid IndexedDB serialization issues
      const areaLeadDashboard = JSON.parse(JSON.stringify({
        areas: dashboardResponse.data.areas || [],
        checkpoints: dashboardResponse.data.checkpoints || []
      }));

      // Try to update existing cache, or create new entry
      const existingCache = await getCachedEventData(eventId);
      if (existingCache) {
        await updateCachedField(eventId, 'areaLeadDashboard', areaLeadDashboard);
      } else {
        await cacheEventData(eventId, { areaLeadDashboard });
      }
      console.log('Area lead dashboard cached for offline access');
    }
  } catch (error) {
    // This might fail if user isn't an area lead - that's fine
    if (error.response?.status !== 403) {
      console.warn('Failed to preload area lead dashboard:', error);
    }
  }

  console.log('Background preload complete');
};

const loadNotes = async () => {
  const eventId = route.params.eventId;
  try {
    // Use getMyNotes to get notes relevant to the current marshal
    const response = await notesApi.getMyNotes(eventId);
    notes.value = response.data || [];
    console.log('Notes loaded:', notes.value.length);
  } catch (error) {
    console.error('Failed to load notes:', error);
  }
};

const loadContacts = async () => {
  const eventId = route.params.eventId;
  contactsLoading.value = true;

  try {
    // Load contacts using the new API that returns scoped contacts for this user
    const response = await contactsApi.getMyContacts(eventId);
    myContacts.value = response.data || [];
    console.log('Contacts loaded:', myContacts.value.length);
  } catch (error) {
    console.warn('Failed to load contacts:', error);
    myContacts.value = [];
  } finally {
    contactsLoading.value = false;
  }
};

// Legacy function - kept for backwards compatibility but now just loads new contacts
const loadAreaContacts = async () => {
  // Area contacts from areas are now handled by the new contacts system
  // This function is kept for any remaining code that calls it
  areaContactsRaw.value = [];
};

const loadChecklist = async () => {
  if (!currentMarshalId.value) {
    console.warn('No marshal ID, skipping checklist load');
    return;
  }

  checklistLoading.value = true;
  checklistError.value = null;

  try {
    console.log('Fetching checklist for marshal:', currentMarshalId.value);
    const response = await checklistApi.getMarshalChecklist(route.params.eventId, currentMarshalId.value);
    checklistItems.value = response.data || [];
    console.log('Checklist loaded:', checklistItems.value.length, 'items');
  } catch (error) {
    console.error('Failed to load checklist:', error.response?.status, error.response?.data);
    checklistError.value = error.response?.data?.message || 'Failed to load checklist';
  } finally {
    checklistLoading.value = false;
  }
};

const handleToggleChecklist = async (item) => {
  if (savingChecklist.value) return;

  savingChecklist.value = true;
  const eventId = route.params.eventId;

  const actionData = {
    marshalId: currentMarshalId.value,
    contextType: item.completionContextType,
    contextId: item.completionContextId,
  };

  try {
    if (item.isCompleted) {
      // Uncomplete
      if (getOfflineMode()) {
        // Queue for offline sync
        await queueOfflineAction('checklist_uncomplete', {
          eventId,
          itemId: item.itemId,
          data: actionData
        });
        // Optimistic update
        item.isCompleted = false;
        item.completedAt = null;
        item.completedByActorName = null;
        await updatePendingCount();
      } else {
        await checklistApi.uncomplete(eventId, item.itemId, actionData);
        await loadChecklist();
      }
    } else {
      // Complete
      if (getOfflineMode()) {
        // Queue for offline sync
        await queueOfflineAction('checklist_complete', {
          eventId,
          itemId: item.itemId,
          data: actionData
        });
        // Optimistic update
        item.isCompleted = true;
        item.completedAt = new Date().toISOString();
        item.completedByActorName = currentPerson.value?.name || 'You';
        await updatePendingCount();
      } else {
        await checklistApi.complete(eventId, item.itemId, actionData);
        await loadChecklist();
      }
    }

    // Update cached checklist
    await updateCachedField(eventId, 'checklist', checklistItems.value);
  } catch (error) {
    console.error('Failed to toggle checklist item:', error);

    // If offline, queue the action
    if (getOfflineMode() || !error.response) {
      const actionType = item.isCompleted ? 'checklist_uncomplete' : 'checklist_complete';
      await queueOfflineAction(actionType, {
        eventId,
        itemId: item.itemId,
        data: actionData
      });
      // Optimistic update
      item.isCompleted = !item.isCompleted;
      await updatePendingCount();
    } else {
      // Show error temporarily
      checklistError.value = error.response?.data?.message || 'Failed to update checklist';
      setTimeout(() => {
        checklistError.value = null;
      }, 3000);
    }
  } finally {
    savingChecklist.value = false;
  }
};

const getContextName = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return null;
  }

  if (item.completionContextType === 'Checkpoint') {
    const location = allLocations.value.find(l => l.id === item.completionContextId);
    if (!location) return null;
    const desc = location.description ? ` - ${location.description}` : '';
    return `At ${termsLower.value.checkpoint} ${location.name}${desc}`;
  }

  if (item.completionContextType === 'Area') {
    const area = areas.value.find(a => a.id === item.completionContextId);
    if (!area) return null;
    return `At ${termsLower.value.area} ${area.name}`;
  }

  if (item.completionContextType === 'Personal') {
    return 'Personal item';
  }

  return null;
};

const startLocationTracking = () => {
  if (!('geolocation' in navigator)) {
    console.warn('Geolocation not supported');
    return;
  }

  console.log('Starting location tracking...');

  locationWatchId = navigator.geolocation.watchPosition(
    (position) => {
      userLocation.value = {
        lat: position.coords.latitude,
        lng: position.coords.longitude,
      };
      locationLastUpdated.value = Date.now();
    },
    (error) => {
      // Provide helpful error messages
      const errorMessages = {
        1: 'Location permission denied. Please allow location access in your browser settings.',
        2: 'Location unavailable. Make sure GPS is enabled on your device.',
        3: 'Location request timed out. Please try again.',
      };
      console.warn('Geolocation error:', errorMessages[error.code] || error.message);
    },
    {
      enableHighAccuracy: true,
      timeout: 15000,
      maximumAge: 10000,
    }
  );
};

const stopLocationTracking = () => {
  if (locationWatchId !== null) {
    navigator.geolocation.clearWatch(locationWatchId);
    locationWatchId = null;
  }
};

const handleCheckIn = async (assign) => {
  checkingIn.value = assign.id;
  checkingInAssignment.value = assign.id;
  checkInError.value = null;

  const eventId = route.params.eventId;

  try {
    if (!('geolocation' in navigator)) {
      throw new Error('Geolocation is not supported by your browser');
    }

    const position = await new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 10000,
      });
    });

    const checkInData = {
      eventId,
      assignmentId: assign.id,
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      manualCheckIn: false,
    };

    if (getOfflineMode()) {
      // Queue for offline sync
      await queueOfflineAction('checkin', checkInData);

      // Optimistic update
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = {
          ...assignments.value[index],
          isCheckedIn: true,
          checkInTime: new Date().toISOString(),
          checkInMethod: 'GPS (pending)',
          checkInLatitude: position.coords.latitude,
          checkInLongitude: position.coords.longitude,
        };
      }
      await updatePendingCount();
      await updateCachedField(eventId, 'locations', allLocations.value);
    } else {
      const response = await checkInApi.checkIn(checkInData);

      // Update the assignment in the list
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = response.data;
      }
    }
  } catch (error) {
    // Check if it's a network error - queue for offline
    if (getOfflineMode() || !error.response) {
      const checkInData = {
        eventId,
        assignmentId: assign.id,
        latitude: userLocation.value?.lat || null,
        longitude: userLocation.value?.lng || null,
        manualCheckIn: true,
      };
      await queueOfflineAction('checkin', checkInData);

      // Optimistic update
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = {
          ...assignments.value[index],
          isCheckedIn: true,
          checkInTime: new Date().toISOString(),
          checkInMethod: 'Manual (pending)',
        };
      }
      await updatePendingCount();
    } else if (error.response?.data?.message) {
      checkInError.value = error.response.data.message;
    } else if (error.message) {
      checkInError.value = error.message;
    } else {
      checkInError.value = 'Failed to check in. Please try manual check-in.';
    }
  } finally {
    checkingIn.value = null;
    checkingInAssignment.value = null;
  }
};

const handleManualCheckIn = (assign) => {
  confirmModalTitle.value = 'Manual Check-In';
  confirmModalMessage.value = 'Are you sure you want to check in manually? This should only be used if GPS is not available.';
  confirmModalCallback.value = async () => {
    checkingIn.value = assign.id;
    checkingInAssignment.value = assign.id;
    checkInError.value = null;

    const eventId = route.params.eventId;
    const checkInData = {
      eventId,
      assignmentId: assign.id,
      latitude: null,
      longitude: null,
      manualCheckIn: true,
    };

    try {
      if (getOfflineMode()) {
        // Queue for offline sync
        await queueOfflineAction('checkin', checkInData);

        // Optimistic update
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = {
            ...assignments.value[index],
            isCheckedIn: true,
            checkInTime: new Date().toISOString(),
            checkInMethod: 'Manual (pending)',
          };
        }
        await updatePendingCount();
        await updateCachedField(eventId, 'locations', allLocations.value);
      } else {
        const response = await checkInApi.checkIn(checkInData);

        // Update the assignment in the list
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = response.data;
        }
      }
    } catch (error) {
      // If network error, queue for offline
      if (getOfflineMode() || !error.response) {
        await queueOfflineAction('checkin', checkInData);

        // Optimistic update
        const index = assignments.value.findIndex(a => a.id === assign.id);
        if (index !== -1) {
          assignments.value[index] = {
            ...assignments.value[index],
            isCheckedIn: true,
            checkInTime: new Date().toISOString(),
            checkInMethod: 'Manual (pending)',
          };
        }
        await updatePendingCount();
      } else {
        checkInError.value = 'Failed to check in manually. Please contact the admin.';
      }
    } finally {
      checkingIn.value = null;
      checkingInAssignment.value = null;
    }
  };
  showConfirmModal.value = true;
};

const handleConfirmModalConfirm = () => {
  showConfirmModal.value = false;
  if (confirmModalCallback.value) {
    confirmModalCallback.value();
    confirmModalCallback.value = null;
  }
};

const handleConfirmModalCancel = () => {
  showConfirmModal.value = false;
  confirmModalCallback.value = null;
};

// Dynamic checkpoint location update methods
const openLocationUpdateModal = (assign) => {
  updatingLocationFor.value = assign;
  locationUpdateError.value = null;
  locationUpdateSuccess.value = null;
  showLocationUpdateModal.value = true;
};

const closeLocationUpdateModal = () => {
  showLocationUpdateModal.value = false;
  updatingLocationFor.value = null;
  locationUpdateError.value = null;
  locationUpdateSuccess.value = null;
  selectingLocationOnMap.value = false;
};

// State for selecting location on map
const selectingLocationOnMap = ref(false);

const startMapLocationSelect = () => {
  selectingLocationOnMap.value = true;
  showLocationUpdateModal.value = false;
  // Expand the map section so user can see it
  expandedSection.value = 'map';
};

const handleMapClick = (coords) => {
  // If in explicit selection mode, update immediately
  if (selectingLocationOnMap.value && updatingLocationFor.value) {
    updateDynamicCheckpointLocation(
      updatingLocationFor.value.locationId,
      coords.lat,
      coords.lng,
      'manual'
    );
    selectingLocationOnMap.value = false;
    updatingLocationFor.value = null;
    return;
  }

  // If user has a dynamic assignment and clicks on the map, offer to update
  if (hasDynamicAssignment.value && firstDynamicAssignment.value) {
    const dynAssign = firstDynamicAssignment.value;
    confirmModalTitle.value = 'Update location';
    confirmModalMessage.value = `Update ${dynAssign.location?.name || 'dynamic checkpoint'} to this location?`;
    confirmModalCallback.value = async () => {
      await updateDynamicCheckpointLocation(
        dynAssign.locationId,
        coords.lat,
        coords.lng,
        'manual'
      );
    };
    showConfirmModal.value = true;
  }
};

const handleLocationClick = (location) => {
  // If in selection mode, don't do anything special - map click will handle it
  if (selectingLocationOnMap.value) return;

  // If user has a dynamic assignment, offer to copy this location
  if (hasDynamicAssignment.value && firstDynamicAssignment.value) {
    const dynAssign = firstDynamicAssignment.value;
    // Don't offer to copy from itself
    if (location.id === dynAssign.locationId) return;

    confirmModalTitle.value = 'Copy location';
    confirmModalMessage.value = `Copy location from "${location.name}" to ${dynAssign.location?.name || 'your dynamic checkpoint'}?`;
    confirmModalCallback.value = async () => {
      await updateDynamicCheckpointLocation(
        dynAssign.locationId,
        location.latitude,
        location.longitude,
        'checkpoint',
        location.id
      );
    };
    showConfirmModal.value = true;
  }
};

// Unified function to update dynamic checkpoint location
const updateDynamicCheckpointLocation = async (locationId, lat, lng, sourceType, sourceCheckpointId = null) => {
  updatingLocation.value = true;

  try {
    const eventId = route.params.eventId;

    const payload = {
      latitude: lat,
      longitude: lng,
      sourceType,
    };
    if (sourceCheckpointId) {
      payload.sourceCheckpointId = sourceCheckpointId;
    }

    const response = await locationsApi.updatePosition(eventId, locationId, payload);

    if (response.data.success) {
      // Stop auto-update when manually setting location
      if (autoUpdateEnabled.value) {
        stopAutoUpdate();
      }

      // Update local state immediately
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
    }
  } catch (error) {
    console.error('Failed to update location:', error);
    if (error.response?.status === 403) {
      alert('You do not have permission to update this location.');
    } else {
      alert('Failed to update location. Please try again.');
    }
  } finally {
    updatingLocation.value = false;
  }
};

const cancelMapLocationSelect = () => {
  selectingLocationOnMap.value = false;
  showLocationUpdateModal.value = true;
};

const updateLocationWithGps = async () => {
  if (!updatingLocationFor.value) return;

  updatingLocation.value = true;
  locationUpdateError.value = null;

  try {
    let latitude, longitude;

    // Use cached location if available and recent (within last 30 seconds)
    if (userLocation.value && locationLastUpdated.value) {
      const ageMs = Date.now() - locationLastUpdated.value;
      if (ageMs < 30000) {
        latitude = userLocation.value.lat;
        longitude = userLocation.value.lng;
      }
    }

    // Fall back to getCurrentPosition if no cached location
    if (!latitude || !longitude) {
      if (!('geolocation' in navigator)) {
        throw new Error('Geolocation is not supported by your browser');
      }

      const position = await new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 15000,
          maximumAge: 30000, // Accept cached position up to 30 seconds old
        });
      });

      latitude = position.coords.latitude;
      longitude = position.coords.longitude;
    }

    const eventId = route.params.eventId;
    const locationId = updatingLocationFor.value.locationId;

    const response = await locationsApi.updatePosition(eventId, locationId, {
      latitude,
      longitude,
      sourceType: 'gps',
    });

    if (response.data.success) {
      // Note: GPS updates don't disable auto-update (only manual selections do)
      locationUpdateSuccess.value = 'Location updated successfully!';
      // Update local state immediately
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      setTimeout(() => closeLocationUpdateModal(), 1500);
    }
  } catch (error) {
    console.error('Failed to update location with GPS:', error);
    if (error.response?.status === 403) {
      locationUpdateError.value = 'You do not have permission to update this location.';
    } else if (error.code === 1) {
      locationUpdateError.value = 'Location access denied. Please enable location permissions.';
    } else if (error.code === 2) {
      locationUpdateError.value = 'Unable to determine your location. Please try again.';
    } else if (error.code === 3 || error.message?.includes('Timeout')) {
      locationUpdateError.value = 'GPS timed out. Please ensure location services are enabled and try again.';
    } else {
      locationUpdateError.value = 'Failed to update location. Please try again.';
    }
  } finally {
    updatingLocation.value = false;
  }
};

const updateLocationFromCheckpoint = async (sourceCheckpointId) => {
  if (!updatingLocationFor.value) return;

  const sourceLocation = allLocations.value.find(l => l.id === sourceCheckpointId);
  if (!sourceLocation) {
    locationUpdateError.value = 'Source checkpoint not found.';
    return;
  }

  updatingLocation.value = true;
  locationUpdateError.value = null;

  try {
    const eventId = route.params.eventId;
    const locationId = updatingLocationFor.value.locationId;

    const response = await locationsApi.updatePosition(eventId, locationId, {
      latitude: sourceLocation.latitude,
      longitude: sourceLocation.longitude,
      sourceType: 'checkpoint',
      sourceCheckpointId: sourceCheckpointId,
    });

    if (response.data.success) {
      // Stop auto-update when manually copying from another checkpoint
      if (autoUpdateEnabled.value) {
        stopAutoUpdate();
      }
      locationUpdateSuccess.value = `Location copied from ${sourceLocation.name}!`;
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      setTimeout(() => closeLocationUpdateModal(), 1500);
    }
  } catch (error) {
    console.error('Failed to copy location from checkpoint:', error);
    if (error.response?.status === 403) {
      locationUpdateError.value = 'You do not have permission to update this location.';
    } else {
      locationUpdateError.value = 'Failed to update location. Please try again.';
    }
  } finally {
    updatingLocation.value = false;
  }
};

const updateLocationManually = async (lat, lng) => {
  if (!updatingLocationFor.value) return;

  updatingLocation.value = true;
  locationUpdateError.value = null;

  try {
    const eventId = route.params.eventId;
    const locationId = updatingLocationFor.value.locationId;

    const response = await locationsApi.updatePosition(eventId, locationId, {
      latitude: lat,
      longitude: lng,
      sourceType: 'manual',
    });

    if (response.data.success) {
      locationUpdateSuccess.value = 'Location updated successfully!';
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      setTimeout(() => closeLocationUpdateModal(), 1500);
    }
  } catch (error) {
    console.error('Failed to update location manually:', error);
    if (error.response?.status === 403) {
      locationUpdateError.value = 'You do not have permission to update this location.';
    } else {
      locationUpdateError.value = 'Failed to update location. Please try again.';
    }
  } finally {
    updatingLocation.value = false;
  }
};

const updateLocalCheckpointPosition = (locationId, lat, lng, lastUpdate) => {
  const location = allLocations.value.find(l => l.id === locationId);
  if (location) {
    location.latitude = lat;
    location.longitude = lng;
    location.lastLocationUpdate = lastUpdate;
  }
};

// Auto-update location every 60 seconds
const toggleAutoUpdate = (assign) => {
  if (autoUpdateEnabled.value) {
    stopAutoUpdate();
  } else {
    startAutoUpdate(assign);
  }
};

const startAutoUpdate = (assign) => {
  autoUpdateEnabled.value = true;
  // Immediately update once
  performAutoUpdate(assign);
  // Then every 60 seconds
  autoUpdateInterval = setInterval(() => {
    performAutoUpdate(assign);
  }, 60000);
};

const stopAutoUpdate = () => {
  autoUpdateEnabled.value = false;
  if (autoUpdateInterval) {
    clearInterval(autoUpdateInterval);
    autoUpdateInterval = null;
  }
};

const performAutoUpdate = async (assign) => {
  if (!assign || !('geolocation' in navigator)) return;

  try {
    const position = await new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 15000,
      });
    });

    const eventId = route.params.eventId;
    const locationId = assign.locationId;

    await locationsApi.updatePosition(eventId, locationId, {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      sourceType: 'gps',
    });

    updateLocalCheckpointPosition(locationId, position.coords.latitude, position.coords.longitude, new Date().toISOString());
  } catch (error) {
    console.warn('Auto-update location failed:', error);
    // Don't stop auto-update on error, just log it
  }
};

// Poll for dynamic checkpoint position updates
const startDynamicCheckpointPolling = () => {
  if (dynamicCheckpointPollInterval) return;

  // Check if there are any dynamic checkpoints to poll
  const hasDynamicCheckpoints = allLocations.value.some(l => l.isDynamic || l.IsDynamic);
  if (!hasDynamicCheckpoints) return;

  dynamicCheckpointPollInterval = setInterval(async () => {
    try {
      const eventId = route.params.eventId;
      const response = await locationsApi.getDynamicCheckpoints(eventId);
      if (response.data && Array.isArray(response.data)) {
        // Update local positions for dynamic checkpoints
        for (const dynamicCp of response.data) {
          const location = allLocations.value.find(l => l.id === dynamicCp.checkpointId);
          if (location) {
            location.latitude = dynamicCp.latitude;
            location.longitude = dynamicCp.longitude;
            location.lastLocationUpdate = dynamicCp.lastLocationUpdate;
          }
        }
      }
    } catch (error) {
      console.warn('Failed to poll dynamic checkpoints:', error);
    }
  }, 60000); // Poll every 60 seconds
};

const stopDynamicCheckpointPolling = () => {
  if (dynamicCheckpointPollInterval) {
    clearInterval(dynamicCheckpointPollInterval);
    dynamicCheckpointPollInterval = null;
  }
};

// Check if a location is a dynamic checkpoint
const isDynamicCheckpoint = (location) => {
  return location?.isDynamic === true || location?.IsDynamic === true;
};

const handleLogout = async () => {
  try {
    await authApi.logout();
  } catch (error) {
    // Ignore logout errors
  }

  localStorage.removeItem('sessionToken');
  localStorage.removeItem(`marshal_${route.params.eventId}`);

  isAuthenticated.value = false;
  currentPerson.value = null;
  currentMarshalId.value = null;
  assignments.value = [];
  checklistItems.value = [];
  areaContactsRaw.value = [];

  // Redirect to home
  router.push('/');
};

const formatTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString();
};

const formatTimeRange = (startTime, endTime) => {
  if (!startTime && !endTime) return '';
  const start = startTime ? formatTime(startTime) : '?';
  const end = endTime ? formatTime(endTime) : '?';
  return `${start} - ${end}`;
};

const formatEventDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatEventDate = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatRoleName = (role) => {
  if (!role) return '';
  // Convert PascalCase/camelCase to words
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

// Generate checkpoint icon SVG based on resolved style
const getCheckpointIconSvg = (location) => {
  if (!location) return '';

  const resolvedType = location.resolvedStyleType || location.ResolvedStyleType;
  const resolvedBgColor = location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor || location.resolvedStyleColor || location.ResolvedStyleColor;
  const resolvedBorderColor = location.resolvedStyleBorderColor || location.ResolvedStyleBorderColor;
  const resolvedIconColor = location.resolvedStyleIconColor || location.ResolvedStyleIconColor;
  const resolvedShape = location.resolvedStyleBackgroundShape || location.ResolvedStyleBackgroundShape;

  // Check if there's any resolved styling - type, colors, or shape
  const hasResolvedStyle = (resolvedType && resolvedType !== 'default')
    || resolvedBgColor
    || resolvedBorderColor
    || resolvedIconColor
    || (resolvedShape && resolvedShape !== 'circle');

  if (hasResolvedStyle) {
    // Use resolved style from hierarchy
    return generateCheckpointSvg({
      type: resolvedType || 'circle',
      backgroundShape: resolvedShape || 'circle',
      backgroundColor: resolvedBgColor || '#667eea',
      borderColor: resolvedBorderColor || '#ffffff',
      iconColor: resolvedIconColor || '#ffffff',
      size: '75',
      outputSize: 20,
    });
  }

  // Default: use neutral colored circle (not status-based)
  return `<svg width="20" height="20" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
    <circle cx="10" cy="10" r="8" fill="#667eea" stroke="#fff" stroke-width="1.5"/>
  </svg>`;
};

onMounted(async () => {
  const eventId = route.params.eventId;
  const magicCode = route.query.code;

  loading.value = true;

  try {
    // Check for magic code in URL
    if (magicCode) {
      await authenticateWithMagicCode(eventId, magicCode);
    } else {
      // Check for existing session
      const hasSession = await checkExistingSession(eventId);

      if (hasSession) {
        await loadEventData();
      }
    }
  } finally {
    loading.value = false;
  }

  // Start location tracking if authenticated
  if (isAuthenticated.value) {
    startLocationTracking();

    // Schedule background preload when the page is idle
    // This fetches additional data for offline use without blocking the UI
    if ('requestIdleCallback' in window) {
      requestIdleCallback(() => {
        preloadOfflineData();
      }, { timeout: 5000 }); // Ensure it runs within 5 seconds
    } else {
      // Fallback for browsers without requestIdleCallback
      setTimeout(() => {
        preloadOfflineData();
      }, 2000);
    }
  }
});

// Watch for authentication changes to start/stop location tracking
watch(isAuthenticated, (authenticated) => {
  if (authenticated) {
    startLocationTracking();
  } else {
    stopLocationTracking();
  }
});

// Watch for URL query changes (handles pasting a new login link while already logged in)
watch(() => route.query.code, async (newCode) => {
  if (newCode && isAuthenticated.value) {
    // A new magic code was added to the URL while already authenticated
    // Re-authenticate with the new code (switching users)
    loading.value = true;
    try {
      await authenticateWithMagicCode(route.params.eventId, newCode);
    } finally {
      loading.value = false;
    }
  }
});

// Heartbeat to update LastAccessedDate every 5 minutes
let heartbeatInterval = null;

const startHeartbeat = () => {
  if (heartbeatInterval) return;

  heartbeatInterval = setInterval(async () => {
    if (userClaims.value?.marshalId) {
      try {
        await authApi.getMe(route.params.eventId);
      } catch (err) {
        // Silently ignore heartbeat errors
        console.debug('Heartbeat failed:', err);
      }
    }
  }, 5 * 60 * 1000); // 5 minutes
};

const stopHeartbeat = () => {
  if (heartbeatInterval) {
    clearInterval(heartbeatInterval);
    heartbeatInterval = null;
  }
};

onUnmounted(() => {
  stopLocationTracking();
  stopHeartbeat();
  stopAutoUpdate();
  stopDynamicCheckpointPolling();
});
</script>

<style scoped>
.marshal-view {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.header {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  padding: 1.5rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: white;
}

.header h1 {
  margin: 0;
}

.header-title {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.header-event-date {
  font-size: 0.9rem;
  opacity: 0.9;
  font-weight: 400;
}

.header-actions {
  display: flex;
  gap: 0.75rem;
}

.btn-emergency {
  background: #ff4444;
  color: white;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-emergency:hover {
  background: #cc0000;
}

.btn-logout {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.3);
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-logout:hover {
  background: rgba(255, 255, 255, 0.3);
}

.container {
  padding: 2rem;
  max-width: 800px;
  margin: 0 auto;
}

.selection-card,
.welcome-card,
.assignment-card,
.checklist-card,
.contacts-card,
.map-card {
  background: white;
  border-radius: 12px;
  padding: 2rem;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
  margin-bottom: 2rem;
}

.error-card {
  border: 2px solid #ff4444;
}

.selection-card h2,
.welcome-card h2,
.assignment-card h3,
.checklist-card h3,
.contacts-card h3,
.map-card h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

/* Contact list styles */
.contact-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.contact-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background: #f5f7fa;
  border-radius: 8px;
  gap: 1rem;
}

.contact-name {
  font-weight: 500;
  color: #333;
}

.contact-role {
  font-weight: 400;
  color: #666;
}

.contact-name-row {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.primary-badge {
  color: #ffc107;
  font-size: 0.9rem;
}

.contact-role-badge {
  display: inline-block;
  padding: 0.15rem 0.5rem;
  background: #e9ecef;
  color: #495057;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.contact-notes-text {
  font-size: 0.85rem;
  color: #555;
  font-style: italic;
  margin-top: 0.25rem;
}

.primary-contact {
  border-left: 3px solid #ffc107;
  background: #fffef8;
}

.contact-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.contact-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.5rem 0.75rem;
  background: #667eea;
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  transition: background 0.2s;
}

.contact-link:hover {
  background: #5568d3;
}

.contact-icon {
  width: 1rem;
  height: 1rem;
}

.instruction {
  color: #666;
  margin-bottom: 1rem;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: #666;
}

/* Welcome bar - compact horizontal layout */
.welcome-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: white;
  border-radius: 10px;
  padding: 0.75rem 1.25rem;
  margin-bottom: 0.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.welcome-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.welcome-name {
  font-weight: 600;
  color: #333;
  font-size: 1rem;
}

.welcome-email {
  color: #666;
  font-size: 0.8rem;
}

.mode-switch {
  flex-shrink: 0;
}

.btn-mode-switch {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-mode-switch:hover {
  transform: translateY(-1px);
  box-shadow: 0 3px 10px rgba(102, 126, 234, 0.4);
}

.reauth-hint {
  font-size: 0.75rem;
  color: #999;
  font-style: italic;
  cursor: help;
}

.location-info {
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: #f5f7fa;
  border-radius: 8px;
}

.location-info strong {
  font-size: 1.125rem;
  color: #333;
}

.location-info p {
  margin: 0.5rem 0 0 0;
  color: #666;
}

.time-range {
  font-size: 0.9rem;
  color: #667eea;
  font-weight: 500;
}

.no-assignment {
  color: #666;
  font-style: italic;
}

.checked-in-status {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.5rem;
  background: #f1f8f4;
  border: 2px solid #4caf50;
  border-radius: 8px;
}

.check-icon {
  font-size: 2rem;
  color: #4caf50;
}

.checked-in-status strong {
  color: #4caf50;
  font-size: 1.125rem;
}

.check-time,
.check-method {
  margin: 0.25rem 0 0 0;
  color: #666;
  font-size: 0.875rem;
}

.check-in-actions {
  display: flex;
  flex-direction: row;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.btn {
  padding: 1rem 2rem;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 600;
  font-size: 1rem;
  transition: all 0.3s;
  text-decoration: none;
  display: inline-block;
  text-align: center;
}

.btn-large {
  font-size: 1.125rem;
  padding: 1.25rem 2rem;
}

.btn-primary {
  background: #667eea;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #5568d3;
  transform: translateY(-2px);
}

.btn-secondary {
  background: #e0e0e0;
  color: #333;
}

.btn-secondary:hover:not(:disabled) {
  background: #d0d0d0;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error {
  padding: 1rem;
  background: #fee;
  color: #c33;
  border-radius: 6px;
  font-size: 0.875rem;
}

/* Checklist styles */
.checklist-items {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.checklist-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  transition: all 0.2s;
}

.checklist-item.item-completed {
  background: #f1f8f4;
  border-color: #c8e6c9;
}

.item-checkbox {
  display: flex;
  align-items: flex-start;
  padding-top: 0.25rem;
}

.item-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1.2rem;
  height: 1.2rem;
  flex-shrink: 0;
}

.item-checkbox input[type="checkbox"]:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.item-text {
  font-size: 0.95rem;
  color: #333;
}

.item-text.text-completed {
  text-decoration: line-through;
  color: #888;
}

.item-context {
  font-size: 0.85rem;
  color: #667eea;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.completion-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: 0.8rem;
  color: #666;
}

.completion-text {
  color: #4caf50;
}

.completion-time {
  color: #999;
}

.empty-state {
  text-align: center;
  padding: 1.5rem;
  color: #666;
  font-style: italic;
}

/* Map card */
.map-card {
  height: auto;
}

.map-card h3 {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.location-status {
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  border-radius: 12px;
  background: #e0e0e0;
  color: #666;
  font-weight: 500;
}

.location-status.active {
  background: #e8f5e9;
  color: #4caf50;
}

.map-card :deep(.map-container) {
  height: 400px;
  margin-top: 1rem;
}

/* Accordion styles */
.accordion {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.accordion-section {
  background: white;
  border-radius: 12px;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  background: white;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: #333;
  transition: background 0.2s;
}

.accordion-header:hover {
  background: #f8f9fa;
}

.accordion-header.active {
  background: #f0f4ff;
  color: #667eea;
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: #667eea;
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: #667eea;
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid #e0e0e0;
}

.map-content {
  padding-bottom: 0;
}

.map-content :deep(.map-container) {
  height: 350px;
  border-radius: 0 0 12px 12px;
  margin: 0 -1.5rem -1.5rem;
}

/* Nested checkpoint accordion styles */
.assignments-accordion-content {
  padding: 0.5rem;
}

.checkpoint-accordion {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checkpoint-accordion-section {
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 10px;
  overflow: hidden;
}

.checkpoint-accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1rem 1.25rem;
  background: #f8f9fa;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: #333;
  transition: background 0.2s;
  gap: 0.5rem;
}

.checkpoint-accordion-header:hover {
  background: #f0f2f5;
}

.checkpoint-accordion-header.active {
  background: #e8ecf4;
  border-bottom: 1px solid #e0e0e0;
}

.checkpoint-accordion-header.checked-in {
  background: #f1f8f4;
}

.checkpoint-accordion-header.checked-in.active {
  background: #e8f5e9;
}

.checkpoint-header-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkpoint-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.checkpoint-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 20px;
  height: 20px;
}

.checkpoint-icon :deep(svg) {
  width: 20px;
  height: 20px;
}

.checkpoint-check-icon {
  color: #4caf50;
  font-weight: bold;
  flex-shrink: 0;
}

.checkpoint-name {
  color: #333;
}

.checkpoint-time-badge {
  font-size: 0.8rem;
  font-weight: 500;
  color: #667eea;
  background: #e8ecf4;
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
  white-space: nowrap;
}

.checkpoint-accordion-header.checked-in .checkpoint-time-badge {
  background: #d4edda;
  color: #28a745;
}

.checkpoint-description-preview {
  font-size: 0.85rem;
  font-weight: 400;
  color: #666;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.checkpoint-accordion-content {
  padding: 1rem 1.25rem;
  background: white;
}

.checkpoint-area-contacts {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: #f5f7fa;
  border-radius: 8px;
}

.checkpoint-area-contacts .contact-list {
  margin-top: 0.5rem;
}

.checkpoint-area-contacts .contact-item {
  background: white;
}

/* Assignment list styles */
.assignments-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.assignment-item {
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 10px;
  padding: 1rem;
}

.assignment-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
  flex-wrap: wrap;
}

.assignment-header strong {
  font-size: 1.1rem;
  color: #333;
}

.assignment-description {
  color: #666;
  font-size: 0.9rem;
  margin: 0 0 0.5rem 0;
}

.area-badge {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  background: #667eea;
  color: white;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: 12px;
}

/* Area Lead badges in accordion header */
.area-lead-badges {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  margin-left: 0.5rem;
}

.area-lead-badge {
  display: inline-block;
  padding: 0.15rem 0.5rem;
  font-size: 0.7rem;
  font-weight: 600;
  color: white;
  border-radius: 10px;
}

/* Area lead accordion content */
.area-lead-accordion-content {
  padding: 0.5rem;
}

/* Checkpoint marshals list */
.checkpoint-marshals {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: #fff;
  border-radius: 8px;
}

.marshals-label {
  font-size: 0.8rem;
  color: #666;
  margin-bottom: 0.5rem;
  font-weight: 500;
}

.marshals-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.marshal-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.3rem 0.6rem;
  background: #e8e8e8;
  border-radius: 16px;
  font-size: 0.85rem;
  color: #555;
}

.marshal-tag.is-you {
  background: #e3e9ff;
  color: #667eea;
  font-weight: 500;
}

.marshal-tag.checked-in {
  background: #e8f5e9;
  color: #4caf50;
}

.marshal-tag.is-you.checked-in {
  background: linear-gradient(135deg, #e3e9ff 50%, #e8f5e9 50%);
}

.check-badge {
  font-size: 0.75rem;
}

/* Area contacts group */
.area-contacts-group {
  margin-bottom: 1.5rem;
}

.area-contacts-group:last-child {
  margin-bottom: 0;
}

.area-group-title {
  margin: 0 0 0.75rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid #667eea;
  color: #333;
  font-size: 1rem;
}

.contact-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.contact-detail {
  font-size: 0.85rem;
  color: #555;
}

/* Event details styles */
.event-details {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.event-detail-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.event-description {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.detail-label {
  font-size: 0.85rem;
  color: #666;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.detail-value {
  font-size: 1rem;
  color: #333;
  margin: 0;
}

.event-description .detail-value {
  white-space: pre-wrap;
  line-height: 1.5;
}

/* Mini-map in checkpoint cards */
.checkpoint-mini-map {
  margin-top: 1rem;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid #e0e0e0;
}

.checkpoint-mini-map :deep(.map-container) {
  height: 200px;
  min-height: 200px;
}

/* Dynamic location section */
.dynamic-location-section {
  margin-top: 1rem;
  padding: 0.75rem;
  background: #f0f4ff;
  border-radius: 8px;
  border-left: 3px solid #667eea;
}

.dynamic-location-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.dynamic-badge {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  background: #667eea;
  color: white;
  font-size: 0.75rem;
  font-weight: 600;
  border-radius: 4px;
  text-transform: uppercase;
}

.last-update {
  font-size: 0.8rem;
  color: #666;
}

.dynamic-location-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-update-location {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  background: #667eea;
  color: white;
  padding: 0.6rem 1rem;
  border-radius: 6px;
  font-size: 0.9rem;
}

.btn-update-location:hover:not(:disabled) {
  background: #5568d3;
}

.btn-auto-update {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  background: #e0e0e0;
  color: #333;
  padding: 0.6rem 0.75rem;
  border-radius: 6px;
  font-size: 0.9rem;
}

.btn-auto-update:hover:not(:disabled) {
  background: #d0d0d0;
}

.btn-auto-update.active {
  background: #4caf50;
  color: white;
}

.btn-auto-update.active:hover:not(:disabled) {
  background: #43a047;
}

.btn-icon {
  width: 1.1rem;
  height: 1.1rem;
  flex-shrink: 0;
}

/* Modal styles */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-content {
  background: white;
  border-radius: 12px;
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.3);
}

.location-update-modal .modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #e0e0e0;
}

.location-update-modal .modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: #333;
}

.modal-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: #666;
  padding: 0;
  line-height: 1;
}

.modal-close:hover {
  color: #333;
}

.modal-body {
  padding: 1.5rem;
}

.modal-description {
  margin: 0 0 1.5rem 0;
  color: #555;
}

.update-option {
  margin-bottom: 1.5rem;
}

.option-label {
  display: block;
  font-size: 0.9rem;
  color: #555;
  margin-bottom: 0.5rem;
}

.btn-full {
  width: 100%;
  justify-content: center;
}

.checkpoint-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.btn-checkpoint-source {
  text-align: left;
  padding: 0.75rem 1rem;
}

.no-checkpoints {
  font-size: 0.85rem;
  color: #888;
  font-style: italic;
}

.success-message {
  padding: 1rem;
  background: #e8f5e9;
  color: #2e7d32;
  border-radius: 8px;
  text-align: center;
  font-weight: 500;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 1rem 1.5rem;
  border-top: 1px solid #e0e0e0;
}

/* New modal elements */
.last-update-info {
  font-size: 0.85rem;
  color: #666;
  margin-bottom: 1rem;
  padding: 0.5rem;
  background: #f5f5f5;
  border-radius: 4px;
}

.gps-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: #666;
  margin-bottom: 1rem;
}

.gps-status.active {
  color: #2e7d32;
}

.gps-status .status-icon {
  width: 16px;
  height: 16px;
}

.auto-update-option {
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 8px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: 500;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.option-hint {
  font-size: 0.8rem;
  color: #666;
  margin: 0.5rem 0 0 0;
  padding-left: 27px;
}

/* Map selection mode */
.map-selection-banner {
  background: #e3f2fd;
  border: 1px solid #2196f3;
  border-radius: 8px;
  padding: 1rem;
  margin-bottom: 0.5rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.map-selection-banner span {
  flex: 1;
  color: #1565c0;
}

.btn-sm {
  padding: 0.35rem 0.75rem;
  font-size: 0.85rem;
}

.selecting-location {
  cursor: crosshair !important;
}

.selecting-location :deep(.leaflet-container) {
  cursor: crosshair !important;
}

@media (max-width: 768px) {
  .header {
    flex-direction: column;
    gap: 1rem;
    text-align: center;
  }

  .header h1 {
    font-size: 1.25rem;
  }

  .header-event-date {
    font-size: 0.85rem;
  }

  .container {
    padding: 1rem;
  }

  .selection-card,
  .welcome-card,
  .assignment-card,
  .checklist-card,
  .contacts-card,
  .map-card {
    padding: 1.5rem;
  }

  .map-card :deep(.map-container) {
    height: 300px;
  }

  .contact-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .contact-actions {
    width: 100%;
  }

  .contact-link {
    flex: 1;
    justify-content: center;
  }

  /* Accordion mobile adjustments */
  .accordion-header {
    padding: 1rem;
  }

  .accordion-content {
    padding: 1rem;
  }

  .assignments-accordion-content {
    padding: 0.25rem;
  }

  .checkpoint-accordion-header {
    padding: 0.75rem 1rem;
  }

  .checkpoint-accordion-content {
    padding: 0.75rem 1rem;
  }

  .checkpoint-area-contacts {
    padding: 0.5rem;
  }

  .map-content :deep(.map-container) {
    height: 300px;
    margin: 0 -1rem -1rem;
  }

  .assignment-item {
    padding: 0.75rem;
  }

  .check-in-actions {
    flex-direction: row;
    gap: 0.5rem;
  }

  .check-in-actions .btn {
    padding: 0.75rem 1rem;
    font-size: 0.9rem;
    flex: 1;
  }

  /* Dynamic location mobile adjustments */
  .dynamic-location-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .dynamic-location-actions {
    width: 100%;
  }

  .btn-update-location {
    flex: 1;
  }
}
</style>
