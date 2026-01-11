<template>
  <div class="marshal-view" :style="pageBackgroundStyle">
    <!-- Offline Indicator -->
    <OfflineIndicator />

    <header class="header" :class="`logo-${brandingLogoPosition}`" :style="headerStyle">
      <!-- Cover logo background -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'cover'" class="header-logo-cover">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-cover-img" />
      </div>

      <!-- Logout button on left (when logo is on right) -->
      <button
        v-if="isAuthenticated && brandingLogoPosition === 'right'"
        @click="confirmLogout"
        class="btn-logout-icon btn-logout-left"
        :style="{ color: headerTextColor }"
        :title="`Logout ${currentPerson?.name || ''}`"
      >
        <span v-html="getIcon('logout')"></span>
      </button>

      <!-- Left logo -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'left'" class="header-logo header-logo-left">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-img" />
      </div>

      <div class="header-center">
        <div class="header-title" :style="{ color: headerTextColor }">
          <h1 v-if="event">{{ event.name }}</h1>
          <div v-if="event?.eventDate" class="header-event-date">
            {{ formatEventDate(event.eventDate) }}
          </div>
        </div>
        <div v-if="isAuthenticated" class="header-actions">
          <button @click="showReportIncident = true" class="btn-header-action btn-report-incident">
            Report Incident
          </button>
          <button @click="showEmergency = true" class="btn-header-action btn-emergency">
            Emergency Info
          </button>
        </div>
      </div>

      <!-- Right logo -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'right'" class="header-logo header-logo-right">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-img" />
      </div>

      <!-- Logout button on right (when logo is on left, cover, or none) -->
      <button
        v-if="isAuthenticated && brandingLogoPosition !== 'right'"
        @click="confirmLogout"
        class="btn-logout-icon btn-logout-right"
        :style="{ color: headerTextColor }"
        :title="`Logout ${currentPerson?.name || ''}`"
      >
        <span v-html="getIcon('logout')"></span>
      </button>
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
        <h2>Login failed</h2>
        <p class="error">{{ loginError }}</p>
        <p class="instruction">Please contact the event organiser for a new login link.</p>
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
          <!-- Assignments Section -->
          <div class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'assignments' }"
              @click="toggleSection('assignments')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('checkpoint')"></span>
                Your {{ assignments.length === 1 ? termsLower.checkpoint : termsLower.checkpoints }}{{ assignments.length > 1 ? ` (${assignments.length})` : '' }}
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
                    :class="{ active: expandedCheckpoint === assign.id, 'checked-in': assign.effectiveIsCheckedIn }"
                    @click="toggleCheckpoint(assign.id)"
                  >
                    <div class="checkpoint-header-content">
                      <div class="checkpoint-title-row">
                        <span class="checkpoint-icon" v-html="getCheckpointIconSvg(assign.location)"></span>
                        <span v-if="assign.effectiveIsCheckedIn" class="checkpoint-check-icon">✓</span>
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
                      <CommonMap
                        :ref="(el) => setCheckpointMapRef(assign.id, el)"
                        :locations="allLocations"
                        :route="eventRoute"
                        :center="{ lat: assign.location.latitude, lng: assign.location.longitude }"
                        :zoom="16"
                        :user-location="userLocation"
                        :highlight-location-id="assign.locationId"
                        :marshal-mode="true"
                        :simplify-non-highlighted="true"
                        :clickable="hasDynamicAssignment"
                        :show-fullscreen="true"
                        :fullscreen-title="assign.location?.name || assign.locationName"
                        :fullscreen-header-style="headerStyle"
                        :fullscreen-header-text-color="headerTextColor"
                        :toolbar-actions="getCheckpointMapActions(assign.id)"
                        :hide-recenter-button="true"
                        height="280px"
                        @map-click="handleMapClick"
                        @location-click="handleLocationClick"
                        @action-click="(e) => handleCheckpointMapAction(assign, e)"
                        @visibility-change="(e) => handleCheckpointMapVisibilityChange(assign.id, e)"
                      >
                        <!-- Update location button for dynamic checkpoints -->
                        <template v-if="assign.location?.isDynamic" #fullscreen-footer>
                          <button
                            class="btn btn-primary"
                            @click="openLocationUpdateModal(assign)"
                          >
                            <svg viewBox="0 0 24 24" fill="currentColor" width="18" height="18" style="margin-right: 0.5rem;">
                              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
                            </svg>
                            Update location
                          </button>
                        </template>
                      </CommonMap>
                    </div>

                    <!-- My check-in toggle button -->
                    <div class="my-checkin-section">
                      <CheckInToggleButton
                        :is-checked-in="assign.effectiveIsCheckedIn"
                        :check-in-time="assign.checkInTime"
                        :check-in-method="assign.checkInMethod"
                        :checked-in-by="assign.checkedInBy"
                        :marshal-name="currentMarshalName"
                        :is-loading="checkingIn === assign.id"
                        @toggle="handleCheckInToggle(assign, true)"
                      />
                      <div v-if="checkInError && checkingInAssignment === assign.id" class="check-in-error">{{ checkInError }}</div>
                    </div>

                    <!-- Marshals on this checkpoint -->
                    <div class="checkpoint-marshals">
                      <div class="marshals-label">{{ terms.people }}:</div>
                      <!-- Simple view for non-area leads -->
                      <div v-if="!isAreaLeadForAreas(assign.areaIds)" class="marshals-list">
                        <span
                          v-for="m in assign.allMarshals"
                          :key="m.marshalId"
                          class="marshal-tag"
                          :class="{ 'is-you': m.marshalId === currentMarshalId, 'checked-in': m.effectiveIsCheckedIn }"
                        >
                          {{ m.marshalName }}{{ m.marshalId === currentMarshalId ? ' (you)' : '' }}
                          <span v-if="m.effectiveIsCheckedIn" class="check-badge">✓</span>
                        </span>
                      </div>
                      <!-- Expandable view for area leads -->
                      <div v-else class="marshals-list-expanded">
                        <div
                          v-for="m in assign.allMarshals"
                          :key="m.marshalId"
                          class="marshal-card-mini"
                          :class="{
                            'is-you': m.marshalId === currentMarshalId,
                            'checked-in': m.effectiveIsCheckedIn,
                            'expanded': expandedMarshalId === m.marshalId
                          }"
                        >
                          <button
                            class="marshal-card-header"
                            @click="toggleMarshalDetails(m.marshalId)"
                          >
                            <span class="marshal-name-text">
                              {{ m.marshalName }}{{ m.marshalId === currentMarshalId ? ' (you)' : '' }}
                            </span>
                            <span class="marshal-status-icons">
                              <span v-if="m.effectiveIsCheckedIn" class="check-badge">✓</span>
                              <span class="expand-icon">{{ expandedMarshalId === m.marshalId ? '−' : '+' }}</span>
                            </span>
                          </button>
                          <div v-if="expandedMarshalId === m.marshalId" class="marshal-details-panel">
                            <div class="detail-row">
                              <span class="detail-label">Status:</span>
                              <span :class="m.effectiveIsCheckedIn ? 'status-checked-in' : 'status-not-checked-in'">
                                {{ m.effectiveIsCheckedIn ? 'Checked in' : 'Not checked in' }}
                              </span>
                            </div>
                            <div v-if="m.effectiveIsCheckedIn && m.checkInTime" class="detail-row">
                              <span class="detail-label">Check-in time:</span>
                              <span>{{ formatCheckInTime(m.checkInTime) }}</span>
                            </div>
                            <div v-if="m.checkInMethod" class="detail-row">
                              <span class="detail-label">Method:</span>
                              <span>{{ formatCheckInMethod(m.checkInMethod) }}</span>
                            </div>
                            <div v-if="m.email" class="detail-row">
                              <span class="detail-label">Email:</span>
                              <a :href="'mailto:' + m.email">{{ m.email }}</a>
                            </div>
                            <div v-if="m.phoneNumber" class="detail-row">
                              <span class="detail-label">Phone:</span>
                              <a :href="'tel:' + m.phoneNumber">{{ m.phoneNumber }}</a>
                            </div>
                            <div v-if="m.email || m.phoneNumber" class="contact-buttons-mini">
                              <a v-if="m.phoneNumber" :href="'tel:' + m.phoneNumber" class="btn btn-sm btn-primary">Call</a>
                              <a v-if="m.phoneNumber" :href="'sms:' + m.phoneNumber" class="btn btn-sm btn-secondary">Text</a>
                              <a v-if="m.email" :href="'mailto:' + m.email" class="btn btn-sm btn-secondary">Email</a>
                            </div>
                            <!-- Check-in/check-out button for area leads (not for yourself) -->
                            <div v-if="m.marshalId !== currentMarshalId" class="checkpoint-marshal-checkin">
                              <CheckInToggleButton
                                :is-checked-in="m.effectiveIsCheckedIn"
                                :check-in-time="m.checkInTime"
                                :check-in-method="m.checkInMethod"
                                :checked-in-by="m.checkedInBy"
                                :marshal-name="m.marshalName"
                                :is-loading="checkingIn === m.id"
                                @toggle="handleCheckInToggle(m, false, assign.locationId)"
                              />
                            </div>
                          </div>
                        </div>
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

                    <!-- Notes for this checkpoint -->
                    <div v-if="getNotesForCheckpoint(assign.locationId, assign.areaIds).length > 0" class="checkpoint-notes">
                      <div class="marshals-label">Notes:</div>
                      <div class="checkpoint-notes-list">
                        <div
                          v-for="note in getNotesForCheckpoint(assign.locationId, assign.areaIds)"
                          :key="note.noteId"
                          class="checkpoint-note-item"
                          :class="[(note.priority || 'Normal').toLowerCase()]"
                        >
                          <div class="checkpoint-note-header">
                            <span v-if="note.isPinned" class="pin-icon" title="Pinned">📌</span>
                            <span class="priority-indicator" :class="(note.priority || 'Normal').toLowerCase()"></span>
                            <strong class="checkpoint-note-title">{{ note.title }}</strong>
                            <span class="priority-badge" :class="(note.priority || 'Normal').toLowerCase()">
                              {{ note.priority || 'Normal' }}
                            </span>
                          </div>
                          <div v-if="note.content" class="checkpoint-note-content">
                            {{ note.content }}
                          </div>
                        </div>
                      </div>
                    </div>

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

              <!-- Area leads see two sections: Your jobs and Your area's jobs -->
              <template v-else-if="isAreaLead">
                <!-- Your jobs section (simple list) -->
                <div class="checklist-section">
                  <h4 class="checklist-section-title">Your jobs</h4>
                  <div v-if="myChecklistItems.length === 0" class="empty-state small">
                    <p>No jobs assigned to you.</p>
                  </div>
                  <div v-else class="my-jobs-list">
                    <div
                      v-for="item in myChecklistItems"
                      :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
                      class="my-job-item"
                    >
                      <input
                        type="checkbox"
                        :checked="item.isCompleted"
                        :disabled="savingChecklist"
                        @change="handleToggleChecklist(item)"
                      />
                      <span class="my-job-text" :class="{ completed: item.isCompleted }">
                        {{ item.text }}
                      </span>
                    </div>
                  </div>
                </div>

                <!-- Your area's jobs section -->
                <div v-if="areaChecklistItems.length > 0" class="checklist-section">
                  <h4 class="checklist-section-title">Your {{ termsLower.area }}'s jobs</h4>
                  <GroupedTasksList
                    :items="areaChecklistItemsWithLocalState"
                    :locations="allLocations"
                    :areas="areas"
                    :marshals="areaMarshalsForChecklist"
                    @toggle-complete="handleToggleChecklist"
                  />
                </div>
              </template>

              <!-- Non-leads see simple list or grouped by checkpoint -->
              <template v-else>
                <!-- Ungrouped view (when single checkpoint or no grouping needed) -->
                <div v-if="effectiveChecklistGroupBy === 'none'" class="checklist-items">
                  <div
                    v-for="item in visibleChecklistItems"
                    :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
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

                <!-- Grouped by checkpoint view (for non-leads with multiple checkpoints) -->
                <div v-else class="checklist-groups">
                  <div
                    v-for="group in groupedChecklistItems"
                    :key="group.key"
                    class="checklist-group"
                  >
                    <button
                      class="checklist-group-header"
                      :class="{ expanded: expandedChecklistGroup === group.key, 'all-complete': group.completedCount === group.items.length }"
                      @click="toggleChecklistGroup(group.key)"
                    >
                      <span class="group-title">{{ group.name }}</span>
                      <span class="group-status">
                        <span class="group-count">{{ group.completedCount }}/{{ group.items.length }}</span>
                        <span class="group-expand-icon">{{ expandedChecklistGroup === group.key ? '−' : '+' }}</span>
                      </span>
                    </button>
                    <div v-if="expandedChecklistGroup === group.key" class="checklist-group-items">
                      <div
                        v-for="item in group.items"
                        :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
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
                          <div v-if="item.isCompleted" class="completion-info">
                            <span class="completion-text">
                              {{ item.completedByActorName || 'Unknown' }}
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
              </template>
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
                Your {{ notes.length === 1 ? 'note' : 'notes' }}{{ notes.length > 1 ? ` (${notes.length})` : '' }}
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

          <!-- Contacts Section -->
          <div v-if="eventContacts.length > 0 || contactsLoading" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'eventContacts' }"
              @click="toggleSection('eventContacts')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('contacts')"></span>
                Your {{ eventContacts.length === 1 ? 'contact' : 'contacts' }}{{ eventContacts.length > 1 ? ` (${eventContacts.length})` : '' }}
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
                      <span v-if="contact.role" class="contact-role-badge" :class="{ 'emergency-role': isEmergencyRole(contact.role) }">{{ formatRoleName(contact.role) }}</span>
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

          <!-- Your Incidents Section -->
          <div v-if="myIncidents.length > 0 || incidentsLoading" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'incidents' }"
              @click="toggleSection('incidents')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('incidents')"></span>
                Your {{ myIncidents.length === 1 ? 'incident' : 'incidents' }}{{ myIncidents.length > 1 ? ` (${myIncidents.length})` : '' }}
              </span>
              <span class="accordion-icon">{{ expandedSection === 'incidents' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'incidents'" class="accordion-content">
              <div v-if="incidentsLoading" class="loading-state">
                Loading incidents...
              </div>
              <div v-else-if="myIncidents.length === 0" class="empty-state">
                <p>No incidents to show.</p>
              </div>
              <div v-else class="incidents-list">
                <IncidentCard
                  v-for="incident in myIncidents"
                  :key="incident.incidentId"
                  :incident="incident"
                  @select="openIncidentDetail(incident)"
                />
              </div>
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
                <span class="section-icon" v-html="getIcon('area')"></span>
                Your {{ areaLeadAreas.length === 1 ? termsLower.area : termsLower.areas }}{{ areaLeadAreas.length > 1 ? ` (${areaLeadAreas.length})` : '' }}
              </span>
              <span class="accordion-icon">{{ expandedSection === 'areaLead' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'areaLead'" class="accordion-content area-lead-accordion-content">
              <!-- Area filter pills (only show if more than one area) -->
              <div v-if="areaLeadAreas.length > 1" class="area-filter-pills">
                <button
                  v-for="area in areaLeadAreas"
                  :key="area.areaId"
                  class="area-filter-pill"
                  :class="{ selected: selectedAreaFilters.has(area.areaId) }"
                  :style="selectedAreaFilters.has(area.areaId) ? { backgroundColor: area.color || '#667eea', borderColor: area.color || '#667eea' } : {}"
                  @click="toggleAreaFilter(area.areaId)"
                >
                  {{ area.name }}
                </button>
              </div>
              <AreaLeadSection
                ref="areaLeadRef"
                :event-id="route.params.eventId"
                :area-ids="filteredAreaLeadAreaIds"
                :marshal-id="currentMarshalId"
                :route="eventRoute"
                @checklist-updated="loadChecklist"
              />
            </div>
          </div>

          <!-- Your Marshals Section (for area leads with marshals at their checkpoints) -->
          <div v-if="isAreaLead && allAreaLeadMarshals.length > 0" class="accordion-section">
            <button
              class="accordion-header"
              :class="{ active: expandedSection === 'areaLeadMarshals' }"
              @click="toggleSection('areaLeadMarshals')"
            >
              <span class="accordion-title">
                <span class="section-icon" v-html="getIcon('marshal')"></span>
                Your {{ allAreaLeadMarshals.length === 1 ? termsLower.person : termsLower.people }}{{ allAreaLeadMarshals.length > 1 ? ` (${allAreaLeadMarshals.length})` : '' }}
              </span>
              <span class="accordion-icon">{{ expandedSection === 'areaLeadMarshals' ? '−' : '+' }}</span>
            </button>
            <div v-if="expandedSection === 'areaLeadMarshals'" class="accordion-content area-lead-marshals-content">
              <div class="area-lead-marshals-list">
                <div
                  v-for="marshal in allAreaLeadMarshals"
                  :key="marshal.marshalId"
                  class="area-lead-marshal-item"
                >
                  <button
                    class="area-lead-marshal-header"
                    :class="{ active: expandedAreaLeadMarshal === marshal.marshalId, 'is-checked-in': marshal.isCheckedIn }"
                    @click="toggleAreaLeadMarshalExpansion(marshal.marshalId)"
                  >
                    <div class="marshal-header-info">
                      <div class="marshal-name-row">
                        <span class="marshal-name">{{ marshal.name }}</span>
                        <span class="check-status-badge" :class="{ 'checked-in': marshal.isCheckedIn }">
                          {{ marshal.isCheckedIn ? '✓' : '○' }}
                        </span>
                      </div>
                      <div class="marshal-meta">
                        <span class="marshal-checkpoint">
                          {{ formatMarshalCheckpoints(marshal.checkpoints) }}
                        </span>
                        <span v-if="marshal.totalTaskCount > 0" class="marshal-task-count">
                          {{ marshal.completedTaskCount }} / {{ marshal.totalTaskCount }} {{ termsLower.checklists }} complete
                        </span>
                      </div>
                    </div>
                    <span class="accordion-icon">{{ expandedAreaLeadMarshal === marshal.marshalId ? '−' : '+' }}</span>
                  </button>

                  <div v-if="expandedAreaLeadMarshal === marshal.marshalId" class="area-lead-marshal-content">
                    <!-- Check-in status -->
                    <div class="marshal-checkin-section">
                      <CheckInToggleButton
                        :is-checked-in="marshal.isCheckedIn"
                        :check-in-time="marshal.checkInTime"
                        :check-in-method="marshal.checkInMethod"
                        :checked-in-by="marshal.checkedInBy"
                        :marshal-name="marshal.marshalName"
                        :is-loading="checkingIn === marshal.id"
                        @toggle="handleAreaLeadMarshalCheckIn(marshal)"
                      />
                    </div>

                    <!-- Contact details -->
                    <div v-if="marshal.email || marshal.phoneNumber" class="marshal-contact-section">
                      <a v-if="marshal.phoneNumber" :href="`tel:${marshal.phoneNumber}`" class="contact-link">
                        {{ marshal.phoneNumber }}
                      </a>
                      <a v-if="marshal.email" :href="`mailto:${marshal.email}`" class="contact-link">
                        {{ marshal.email }}
                      </a>
                    </div>

                    <!-- Tasks -->
                    <div v-if="marshal.allTasks.length > 0" class="marshal-tasks-section">
                      <div class="tasks-label">{{ terms.checklists }}</div>
                      <div class="tasks-list">
                        <div
                          v-for="task in marshal.allTasks"
                          :key="`${task.itemId}-${task.contextId}`"
                          class="task-item"
                          :class="{ 'task-completed': task.isCompleted }"
                        >
                          <input
                            type="checkbox"
                            :checked="task.isCompleted"
                            :disabled="savingAreaLeadMarshalTask"
                            @change="toggleAreaLeadMarshalTask(task, marshal)"
                          />
                          <span class="task-text">{{ task.text }}</span>
                        </div>
                      </div>
                    </div>

                    <div v-else class="no-tasks-message">
                      No {{ termsLower.checklists }} assigned.
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

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
              <div class="map-wrapper">
                <CommonMap
                  ref="courseMapRef"
                  :locations="allLocations"
                  :route="eventRoute"
                  :center="mapCenter"
                  :zoom="15"
                  :user-location="userLocation"
                  :highlight-location-ids="assignmentLocationIds"
                  :marshal-mode="true"
                  :clickable="selectingLocationOnMap || hasDynamicAssignment"
                  :show-fullscreen="true"
                  :fullscreen-title="terms.course"
                  :fullscreen-header-style="headerStyle"
                  :fullscreen-header-text-color="headerTextColor"
                  :toolbar-actions="courseMapActions"
                  :hide-recenter-button="true"
                  :class="{ 'selecting-location': selectingLocationOnMap }"
                  height="100%"
                  @map-click="handleMapClick"
                  @location-click="handleLocationClick"
                  @action-click="handleCourseMapAction"
                  @visibility-change="handleCourseMapVisibilityChange"
                >
                  <!-- Selection mode banner -->
                  <template v-if="selectingLocationOnMap" #fullscreen-banner>
                    <span>Click on the map to set the new location for <strong>{{ updatingLocationFor?.location?.name }}</strong></span>
                    <button @click="cancelMapLocationSelect" class="btn btn-secondary btn-sm">Cancel</button>
                  </template>
                </CommonMap>
              </div>
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

    <!-- Report Incident Modal -->
    <ReportIncidentModal
      :show="showReportIncident"
      :event-id="eventId || ''"
      :checkpoint="defaultIncidentCheckpoint"
      :initial-location="userLocation"
      :checkpoints="allLocations"
      :route="eventRoute"
      @close="showReportIncident = false"
      @submit="handleReportIncident"
    />

    <IncidentDetailModal
      :show="showIncidentDetail"
      :incident="selectedIncident"
      :route="eventRoute"
      :checkpoints="allLocations"
      :can-manage="isAreaLead"
      @close="showIncidentDetail = false"
      @status-change="handleIncidentStatusChange"
      @open-add-note="openAddIncidentNoteModal"
    />

    <!-- Add Incident Update Modal -->
    <BaseModal
      :show="showAddIncidentNoteModal"
      title="Add update"
      size="small"
      @close="closeAddIncidentNoteModal"
    >
      <div class="add-note-form">
        <textarea
          v-model="incidentNoteText"
          class="note-textarea"
          placeholder="Enter your update..."
          rows="4"
        ></textarea>
      </div>
      <template #footer>
        <div class="modal-actions">
          <button type="button" class="btn btn-secondary" @click="closeAddIncidentNoteModal">
            Cancel
          </button>
          <button
            type="button"
            class="btn btn-primary"
            @click="submitIncidentNote"
            :disabled="!incidentNoteText.trim() || submittingIncidentNote"
          >
            {{ submittingIncidentNote ? 'Adding...' : 'Add update' }}
          </button>
        </div>
      </template>
    </BaseModal>

    <ConfirmModal
      :show="showConfirmModal"
      :title="confirmModalTitle"
      :message="confirmModalMessage"
      @confirm="handleConfirmModalConfirm"
      @cancel="handleConfirmModalCancel"
    />

    <!-- Message Modal (for success/error messages) -->
    <ConfirmModal
      :show="showMessageModal"
      :title="messageModalTitle"
      :message="messageModalMessage"
      :show-cancel="false"
      @confirm="handleMessageModalClose"
    />

    <!-- Check-in Reminder Modal -->
    <div v-if="showCheckInReminderModal && checkInReminderCheckpoint" class="modal-overlay" @click.self="dismissCheckInReminder(checkInReminderCheckpoint.id)">
      <div class="modal-content check-in-reminder-modal">
        <div class="modal-header warning">
          <h3>Check-in reminder</h3>
          <button class="modal-close" @click="dismissCheckInReminder(checkInReminderCheckpoint.id)">&times;</button>
        </div>
        <div class="modal-body">
          <div class="reminder-icon">⚠️</div>
          <p class="reminder-message">
            You have not checked in yet to {{ terms.checkpoint.toLowerCase() }}
            <strong>{{ checkInReminderCheckpoint.location?.name }}</strong><span v-if="checkInReminderCheckpoint.location?.description"> - {{ checkInReminderCheckpoint.location.description }}</span>.
          </p>
          <p class="reminder-hint">
            Please check in when you arrive at your {{ terms.checkpoint.toLowerCase() }}.
          </p>
        </div>
        <div class="modal-footer">
          <button @click="dismissCheckInReminder(checkInReminderCheckpoint.id)" class="btn btn-secondary">
            Dismiss
          </button>
          <button
            @click="dismissCheckInReminder(checkInReminderCheckpoint.id); expandedSection = 'assignments'; expandedCheckpoint = checkInReminderCheckpoint.id"
            class="btn btn-primary"
            :style="accentButtonStyle"
          >
            Go to {{ terms.checkpoint }}
          </button>
        </div>
      </div>
    </div>

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
                :style="accentButtonStyle"
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
import { authApi, checkInApi, checklistApi, eventsApi, assignmentsApi, locationsApi, areasApi, notesApi, contactsApi, incidentsApi, queueOfflineAction, getOfflineMode } from '../services/api';
import CommonMap from '../components/common/CommonMap.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import BaseModal from '../components/BaseModal.vue';
import EmergencyContactModal from '../components/event-manage/modals/EmergencyContactModal.vue';
import ReportIncidentModal from '../components/ReportIncidentModal.vue';
import IncidentCard from '../components/IncidentCard.vue';
import IncidentDetailModal from '../components/IncidentDetailModal.vue';
import AreaLeadSection from '../components/AreaLeadSection.vue';
import GroupedTasksList from '../components/event-manage/GroupedTasksList.vue';
import NotesView from '../components/NotesView.vue';
import OfflineIndicator from '../components/OfflineIndicator.vue';
import CheckInToggleButton from '../components/common/CheckInToggleButton.vue';
import { setTerminology, useTerminology } from '../composables/useTerminology';
import { getIcon } from '../utils/icons';
import { getContrastTextColor, getGradientContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';
import { generateCheckpointSvg } from '../constants/checkpointIcons';
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

// Event ID from route params (reactive for template usage)
const eventId = computed(() => route.params.eventId);

// Auth state
const isAuthenticated = ref(false);
const authenticating = ref(false);
const loginError = ref(null);
const currentPerson = ref(null);
const currentMarshalId = ref(null);
const userClaims = ref(null);

// Current marshal name for check-in display
const currentMarshalName = computed(() => currentPerson.value?.name || null);

// Area lead state
const areaLeadAreaIds = computed(() => {
  if (!userClaims.value?.eventRoles) return [];
  const areaLeadRoles = userClaims.value.eventRoles.filter(r => r.role === 'EventAreaLead');
  return areaLeadRoles.flatMap(r => r.areaIds || []);
});

const isAreaLead = computed(() => areaLeadAreaIds.value.length > 0);

// Area filter state for "Your areas" section
const selectedAreaFilters = ref(new Set());

// Get area objects for the filter pills
const areaLeadAreas = computed(() => {
  if (!areaLeadAreaIds.value.length) return [];
  return areas.value
    .filter(a => areaLeadAreaIds.value.includes(a.id))
    .map(a => ({ areaId: a.id, name: a.name, color: a.color }))
    .sort((a, b) => (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' }));
});

// Initialize selected filters when areas load
watch(areaLeadAreas, (newAreas) => {
  if (newAreas.length > 0 && selectedAreaFilters.value.size === 0) {
    // Initialize with all areas selected
    selectedAreaFilters.value = new Set(newAreas.map(a => a.areaId));
  }
}, { immediate: true });

// Filtered area IDs based on selection
const filteredAreaLeadAreaIds = computed(() => {
  // Return only the selected areas (empty array if none selected)
  return areaLeadAreaIds.value.filter(id => selectedAreaFilters.value.has(id));
});

// Toggle area filter selection
const toggleAreaFilter = (areaId) => {
  const newSet = new Set(selectedAreaFilters.value);
  if (newSet.has(areaId)) {
    newSet.delete(areaId);
  } else {
    newSet.add(areaId);
  }
  selectedAreaFilters.value = newSet;
};

// All checkpoints available for incident reporting
// Marshals: see their assigned checkpoints
// Area leads: see all checkpoints in their areas plus their own assignments
const incidentCheckpoints = computed(() => {
  const checkpointMap = new Map();

  // Add marshal's assigned checkpoints
  for (const assign of assignmentsWithDetails.value) {
    if (assign.location) {
      checkpointMap.set(assign.location.id, {
        id: assign.location.id,
        name: assign.location.name,
        description: assign.location.description,
        order: assign.location.order ?? 999,
      });
    }
  }

  // For area leads, also add all checkpoints in their areas
  if (isAreaLead.value) {
    for (const loc of allLocations.value) {
      if (checkpointMap.has(loc.id)) continue;
      const locAreaIds = loc.areaIds || loc.AreaIds || [];
      if (locAreaIds.some(areaId => areaLeadAreaIds.value.includes(areaId))) {
        checkpointMap.set(loc.id, {
          id: loc.id,
          name: loc.name,
          description: loc.description,
          order: loc.order ?? 999,
        });
      }
    }
  }

  // Sort by order, then by name (natural sort so "2 A" comes before "10 - B")
  return Array.from(checkpointMap.values()).sort((a, b) => {
    if (a.order !== b.order) return a.order - b.order;
    return (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' });
  });
});

// Default checkpoint for incident reporting - the checked-in checkpoint or first assignment
const defaultIncidentCheckpoint = computed(() => {
  // Find the first checked-in checkpoint
  const checkedIn = assignmentsWithDetails.value.find(a => {
    const effectiveIsCheckedIn = a.isCheckedIn && !isCheckInStale(a.checkInTime);
    return effectiveIsCheckedIn && a.location;
  });

  if (checkedIn?.location) {
    return {
      id: checkedIn.location.id,
      locationId: checkedIn.location.id,
      name: checkedIn.location.name,
      description: checkedIn.location.description,
    };
  }

  // Otherwise use expanded or first assignment
  if (expandedCheckpoint.value) {
    const expanded = assignmentsWithDetails.value.find(a => a.id === expandedCheckpoint.value);
    if (expanded?.location) {
      return {
        id: expanded.location.id,
        locationId: expanded.location.id,
        name: expanded.location.name,
        description: expanded.location.description,
      };
    }
  }

  // Fall back to first assignment
  const first = assignmentsWithDetails.value[0];
  if (first?.location) {
    return {
      id: first.location.id,
      locationId: first.location.id,
      name: first.location.name,
      description: first.location.description,
    };
  }

  return null;
});

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

// Branding styles computed from event data
const pageBackgroundStyle = computed(() => {
  const start = event.value?.brandingPageGradientStart || DEFAULT_COLORS.pageGradientStart;
  const end = event.value?.brandingPageGradientEnd || DEFAULT_COLORS.pageGradientEnd;
  return { background: `linear-gradient(135deg, ${start} 0%, ${end} 100%)` };
});

const headerStyle = computed(() => {
  const start = event.value?.brandingHeaderGradientStart || DEFAULT_COLORS.headerGradientStart;
  const end = event.value?.brandingHeaderGradientEnd || DEFAULT_COLORS.headerGradientEnd;
  return { background: `linear-gradient(135deg, ${start} 0%, ${end} 100%)` };
});

const headerTextColor = computed(() => {
  const start = event.value?.brandingHeaderGradientStart || DEFAULT_COLORS.headerGradientStart;
  const end = event.value?.brandingHeaderGradientEnd || DEFAULT_COLORS.headerGradientEnd;
  return getGradientContrastTextColor(start, end);
});

const accentColor = computed(() => {
  return event.value?.brandingAccentColor || DEFAULT_COLORS.accentColor;
});

const accentTextColor = computed(() => {
  return getContrastTextColor(accentColor.value);
});

const accentButtonStyle = computed(() => ({
  background: accentColor.value,
  color: accentTextColor.value,
}));

const brandingLogoUrl = computed(() => event.value?.brandingLogoUrl || '');
const brandingLogoPosition = computed(() => event.value?.brandingLogoPosition || 'left');

// Accordion state
const expandedSection = ref('assignments');

// Track when each section's data was last loaded (for stale data refresh)
const STALE_DATA_THRESHOLD_MS = 60000; // 60 seconds
const sectionLastLoadedAt = ref({
  checklist: 0,
  notes: 0,
  incidents: 0,
  eventContacts: 0,
});

// Check-in state
const checkingIn = ref(null); // Now stores the assignment ID being checked in
const checkingInAssignment = ref(null);
const checkInError = ref(null);

// Checklist state
const checklistItems = ref([]);
const checklistLoading = ref(false);
const checklistError = ref(null);
const savingChecklist = ref(false);
const checklistGroupBy = ref('person'); // 'none', 'checkpoint', 'person', 'job'
const expandedChecklistGroup = ref(null);

// Area lead checkpoints data (for marshal name lookup in checklists)
const areaLeadCheckpoints = ref([]);

// Notes state
const notes = ref([]);

// Incidents state (my incidents and area lead incidents)
const myIncidents = ref([]);
const incidentsLoading = ref(false);
const selectedIncident = ref(null);
const showIncidentDetail = ref(false);
const showAddIncidentNoteModal = ref(false);
const incidentNoteText = ref('');
const submittingIncidentNote = ref(false);

// Count of open incidents for badge
const openIncidentsCount = computed(() => {
  return myIncidents.value.filter(i =>
    i.status === 'open' || i.status === 'acknowledged' || i.status === 'in_progress'
  ).length;
});

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

// Area Lead Marshals section state
const expandedAreaLeadMarshal = ref(null);
const savingAreaLeadMarshalTask = ref(false);
const areaLeadMarshalDataVersion = ref(0); // Trigger for recomputing marshals

// Toggle area lead marshal expansion
const toggleAreaLeadMarshalExpansion = (marshalId) => {
  expandedAreaLeadMarshal.value = expandedAreaLeadMarshal.value === marshalId ? null : marshalId;
};

// Aggregate all marshals from area lead checkpoints (deduplicated, sorted by name)
const allAreaLeadMarshals = computed(() => {
  // Access the version trigger to force recomputation when data changes
  // eslint-disable-next-line no-unused-vars
  const _version = areaLeadMarshalDataVersion.value;
  const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
  const marshalMap = new Map();

  for (const checkpoint of checkpoints) {
    for (const marshal of (checkpoint.marshals || [])) {
      if (!marshalMap.has(marshal.marshalId)) {
        marshalMap.set(marshal.marshalId, {
          ...marshal,
          checkpoints: [],
          allTasks: [],
          totalTaskCount: 0,
          completedTaskCount: 0,
        });
      }
      const m = marshalMap.get(marshal.marshalId);
      m.checkpoints.push({
        checkpointId: checkpoint.checkpointId,
        name: checkpoint.name,
        description: checkpoint.description,
      });
      // Add marshal's outstanding tasks
      if (marshal.outstandingTasks) {
        for (const task of marshal.outstandingTasks) {
          m.allTasks.push({ ...task, isCompleted: false, checkpointName: checkpoint.name });
          m.totalTaskCount++;
        }
      }
      // Add marshal's completed tasks if available
      if (marshal.completedTasks) {
        for (const task of marshal.completedTasks) {
          m.allTasks.push({ ...task, isCompleted: true, checkpointName: checkpoint.name });
          m.totalTaskCount++;
          m.completedTaskCount++;
        }
      }
    }
  }

  return Array.from(marshalMap.values()).sort((a, b) =>
    (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' })
  );
});

// Handle check-in for area lead marshals section (uses unified handleCheckInToggle)
const handleAreaLeadMarshalCheckIn = async (marshal) => {
  // Find a checkpoint that has this marshal to get the assignment ID
  const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
  const checkpoint = checkpoints.find(c =>
    c.marshals?.some(m => m.marshalId === marshal.marshalId)
  );
  if (!checkpoint) {
    console.error('Could not find checkpoint for marshal:', marshal);
    return;
  }

  // Find the full marshal object from the checkpoint
  const checkpointMarshal = checkpoint.marshals.find(m => m.marshalId === marshal.marshalId);
  if (!checkpointMarshal) return;

  // Build an assignment-like object for handleCheckInToggle
  const assignmentId = checkpointMarshal.assignmentId || checkpointMarshal.id;
  if (!assignmentId) {
    console.error('No assignment ID found for marshal:', checkpointMarshal);
    return;
  }

  const assignmentLike = {
    id: assignmentId,
    marshalId: marshal.marshalId,
    isCheckedIn: marshal.isCheckedIn,
    effectiveIsCheckedIn: marshal.isCheckedIn,
  };

  // Use unified toggle (no GPS for checking in others)
  await handleCheckInToggle(assignmentLike, false, checkpoint.locationId);
};

// Toggle task completion from the area lead marshals section
const toggleAreaLeadMarshalTask = async (task, marshal) => {
  if (savingAreaLeadMarshalTask.value) return;
  savingAreaLeadMarshalTask.value = true;

  const actionData = {
    marshalId: marshal.marshalId,
    contextType: task.contextType,
    contextId: task.contextId,
    actorMarshalId: currentMarshalId.value,
  };

  try {
    if (task.isCompleted) {
      // Uncomplete
      await checklistApi.uncomplete(eventId.value, task.itemId, actionData);
    } else {
      // Complete
      await checklistApi.complete(eventId.value, task.itemId, actionData);
    }
    // Reload the area lead dashboard data
    if (areaLeadRef.value?.loadDashboard) {
      await areaLeadRef.value.loadDashboard();
    }
    // Force recomputation of allAreaLeadMarshals
    areaLeadMarshalDataVersion.value++;
    // Also reload the main checklist
    await loadChecklist(true);
  } catch (err) {
    console.error('Failed to toggle task:', err);
  } finally {
    savingAreaLeadMarshalTask.value = false;
  }
};

// UI state
const showEmergency = ref(false);
const showReportIncident = ref(false);
const reportingIncident = ref(false);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

// Message modal state (for success/error messages)
const showMessageModal = ref(false);
const messageModalTitle = ref('');
const messageModalMessage = ref('');

// Check-in reminder modal state
const showCheckInReminderModal = ref(false);
const checkInReminderCheckpoint = ref(null);
const dismissedCheckInReminders = ref(new Set());

// Course map ref
const courseMapRef = ref(null);

// Course map visibility tracking (default to false so buttons show initially)
const courseMapVisibility = ref({
  userLocationInView: false,
  highlightedLocationInView: false,
});

// Checkpoint map visibility tracking (keyed by assignment ID)
const checkpointMapVisibility = ref({});

// Refs for checkpoint mini-maps (plain object, not reactive)
const checkpointMapRefs = {};

// Expanded marshal details (keyed by marshalId)
const expandedMarshalId = ref(null);

// Check if user is area lead for a given set of area IDs
const isAreaLeadForAreas = (areaIds) => {
  if (!areaIds || areaIds.length === 0) return false;
  return areaIds.some(areaId => areaLeadAreaIds.value.includes(areaId));
};

// Toggle marshal details expansion
const toggleMarshalDetails = (marshalId) => {
  expandedMarshalId.value = expandedMarshalId.value === marshalId ? null : marshalId;
};

// Format check-in time for display
const formatCheckInTime = (checkInTime) => {
  if (!checkInTime) return '';
  const date = new Date(checkInTime);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
};

// Format check-in method for display
const formatCheckInMethod = (method) => {
  if (!method) return '';
  switch (method) {
    case 'GPS': return 'GPS';
    case 'Manual': return 'Manual';
    case 'AreaLead': return 'By area lead';
    default: return method;
  }
};

// Format marshal checkpoints with descriptions for display
const formatMarshalCheckpoints = (checkpoints) => {
  if (!checkpoints || checkpoints.length === 0) return '';
  return checkpoints.map(c => {
    if (c.description) {
      // Truncate description if too long
      const maxDescLength = 40;
      const desc = c.description.length > maxDescLength
        ? c.description.substring(0, maxDescLength).trim() + '...'
        : c.description;
      return `${c.name} - ${desc}`;
    }
    return c.name;
  }).join(', ');
};

// Function to set checkpoint map ref
const setCheckpointMapRef = (assignId, el) => {
  if (el) {
    checkpointMapRefs[assignId] = el;
  }
};

// Helper to check if a check-in is stale (more than 24 hours old)
const isCheckInStale = (checkInTime) => {
  if (!checkInTime) return true;
  const checkInDate = new Date(checkInTime);
  const now = new Date();
  const hoursDiff = (now - checkInDate) / (1000 * 60 * 60);
  return hoursDiff > 24;
};

// GPS tracking
const userLocation = ref(null);
const locationLastUpdated = ref(null); // Track when location was last updated
let locationWatchId = null;

// Toggle accordion section
const toggleSection = (section) => {
  const wasExpanded = expandedSection.value === section;
  expandedSection.value = wasExpanded ? null : section;

  // If expanding a section, check if data is stale and refresh in background
  if (!wasExpanded) {
    const now = Date.now();
    const isStale = (key) => now - sectionLastLoadedAt.value[key] > STALE_DATA_THRESHOLD_MS;

    switch (section) {
      case 'checklist':
        if (isStale('checklist')) {
          loadChecklist();
        }
        break;
      case 'notes':
        if (isStale('notes')) {
          loadNotes();
        }
        break;
      case 'incidents':
        if (isStale('incidents')) {
          loadMyIncidents();
        }
        break;
      case 'eventContacts':
        if (isStale('eventContacts')) {
          loadContacts();
        }
        break;
    }
  }
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

    // Get all marshals assigned to this checkpoint, with effective check-in status
    // Sort alphabetically by name per project guidelines
    const rawMarshals = location?.assignments || [];
    const allMarshals = rawMarshals
      .map(m => ({
        ...m,
        effectiveIsCheckedIn: m.isCheckedIn && !isCheckInStale(m.checkInTime),
      }))
      .sort((a, b) => (a.marshalName || '').localeCompare(b.marshalName || '', undefined, { sensitivity: 'base' }));

    // Get area contacts for this checkpoint's areas
    // Filter out self and deduplicate
    const rawAreaContacts = areaContactsRaw.value.filter(contact =>
      areaIds.includes(contact.areaId) &&
      (!contact.marshalId || contact.marshalId !== currentMarshalId.value)
    );
    const seenContacts = new Set();
    const areaContacts = rawAreaContacts
      .filter(contact => {
        const key = JSON.stringify({
          name: contact.name || '',
          marshalId: contact.marshalId || '',
          role: contact.role || '',
          phone: contact.phone || '',
          email: contact.email || '',
          notes: contact.notes || '',
        });
        if (seenContacts.has(key)) {
          return false;
        }
        seenContacts.add(key);
        return true;
      })
      .sort((a, b) => (a.marshalName || a.name || '').localeCompare(b.marshalName || b.name || '', undefined, { sensitivity: 'base' }));

    // Effective check-in status (reset if checked in more than 24 hours ago)
    const effectiveIsCheckedIn = assign.isCheckedIn && !isCheckInStale(assign.checkInTime);

    return {
      ...assign,
      location,
      areaName,
      areaIds,
      allMarshals,
      areaContacts,
      locationName: location?.name || 'Unknown Location',
      effectiveIsCheckedIn,
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

// Find checkpoints that are overdue for check-in and haven't been dismissed
const overdueCheckpoints = computed(() => {
  if (!event.value || !assignmentsWithDetails.value.length) return [];

  const now = new Date();
  const eventStartTime = event.value.eventDate ? new Date(event.value.eventDate) : null;

  return assignmentsWithDetails.value.filter(assign => {
    // Skip if already effectively checked in
    if (assign.effectiveIsCheckedIn) return false;

    // Skip if already dismissed
    if (dismissedCheckInReminders.value.has(assign.id)) return false;

    const location = assign.location;
    if (!location) return false;

    // Determine start time: checkpoint start time, or event start time
    const checkpointStartTime = location.startTime ? new Date(location.startTime) : eventStartTime;
    if (!checkpointStartTime) return false;

    // Determine end time: checkpoint end time, or event start + 2 hours
    let checkpointEndTime;
    if (location.endTime) {
      checkpointEndTime = new Date(location.endTime);
    } else if (eventStartTime) {
      checkpointEndTime = new Date(eventStartTime.getTime() + 2 * 60 * 60 * 1000);
    } else {
      // No end time can be determined, skip
      return false;
    }

    // Check if current time is within the reminder window
    return now >= checkpointStartTime && now < checkpointEndTime;
  });
});

// Watch for overdue checkpoints and show reminder modal
watch(overdueCheckpoints, (overdue) => {
  if (overdue.length > 0 && !showCheckInReminderModal.value) {
    checkInReminderCheckpoint.value = overdue[0];
    showCheckInReminderModal.value = true;
  }
}, { immediate: true });

// Dismiss check-in reminder for a checkpoint
const dismissCheckInReminder = (assignmentId) => {
  dismissedCheckInReminders.value.add(assignmentId);
  showCheckInReminderModal.value = false;
  checkInReminderCheckpoint.value = null;
};

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
    // unless this marshal completed it (so they can uncomplete it)
    const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];
    if (sharedScopes.includes(item.matchedScope)) {
      // Show if this marshal completed it
      return item.completedByActorId === currentMarshalId.value;
    }

    // Show all other completed tasks (personal tasks, everyone tasks, etc.)
    return true;
  });
});

// Checklist completion count (from visible items only)
const completedChecklistCount = computed(() => {
  return visibleChecklistItems.value.filter(item => item.isCompleted).length;
});

// Separate checklist items into "your jobs" vs "your area's jobs"
// "Your jobs" = tasks assigned specifically to you or applicable to everyone
// "Your area's jobs" = tasks for other people in your areas (area leads only)
const myChecklistItems = computed(() => {
  const myAssignmentIds = assignments.value.map(a => a.locationId);

  return visibleChecklistItems.value.filter(item => {
    // Personal tasks assigned to current marshal
    if (item.completionContextType === 'Personal') {
      return item.contextOwnerMarshalId === currentMarshalId.value;
    }
    // Checkpoint tasks for checkpoints the marshal is assigned to
    if (item.completionContextType === 'Checkpoint') {
      return myAssignmentIds.includes(item.completionContextId);
    }
    // Area-scoped tasks where you're in that area
    if (item.completionContextType === 'Area') {
      return areaLeadAreaIds.value.includes(item.completionContextId);
    }
    // Everyone tasks are yours
    return true;
  });
});

const areaChecklistItems = computed(() => {
  if (!isAreaLead.value) return [];
  const myAssignmentIds = assignments.value.map(a => a.locationId);

  return visibleChecklistItems.value.filter(item => {
    // Personal tasks for OTHER marshals
    if (item.completionContextType === 'Personal') {
      return item.contextOwnerMarshalId !== currentMarshalId.value;
    }
    // Checkpoint tasks for checkpoints the marshal is NOT assigned to
    if (item.completionContextType === 'Checkpoint') {
      return !myAssignmentIds.includes(item.completionContextId);
    }
    // Area tasks not in your personal areas (shouldn't happen but filter anyway)
    if (item.completionContextType === 'Area') {
      return !areaLeadAreaIds.value.includes(item.completionContextId);
    }
    return false;
  });
});

// Convert items to format needed by GroupedTasksList
const myChecklistItemsWithLocalState = computed(() => {
  return myChecklistItems.value.map(item => ({
    ...item,
    localIsCompleted: item.isCompleted,
    isModified: false,
  }));
});

const areaChecklistItemsWithLocalState = computed(() => {
  return areaChecklistItems.value.map(item => ({
    ...item,
    localIsCompleted: item.isCompleted,
    isModified: false,
  }));
});

// Keep for backwards compatibility (combines both)
// Checklist items with localIsCompleted for GroupedTasksList component
const checklistItemsWithLocalState = computed(() => {
  return visibleChecklistItems.value.map(item => ({
    ...item,
    localIsCompleted: item.isCompleted,
    isModified: false, // We don't track unsaved changes in marshal view
  }));
});

// Collect all marshals from area lead's checkpoints for name lookup
// Uses AreaLeadSection data if available, falls back to preloaded data
const areaMarshalsForChecklist = computed(() => {
  // Try to get checkpoints from AreaLeadSection (most up-to-date)
  // or fall back to preloaded area lead checkpoints
  const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
  if (checkpoints.length === 0) return [];

  const marshals = [];
  const seenIds = new Set();
  for (const checkpoint of checkpoints) {
    for (const marshal of (checkpoint.marshals || [])) {
      if (!seenIds.has(marshal.marshalId)) {
        seenIds.add(marshal.marshalId);
        marshals.push(marshal);
      }
    }
  }
  return marshals;
});

// Check if there are multiple checkpoints or jobs (to show grouping selector)
const hasMultipleChecklistContexts = computed(() => {
  const items = visibleChecklistItems.value;
  if (items.length <= 1) return false;

  // Check for multiple checkpoints
  const checkpointIds = new Set();
  const jobTexts = new Set();

  for (const item of items) {
    if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
      checkpointIds.add(item.completionContextId);
    }
    jobTexts.add(item.text);
  }

  return checkpointIds.size > 1 || jobTexts.size > 1;
});

// Effective grouping mode for non-leads (checkpoint grouping if multiple checkpoints exist)
const effectiveChecklistGroupBy = computed(() => {
  // Area leads use GroupedTasksList component instead
  if (isAreaLead.value) {
    return 'none'; // Not used for leads
  }

  // For non-leads, check if there are multiple checkpoints
  const items = visibleChecklistItems.value;
  const checkpointIds = new Set();
  for (const item of items) {
    if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
      checkpointIds.add(item.completionContextId);
    }
  }

  // If multiple checkpoints, group by checkpoint; otherwise show flat list
  return checkpointIds.size > 1 ? 'checkpoint' : 'none';
});

// Group checklist items by checkpoint (for non-leads with multiple checkpoints)
const groupedChecklistItems = computed(() => {
  const items = visibleChecklistItems.value;
  const groups = {};

  for (const item of items) {
    let key, name;

    if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
      key = `checkpoint_${item.completionContextId}`;
      const location = allLocations.value.find(l => l.id === item.completionContextId);
      name = location?.name || 'Unknown ' + terms.value.checkpoint;
    } else if (item.completionContextType === 'Area' && item.completionContextId) {
      key = `area_${item.completionContextId}`;
      const area = areas.value.find(a => a.id === item.completionContextId);
      name = area?.name || 'Unknown ' + terms.value.area;
    } else {
      key = 'personal';
      name = 'Personal';
    }

    if (!groups[key]) {
      groups[key] = { key, name, items: [], completedCount: 0 };
    }
    groups[key].items.push(item);
    if (item.isCompleted) {
      groups[key].completedCount++;
    }
  }

  // Sort groups by name using natural sort
  return Object.values(groups).sort((a, b) =>
    a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' })
  );
});

// Toggle checklist group expansion
const toggleChecklistGroup = (key) => {
  expandedChecklistGroup.value = expandedChecklistGroup.value === key ? null : key;
};

// Get short context name for grouped view (without "At checkpoint" prefix)
const getContextNameShort = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return 'Personal';
  }

  if (item.completionContextType === 'Checkpoint') {
    const location = allLocations.value.find(l => l.id === item.completionContextId);
    return location?.name || 'Unknown';
  }

  if (item.completionContextType === 'Area') {
    const area = areas.value.find(a => a.id === item.completionContextId);
    return area?.name || 'Unknown';
  }

  return 'Personal';
};

// Event contacts - loaded from new contacts API
const myContacts = ref([]);
const contactsLoading = ref(false);

// Legacy event contacts computed for backwards compatibility
const eventContacts = computed(() => {
  // Use new contacts API data, filtering to show only event-wide contacts
  // (those not specifically tied to areas/checkpoints in their scope)

  // Filter out contacts that are linked to the current marshal (don't show yourself)
  const filtered = myContacts.value.filter(contact => {
    return !contact.marshalId || contact.marshalId !== currentMarshalId.value;
  });

  // Deduplicate contacts with same name, marshalId, role, phone, email, and notes
  const seen = new Set();
  const deduplicated = filtered.filter(contact => {
    const key = JSON.stringify({
      name: contact.name || '',
      marshalId: contact.marshalId || '',
      role: contact.role || '',
      phone: contact.phone || '',
      email: contact.email || '',
      notes: contact.notes || '',
    });
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });

  // Sort by name alphabetically
  return deduplicated.sort((a, b) =>
    (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' })
  );
});

// Emergency contacts - filter for specific roles that should be shown in emergency modal
const emergencyContacts = computed(() => {
  const emergencyRoles = ['EmergencyContact', 'EventDirector', 'MedicalLead', 'SafetyOfficer'];
  // Use eventContacts which already filters out self and deduplicates
  return eventContacts.value.filter(contact => emergencyRoles.includes(contact.role));
});

// Emergency notes - filter for Emergency or Urgent priority notes
const emergencyNotes = computed(() => {
  return notes.value.filter(note => {
    const priority = note.priority || note.Priority;
    return priority === 'Emergency' || priority === 'Urgent';
  });
});

// Get notes that are scoped to a specific checkpoint (area and checkpoint scopes only, not marshal scopes)
const getNotesForCheckpoint = (locationId, passedAreaIds = []) => {
  if (!notes.value || notes.value.length === 0) return [];

  // Get area IDs from the location directly as a fallback (matching NotesView behavior)
  const location = allLocations.value.find(l => l.id === locationId);
  const locationAreaIds = location?.areaIds || location?.AreaIds || [];
  // Use passed areaIds if available, otherwise use location's areaIds
  const areaIds = passedAreaIds.length > 0 ? passedAreaIds : locationAreaIds;

  const matchedNotes = [];
  const seenNoteIds = new Set();

  for (const note of notes.value) {
    const noteId = note.noteId || note.NoteId || note.id;
    if (seenNoteIds.has(noteId)) continue;

    // Handle both camelCase and PascalCase from backend
    const scopeConfigurations = note.scopeConfigurations || note.ScopeConfigurations || [];
    let matched = false;

    // First try scopeConfigurations if available
    if (scopeConfigurations.length > 0) {
      for (const config of scopeConfigurations) {
        const itemType = config.itemType || config.ItemType;
        const ids = config.ids || config.Ids || [];

        // Check checkpoint-specific scope
        if (itemType === 'Checkpoint') {
          if (ids.includes(locationId) || ids.includes('ALL_CHECKPOINTS')) {
            matched = true;
            break;
          }
        }

        // Check area-based scope (notes scoped to area apply to checkpoints in that area)
        if (itemType === 'Area') {
          const areaMatch = ids.includes('ALL_AREAS') || (areaIds.length > 0 && areaIds.some(areaId => ids.includes(areaId)));
          if (areaMatch) {
            matched = true;
            break;
          }
        }
      }
    } else {
      // Fallback: use matchedScope from my-notes API to determine if note applies to checkpoint/area
      // matchedScope indicates why the note was included for this marshal
      const matchedScope = note.matchedScope || note.MatchedScope || '';

      // Area and checkpoint scopes should show in checkpoint section
      // Marshal-specific scopes (SpecificPeople, AllMarshals) should NOT show in checkpoint section
      const areaOrCheckpointScopes = [
        'EveryoneInAreas',
        'EveryAreaLead',
        'EveryoneAtCheckpoints',
        'Area',
        'Checkpoint',
      ];

      if (areaOrCheckpointScopes.some(scope => matchedScope.includes(scope) || matchedScope === scope)) {
        matched = true;
      }
    }

    if (matched) {
      seenNoteIds.add(noteId);
      matchedNotes.push(note);
    }
  }

  // Sort by priority (Emergency > Urgent > High > Normal > Low), then pinned, then by date
  const priorityOrder = { 'Emergency': 0, 'Urgent': 1, 'High': 2, 'Normal': 3, 'Low': 4 };
  return matchedNotes.sort((a, b) => {
    // Pinned notes first
    const aPinned = a.isPinned || a.IsPinned;
    const bPinned = b.isPinned || b.IsPinned;
    if (aPinned && !bPinned) return -1;
    if (!aPinned && bPinned) return 1;

    // Then by priority
    const priorityA = priorityOrder[a.priority || a.Priority || 'Normal'] ?? 3;
    const priorityB = priorityOrder[b.priority || b.Priority || 'Normal'] ?? 3;
    if (priorityA !== priorityB) return priorityA - priorityB;

    // Then by date (newest first)
    const dateA = a.createdAt || a.CreatedAt;
    const dateB = b.createdAt || b.CreatedAt;
    return new Date(dateB) - new Date(dateA);
  });
};

// Area contacts - loaded from API, grouped by area
const areaContactsRaw = ref([]);

// Area contacts grouped by area
const areaContactsByArea = computed(() => {
  if (areaContactsRaw.value.length === 0) return [];

  // Filter out contacts that are linked to the current marshal (don't show yourself)
  const filtered = areaContactsRaw.value.filter(contact => {
    return !contact.marshalId || contact.marshalId !== currentMarshalId.value;
  });

  // Deduplicate contacts with same name, marshalId, role, phone, email, and notes
  const seen = new Set();
  const deduped = filtered.filter(contact => {
    const key = JSON.stringify({
      name: contact.name || '',
      marshalId: contact.marshalId || '',
      role: contact.role || '',
      phone: contact.phone || '',
      email: contact.email || '',
      notes: contact.notes || '',
      areaId: contact.areaId || '',
    });
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });

  // Group by area
  const grouped = {};
  for (const contact of deduped) {
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

  // Sort contacts within each group by name
  const groups = Object.values(grouped);
  for (const group of groups) {
    group.contacts.sort((a, b) =>
      (a.marshalName || a.name || '').localeCompare(b.marshalName || b.name || '', undefined, { sensitivity: 'base' })
    );
  }

  return groups;
});

// Total area contacts count (after filtering and deduplication)
const totalAreaContacts = computed(() => {
  return areaContactsByArea.value.reduce((sum, group) => sum + group.contacts.length, 0);
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

// SVG icons for course map toolbar actions (defined as constants to avoid reactivity issues)
const ICON_MY_LOCATION = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><circle cx="12" cy="12" r="4"/><path d="M13 4.069V2h-2v2.069A8.01 8.01 0 0 0 4.069 11H2v2h2.069A8.008 8.008 0 0 0 11 19.931V22h2v-2.069A8.007 8.007 0 0 0 19.931 13H22v-2h-2.069A8.008 8.008 0 0 0 13 4.069zM12 18c-3.309 0-6-2.691-6-6s2.691-6 6-6 6 2.691 6 6-2.691 6-6 6z"/></svg>';
const ICON_CHECKPOINT = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/></svg>';

// First assignment with a valid location (for recentering)
const firstAssignmentWithLocation = computed(() => {
  return assignmentsWithDetails.value.find(a =>
    a.location?.latitude && a.location?.longitude &&
    !(a.location.latitude === 0 && a.location.longitude === 0)
  );
});

// Course map toolbar actions for recentering (only show when location is off-screen)
const courseMapActions = computed(() => {
  const actions = [];

  // Recenter on my location (if GPS is available AND location is off-screen)
  if (userLocation.value && !courseMapVisibility.value.userLocationInView) {
    actions.push({
      id: 'recenter-user',
      label: 'My location',
      icon: 'custom',
      customIcon: ICON_MY_LOCATION,
    });
  }

  // Recenter on my checkpoint (if I have one with a location AND it's off-screen)
  // Use highlightedLocationInView since we highlight our assignments on the course map
  if (firstAssignmentWithLocation.value && !courseMapVisibility.value.highlightedLocationInView) {
    actions.push({
      id: 'recenter-checkpoint',
      label: 'My ' + termsLower.value.checkpoint,
      icon: 'custom',
      customIcon: ICON_CHECKPOINT,
    });
  }

  return actions;
});

// Handle course map visibility changes (only update if changed to avoid infinite loops)
const handleCourseMapVisibilityChange = (visibility) => {
  const current = courseMapVisibility.value;
  if (current.userLocationInView !== visibility.userLocationInView ||
      current.highlightedLocationInView !== visibility.highlightedLocationInView) {
    courseMapVisibility.value = visibility;
  }
};

// Handle course map toolbar actions
const handleCourseMapAction = ({ actionId }) => {
  if (actionId === 'recenter-user' && userLocation.value) {
    courseMapRef.value?.recenterOnUserLocation();
  } else if (actionId === 'recenter-checkpoint' && firstAssignmentWithLocation.value) {
    const loc = firstAssignmentWithLocation.value.location;
    courseMapRef.value?.recenterOnLocation(loc.latitude, loc.longitude);
  }
};

// Get toolbar actions for a checkpoint mini-map
const getCheckpointMapActions = (assignId) => {
  const actions = [];
  const visibility = checkpointMapVisibility.value[assignId] || { userLocationInView: false, highlightedLocationInView: false };

  // Recenter on my location (if GPS is available AND location is off-screen)
  if (userLocation.value && !visibility.userLocationInView) {
    actions.push({
      id: 'recenter-user',
      label: 'My location',
      icon: 'custom',
      customIcon: ICON_MY_LOCATION,
    });
  }

  // Recenter on this checkpoint (if it's off-screen)
  if (!visibility.highlightedLocationInView) {
    actions.push({
      id: 'recenter-checkpoint',
      label: termsLower.value.checkpoint.charAt(0).toUpperCase() + termsLower.value.checkpoint.slice(1),
      icon: 'custom',
      customIcon: ICON_CHECKPOINT,
    });
  }

  return actions;
};

// Handle checkpoint map visibility changes (only update if changed to avoid infinite loops)
const handleCheckpointMapVisibilityChange = (assignId, visibility) => {
  const current = checkpointMapVisibility.value[assignId];
  if (!current ||
      current.userLocationInView !== visibility.userLocationInView ||
      current.highlightedLocationInView !== visibility.highlightedLocationInView) {
    checkpointMapVisibility.value = {
      ...checkpointMapVisibility.value,
      [assignId]: visibility,
    };
  }
};

// Handle checkpoint map toolbar actions
const handleCheckpointMapAction = (assign, { actionId }) => {
  const mapRef = checkpointMapRefs[assign.id];
  if (actionId === 'recenter-user' && userLocation.value) {
    mapRef?.recenterOnUserLocation();
  } else if (actionId === 'recenter-checkpoint' && assign.location) {
    mapRef?.recenterOnLocation(assign.location.latitude, assign.location.longitude);
  }
};

const authenticateWithMagicCode = async (eventId, code, isReauth = false) => {
  authenticating.value = true;
  loginError.value = null;
  console.log('Authenticating with magic code:', { eventId, code, isReauth });

  try {
    const response = await authApi.marshalLogin(eventId, code);
    console.log('Auth response:', response.data);

    if (response.data.success) {
      // Store session token and magic code for future re-authentication
      localStorage.setItem('sessionToken', response.data.sessionToken);
      localStorage.setItem(`marshal_${eventId}`, response.data.marshalId);
      localStorage.setItem(`marshalCode_${eventId}`, code); // Store for re-auth

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
  const storedCode = localStorage.getItem(`marshalCode_${eventId}`);

  if (!sessionToken || !marshalId) {
    // No session, but check if we have a stored code to re-authenticate
    if (storedCode) {
      console.log('No session but have stored code, attempting re-authentication...');
      await authenticateWithMagicCode(eventId, storedCode, true);
      return isAuthenticated.value;
    }
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
    console.log('Session validation failed, attempting re-authentication...', error);

    // Session invalid - try to re-authenticate with stored magic code
    if (storedCode) {
      try {
        await authenticateWithMagicCode(eventId, storedCode, true);
        if (isAuthenticated.value) {
          console.log('Re-authentication successful');
          return true;
        }
      } catch (reAuthError) {
        console.error('Re-authentication failed:', reAuthError);
      }
    }

    // Re-auth failed or no stored code, clear session
    localStorage.removeItem('sessionToken');
    localStorage.removeItem(`marshal_${eventId}`);
    localStorage.removeItem(`marshalCode_${eventId}`);
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

      // Load area lead checkpoints for marshal name lookup
      if (cachedData.areaLeadDashboard?.checkpoints) {
        areaLeadCheckpoints.value = cachedData.areaLeadDashboard.checkpoints;
      }

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

  // Phase 2: Supplementary data (4 calls in parallel)
  await Promise.allSettled([
    loadChecklist(),
    loadContacts(),
    loadNotes(),
    loadMyIncidents()
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
      // Store checkpoints for marshal name lookup in checklists
      areaLeadCheckpoints.value = dashboardResponse.data.checkpoints || [];

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
  const eventIdVal = route.params.eventId;
  try {
    // Use getMyNotes to get notes relevant to the current marshal
    const response = await notesApi.getMyNotes(eventIdVal);
    notes.value = response.data || [];
    sectionLastLoadedAt.value.notes = Date.now();
    console.log('Notes loaded:', notes.value.length);
  } catch (error) {
    console.error('Failed to load notes:', error);
  }
};

// Load incidents for "Your incidents" section
// Shows: incidents I reported + incidents in areas I'm a lead for
const loadMyIncidents = async () => {
  const eventId = route.params.eventId;
  incidentsLoading.value = true;

  try {
    const incidentMap = new Map();

    // For area leads, load incidents from their areas
    if (isAreaLead.value && areaLeadAreaIds.value.length > 0) {
      for (const areaId of areaLeadAreaIds.value) {
        try {
          const response = await incidentsApi.getForArea(eventId, areaId);
          const areaIncidents = response.data.incidents || [];
          for (const incident of areaIncidents) {
            incidentMap.set(incident.incidentId, incident);
          }
        } catch (err) {
          console.warn(`Failed to load incidents for area ${areaId}:`, err);
        }
      }
    }

    // Also try to get all incidents and filter to ones I reported
    // (in case I reported an incident outside my area)
    try {
      const response = await incidentsApi.getAll(eventId);
      const allIncidents = response.data.incidents || [];
      for (const incident of allIncidents) {
        if (incident.reportedBy?.marshalId === currentMarshalId.value) {
          incidentMap.set(incident.incidentId, incident);
        }
      }
    } catch (err) {
      // Non-leads may not have access to getAll, which is fine
      console.debug('Could not load all incidents:', err);
    }

    myIncidents.value = Array.from(incidentMap.values()).sort((a, b) => {
      // Sort by date, most recent first
      return new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt);
    });

    sectionLastLoadedAt.value.incidents = Date.now();
    console.log('My incidents loaded:', myIncidents.value.length);
  } catch (error) {
    console.error('Failed to load incidents:', error);
  } finally {
    incidentsLoading.value = false;
  }
};

const loadContacts = async () => {
  const eventIdVal = route.params.eventId;
  contactsLoading.value = true;

  try {
    // Load contacts using the new API that returns scoped contacts for this user
    const response = await contactsApi.getMyContacts(eventIdVal);
    myContacts.value = response.data || [];
    sectionLastLoadedAt.value.eventContacts = Date.now();
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

const loadChecklist = async (silent = false) => {
  if (!currentMarshalId.value) {
    console.warn('No marshal ID, skipping checklist load');
    return;
  }

  // Only show loading state for initial load, not refreshes
  if (!silent) {
    checklistLoading.value = true;
  }
  checklistError.value = null;

  try {
    const eventId = route.params.eventId;
    console.log('Fetching checklist for marshal:', currentMarshalId.value);

    // Fetch personal checklist items
    const marshalResponse = await checklistApi.getMarshalChecklist(eventId, currentMarshalId.value);
    let allItems = marshalResponse.data || [];

    // For area leads, also fetch checklist items for their areas
    if (isAreaLead.value && areaLeadAreaIds.value.length > 0) {
      console.log('Fetching area checklists for areas:', areaLeadAreaIds.value);
      const areaPromises = areaLeadAreaIds.value.map(areaId =>
        checklistApi.getAreaChecklist(eventId, areaId).catch(err => {
          console.warn(`Failed to fetch checklist for area ${areaId}:`, err);
          return { data: [] };
        })
      );
      const areaResponses = await Promise.all(areaPromises);

      // Merge and deduplicate items
      const itemMap = new Map();
      for (const item of allItems) {
        const key = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
        itemMap.set(key, item);
      }
      for (const response of areaResponses) {
        for (const item of (response.data || [])) {
          const key = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
          if (!itemMap.has(key)) {
            itemMap.set(key, item);
          }
        }
      }
      allItems = Array.from(itemMap.values());
      console.log('Merged checklist items:', allItems.length);
    }

    checklistItems.value = allItems;
    sectionLastLoadedAt.value.checklist = Date.now();
    console.log('Checklist loaded:', checklistItems.value.length, 'items');
  } catch (error) {
    console.error('Failed to load checklist:', error.response?.status, error.response?.data);
    checklistError.value = error.response?.data?.message || 'Failed to load checklist';
  } finally {
    if (!silent) {
      checklistLoading.value = false;
    }
  }
};

const handleToggleChecklist = async (item) => {
  if (savingChecklist.value) return;

  savingChecklist.value = true;
  const eventId = route.params.eventId;

  // For Personal items, use the owner's marshal ID; otherwise use current marshal
  // Also send actorMarshalId to identify who is actually completing the task
  const actionData = {
    marshalId: item.contextOwnerMarshalId || currentMarshalId.value,
    contextType: item.completionContextType,
    contextId: item.completionContextId,
    actorMarshalId: currentMarshalId.value,
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
        await loadChecklist(true); // Silent refresh to preserve UI state
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
        await loadChecklist(true); // Silent refresh to preserve UI state
      }
    }

    // Update cached checklist (convert to plain objects for IndexedDB)
    await updateCachedField(eventId, 'checklist', JSON.parse(JSON.stringify(checklistItems.value)));
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

/**
 * Unified check-in/check-out toggle handler
 * @param {Object} assign - The assignment object
 * @param {boolean} tryGps - Whether to attempt GPS (true for self, false for checking in others)
 * @param {string} locationId - Optional location ID for updating location assignments
 */
const handleCheckInToggle = async (assign, tryGps = true, locationId = null) => {
  checkingIn.value = assign.id;
  checkingInAssignment.value = assign.id;
  checkInError.value = null;

  const eventId = route.params.eventId;
  const isCurrentlyCheckedIn = assign.effectiveIsCheckedIn || assign.isCheckedIn;
  const action = isCurrentlyCheckedIn ? 'check-out' : 'check-in';
  const newIsCheckedIn = !isCurrentlyCheckedIn;

  // Try to get GPS coordinates if requested (for self check-in, not check-out)
  let latitude = null;
  let longitude = null;
  let method = 'Manual';

  if (tryGps && !isCurrentlyCheckedIn && 'geolocation' in navigator) {
    try {
      const position = await new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 5000, // Shorter timeout - fall back to manual quickly
        });
      });
      latitude = position.coords.latitude;
      longitude = position.coords.longitude;
      method = 'GPS';
    } catch (gpsError) {
      // GPS failed, fall back to manual silently
      console.log('GPS unavailable, using manual check-in:', gpsError.message);
    }
  }

  const gpsData = { latitude, longitude, action };

  try {
    if (getOfflineMode()) {
      // Queue for offline sync
      await queueOfflineAction('checkin_toggle', {
        eventId,
        assignmentId: assign.id,
        gpsData,
      });

      // Optimistic update for own assignments
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = {
          ...assignments.value[index],
          isCheckedIn: newIsCheckedIn,
          checkInTime: newIsCheckedIn ? new Date().toISOString() : null,
          checkInMethod: newIsCheckedIn ? `${method} (pending)` : null,
          checkInLatitude: latitude,
          checkInLongitude: longitude,
        };
      }

      // Also update in location assignments if checking in another marshal
      if (locationId) {
        const location = allLocations.value.find(l => l.id === locationId);
        if (location?.assignments) {
          const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
          if (locAssign) {
            locAssign.isCheckedIn = newIsCheckedIn;
            locAssign.checkInTime = newIsCheckedIn ? new Date().toISOString() : null;
          }
        }
      }

      await updatePendingCount();
      await updateCachedField(eventId, 'locations', allLocations.value);
    } else {
      const response = await checkInApi.toggleCheckIn(eventId, assign.id, gpsData);

      // Update the assignment in the list
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = response.data;
      }

      // Also update in location assignments if checking in another marshal
      if (locationId) {
        const location = allLocations.value.find(l => l.id === locationId);
        if (location?.assignments) {
          const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
          if (locAssign) {
            Object.assign(locAssign, response.data);
          }
        }
      }
    }

    // Refresh area lead dashboard if applicable
    if (locationId && areaLeadRef.value?.loadDashboard) {
      await areaLeadRef.value.loadDashboard();
    }
    areaLeadMarshalDataVersion.value++;
  } catch (error) {
    // Check if it's a network error - queue for offline
    if (getOfflineMode() || !error.response) {
      await queueOfflineAction('checkin_toggle', {
        eventId,
        assignmentId: assign.id,
        gpsData,
      });

      // Optimistic update
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = {
          ...assignments.value[index],
          isCheckedIn: newIsCheckedIn,
          checkInTime: newIsCheckedIn ? new Date().toISOString() : null,
          checkInMethod: newIsCheckedIn ? 'Manual (pending)' : null,
        };
      }
      await updatePendingCount();
    } else if (error.response?.data?.message) {
      checkInError.value = error.response.data.message;
    } else if (error.message) {
      checkInError.value = error.message;
    } else {
      checkInError.value = `Failed to ${action}. Please try again.`;
    }
  } finally {
    checkingIn.value = null;
    checkingInAssignment.value = null;
  }
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

// Message modal helper
const showMessage = (title, message) => {
  messageModalTitle.value = title;
  messageModalMessage.value = message;
  showMessageModal.value = true;
};

const handleMessageModalClose = () => {
  showMessageModal.value = false;
};

// Incident reporting
const handleReportIncident = async (incidentData) => {
  reportingIncident.value = true;
  try {
    await incidentsApi.create(eventId.value, incidentData);
    showReportIncident.value = false;
    // Reload incidents to show the new one
    loadMyIncidents();
    showMessage('Incident Reported', 'An administrator will review it shortly.');
  } catch (error) {
    console.error('Failed to report incident:', error);
    showMessage('Error', error.response?.data?.message || 'Failed to report incident. Please try again.');
  } finally {
    reportingIncident.value = false;
  }
};

// Incident detail handlers
const openIncidentDetail = (incident) => {
  selectedIncident.value = incident;
  showIncidentDetail.value = true;
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
    showMessage('Error', 'Failed to update status. Please try again.');
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
    showMessage('Error', 'Failed to add note. Please try again.');
  } finally {
    submittingIncidentNote.value = false;
  }
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

  const eventId = route.params.eventId;
  localStorage.removeItem('sessionToken');
  localStorage.removeItem(`marshal_${eventId}`);
  localStorage.removeItem(`marshalCode_${eventId}`);

  isAuthenticated.value = false;
  currentPerson.value = null;
  currentMarshalId.value = null;
  assignments.value = [];
  checklistItems.value = [];
  areaContactsRaw.value = [];

  // Redirect to home
  router.push('/');
};

const confirmLogout = () => {
  confirmModalTitle.value = 'Logout';
  confirmModalMessage.value = `Are you sure you want to logout${currentPerson.value?.name ? `, ${currentPerson.value.name}` : ''}?`;
  confirmModalCallback.value = handleLogout;
  showConfirmModal.value = true;
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

// Check if a role is an emergency contact (for red styling)
const isEmergencyRole = (role) => {
  return role === 'EmergencyContact';
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

// Update document title when event loads - use configured person term
watch(event, (newEvent) => {
  if (newEvent) {
    document.title = `OnTheDay App - ${terms.value.person}`;
  }
}, { immediate: true });

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
/* Z-index layering - modals should always be above fullscreen map */
.marshal-view {
  --z-fullscreen-map: 2000;
  --z-modal: calc(var(--z-fullscreen-map) + 1000);
  min-height: 100vh;
  background: var(--brand-gradient);
}

.header {
  backdrop-filter: blur(10px);
  padding: 1rem 2rem;
  display: flex;
  align-items: center;
  position: relative;
}

.header.logo-left,
.header.logo-right {
  flex-direction: row;
  padding-top: 0;
  padding-bottom: 0;
  align-items: stretch;
  height: 120px;
}

.header.logo-left {
  padding-left: 0;
}

.header.logo-right {
  padding-right: 0;
}

.header.logo-cover {
  flex-direction: column;
}

.header-center {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 1rem 0;
  position: relative;
  z-index: 1;
}

.header h1 {
  margin: 0;
}

.header-title {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
}

.header-event-date {
  font-size: 0.9rem;
  opacity: 0.9;
  font-weight: 400;
}

.header-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  align-self: stretch;
  aspect-ratio: 1 / 1;
  overflow: hidden;
}

.header-logo-left {
  margin-right: 0;
}

.header-logo-right {
  margin-left: 0;
}

.header-logo-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
}

/* Cover logo background */
.header-logo-cover {
  position: absolute;
  inset: 0;
  overflow: hidden;
  z-index: 0;
}

.header-logo-cover-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
  opacity: 0.25;
}

.header-actions {
  display: flex;
  gap: 0.5rem;
  position: relative;
  z-index: 1;
}

.btn-header-action {
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.85rem;
  transition: all 0.2s;
  min-width: 110px;
  text-align: center;
}

.btn-report-incident {
  background: var(--warning-orange);
  color: white;
}

.btn-report-incident:hover {
  background: var(--warning-dark);
}

.btn-emergency {
  background: var(--emergency-bg);
  color: white;
}

.btn-emergency:hover {
  background: var(--emergency-hover);
}

.btn-logout-icon {
  position: absolute;
  top: 35%;
  transform: translateY(-50%);
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: 50%;
  width: 54px;
  height: 54px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  z-index: 2;
}

.btn-logout-icon:hover {
  background: var(--glass-bg-strong);
}

.btn-logout-icon svg {
  width: 26px;
  height: 26px;
}

.btn-logout-left {
  left: 1rem;
}

.btn-logout-right {
  right: 1rem;
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
  background: var(--card-bg);
  border-radius: 12px;
  padding: 2rem;
  box-shadow: var(--shadow-lg);
  margin-bottom: 2rem;
}

.error-card {
  border: 2px solid var(--emergency-bg);
}

.selection-card h2,
.welcome-card h2,
.assignment-card h3,
.checklist-card h3,
.contacts-card h3,
.map-card h3 {
  margin: 0 0 1rem 0;
  color: var(--text-dark);
}

/* Contact list styles */
.contact-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

/* Incidents list styles */
.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.contact-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background: var(--bg-muted);
  border-radius: 8px;
  gap: 1rem;
}

.contact-name {
  font-weight: 500;
  color: var(--text-dark);
}

.contact-role {
  font-weight: 400;
  color: var(--text-secondary);
}

.contact-name-row {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.primary-badge {
  color: var(--warning);
  font-size: 0.9rem;
}

.contact-role-badge {
  display: inline-block;
  padding: 0.15rem 0.5rem;
  background: var(--bg-tertiary);
  color: var(--text-darker);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.contact-role-badge.emergency-role {
  background: var(--danger);
  color: var(--card-bg);
}

.contact-notes-text {
  font-size: 0.85rem;
  color: var(--text-darker);
  font-style: italic;
  margin-top: 0.25rem;
}

.primary-contact {
  border-left: 3px solid var(--warning);
  background: var(--warning-bg-lighter);
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
  background: var(--brand-primary);
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  transition: background 0.2s;
}

.contact-link:hover {
  background: var(--brand-primary-hover);
}

.contact-icon {
  width: 1rem;
  height: 1rem;
}

.instruction {
  color: var(--text-secondary);
  margin-bottom: 1rem;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: var(--text-secondary);
}

/* Welcome bar - compact horizontal layout */
.welcome-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: var(--card-bg);
  border-radius: 10px;
  padding: 0.75rem 1.25rem;
  margin-bottom: 0.5rem;
  box-shadow: var(--shadow-sm);
}

.welcome-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.welcome-name {
  font-weight: 600;
  color: var(--text-dark);
  font-size: 1rem;
}

.welcome-email {
  color: var(--text-secondary);
  font-size: 0.8rem;
}

.mode-switch {
  flex-shrink: 0;
}

.btn-mode-switch {
  background: var(--brand-gradient);
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
  box-shadow: 0 3px 10px var(--brand-shadow-lg);
}

.reauth-hint {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-style: italic;
  cursor: help;
}

.location-info {
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: var(--bg-muted);
  border-radius: 8px;
}

.location-info strong {
  font-size: 1.125rem;
  color: var(--text-dark);
}

.location-info p {
  margin: 0.5rem 0 0 0;
  color: var(--text-secondary);
}

.time-range {
  font-size: 0.9rem;
  color: var(--brand-primary);
  font-weight: 500;
}

.no-assignment {
  color: var(--text-secondary);
  font-style: italic;
}

.checked-in-status {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.5rem;
  background: var(--checked-in-bg);
  border: 2px solid var(--success-light);
  border-radius: 8px;
}

.check-icon {
  font-size: 2rem;
  color: var(--checked-in-text);
}

.checked-in-status strong {
  color: var(--checked-in-text);
  font-size: 1.125rem;
}

.check-time,
.check-method {
  margin: 0.25rem 0 0 0;
  color: var(--text-secondary);
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
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--brand-primary-hover);
  transform: translateY(-2px);
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--btn-cancel-hover);
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Add Incident Note Modal */
.add-note-form {
  padding: 0.5rem 0;
}

.note-textarea {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 6px;
  background: var(--input-bg);
  color: var(--text-primary);
  font-size: 0.9rem;
  font-family: inherit;
  resize: vertical;
  min-height: 80px;
}

.note-textarea:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}

.modal-actions .btn {
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
}

.error {
  padding: 1rem;
  background: var(--danger-bg-lighter);
  color: var(--danger);
  border-radius: 6px;
  font-size: 0.875rem;
}

/* Checklist styles */
.checklist-section {
  margin-bottom: 1.5rem;
}

.checklist-section:last-child {
  margin-bottom: 0;
}

.checklist-section-title {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 0.75rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border-light);
}

.checklist-section .empty-state.small {
  padding: 1rem;
  font-size: 0.9rem;
}

.my-jobs-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.my-job-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.75rem;
  background: var(--bg-secondary);
  border-radius: 6px;
}

.my-job-item input[type="checkbox"] {
  width: 1.1rem;
  height: 1.1rem;
  cursor: pointer;
  flex-shrink: 0;
}

.my-job-text {
  font-size: 0.95rem;
  color: var(--text-primary);
}

.my-job-text.completed {
  text-decoration: line-through;
  color: var(--text-muted);
}

.checklist-items {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.checklist-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 6px;
  transition: all 0.2s;
}

.checklist-item.item-completed {
  background: var(--checked-in-bg);
  border-color: var(--checked-in-border);
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
  color: var(--text-dark);
}

.item-text.text-completed {
  text-decoration: line-through;
  color: var(--text-light);
}

.item-context {
  font-size: 0.85rem;
  color: var(--brand-primary);
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
  color: var(--text-secondary);
}

.completion-text {
  color: var(--checked-in-text);
}

.completion-time {
  color: var(--text-muted);
}

/* Checklist grouping */
.checklist-group-selector {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 0.5rem;
  background: var(--bg-secondary);
  border-radius: 6px;
}

.checklist-group-selector label {
  font-size: 0.875rem;
  color: var(--text-secondary);
  white-space: nowrap;
}

.checklist-group-selector select {
  flex: 1;
  min-width: 0;
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background: var(--card-bg);
  color: var(--text-dark);
  font-size: 0.875rem;
}

.checklist-groups {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checklist-group {
  background: var(--card-bg);
  border: 1px solid var(--border-light);
  border-radius: 8px;
  overflow: hidden;
}

.checklist-group-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 0.875rem 1rem;
  background: var(--bg-secondary);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 0.95rem;
  font-weight: 500;
  color: var(--text-dark);
  transition: background 0.2s;
}

.checklist-group-header:hover {
  background: var(--bg-tertiary);
}

.checklist-group-header.expanded {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.checklist-group-header.all-complete {
  background: var(--checked-in-bg);
}

.checklist-group-header.all-complete .group-title {
  color: var(--checked-in-text);
}

.group-title {
  flex: 1;
  min-width: 0;
  word-wrap: break-word;
  overflow-wrap: break-word;
}

.group-status {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-shrink: 0;
}

.group-count {
  font-size: 0.85rem;
  color: var(--text-secondary);
  background: var(--bg-muted);
  padding: 0.2rem 0.5rem;
  border-radius: 10px;
}

.checklist-group-header.all-complete .group-count {
  background: var(--checked-in-border);
  color: var(--checked-in-text);
}

.group-expand-icon {
  font-size: 1.1rem;
  color: var(--text-muted);
}

.checklist-group-items {
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  border-top: 1px solid var(--border-light);
}

.checklist-group-items .checklist-item {
  padding: 0.5rem 0.75rem;
}

.empty-state {
  text-align: center;
  padding: 1.5rem;
  color: var(--text-secondary);
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
  background: var(--btn-cancel-bg);
  color: var(--text-secondary);
  font-weight: 500;
}

.location-status.active {
  background: var(--success-bg-light);
  color: var(--checked-in-text);
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
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
  overflow: hidden;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.active {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--brand-primary);
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

.accordion-content.map-content {
  padding: 0 !important;
  margin: 0;
  border: none;
  box-sizing: border-box;
}

.map-wrapper {
  position: relative;
  width: 100%;
  display: block;
  box-sizing: border-box;
}

.map-content :deep(.map-container) {
  width: 100% !important;
  height: calc(100vh - 350px);
  min-height: 300px;
  max-height: 500px;
  border-radius: 0 0 12px 12px !important;
  margin: 0 !important;
  padding: 0 !important;
  display: block;
  box-sizing: border-box;
}

.map-content :deep(.leaflet-container) {
  border-radius: 0 0 12px 12px;
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
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 10px;
  overflow: hidden;
}

.checkpoint-accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1rem 1.25rem;
  background: var(--bg-secondary);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
  gap: 0.5rem;
}

.checkpoint-accordion-header:hover {
  background: var(--bg-hover);
}

.checkpoint-accordion-header.active {
  background: var(--bg-tertiary);
  border-bottom: 1px solid var(--border-light);
}

.checkpoint-accordion-header.checked-in {
  background: var(--checked-in-bg);
}

.checkpoint-accordion-header.checked-in.active {
  background: var(--success-bg-light);
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
  color: var(--checked-in-text);
  font-weight: bold;
  flex-shrink: 0;
}

.checkpoint-name {
  color: var(--text-dark);
}

.checkpoint-time-badge {
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--brand-primary);
  background: var(--bg-tertiary);
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
  white-space: nowrap;
}

.checkpoint-accordion-header.checked-in .checkpoint-time-badge {
  background: var(--success-bg);
  color: var(--success);
}

.checkpoint-description-preview {
  font-size: 0.85rem;
  font-weight: 400;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.checkpoint-accordion-content {
  padding: 1rem 1.25rem;
  background: var(--card-bg);
}

.my-checkin-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  margin-top: 1rem;
  margin-bottom: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--border-light);
}

.checkpoint-area-contacts {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--bg-muted);
  border-radius: 8px;
}

.checkpoint-area-contacts .contact-list {
  margin-top: 0.5rem;
}

.checkpoint-area-contacts .contact-item {
  background: var(--card-bg);
}

/* Checkpoint notes styles */
.checkpoint-notes {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--bg-muted);
  border-radius: 8px;
}

.checkpoint-notes-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

.checkpoint-note-item {
  background: var(--card-bg);
  border-radius: 6px;
  padding: 0.75rem;
  border-left: 3px solid var(--text-muted);
}

.checkpoint-note-item.emergency {
  border-left-color: var(--danger);
  background: rgba(239, 68, 68, 0.1);
}

.checkpoint-note-item.urgent {
  border-left-color: var(--warning);
  background: rgba(245, 158, 11, 0.1);
}

.checkpoint-note-item.high {
  border-left-color: #f97316;
}

.checkpoint-note-item.normal {
  border-left-color: var(--accent-primary);
}

.checkpoint-note-item.low {
  border-left-color: var(--text-muted);
}

.checkpoint-note-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.checkpoint-note-header .pin-icon {
  font-size: 0.85rem;
}

.checkpoint-note-header .priority-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.checkpoint-note-header .priority-indicator.emergency {
  background: var(--danger);
}

.checkpoint-note-header .priority-indicator.urgent {
  background: var(--warning);
}

.checkpoint-note-header .priority-indicator.high {
  background: #f97316;
}

.checkpoint-note-header .priority-indicator.normal {
  background: var(--accent-primary);
}

.checkpoint-note-header .priority-indicator.low {
  background: var(--text-muted);
}

.checkpoint-note-title {
  font-size: 0.9rem;
  color: var(--text-dark);
  flex: 1;
}

.checkpoint-note-header .priority-badge {
  font-size: 0.7rem;
  padding: 0.15rem 0.4rem;
  border-radius: 4px;
  font-weight: 500;
  text-transform: uppercase;
}

.checkpoint-note-header .priority-badge.emergency {
  background: var(--danger);
  color: white;
}

.checkpoint-note-header .priority-badge.urgent {
  background: var(--warning);
  color: white;
}

.checkpoint-note-header .priority-badge.high {
  background: #f97316;
  color: white;
}

.checkpoint-note-header .priority-badge.normal {
  background: var(--bg-secondary);
  color: var(--text-secondary);
}

.checkpoint-note-header .priority-badge.low {
  background: var(--bg-secondary);
  color: var(--text-muted);
}

.checkpoint-note-content {
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.4;
  white-space: pre-wrap;
}

/* Assignment list styles */
.assignments-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.assignment-item {
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
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
  color: var(--text-dark);
}

.assignment-description {
  color: var(--text-secondary);
  font-size: 0.9rem;
  margin: 0 0 0.5rem 0;
}

.area-badge {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  background: var(--brand-primary);
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

/* Area filter pills */
.area-filter-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 0.5rem;
  margin-bottom: 0.5rem;
}

.area-filter-pill {
  padding: 0.4rem 0.8rem;
  border: 2px solid var(--border-light);
  border-radius: 20px;
  background: var(--card-bg);
  color: var(--text-secondary);
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.area-filter-pill:hover {
  border-color: var(--brand-primary);
}

.area-filter-pill.selected {
  color: white;
  border-color: transparent;
}

/* Area Lead Marshals Section Styles */
.area-lead-marshals-content {
  padding: 0.75rem;
}

.area-lead-marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.area-lead-marshal-item {
  background: var(--card-bg);
  border: 1px solid var(--border-light);
  border-radius: 8px;
  overflow: hidden;
}

.area-lead-marshal-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
  transition: background 0.2s;
}

.area-lead-marshal-header:hover {
  background: var(--bg-secondary);
}

.area-lead-marshal-header.active {
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-light);
}

.area-lead-marshal-header.is-checked-in {
  background: var(--success-bg-lighter, #f0fdf4);
}

.area-lead-marshal-header.is-checked-in:hover {
  background: var(--success-bg-light);
}

.area-lead-marshal-header .marshal-header-info {
  flex: 1;
  min-width: 0;
}

.area-lead-marshal-header .marshal-name-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.area-lead-marshal-header .marshal-name {
  font-weight: 600;
  color: var(--text-dark);
}

.area-lead-marshal-header .check-status-badge {
  width: 1.25rem;
  height: 1.25rem;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  background: var(--danger-bg-light);
  color: var(--danger);
}

.area-lead-marshal-header .check-status-badge.checked-in {
  background: var(--success-bg-light);
  color: var(--success-dark);
}

.area-lead-marshal-header .marshal-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 0.25rem;
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.area-lead-marshal-header .marshal-checkpoint {
  color: var(--text-secondary);
}

.area-lead-marshal-header .marshal-task-count {
  padding: 0.1rem 0.4rem;
  background: var(--info-bg);
  color: var(--info-blue);
  border-radius: 4px;
  font-size: 0.7rem;
  font-weight: 500;
}

.area-lead-marshal-content {
  padding: 1rem;
  background: var(--bg-secondary);
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.area-lead-marshal-content .marshal-checkin-section {
  padding: 0.75rem;
  background: var(--card-bg);
  border-radius: 6px;
}

.area-lead-marshal-content .checkin-status-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
}

.area-lead-marshal-content .checkin-label {
  font-size: 0.9rem;
  color: var(--text-dark);
}

.area-lead-marshal-content .checkin-time {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.area-lead-marshal-content .checkin-action-btn {
  font-size: 0.8rem;
  padding: 0.35rem 0.75rem;
  background: var(--brand-primary);
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s;
}

.area-lead-marshal-content .checkin-action-btn:hover:not(:disabled) {
  background: var(--brand-primary-hover);
}

.area-lead-marshal-content .checkin-action-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.area-lead-marshal-content .checkin-action-btn.undo-btn {
  background: var(--warning-dark);
}

.area-lead-marshal-content .marshal-contact-section {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.area-lead-marshal-content .contact-link {
  font-size: 0.85rem;
  color: var(--brand-primary);
  text-decoration: none;
}

.area-lead-marshal-content .contact-link:hover {
  text-decoration: underline;
}

.area-lead-marshal-content .marshal-tasks-section {
  background: var(--card-bg);
  border-radius: 6px;
  padding: 0.75rem;
}

.area-lead-marshal-content .tasks-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.area-lead-marshal-content .tasks-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.area-lead-marshal-content .task-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem;
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 6px;
}

.area-lead-marshal-content .task-item input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.area-lead-marshal-content .task-text {
  flex: 1;
  font-size: 0.9rem;
  color: var(--text-dark);
}

.area-lead-marshal-content .task-item.task-completed {
  opacity: 0.7;
}

.area-lead-marshal-content .task-item.task-completed .task-text {
  text-decoration: line-through;
  color: var(--text-muted);
}

.area-lead-marshal-content .no-tasks-message {
  font-size: 0.85rem;
  color: var(--text-muted);
  font-style: italic;
}

/* Checkpoint marshals list */
.checkpoint-marshals {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--card-bg);
  border-radius: 8px;
}

.marshals-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
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
  background: var(--bg-tertiary);
  border-radius: 16px;
  font-size: 0.85rem;
  color: var(--text-darker);
}

.marshal-tag.is-you {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
  font-weight: 500;
}

.marshal-tag.checked-in {
  background: var(--success-bg-light);
  color: var(--checked-in-text);
}

.marshal-tag.is-you.checked-in {
  background: linear-gradient(135deg, var(--brand-primary-bg) 50%, var(--success-bg-light) 50%);
}

/* Expandable marshal cards for area leads */
.marshals-list-expanded {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.marshal-card-mini {
  background: var(--bg-tertiary);
  border-radius: 8px;
  overflow: hidden;
}

.marshal-card-mini.checked-in {
  background: var(--success-bg-light);
}

.marshal-card-mini.is-you {
  border: 2px solid var(--brand-primary);
}

.marshal-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
  padding: 0.6rem 0.8rem;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 0.9rem;
  color: var(--text-darker);
}

.marshal-card-header:hover {
  background: rgba(0, 0, 0, 0.05);
}

.marshal-name-text {
  font-weight: 500;
}

.marshal-status-icons {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.expand-icon {
  font-size: 1.1rem;
  font-weight: bold;
  color: var(--text-secondary);
}

.marshal-details-panel {
  padding: 0.5rem 0.8rem 0.8rem;
  border-top: 1px solid var(--border-color);
  background: var(--card-bg);
}

.detail-row {
  display: flex;
  gap: 0.5rem;
  padding: 0.3rem 0;
  font-size: 0.85rem;
}

.detail-label {
  color: var(--text-secondary);
  min-width: 90px;
}

.status-checked-in {
  color: var(--success-color);
  font-weight: 500;
}

.status-not-checked-in {
  color: var(--text-secondary);
}

.contact-buttons-mini {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--border-color);
}

.contact-buttons-mini .btn {
  flex: 1;
  text-align: center;
}

.btn-sm {
  padding: 0.35rem 0.6rem;
  font-size: 0.8rem;
}

.btn-success {
  background: var(--success-color, #22c55e);
  color: white;
  border: 1px solid var(--success-color, #22c55e);
}

.btn-success:hover:not(:disabled) {
  background: var(--success-dark, #16a34a);
  border-color: var(--success-dark, #16a34a);
}

.btn-outline-secondary {
  background: transparent;
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
}

.btn-outline-secondary:hover:not(:disabled) {
  background: var(--bg-secondary);
}

.checkpoint-marshal-checkin {
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--border-color);
}

.checkpoint-marshal-checkin .btn {
  width: 100%;
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
  border-bottom: 2px solid var(--brand-primary);
  color: var(--text-dark);
  font-size: 1rem;
}

.contact-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.contact-detail {
  font-size: 0.85rem;
  color: var(--text-darker);
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
  color: var(--text-secondary);
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.detail-value {
  font-size: 1rem;
  color: var(--text-dark);
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
  border: 1px solid var(--border-light);
  position: relative;
}

/* Map height is controlled via the height prop on CommonMap */

/* Dynamic location section */
.dynamic-location-section {
  margin-top: 1rem;
  padding: 0.75rem;
  background: var(--brand-primary-bg);
  border-radius: 8px;
  border-left: 3px solid var(--brand-primary);
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
  background: var(--brand-primary);
  color: white;
  font-size: 0.75rem;
  font-weight: 600;
  border-radius: 4px;
  text-transform: uppercase;
}

.last-update {
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.dynamic-location-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-update-location {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  background: var(--brand-primary);
  color: white;
  padding: 0.6rem 1rem;
  border-radius: 6px;
  font-size: 0.9rem;
}

.btn-update-location:hover:not(:disabled) {
  background: var(--brand-primary-hover);
}

.btn-auto-update {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
  padding: 0.6rem 0.75rem;
  border-radius: 6px;
  font-size: 0.9rem;
}

.btn-auto-update:hover:not(:disabled) {
  background: var(--btn-cancel-hover);
}

.btn-auto-update.active {
  background: var(--checked-in-text);
  color: white;
}

.btn-auto-update.active:hover:not(:disabled) {
  background: var(--success);
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
  background: var(--modal-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: var(--z-modal, 3000);
  padding: 1rem;
}

.modal-content {
  background: var(--modal-bg);
  border-radius: 12px;
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: var(--shadow-xl);
}

/* Check-in Reminder Modal */
.check-in-reminder-modal .modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid var(--border-light);
}

.check-in-reminder-modal .modal-header.warning {
  background: var(--warning-bg);
  border-bottom: 1px solid var(--warning);
}

.check-in-reminder-modal .modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--warning-text);
}

.check-in-reminder-modal .modal-body {
  text-align: center;
  padding: 2rem 1.5rem;
}

.check-in-reminder-modal .reminder-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.check-in-reminder-modal .reminder-message {
  font-size: 1.1rem;
  color: var(--text-dark);
  margin-bottom: 0.75rem;
}

.check-in-reminder-modal .reminder-hint {
  font-size: 0.9rem;
  color: var(--text-secondary);
  margin: 0;
}

.check-in-reminder-modal .modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

.location-update-modal .modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid var(--border-light);
}

.location-update-modal .modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-dark);
}

.modal-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.modal-close:hover {
  color: var(--text-dark);
}

.modal-body {
  padding: 1.5rem;
}

.modal-description {
  margin: 0 0 1.5rem 0;
  color: var(--text-darker);
}

.update-option {
  margin-bottom: 1.5rem;
}

.option-label {
  display: block;
  font-size: 0.9rem;
  color: var(--text-darker);
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
  color: var(--text-light);
  font-style: italic;
}

.success-message {
  padding: 1rem;
  background: var(--success-bg-light);
  color: var(--success-dark);
  border-radius: 8px;
  text-align: center;
  font-weight: 500;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

/* New modal elements */
.last-update-info {
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 1rem;
  padding: 0.5rem;
  background: var(--bg-muted);
  border-radius: 4px;
}

.gps-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 1rem;
}

.gps-status.active {
  color: var(--success-dark);
}

.gps-status .status-icon {
  width: 16px;
  height: 16px;
}

.auto-update-option {
  background: var(--bg-secondary);
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
  color: var(--text-secondary);
  margin: 0.5rem 0 0 0;
  padding-left: 27px;
}

/* Map selection mode */
.map-selection-banner {
  background: var(--info-bg);
  border: none;
  border-bottom: 1px solid var(--info);
  border-radius: 0;
  padding: 0.75rem 1rem;
  margin: 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.map-selection-banner span {
  flex: 1;
  color: var(--status-open);
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

/* Wide screen layout - multi-column checkpoints */
@media (min-width: 1200px) {
  .container {
    max-width: 1400px;
  }

  .checkpoint-accordion {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 0.75rem;
  }

  /* Expanded checkpoint spans full width */
  .checkpoint-accordion-section:has(.checkpoint-accordion-header.active) {
    grid-column: 1 / -1;
  }
}

/* Extra wide screens - 3 columns */
@media (min-width: 1600px) {
  .container {
    max-width: 1800px;
  }

  .checkpoint-accordion {
    grid-template-columns: repeat(3, 1fr);
  }
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
