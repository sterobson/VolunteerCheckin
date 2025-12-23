<template>
  <div class="admin-event-manage">
    <header class="header">
      <div class="header-left">
        <button @click="goBack" class="btn-back">← Back</button>
        <div v-if="event">
          <h1>{{ event.name }}</h1>
          <p class="event-date">{{ formatDate(event.eventDate) }}</p>
        </div>
      </div>
      <div class="header-actions">
        <button @click="showUploadRoute = true" class="btn btn-secondary">Upload route</button>
        <button @click="shareEvent" class="btn btn-primary">Share marshal link</button>
      </div>
    </header>

    <div class="container">
      <!-- Tabs Navigation -->
      <div class="tabs-nav">
        <button
          class="tab-button"
          :class="{ active: activeTab === 'course' }"
          @click="activeTab = 'course'"
        >
          Course
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'marshals' }"
          @click="activeTab = 'marshals'"
        >
          Marshals
        </button>
      </div>

      <!-- Course Tab -->
      <div v-if="activeTab === 'course'" class="tab-content-wrapper">
        <div class="content-grid">
          <div class="map-section">
            <MapView
              :locations="locationStatuses"
              :route="event?.route || []"
              :clickable="true"
              @map-click="handleMapClick"
              @location-click="handleLocationClick"
            />
          </div>

          <div class="sidebar">
            <div class="section">
              <CheckpointsList
                :locations="locationStatuses"
                @add-checkpoint="showAddLocation = true"
                @import-checkpoints="showImportLocations = true"
                @select-location="selectLocation"
              />
            </div>

            <div class="section">
              <AdminsList
                :admins="eventAdmins"
                @add-admin="showAddAdmin = true"
                @remove-admin="removeAdmin"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Marshals Tab -->
      <div v-if="activeTab === 'marshals'" class="tab-content-wrapper">
        <div class="marshals-tab-header">
          <h2>Marshals management</h2>
          <div class="button-group">
            <button @click="addNewMarshal" class="btn btn-primary">
              Add marshal
            </button>
            <button @click="showImportMarshals = true" class="btn btn-secondary">
              Import CSV
            </button>
          </div>
        </div>

        <div class="marshals-grid">
          <table class="marshals-table">
            <thead>
              <tr>
                <th>Name</th>
                <th class="hide-on-mobile">Email</th>
                <th class="hide-on-mobile">Phone</th>
                <th>Checkpoint</th>
                <th>Status</th>
                <th class="hide-on-mobile">Actions</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="marshal in marshals" :key="marshal.id">
                <template v-if="getMarshalAssignments(marshal.id).length > 0">
                  <tr
                    v-for="(assignment, index) in getMarshalAssignments(marshal.id)"
                    :key="`${marshal.id}-${assignment.id}`"
                    class="marshal-row"
                    @click="selectMarshal(marshal)"
                  >
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length">
                      {{ marshal.name }}
                    </td>
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile">
                      {{ marshal.email || '-' }}
                    </td>
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile">
                      {{ marshal.phoneNumber || '-' }}
                    </td>
                    <td>{{ getLocationName(assignment.locationId) }}</td>
                    <td @click.stop>
                      <select
                        :value="getAssignmentStatusValue(assignment)"
                        @change="changeAssignmentStatus(assignment, $event.target.value)"
                        class="status-select hide-on-mobile"
                        :class="getStatusClass(assignment)"
                      >
                        <option value="not-checked-in">Not checked-in</option>
                        <option value="checked-in">Checked-in</option>
                        <option value="admin-checked-in">Admin checked-in</option>
                        <option value="wrong-location">Checked-in but not at correct location</option>
                      </select>
                      <span class="status-icon show-on-mobile" :class="getStatusClass(assignment)">
                        {{ getStatusIcon(assignment) }}
                      </span>
                    </td>
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile">
                      <div class="action-buttons" @click.stop>
                        <button @click="selectMarshal(marshal)" class="btn btn-small btn-secondary">
                          Edit
                        </button>
                        <button @click="handleDeleteMarshalFromGrid(marshal)" class="btn btn-small btn-danger">
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                </template>
                <tr v-else class="marshal-row" @click="selectMarshal(marshal)">
                  <td>{{ marshal.name }}</td>
                  <td class="hide-on-mobile">{{ marshal.email || '-' }}</td>
                  <td class="hide-on-mobile">{{ marshal.phoneNumber || '-' }}</td>
                  <td style="color: #999; font-style: italic;">No checkpoint assigned</td>
                  <td>
                    <span class="hide-on-mobile">-</span>
                    <span class="status-icon show-on-mobile" style="color: #999;">-</span>
                  </td>
                  <td class="hide-on-mobile">
                    <div class="action-buttons" @click.stop>
                      <button @click="selectMarshal(marshal)" class="btn btn-small btn-secondary">
                        Edit
                      </button>
                      <button @click="handleDeleteMarshalFromGrid(marshal)" class="btn btn-small btn-danger">
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Add Location Modal -->
    <div v-if="showAddLocation" class="modal-sidebar">
      <div class="modal-sidebar-content">
        <button @click="tryCloseModal(() => closeLocationModal())" class="modal-close-btn">✕</button>
        <h2>Add location</h2>
        <p class="instruction">
          <strong>👆 Click on the map</strong> to set the location, or enter coordinates manually
        </p>

        <div v-if="locationForm.latitude !== 0 && locationForm.longitude !== 0" class="location-set-notice">
          ✓ Location set on map
        </div>

        <form @submit.prevent="handleSaveLocation">
          <div class="form-group">
            <label>Name</label>
            <input
              v-model="locationForm.name"
              type="text"
              required
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>Description</label>
            <input
              v-model="locationForm.description"
              type="text"
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>What3Words (optional)</label>
            <input
              v-model="locationForm.what3Words"
              type="text"
              class="form-input"
              placeholder="e.g. filled.count.soap or filled/count/soap"
              @input="markFormDirty"
            />
            <small v-if="locationForm.what3Words && !isValidWhat3Words(locationForm.what3Words)" class="form-error">
              Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
            </small>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>Latitude</label>
              <input
                v-model.number="locationForm.latitude"
                type="number"
                step="any"
                required
                class="form-input"
                @input="markFormDirty"
              />
            </div>

            <div class="form-group">
              <label>Longitude</label>
              <input
                v-model.number="locationForm.longitude"
                type="number"
                step="any"
                required
                class="form-input"
                @input="markFormDirty"
              />
            </div>
          </div>

          <div class="form-group">
            <label>Required marshals</label>
            <input
              v-model.number="locationForm.requiredMarshals"
              type="number"
              min="1"
              required
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="modal-actions">
            <button type="button" @click="tryCloseModal(() => closeLocationModal())" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary">Add location</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Edit Location Modal -->
    <div v-if="showEditLocation" class="modal" @click.self="tryCloseModal(() => closeEditLocationModal())">
      <div class="modal-content modal-content-large">
        <button @click="tryCloseModal(() => closeEditLocationModal())" class="modal-close-btn">✕</button>
        <h2>Edit checkpoint: {{ selectedLocation?.name }}</h2>

        <!-- Modal Tabs Navigation -->
        <div class="edit-modal-tabs-nav">
          <button
            class="edit-modal-tab-button"
            :class="{ active: editLocationTab === 'details' }"
            @click="editLocationTab = 'details'"
            type="button"
          >
            Details
          </button>
          <button
            class="edit-modal-tab-button"
            :class="{ active: editLocationTab === 'marshals' }"
            @click="editLocationTab = 'marshals'"
            type="button"
          >
            Marshals
          </button>
        </div>

        <!-- Details Tab -->
        <div v-if="editLocationTab === 'details'" class="edit-modal-tab-content">
          <form @submit.prevent="handleUpdateLocation">
            <div class="form-group">
              <label>Name</label>
              <input
                v-model="editLocationForm.name"
                type="text"
                required
                class="form-input"
                @input="markFormDirty"
              />
            </div>

            <div class="form-group">
              <label>Description (optional)</label>
              <input
                v-model="editLocationForm.description"
                type="text"
                class="form-input"
                @input="markFormDirty"
              />
            </div>

            <div class="form-group">
              <label>What3Words (optional)</label>
              <input
                v-model="editLocationForm.what3Words"
                type="text"
                class="form-input"
                placeholder="e.g. filled.count.soap or filled/count/soap"
                @input="markFormDirty"
              />
              <small v-if="editLocationForm.what3Words && !isValidWhat3Words(editLocationForm.what3Words)" class="form-error">
                Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
              </small>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Latitude</label>
                <input
                  :value="formatCoordinate(editLocationForm.latitude, 6)"
                  @input="editLocationForm.latitude = parseFloat($event.target.value) || 0; markFormDirty()"
                  type="text"
                  required
                  class="form-input"
                  :disabled="isMovingLocation"
                  placeholder="e.g., 51.505123"
                />
              </div>

              <div class="form-group">
                <label>Longitude</label>
                <input
                  :value="formatCoordinate(editLocationForm.longitude, 6)"
                  @input="editLocationForm.longitude = parseFloat($event.target.value) || 0; markFormDirty()"
                  type="text"
                  required
                  class="form-input"
                  :disabled="isMovingLocation"
                  placeholder="e.g., -0.091234"
                />
              </div>
            </div>

            <div class="form-group">
              <button
                type="button"
                @click="startMoveLocation"
                class="btn btn-secondary"
                :class="{ 'btn-primary': isMovingLocation }"
              >
                {{ isMovingLocation ? 'Click on map to set new location...' : 'Move checkpoint...' }}
              </button>
            </div>
          </form>
        </div>

        <!-- Marshals Tab -->
        <div v-if="editLocationTab === 'marshals'" class="edit-modal-tab-content">
          <div class="form-group">
            <label>Required marshals</label>
            <input
              v-model.number="editLocationForm.requiredMarshals"
              type="number"
              min="1"
              required
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <h3 style="margin-top: 0;">Assigned marshals ({{ (selectedLocation?.assignments?.length || 0) + pendingAssignments.length }})</h3>
          <div class="assignments-list">
            <div
              v-for="assignment in selectedLocation.assignments"
              :key="assignment.id"
              class="assignment-item"
              :class="{ 'checked-in': assignment.isCheckedIn, 'pending-delete': isPendingDelete(assignment.id) }"
            >
              <div class="assignment-info">
                <div style="display: flex; align-items: center; gap: 8px;">
                  <span
                    class="status-indicator"
                    :style="{ color: getAssignmentStatus(assignment).color }"
                    :title="getAssignmentStatus(assignment).text"
                  >
                    {{ getAssignmentStatus(assignment).icon }}
                  </span>
                  <strong>{{ assignment.marshalName }}</strong>
                  <span v-if="isPendingDelete(assignment.id)" class="pending-badge">Will be removed</span>
                </div>
                <span v-if="assignment.isCheckedIn" class="check-in-info">
                  {{ formatTime(assignment.checkInTime) }}
                  <span class="check-in-method">({{ assignment.checkInMethod }})</span>
                </span>
                <span class="distance-info">
                  {{ getAssignmentStatus(assignment).text }}
                </span>
              </div>
              <div class="assignment-actions">
                <button
                  @click="toggleCheckIn(assignment)"
                  class="btn btn-small"
                  :class="assignment.isCheckedIn ? 'btn-danger' : 'btn-secondary'"
                >
                  {{ assignment.isCheckedIn ? 'Undo' : 'Check in' }}
                </button>
                <button
                  @click="markAssignmentForDelete(assignment)"
                  class="btn btn-small"
                  :class="isPendingDelete(assignment.id) ? 'btn-secondary' : 'btn-danger'"
                >
                  {{ isPendingDelete(assignment.id) ? 'Undo' : 'Remove' }}
                </button>
              </div>
            </div>

            <!-- Pending assignments -->
            <div
              v-for="pending in pendingAssignments"
              :key="`pending-${pending.marshalId}`"
              class="assignment-item pending-add"
            >
              <div class="assignment-info">
                <div style="display: flex; align-items: center; gap: 8px;">
                  <span class="status-indicator" style="color: #666;">✗</span>
                  <strong>{{ pending.marshalName }}</strong>
                  <span class="pending-badge">Will be added</span>
                </div>
              </div>
              <div class="assignment-actions">
                <button
                  @click="removePendingAssignment(pending.marshalId)"
                  class="btn btn-small btn-danger"
                >
                  Remove
                </button>
              </div>
            </div>

            <!-- Assign Button at the end of list -->
            <button @click="showAssignMarshalModal = true" class="btn btn-secondary" style="width: 100%; margin-top: 1rem;">
              Assign...
            </button>
          </div>
        </div>

        <!-- Save/Delete buttons fixed at bottom for all tabs -->
        <div class="modal-actions-fixed">
          <button type="button" @click="handleUpdateLocation" class="btn btn-primary">
            Save changes
          </button>
          <button
            type="button"
            @click="handleDeleteLocation"
            class="btn btn-danger"
          >
            Delete checkpoint
          </button>
        </div>
      </div>
    </div>

    <!-- Share Link Modal -->
    <ShareLinkModal
      :show="showShareLink"
      :link="marshalLink"
      :isDirty="false"
      @close="showShareLink = false"
    />

    <!-- Add Admin Modal -->
    <AddAdminModal
      :show="showAddAdmin"
      :isDirty="formDirty"
      @close="closeAdminModal"
      @submit="handleAddAdminSubmit"
      @update:isDirty="markFormDirty"
    />

    <!-- Upload Route Modal -->
    <UploadRouteModal
      :show="showUploadRoute"
      :uploading="uploading"
      :error="uploadError"
      :isDirty="formDirty"
      @close="closeRouteModal"
      @submit="handleUploadRouteSubmit"
      @update:isDirty="markFormDirty"
    />

    <!-- Import Locations Modal -->
    <ImportLocationsModal
      :show="showImportLocations"
      :importing="importing"
      :error="importError"
      :result="importResult"
      :isDirty="formDirty"
      @close="closeImportModal"
      @submit="handleImportLocationsSubmit"
      @update:isDirty="markFormDirty"
    />

    <!-- Edit Marshal Modal -->
    <div v-if="showEditMarshal" class="modal" @click.self="tryCloseModal(closeEditMarshalModal)">
      <div class="modal-content modal-content-large">
        <button @click="tryCloseModal(closeEditMarshalModal)" class="modal-close-btn">✕</button>
        <h2>{{ selectedMarshal ? 'Edit marshal' : 'Add marshal' }}</h2>

        <!-- Modal Tabs Navigation -->
        <div v-if="selectedMarshal" class="edit-modal-tabs-nav">
          <button
            class="edit-modal-tab-button"
            :class="{ active: editMarshalTab === 'details' }"
            @click="editMarshalTab = 'details'"
            type="button"
          >
            Details
          </button>
          <button
            class="edit-modal-tab-button"
            :class="{ active: editMarshalTab === 'checkpoints' }"
            @click="editMarshalTab = 'checkpoints'"
            type="button"
          >
            Checkpoints
          </button>
        </div>

        <!-- Details Tab -->
        <div v-if="editMarshalTab === 'details' || !selectedMarshal" class="edit-modal-tab-content">
          <div class="form-group">
            <label>Name *</label>
            <input
              v-model="editMarshalForm.name"
              type="text"
              required
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>Email</label>
            <input
              v-model="editMarshalForm.email"
              type="email"
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>Phone number</label>
            <input
              v-model="editMarshalForm.phoneNumber"
              type="tel"
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>Notes</label>
            <textarea
              v-model="editMarshalForm.notes"
              class="form-input"
              rows="3"
              placeholder="e.g., Needs to leave by 11am"
              @input="markFormDirty"
            ></textarea>
          </div>
        </div>

        <!-- Checkpoints Tab -->
        <div v-if="editMarshalTab === 'checkpoints' && selectedMarshal" class="edit-modal-tab-content">
          <h3 style="margin-top: 0;">Assigned checkpoints ({{ getMarshalAssignmentCount() }})</h3>
          <div class="assignments-list">
            <!-- Existing assignments -->
            <div
              v-for="assignment in getMarshalAssignmentsForEdit()"
              :key="assignment.id"
              class="assignment-item"
              :class="{ 'checked-in': assignment.isCheckedIn, 'pending-delete': isPendingMarshalDelete(assignment.id) }"
            >
              <div class="assignment-info">
                <div style="display: flex; align-items: center; gap: 8px;">
                  <span
                    class="status-indicator"
                    :style="{ color: getMarshalAssignmentStatus(assignment).color }"
                    :title="getMarshalAssignmentStatus(assignment).text"
                  >
                    {{ getMarshalAssignmentStatus(assignment).icon }}
                  </span>
                  <strong>{{ getLocationName(assignment.locationId) }}</strong>
                  <span v-if="isPendingMarshalDelete(assignment.id)" class="pending-badge">Will be removed</span>
                </div>
                <span v-if="assignment.isCheckedIn" class="check-in-info">
                  {{ formatTime(assignment.checkInTime) }}
                  <span class="check-in-method">({{ assignment.checkInMethod }})</span>
                </span>
                <span class="distance-info">
                  {{ getMarshalAssignmentStatus(assignment).text }}
                </span>
              </div>
              <div class="assignment-actions">
                <button
                  @click="toggleMarshalCheckIn(assignment)"
                  class="btn btn-small"
                  :class="assignment.isCheckedIn ? 'btn-danger' : 'btn-secondary'"
                >
                  {{ assignment.isCheckedIn ? 'Undo' : 'Check in' }}
                </button>
                <button
                  @click="markMarshalAssignmentForDelete(assignment)"
                  class="btn btn-small"
                  :class="isPendingMarshalDelete(assignment.id) ? 'btn-secondary' : 'btn-danger'"
                >
                  {{ isPendingMarshalDelete(assignment.id) ? 'Undo' : 'Remove' }}
                </button>
              </div>
            </div>

            <!-- Pending assignments -->
            <div
              v-for="pending in pendingMarshalAssignments"
              :key="`pending-${pending.locationId}`"
              class="assignment-item pending-add"
            >
              <div class="assignment-info">
                <div style="display: flex; align-items: center; gap: 8px;">
                  <span class="status-indicator" style="color: #666;">✗</span>
                  <strong>{{ getLocationName(pending.locationId) }}</strong>
                  <span class="pending-badge">Will be added</span>
                </div>
              </div>
              <div class="assignment-actions">
                <button
                  @click="removePendingMarshalAssignment(pending.locationId)"
                  class="btn btn-small btn-danger"
                >
                  Remove
                </button>
              </div>
            </div>

            <!-- Assign button -->
            <div class="form-group" style="margin-top: 1.5rem;">
              <label>Assign to checkpoint</label>
              <select v-model="newAssignmentLocationId" class="form-input">
                <option value="">Select a checkpoint...</option>
                <option
                  v-for="location in availableMarshalLocations"
                  :key="location.id"
                  :value="location.id"
                >
                  {{ location.name }}
                </option>
              </select>
              <button
                @click="assignMarshalToLocation"
                class="btn btn-secondary"
                style="margin-top: 0.5rem; width: 100%;"
                :disabled="!newAssignmentLocationId"
              >
                Assign to checkpoint
              </button>
            </div>
          </div>
        </div>

        <!-- Save/Delete buttons fixed at bottom for all tabs -->
        <div class="modal-actions-fixed">
          <button type="button" @click="handleSaveMarshal" class="btn btn-primary">
            {{ selectedMarshal ? 'Save changes' : 'Add marshal' }}
          </button>
          <button
            v-if="selectedMarshal"
            type="button"
            @click="handleDeleteMarshal"
            class="btn btn-danger"
          >
            Delete marshal
          </button>
        </div>
      </div>
    </div>

    <!-- Import Marshals Modal -->
    <ImportMarshalsModal
      :show="showImportMarshals"
      :importing="importingMarshals"
      :error="importError"
      :result="importMarshalsResult"
      :isDirty="formDirty"
      @close="closeImportMarshalsModal"
      @submit="handleImportMarshalsSubmit"
      @update:isDirty="markFormDirty"
    />

    <!-- Marshal Assignment Conflict Modal -->
    <div v-if="showAssignmentConflict" class="modal" @click.self="showAssignmentConflict = false">
      <div class="modal-content">
        <button @click="showAssignmentConflict = false" class="modal-close-btn">✕</button>
        <h2>Marshal already assigned</h2>
        <p>
          <strong>{{ assignmentConflictData.marshalName }}</strong> is currently assigned to:
        </p>
        <ul style="margin: 1rem 0; padding-left: 1.5rem;">
          <li v-for="loc in assignmentConflictData.locations" :key="loc">{{ loc }}</li>
        </ul>
        <p>What would you like to do?</p>

        <div class="modal-actions" style="flex-direction: column; gap: 0.5rem;">
          <button @click="handleConflictChoice('move')" class="btn btn-primary" style="width: 100%;">
            Move to this checkpoint (remove from others)
          </button>
          <button @click="handleConflictChoice('both')" class="btn btn-secondary" style="width: 100%;">
            Assign to both (keep existing assignments)
          </button>
          <button @click="handleConflictChoice('cancel')" class="btn btn-secondary" style="width: 100%;">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Assign Marshal Modal -->
    <div v-if="showAssignMarshalModal" class="modal" @click.self="closeAssignMarshalModal">
      <div class="modal-content">
        <button @click="closeAssignMarshalModal" class="modal-close-btn">✕</button>
        <h2>{{ showAddNewMarshalInline ? 'Create new marshal' : `Assign marshal to ${selectedLocation?.name}` }}</h2>

        <!-- Select existing marshal - only show if not creating new -->
        <div v-if="!showAddNewMarshalInline" class="assign-marshal-modal-section">
          <h3>Select existing marshal</h3>
          <select v-model="selectedMarshalToAssign" class="form-input">
            <option value="">Choose a marshal...</option>
            <option
              v-for="marshal in availableMarshalsForAssignment"
              :key="marshal.id"
              :value="marshal.id"
            >
              {{ marshal.name }}
              <template v-if="marshal.assignedLocationIds.length > 0">
                (assigned to {{ marshal.assignedLocationIds.length }} checkpoint{{ marshal.assignedLocationIds.length > 1 ? 's' : '' }})
              </template>
            </option>
          </select>
          <button
            @click="handleAssignExistingMarshal"
            class="btn btn-primary"
            style="margin-top: 1rem; width: 100%;"
            :disabled="!selectedMarshalToAssign"
          >
            Assign selected marshal
          </button>

          <div style="text-align: center; margin: 1.5rem 0; color: #666; font-weight: 600;">OR</div>

          <button @click="showAddNewMarshalInline = true" class="btn btn-secondary" style="width: 100%;">
            Create new marshal
          </button>
        </div>

        <!-- Create new marshal form - only show when creating -->
        <div v-else class="assign-marshal-modal-section" style="background: #f5f7fa; padding: 1.5rem; border-radius: 8px;">
          <h3>Create new marshal</h3>
          <div class="form-group">
            <label>Name *</label>
            <input
              v-model="newMarshalInlineForm.name"
              type="text"
              required
              class="form-input"
            />
          </div>
          <div class="form-group">
            <label>Email</label>
            <input
              v-model="newMarshalInlineForm.email"
              type="email"
              class="form-input"
            />
          </div>
          <div class="form-group">
            <label>Phone</label>
            <input
              v-model="newMarshalInlineForm.phoneNumber"
              type="tel"
              class="form-input"
            />
          </div>
          <div class="modal-actions">
            <button @click="handleAddNewMarshalInline" class="btn btn-primary">
              Create & assign
            </button>
            <button @click="cancelAddNewMarshalInline" class="btn btn-secondary">
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Move Checkpoint Overlay -->
    <div v-if="isMovingLocation" class="move-checkpoint-overlay">
      <div class="move-checkpoint-instructions">
        <h3>Move checkpoint: {{ editLocationForm.name }}</h3>
        <p>Click on the map to set the new location for this checkpoint</p>
        <div class="move-checkpoint-actions">
          <button @click="cancelMoveLocation" class="btn btn-danger">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { checkInApi, eventAdminsApi, eventsApi, locationsApi, marshalsApi } from '../services/api';
import { startSignalRConnection, stopSignalRConnection } from '../services/signalr';
import MapView from '../components/MapView.vue';
import ShareLinkModal from '../components/event-manage/modals/ShareLinkModal.vue';
import AddAdminModal from '../components/event-manage/modals/AddAdminModal.vue';
import UploadRouteModal from '../components/event-manage/modals/UploadRouteModal.vue';
import ImportLocationsModal from '../components/event-manage/modals/ImportLocationsModal.vue';
import ImportMarshalsModal from '../components/event-manage/modals/ImportMarshalsModal.vue';
import CheckpointsList from '../components/event-manage/lists/CheckpointsList.vue';
import AdminsList from '../components/event-manage/lists/AdminsList.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

const activeTab = ref('course');
const editLocationTab = ref('details');
const editMarshalTab = ref('details');
const event = ref(null);
const locations = ref([]);
const locationStatuses = ref([]);
const selectedLocation = ref(null);
const showAddLocation = ref(false);
const showShareLink = ref(false);
const eventAdmins = ref([]);
const showAddAdmin = ref(false);
const showUploadRoute = ref(false);
const selectedGpxFile = ref(null);
const uploading = ref(false);
const uploadError = ref(null);
const showImportLocations = ref(false);
const showImportMarshals = ref(false);
const selectedCsvFile = ref(null);
const selectedMarshalsCsvFile = ref(null);
const deleteExistingLocations = ref(false);
const importing = ref(false);
const importingMarshals = ref(false);
const importError = ref(null);
const importResult = ref(null);
const importMarshalsResult = ref(null);
const showEditLocation = ref(false);
const isMovingLocation = ref(false);
const editLocationForm = ref({
  id: '',
  name: '',
  description: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  what3Words: '',
});
const selectedMarshalToAssign = ref('');
const showAddNewMarshalInline = ref(false);
const showAssignMarshalModal = ref(false);
const newMarshalInlineForm = ref({
  name: '',
  email: '',
  phoneNumber: '',
});
const showAssignmentConflict = ref(false);
const assignmentConflictData = ref({
  marshalName: '',
  locations: [],
  marshal: null,
});
const conflictResolveCallback = ref(null);
const marshals = ref([]);
const showEditMarshal = ref(false);
const selectedMarshal = ref(null);
const editMarshalForm = ref({
  id: '',
  name: '',
  email: '',
  phoneNumber: '',
  notes: '',
});
const marshalSortBy = ref('name');
const marshalSortOrder = ref('asc');
const newAssignmentLocationId = ref('');
const formDirty = ref(false);
const pendingAssignments = ref([]);
const pendingDeleteAssignments = ref([]);
const pendingMarshalAssignments = ref([]);
const pendingMarshalDeleteAssignments = ref([]);

const locationForm = ref({
  name: '',
  description: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  what3Words: '',
});

const adminForm = ref({
  email: '',
});

const marshalLink = computed(() => {
  return `${window.location.origin}/event/${route.params.eventId}`;
});

const sortedMarshals = computed(() => {
  const sorted = [...marshals.value];

  sorted.sort((a, b) => {
    let compareValue = 0;

    if (marshalSortBy.value === 'name') {
      compareValue = a.name.localeCompare(b.name);
    } else if (marshalSortBy.value === 'status') {
      compareValue = (b.isCheckedIn ? 1 : 0) - (a.isCheckedIn ? 1 : 0);
    } else if (marshalSortBy.value === 'assignments') {
      compareValue = a.assignedLocationIds.length - b.assignedLocationIds.length;
    }

    return marshalSortOrder.value === 'asc' ? compareValue : -compareValue;
  });

  return sorted;
});

const availableLocations = computed(() => {
  if (!selectedMarshal.value) return locationStatuses.value;

  return locationStatuses.value.filter(
    loc => !selectedMarshal.value.assignedLocationIds.includes(loc.id)
  );
});

const availableMarshalLocations = computed(() => {
  if (!selectedMarshal.value) return locationStatuses.value;

  const assignedLocationIds = selectedMarshal.value.assignedLocationIds || [];
  const pendingLocationIds = pendingMarshalAssignments.value.map(p => p.locationId);
  const deleteLocationIds = pendingMarshalDeleteAssignments.value;

  return locationStatuses.value.filter(
    loc => !assignedLocationIds.includes(loc.id) &&
           !pendingLocationIds.includes(loc.id) ||
           deleteLocationIds.includes(loc.id)
  );
});

const availableMarshalsForAssignment = computed(() => {
  if (!selectedLocation.value) return marshals.value;

  const assignedMarshalNames = selectedLocation.value.assignments.map(a => a.marshalName);
  const pendingMarshalNames = pendingAssignments.value.map(p => p.marshalName);
  return marshals.value.filter(
    marshal => !assignedMarshalNames.includes(marshal.name) && !pendingMarshalNames.includes(marshal.name)
  );
});

const markFormDirty = () => {
  formDirty.value = true;
};

const tryCloseModal = (closeFunction) => {
  if (formDirty.value || pendingAssignments.value.length > 0 || pendingDeleteAssignments.value.length > 0 ||
      pendingMarshalAssignments.value.length > 0 || pendingMarshalDeleteAssignments.value.length > 0) {
    if (confirm('You have unsaved changes. Are you sure you want to close without saving?')) {
      formDirty.value = false;
      pendingAssignments.value = [];
      pendingDeleteAssignments.value = [];
      pendingMarshalAssignments.value = [];
      pendingMarshalDeleteAssignments.value = [];
      closeFunction();
    }
  } else {
    closeFunction();
  }
};

const isValidWhat3Words = (what3Words) => {
  if (!what3Words || what3Words.trim() === '') {
    return true;
  }
  const pattern = /^[a-z]{1,20}[./][a-z]{1,20}[./][a-z]{1,20}$/;
  if (!pattern.test(what3Words)) {
    return false;
  }
  const hasDots = what3Words.includes('.');
  const hasSlashes = what3Words.includes('/');
  return hasDots !== hasSlashes;
};

const loadEventData = async () => {
  try {
    await eventsStore.fetchEvent(route.params.eventId);
    event.value = eventsStore.currentEvent;

    await eventsStore.fetchLocations(route.params.eventId);
    locations.value = eventsStore.locations;

    await eventsStore.fetchEventStatus(route.params.eventId);
    locationStatuses.value = eventsStore.eventStatus.locations;

    await loadEventAdmins();
    await loadMarshals();
  } catch (error) {
    console.error('Failed to load event data:', error);
  }
};

const loadMarshals = async () => {
  try {
    const response = await marshalsApi.getByEvent(route.params.eventId);
    marshals.value = response.data;
  } catch (error) {
    console.error('Failed to load marshals:', error);
  }
};

const loadEventAdmins = async () => {
  try {
    const response = await eventAdminsApi.getAdmins(route.params.eventId);
    eventAdmins.value = response.data;
  } catch (error) {
    console.error('Failed to load event admins:', error);
  }
};

const handleAddAdmin = async () => {
  try {
    await eventAdminsApi.addAdmin(route.params.eventId, adminForm.value.email);
    await loadEventAdmins();
    closeAdminModal();
  } catch (error) {
    console.error('Failed to add admin:', error);
    alert(error.response?.data?.message || 'Failed to add administrator. Please try again.');
  }
};

const handleAddAdminSubmit = async (email) => {
  try {
    await eventAdminsApi.addAdmin(route.params.eventId, email);
    await loadEventAdmins();
    closeAdminModal();
  } catch (error) {
    console.error('Failed to add admin:', error);
    alert(error.response?.data?.message || 'Failed to add administrator. Please try again.');
  }
};

const removeAdmin = async (userEmail) => {
  if (confirm(`Remove ${userEmail} as an administrator?`)) {
    try {
      await eventAdminsApi.removeAdmin(route.params.eventId, userEmail);
      await loadEventAdmins();
    } catch (error) {
      console.error('Failed to remove admin:', error);
      alert(error.response?.data?.message || 'Failed to remove administrator. Please try again.');
    }
  }
};

const closeAdminModal = () => {
  showAddAdmin.value = false;
  adminForm.value = { email: '' };
  formDirty.value = false;
};

const handleFileChange = (event) => {
  selectedGpxFile.value = event.target.files[0];
  uploadError.value = null;
};

const handleUploadRoute = async () => {
  if (!selectedGpxFile.value) {
    uploadError.value = 'Please select a GPX file';
    return;
  }

  uploading.value = true;
  uploadError.value = null;

  try {
    await eventsApi.uploadGpx(route.params.eventId, selectedGpxFile.value);
    await loadEventData();
    closeRouteModal();
  } catch (error) {
    console.error('Failed to upload route:', error);
    uploadError.value = error.response?.data?.message || 'Failed to upload route. Please try again.';
  } finally {
    uploading.value = false;
  }
};

const handleUploadRouteSubmit = async (file) => {
  if (!file) {
    uploadError.value = 'Please select a GPX file';
    return;
  }

  uploading.value = true;
  uploadError.value = null;

  try {
    await eventsApi.uploadGpx(route.params.eventId, file);
    await loadEventData();
    closeRouteModal();
  } catch (error) {
    console.error('Failed to upload route:', error);
    uploadError.value = error.response?.data?.message || 'Failed to upload route. Please try again.';
  } finally {
    uploading.value = false;
  }
};

const closeRouteModal = () => {
  showUploadRoute.value = false;
  selectedGpxFile.value = null;
  uploadError.value = null;
};

const handleCsvFileChange = (event) => {
  selectedCsvFile.value = event.target.files[0];
  importError.value = null;
  importResult.value = null;
};

const handleImportLocations = async () => {
  if (!selectedCsvFile.value) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importing.value = true;
  importError.value = null;
  importResult.value = null;

  try {
    const response = await locationsApi.importCsv(
      route.params.eventId,
      selectedCsvFile.value,
      deleteExistingLocations.value
    );
    importResult.value = response.data;
    await loadEventData();
  } catch (error) {
    console.error('Failed to import locations:', error);
    importError.value = error.response?.data?.message || 'Failed to import locations. Please try again.';
  } finally {
    importing.value = false;
  }
};

const handleImportLocationsSubmit = async ({ file, deleteExisting }) => {
  if (!file) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importing.value = true;
  importError.value = null;
  importResult.value = null;

  try {
    const response = await locationsApi.importCsv(
      route.params.eventId,
      file,
      deleteExisting
    );
    importResult.value = response.data;
    await loadEventData();
  } catch (error) {
    console.error('Failed to import locations:', error);
    importError.value = error.response?.data?.message || 'Failed to import locations. Please try again.';
  } finally {
    importing.value = false;
  }
};

const closeImportModal = () => {
  showImportLocations.value = false;
  selectedCsvFile.value = null;
  deleteExistingLocations.value = false;
  importError.value = null;
  importResult.value = null;
};

const handleMapClick = (coords) => {
  if (showAddLocation.value) {
    locationForm.value.latitude = coords.lat;
    locationForm.value.longitude = coords.lng;
    markFormDirty();
  } else if (isMovingLocation.value) {
    editLocationForm.value.latitude = coords.lat;
    editLocationForm.value.longitude = coords.lng;
    isMovingLocation.value = false;
    showEditLocation.value = true;
    markFormDirty();
  } else {
    // Check if click is within 25m of any existing checkpoint
    const nearbyCheckpoint = locationStatuses.value.find(location => {
      const distance = calculateDistance(
        coords.lat,
        coords.lng,
        location.latitude,
        location.longitude
      );
      return distance <= 25;
    });

    // If not near any checkpoint, open Add Checkpoint modal
    if (!nearbyCheckpoint) {
      locationForm.value.latitude = coords.lat;
      locationForm.value.longitude = coords.lng;
      showAddLocation.value = true;
      markFormDirty();
    }
  }
};

const handleLocationClick = (location) => {
  selectLocation(location);
};

const selectLocation = (location) => {
  selectedLocation.value = location;
  editLocationForm.value = {
    id: location.id,
    name: location.name,
    description: location.description || '',
    latitude: location.latitude,
    longitude: location.longitude,
    requiredMarshals: location.requiredMarshals,
    what3Words: location.what3Words || '',
  };
  pendingAssignments.value = [];
  pendingDeleteAssignments.value = [];
  formDirty.value = false;
  showEditLocation.value = true;
};

const handleSaveLocation = async () => {
  if (!isValidWhat3Words(locationForm.value.what3Words)) {
    alert('Invalid What3Words format. Please use word.word.word or word/word/word (lowercase letters only, 1-20 characters each)');
    return;
  }

  try {
    await eventsStore.createLocation({
      eventId: route.params.eventId,
      ...locationForm.value,
    });

    await loadEventData();
    closeLocationModal();
  } catch (error) {
    console.error('Failed to save location:', error);
    alert('Failed to save location. Please try again.');
  }
};

const closeLocationModal = () => {
  showAddLocation.value = false;
  locationForm.value = {
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
  };
  formDirty.value = false;
};

const closeEditLocationModal = () => {
  showEditLocation.value = false;
  selectedLocation.value = null;
  isMovingLocation.value = false;
  editLocationTab.value = 'details';
  showAssignMarshalModal.value = false;
  selectedMarshalToAssign.value = '';
  showAddNewMarshalInline.value = false;
  newMarshalInlineForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
  editLocationForm.value = {
    id: '',
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
  };
  pendingAssignments.value = [];
  pendingDeleteAssignments.value = [];
  formDirty.value = false;
};

const closeAssignMarshalModal = () => {
  showAssignMarshalModal.value = false;
  selectedMarshalToAssign.value = '';
  showAddNewMarshalInline.value = false;
  newMarshalInlineForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
};

const startMoveLocation = () => {
  isMovingLocation.value = true;
  showEditLocation.value = false;
  activeTab.value = 'course';
};

const cancelMoveLocation = () => {
  isMovingLocation.value = false;
  showEditLocation.value = true;
};

const handleUpdateLocation = async () => {
  if (!isValidWhat3Words(editLocationForm.value.what3Words)) {
    alert('Invalid What3Words format. Please use word.word.word or word/word/word (lowercase letters only, 1-20 characters each)');
    return;
  }

  try {
    // Update location
    await locationsApi.update(
      route.params.eventId,
      editLocationForm.value.id,
      {
        eventId: route.params.eventId,
        name: editLocationForm.value.name,
        description: editLocationForm.value.description,
        latitude: editLocationForm.value.latitude,
        longitude: editLocationForm.value.longitude,
        requiredMarshals: editLocationForm.value.requiredMarshals,
        what3Words: editLocationForm.value.what3Words || null,
      }
    );

    // Process pending deletions
    for (const assignmentId of pendingDeleteAssignments.value) {
      await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
    }

    // Process pending additions
    for (const pending of pendingAssignments.value) {
      // Check if marshal is already assigned to other checkpoints
      const marshal = marshals.value.find(m => m.id === pending.marshalId);
      if (marshal && marshal.assignedLocationIds.length > 0) {
        const otherLocations = marshal.assignedLocationIds
          .filter(id => id !== selectedLocation.value.id)
          .map(id => getLocationName(id));

        if (otherLocations.length > 0) {
          const choice = await new Promise((resolve) => {
            assignmentConflictData.value = {
              marshalName: marshal.name,
              locations: otherLocations,
              marshal: marshal,
            };
            conflictResolveCallback.value = resolve;
            showAssignmentConflict.value = true;
          });

          if (choice === 'cancel') {
            continue;
          } else if (choice === 'move') {
            for (const locationId of marshal.assignedLocationIds) {
              if (locationId !== selectedLocation.value.id) {
                const location = locationStatuses.value.find(loc => loc.id === locationId);
                if (location) {
                  const assignment = location.assignments.find(
                    a => a.marshalName === marshal.name
                  );
                  if (assignment) {
                    await eventsStore.deleteAssignment(route.params.eventId, assignment.id);
                  }
                }
              }
            }
          }
        }
      }

      await eventsStore.createAssignment({
        eventId: route.params.eventId,
        locationId: selectedLocation.value.id,
        marshalId: pending.marshalId,
        marshalName: pending.marshalName,
      });
    }

    await loadEventData();
    closeEditLocationModal();
  } catch (error) {
    console.error('Failed to update checkpoint:', error);
    alert('Failed to update checkpoint. Please try again.');
  }
};

const handleDeleteLocation = async () => {
  if (confirm(`Are you sure you want to delete "${editLocationForm.value.name}"? This will remove all marshal assignments for this checkpoint.`)) {
    try {
      await locationsApi.delete(route.params.eventId, editLocationForm.value.id);
      await loadEventData();
      closeEditLocationModal();
    } catch (error) {
      console.error('Failed to delete checkpoint:', error);
      alert('Failed to delete checkpoint. Please try again.');
    }
  }
};

const toggleCheckIn = async (assignment) => {
  try {
    await checkInApi.adminCheckIn(assignment.id);
    await loadEventData();
  } catch (error) {
    console.error('Failed to toggle check-in:', error);
    alert('Failed to toggle check-in. Please try again.');
  }
};

const markAssignmentForDelete = (assignment) => {
  const index = pendingDeleteAssignments.value.indexOf(assignment.id);
  if (index > -1) {
    pendingDeleteAssignments.value.splice(index, 1);
  } else {
    pendingDeleteAssignments.value.push(assignment.id);
  }
  markFormDirty();
};

const isPendingDelete = (assignmentId) => {
  return pendingDeleteAssignments.value.includes(assignmentId);
};

const shareEvent = () => {
  showShareLink.value = true;
};

const goBack = () => {
  router.push({ name: 'AdminDashboard' });
};

const formatDate = (dateString) => {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
};

const calculateDistance = (lat1, lon1, lat2, lon2) => {
  const R = 6371000;
  const φ1 = lat1 * Math.PI / 180;
  const φ2 = lat2 * Math.PI / 180;
  const Δφ = (lat2 - lat1) * Math.PI / 180;
  const Δλ = (lon2 - lon1) * Math.PI / 180;

  const a = Math.sin(Δφ/2) * Math.sin(Δφ/2) +
          Math.cos(φ1) * Math.cos(φ2) *
          Math.sin(Δλ/2) * Math.sin(Δλ/2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));

  return R * c;
};

const getAssignmentStatus = (assignment) => {
  if (!assignment.isCheckedIn) {
    return { icon: '✗', color: 'red', text: 'Not checked in' };
  }

  if (!assignment.checkInLatitude || !assignment.checkInLongitude ||
      !selectedLocation.value.latitude || !selectedLocation.value.longitude) {
    return { icon: '✓', color: 'green', text: 'Checked in' };
  }

  const distance = calculateDistance(
    selectedLocation.value.latitude,
    selectedLocation.value.longitude,
    assignment.checkInLatitude,
    assignment.checkInLongitude
  );

  if (distance <= 25) {
    return { icon: '✓', color: 'green', text: `Checked in (${Math.round(distance)}m away)` };
  } else {
    return { icon: '⚠', color: 'orange', text: `Checked in (${Math.round(distance)}m away - outside 25m range)` };
  }
};

const handleCheckinUpdate = (data) => {
  if (data.eventId === route.params.eventId) {
    eventsStore.updateAssignmentCheckIn(data.assignment);
    loadEventData();
  }
};

const selectMarshal = (marshal) => {
  selectedMarshal.value = marshal;
  editMarshalForm.value = {
    id: marshal.id,
    name: marshal.name,
    email: marshal.email || '',
    phoneNumber: marshal.phoneNumber || '',
    notes: marshal.notes || '',
  };
  editMarshalTab.value = 'details';
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  formDirty.value = false;
  showEditMarshal.value = true;
};

const addNewMarshal = () => {
  selectedMarshal.value = null;
  editMarshalForm.value = {
    id: '',
    name: '',
    email: '',
    phoneNumber: '',
    notes: '',
  };
  formDirty.value = false;
  showEditMarshal.value = true;
};

const toggleSortOrder = () => {
  marshalSortOrder.value = marshalSortOrder.value === 'asc' ? 'desc' : 'asc';
};

const closeEditMarshalModal = () => {
  showEditMarshal.value = false;
  selectedMarshal.value = null;
  newAssignmentLocationId.value = '';
  editMarshalTab.value = 'details';
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  editMarshalForm.value = {
    id: '',
    name: '',
    email: '',
    phoneNumber: '',
    notes: '',
  };
  formDirty.value = false;
};

const handleSaveMarshal = async () => {
  try {
    if (selectedMarshal.value) {
      // Update marshal details
      await marshalsApi.update(
        route.params.eventId,
        editMarshalForm.value.id,
        {
          name: editMarshalForm.value.name,
          email: editMarshalForm.value.email,
          phoneNumber: editMarshalForm.value.phoneNumber,
          notes: editMarshalForm.value.notes,
        }
      );

      // Process pending deletions
      for (const assignmentId of pendingMarshalDeleteAssignments.value) {
        await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
      }

      // Process pending additions
      for (const pending of pendingMarshalAssignments.value) {
        await eventsStore.createAssignment({
          eventId: route.params.eventId,
          locationId: pending.locationId,
          marshalId: selectedMarshal.value.id,
          marshalName: selectedMarshal.value.name,
        });
      }
    } else {
      await marshalsApi.create({
        eventId: route.params.eventId,
        name: editMarshalForm.value.name,
        email: editMarshalForm.value.email,
        phoneNumber: editMarshalForm.value.phoneNumber,
        notes: editMarshalForm.value.notes,
      });
    }

    await loadEventData();
    closeEditMarshalModal();
  } catch (error) {
    console.error('Failed to save marshal:', error);
    alert('Failed to save marshal. Please try again.');
  }
};

const handleDeleteMarshal = async () => {
  if (confirm(`Are you sure you want to delete "${editMarshalForm.value.name}"? This will remove all assignments for this marshal.`)) {
    try {
      await marshalsApi.delete(route.params.eventId, editMarshalForm.value.id);
      await loadEventData();
      closeEditMarshalModal();
    } catch (error) {
      console.error('Failed to delete marshal:', error);
      alert('Failed to delete marshal. Please try again.');
    }
  }
};

const assignToLocation = async () => {
  if (!newAssignmentLocationId.value || !selectedMarshal.value) return;

  try {
    await eventsStore.createAssignment({
      eventId: route.params.eventId,
      locationId: newAssignmentLocationId.value,
      marshalId: selectedMarshal.value.id,
      marshalName: selectedMarshal.value.name,
    });

    await loadEventData();
    const response = await marshalsApi.getById(route.params.eventId, selectedMarshal.value.id);
    selectedMarshal.value = response.data;
    newAssignmentLocationId.value = '';
  } catch (error) {
    console.error('Failed to assign marshal:', error);
    alert('Failed to assign marshal. Please try again.');
  }
};

const removeAssignment = async (locationId) => {
  if (!selectedMarshal.value) return;

  const location = locationStatuses.value.find(loc => loc.id === locationId);
  if (!location) return;

  const assignment = location.assignments.find(
    a => a.marshalName === selectedMarshal.value.name
  );

  if (!assignment) return;

  if (confirm(`Remove ${selectedMarshal.value.name} from ${location.name}?`)) {
    try {
      await eventsStore.deleteAssignment(route.params.eventId, assignment.id);
      await loadEventData();
      const response = await marshalsApi.getById(route.params.eventId, selectedMarshal.value.id);
      selectedMarshal.value = response.data;
    } catch (error) {
      console.error('Failed to remove assignment:', error);
      alert('Failed to remove assignment. Please try again.');
    }
  }
};

const getLocationName = (locationId) => {
  const location = locationStatuses.value.find(loc => loc.id === locationId);
  return location ? location.name : 'Unknown';
};

const formatCoordinate = (value, decimals) => {
  if (!value && value !== 0) return '';
  return parseFloat(value).toFixed(decimals);
};

// Marshal edit modal helper functions
const getMarshalAssignmentsForEdit = () => {
  if (!selectedMarshal.value) return [];

  const allAssignments = [];
  locationStatuses.value.forEach(location => {
    location.assignments.forEach(assignment => {
      const marshal = marshals.value.find(m => m.name === assignment.marshalName);
      if (marshal && marshal.id === selectedMarshal.value.id) {
        allAssignments.push({
          ...assignment,
          locationId: location.id,
          locationLatitude: location.latitude,
          locationLongitude: location.longitude,
        });
      }
    });
  });
  return allAssignments;
};

const getMarshalAssignmentCount = () => {
  if (!selectedMarshal.value) return 0;
  const currentCount = getMarshalAssignmentsForEdit().length;
  const pendingAddCount = pendingMarshalAssignments.value.length;
  const pendingDeleteCount = pendingMarshalDeleteAssignments.value.length;
  return currentCount + pendingAddCount - pendingDeleteCount;
};

const getMarshalAssignmentStatus = (assignment) => {
  if (!assignment.isCheckedIn) {
    return { icon: '✗', color: 'red', text: 'Not checked in' };
  }

  if (!assignment.checkInLatitude || !assignment.checkInLongitude ||
      !assignment.locationLatitude || !assignment.locationLongitude) {
    return { icon: '✓', color: 'green', text: 'Checked in' };
  }

  const distance = calculateDistance(
    assignment.locationLatitude,
    assignment.locationLongitude,
    assignment.checkInLatitude,
    assignment.checkInLongitude
  );

  if (distance <= 25) {
    return { icon: '✓', color: 'green', text: `Checked in (${Math.round(distance)}m away)` };
  } else {
    return { icon: '⚠', color: 'orange', text: `Checked in (${Math.round(distance)}m away - outside 25m range)` };
  }
};

const toggleMarshalCheckIn = async (assignment) => {
  try {
    await checkInApi.adminCheckIn(assignment.id);
    await loadEventData();
    // Refresh selected marshal
    if (selectedMarshal.value) {
      const response = await marshalsApi.getById(route.params.eventId, selectedMarshal.value.id);
      selectedMarshal.value = response.data;
    }
  } catch (error) {
    console.error('Failed to toggle check-in:', error);
    alert('Failed to toggle check-in. Please try again.');
  }
};

const markMarshalAssignmentForDelete = (assignment) => {
  const index = pendingMarshalDeleteAssignments.value.indexOf(assignment.id);
  if (index > -1) {
    pendingMarshalDeleteAssignments.value.splice(index, 1);
  } else {
    pendingMarshalDeleteAssignments.value.push(assignment.id);
  }
  markFormDirty();
};

const isPendingMarshalDelete = (assignmentId) => {
  return pendingMarshalDeleteAssignments.value.includes(assignmentId);
};

const assignMarshalToLocation = () => {
  if (!newAssignmentLocationId.value) return;

  const alreadyAssigned = selectedMarshal.value?.assignedLocationIds?.includes(newAssignmentLocationId.value);
  const alreadyPending = pendingMarshalAssignments.value.some(
    p => p.locationId === newAssignmentLocationId.value
  );

  if (alreadyAssigned || alreadyPending) {
    alert('Marshal is already assigned to this checkpoint.');
    return;
  }

  pendingMarshalAssignments.value.push({
    locationId: newAssignmentLocationId.value,
  });
  markFormDirty();
  newAssignmentLocationId.value = '';
};

const removePendingMarshalAssignment = (locationId) => {
  const index = pendingMarshalAssignments.value.findIndex(p => p.locationId === locationId);
  if (index > -1) {
    pendingMarshalAssignments.value.splice(index, 1);
  }
};

const handleConflictChoice = async (choice) => {
  showAssignmentConflict.value = false;

  if (conflictResolveCallback.value) {
    await conflictResolveCallback.value(choice);
    conflictResolveCallback.value = null;
  }
};

const handleAssignExistingMarshal = async () => {
  if (!selectedMarshalToAssign.value || !selectedLocation.value) return;

  const marshal = marshals.value.find(m => m.id === selectedMarshalToAssign.value);
  if (!marshal) return;

  const alreadyAssignedHere = selectedLocation.value.assignments.some(
    a => a.marshalName === marshal.name
  );
  const alreadyPending = pendingAssignments.value.some(
    p => p.marshalId === marshal.id
  );

  if (alreadyAssignedHere || alreadyPending) {
    alert(`${marshal.name} is already assigned to this checkpoint.`);
    return;
  }

  pendingAssignments.value.push({
    marshalId: marshal.id,
    marshalName: marshal.name,
  });
  markFormDirty();
  closeAssignMarshalModal();
};

const removePendingAssignment = (marshalId) => {
  const index = pendingAssignments.value.findIndex(p => p.marshalId === marshalId);
  if (index > -1) {
    pendingAssignments.value.splice(index, 1);
  }
};

const handleAddNewMarshalInline = async () => {
  if (!newMarshalInlineForm.value.name) {
    alert('Please enter a name for the marshal.');
    return;
  }

  try {
    const response = await marshalsApi.create({
      eventId: route.params.eventId,
      name: newMarshalInlineForm.value.name,
      email: newMarshalInlineForm.value.email,
      phoneNumber: newMarshalInlineForm.value.phoneNumber,
      notes: '',
    });

    const newMarshal = response.data;

    pendingAssignments.value.push({
      marshalId: newMarshal.id,
      marshalName: newMarshal.name,
    });
    markFormDirty();

    // Add to local marshals array to avoid map refresh issues
    marshals.value.push(newMarshal);
    closeAssignMarshalModal();
  } catch (error) {
    console.error('Failed to add new marshal:', error);
    alert('Failed to add new marshal. Please try again.');
  }
};

const cancelAddNewMarshalInline = () => {
  showAddNewMarshalInline.value = false;
  newMarshalInlineForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
};

const getMarshalAssignments = (marshalId) => {
  const allAssignments = [];
  locationStatuses.value.forEach(location => {
    location.assignments.forEach(assignment => {
      const marshal = marshals.value.find(m => m.name === assignment.marshalName);
      if (marshal && marshal.id === marshalId) {
        allAssignments.push({
          ...assignment,
          locationId: location.id,
          locationLatitude: location.latitude,
          locationLongitude: location.longitude,
        });
      }
    });
  });
  return allAssignments;
};

const getAssignmentStatusValue = (assignment) => {
  if (!assignment.isCheckedIn) {
    return 'not-checked-in';
  }

  if (assignment.checkInMethod === 'Admin') {
    return 'admin-checked-in';
  }

  if (assignment.checkInLatitude && assignment.checkInLongitude &&
      assignment.locationLatitude && assignment.locationLongitude) {
    const distance = calculateDistance(
      assignment.locationLatitude,
      assignment.locationLongitude,
      assignment.checkInLatitude,
      assignment.checkInLongitude
    );

    if (distance > 25) {
      return 'wrong-location';
    }
  }

  return 'checked-in';
};

const getStatusClass = (assignment) => {
  const status = getAssignmentStatusValue(assignment);
  return `status-${status}`;
};

const getStatusIcon = (assignment) => {
  if (!assignment.isCheckedIn) {
    return '✗';
  }

  if (assignment.checkInMethod === 'Admin') {
    return '✓';
  }

  if (assignment.checkInLatitude && assignment.checkInLongitude &&
      assignment.locationLatitude && assignment.locationLongitude) {
    const distance = calculateDistance(
      assignment.locationLatitude,
      assignment.locationLongitude,
      assignment.checkInLatitude,
      assignment.checkInLongitude
    );

    if (distance > 25) {
      return '⚠';
    }
  }

  return '✓';
};

const changeAssignmentStatus = async (assignment, newStatus) => {
  try {
    if (newStatus === 'not-checked-in') {
      if (assignment.isCheckedIn) {
        await checkInApi.adminCheckIn(assignment.id);
      }
    } else {
      if (!assignment.isCheckedIn) {
        await checkInApi.adminCheckIn(assignment.id);
      }
    }
    await loadEventData();
  } catch (error) {
    console.error('Failed to change assignment status:', error);
    alert('Failed to change status. Please try again.');
  }
};

const handleDeleteMarshalFromGrid = async (marshal) => {
  if (confirm(`Are you sure you want to delete "${marshal.name}"? This will remove all assignments for this marshal.`)) {
    try {
      await marshalsApi.delete(route.params.eventId, marshal.id);
      await loadEventData();
    } catch (error) {
      console.error('Failed to delete marshal:', error);
      alert('Failed to delete marshal. Please try again.');
    }
  }
};

const handleMarshalsCsvFileChange = (event) => {
  selectedMarshalsCsvFile.value = event.target.files[0];
  importError.value = null;
  importMarshalsResult.value = null;
};

const handleImportMarshals = async () => {
  if (!selectedMarshalsCsvFile.value) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importingMarshals.value = true;
  importError.value = null;
  importMarshalsResult.value = null;

  try {
    const response = await marshalsApi.importCsv(
      route.params.eventId,
      selectedMarshalsCsvFile.value
    );
    importMarshalsResult.value = response.data;
    await loadEventData();
  } catch (error) {
    console.error('Failed to import marshals:', error);
    importError.value = error.response?.data?.message || 'Failed to import marshals. Please try again.';
  } finally {
    importingMarshals.value = false;
  }
};

const handleImportMarshalsSubmit = async (file) => {
  if (!file) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importingMarshals.value = true;
  importError.value = null;
  importMarshalsResult.value = null;

  try {
    const response = await marshalsApi.importCsv(
      route.params.eventId,
      file
    );
    importMarshalsResult.value = response.data;
    await loadEventData();
  } catch (error) {
    console.error('Failed to import marshals:', error);
    importError.value = error.response?.data?.message || 'Failed to import marshals. Please try again.';
  } finally {
    importingMarshals.value = false;
  }
};

const closeImportMarshalsModal = () => {
  showImportMarshals.value = false;
  selectedMarshalsCsvFile.value = null;
  importError.value = null;
  importMarshalsResult.value = null;
};

onMounted(async () => {
  await loadEventData();
  startSignalRConnection(handleCheckinUpdate);
});

onUnmounted(() => {
  stopSignalRConnection();
});
</script>

<style scoped>
.admin-event-manage {
  min-height: 100vh;
  background: #f5f7fa;
}

.header {
  background: white;
  padding: 1.5rem 2rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.btn-back {
  background: none;
  border: none;
  color: #667eea;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 600;
  padding: 0.5rem 1rem;
}

.btn-back:hover {
  background: #f0f0f0;
  border-radius: 6px;
}

.header h1 {
  margin: 0;
  color: #333;
}

.event-date {
  margin: 0.25rem 0 0 0;
  color: #666;
  font-size: 0.875rem;
}

.container {
  padding: 2rem;
}

.content-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 2rem;
  max-width: 1400px;
  margin: 0 auto;
}

.map-section {
  background: white;
  border-radius: 12px;
  padding: 1rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  height: calc(100vh - 200px);
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.section {
  background: white;
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.section h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.locations-list,
.assignments-list,
.admins-list {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.location-item,
.assignment-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
}

.location-item:hover {
  border-color: #667eea;
}

.location-item.location-full {
  border-color: #4caf50;
  background: #f1f8f4;
}

.location-item.location-missing {
  border-color: #ff4444;
  background: #fff1f1;
}

.location-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.location-status {
  font-weight: 600;
  color: #667eea;
}

.location-assignments {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.assignment-badge {
  padding: 0.25rem 0.75rem;
  background: #e0e0e0;
  border-radius: 12px;
  font-size: 0.75rem;
  color: #666;
}

.assignment-badge.checked-in {
  background: #4caf50;
  color: white;
}

.assignment-item {
  cursor: default;
}

.assignment-item.checked-in {
  border-color: #4caf50;
  background: #f1f8f4;
}

.assignment-item.pending-delete {
  border-color: #ff4444;
  background: #fff1f1;
  opacity: 0.6;
}

.assignment-item.pending-add {
  border-color: #2196f3;
  background: #e3f2fd;
}

.pending-badge {
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  background: #ff9800;
  color: white;
  border-radius: 4px;
  font-weight: 600;
}

.assignment-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.5rem;
}

.check-in-info {
  font-size: 0.875rem;
  color: #4caf50;
}

.check-in-method {
  color: #666;
  font-size: 0.75rem;
}

.assignment-actions {
  display: flex;
  gap: 0.5rem;
}

.admin-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.admin-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.admin-role {
  font-size: 0.75rem;
  color: #667eea;
  font-weight: 600;
}

.button-group {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
}

.csv-example {
  background: #f5f7fa;
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 1.5rem;
}

.csv-example pre {
  margin: 0.5rem 0 0 0;
  font-size: 0.75rem;
  color: #333;
  overflow-x: auto;
}

.import-result {
  background: #f1f8f4;
  border: 1px solid #4caf50;
  padding: 1rem;
  border-radius: 6px;
  margin-top: 1rem;
}

.import-result p {
  margin: 0 0 0.5rem 0;
  color: #4caf50;
  font-weight: 600;
}

.import-errors {
  margin-top: 0.5rem;
}

.import-errors ul {
  margin: 0.5rem 0 0 0;
  padding-left: 1.5rem;
  font-size: 0.875rem;
  color: #c33;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-small {
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
}

.btn-primary {
  background: #667eea;
  color: white;
}

.btn-primary:hover {
  background: #5568d3;
}

.btn-secondary {
  background: #e0e0e0;
  color: #333;
}

.btn-secondary:hover {
  background: #d0d0d0;
}

.btn-danger {
  background: #ff4444;
  color: white;
}

.btn-danger:hover {
  background: #cc0000;
}

.modal {
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
  padding: 2rem;
}

.modal-content {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  position: relative;
}

.modal-content h2 {
  margin: 0 0 1rem 0;
  color: #333;
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  color: #666;
  cursor: pointer;
  padding: 0.25rem 0.5rem;
  line-height: 1;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.modal-close-btn:hover {
  background: #f0f0f0;
  color: #333;
}

.instruction {
  color: #666;
  margin-bottom: 1.5rem;
  font-size: 0.875rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  color: #333;
  font-weight: 600;
}

.form-input {
  width: 100%;
  padding: 0.75rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 1rem;
  transition: border-color 0.3s;
  box-sizing: border-box;
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
}

.form-error {
  display: block;
  color: #ff4444;
  font-size: 0.875rem;
  margin-top: 0.25rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
}

.share-link-container {
  display: flex;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.share-link-container .form-input {
  flex: 1;
}

.modal-sidebar {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 400px;
  background: white;
  box-shadow: -2px 0 10px rgba(0, 0, 0, 0.3);
  z-index: 1000;
  overflow-y: auto;
}

.modal-sidebar-content {
  padding: 2rem;
  position: relative;
}

.location-set-notice {
  background: #f1f8f4;
  color: #4caf50;
  padding: 0.5rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  font-size: 0.875rem;
  font-weight: 600;
}

.status-indicator {
  font-size: 1.2rem;
  font-weight: bold;
}

.distance-info {
  font-size: 0.75rem;
  color: #666;
}

.modal-content-large {
  max-width: 800px;
  max-height: 90vh;
  overflow-y: auto;
}

.tab-content {
  margin-top: 1rem;
}

.marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 1rem;
}

.marshal-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
}

.marshal-item:hover {
  border-color: #667eea;
}

.marshal-item.checked-in {
  border-color: #4caf50;
  background: #f1f8f4;
}

.marshal-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.marshal-assignments {
  margin-top: 0.25rem;
}

.marshal-notes {
  font-size: 0.75rem;
  color: #666;
  font-style: italic;
  margin-top: 0.5rem;
  padding: 0.5rem;
  background: #f5f7fa;
  border-radius: 4px;
}

.sort-controls {
  display: flex;
  align-items: center;
  margin-top: 1rem;
  margin-bottom: 0.5rem;
  font-size: 0.875rem;
}

.marshal-assignments-section {
  margin-top: 2rem;
  padding-top: 2rem;
  border-top: 2px solid #e0e0e0;
}

.no-assignments {
  color: #999;
  font-style: italic;
  padding: 1rem;
  text-align: center;
  background: #f5f7fa;
  border-radius: 6px;
}

.assigned-checkpoints {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.assigned-checkpoint-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background: #f5f7fa;
  border-radius: 6px;
}

.tabs-nav {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid #e0e0e0;
}

.tab-button {
  padding: 1rem 2rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 600;
  color: #666;
  transition: all 0.3s;
  margin-bottom: -2px;
}

.tab-button:hover {
  color: #667eea;
}

.tab-button.active {
  color: #667eea;
  border-bottom-color: #667eea;
}

.tab-content-wrapper {
  animation: fadeIn 0.3s;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.marshals-tab-header {
  background: white;
  padding: 1.5rem;
  border-radius: 12px;
  margin-bottom: 1.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.marshals-tab-header h2 {
  margin: 0;
  color: #333;
}

.marshals-grid {
  background: white;
  padding: 1.5rem;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  overflow-x: auto;
}

.marshals-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.marshals-table thead {
  background: #f5f7fa;
  border-bottom: 2px solid #e0e0e0;
}

.marshals-table th {
  padding: 1rem;
  text-align: left;
  font-weight: 600;
  color: #333;
}

.marshals-table td {
  padding: 1rem;
  border-bottom: 1px solid #e0e0e0;
  vertical-align: middle;
}

.marshal-row:hover {
  background: #f9fafb;
}

.status-select {
  padding: 0.5rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 0.875rem;
  cursor: pointer;
  transition: border-color 0.3s;
  min-width: 200px;
}

.status-select:focus {
  outline: none;
  border-color: #667eea;
}

.status-select.status-not-checked-in {
  background: #fff1f1;
  border-color: #ff4444;
  color: #cc0000;
}

.status-select.status-checked-in {
  background: #f1f8f4;
  border-color: #4caf50;
  color: #2e7d32;
}

.status-select.status-admin-checked-in {
  background: #e3f2fd;
  border-color: #2196f3;
  color: #1565c0;
}

.status-select.status-wrong-location {
  background: #fff3e0;
  border-color: #ff9800;
  color: #e65100;
}

.action-buttons {
  display: flex;
  gap: 0.5rem;
}

.edit-modal-tabs-nav {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  border-bottom: 2px solid #e0e0e0;
}

.edit-modal-tab-button {
  padding: 0.75rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 600;
  color: #666;
  transition: all 0.3s;
  margin-bottom: -2px;
}

.edit-modal-tab-button:hover {
  color: #667eea;
}

.edit-modal-tab-button.active {
  color: #667eea;
  border-bottom-color: #667eea;
}

.edit-modal-tab-content {
  animation: fadeIn 0.3s;
}

.assign-marshal-section {
  padding: 1rem;
  background: #f9fafb;
  border-radius: 8px;
  margin-bottom: 1.5rem;
}

.inline-add-marshal {
  border: 2px solid #e0e0e0;
}

.inline-add-marshal h4 {
  margin: 0 0 1rem 0;
  color: #333;
}

.assign-marshal-modal-section {
  margin-bottom: 1.5rem;
}

.assign-marshal-modal-section h3 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1rem;
}

.move-checkpoint-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 900;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 2rem;
  pointer-events: none;
}

.move-checkpoint-instructions {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
  max-width: 500px;
  text-align: center;
  pointer-events: all;
}

.move-checkpoint-instructions h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.move-checkpoint-instructions p {
  margin: 0 0 1.5rem 0;
  color: #666;
  font-size: 1rem;
}

.move-checkpoint-actions {
  display: flex;
  justify-content: center;
  gap: 1rem;
}

.error {
  color: #ff4444;
  background: #fff1f1;
  padding: 0.75rem;
  border-radius: 6px;
  margin-top: 1rem;
  font-size: 0.875rem;
}

/* Fixed modal actions */
.modal-actions-fixed {
  position: sticky;
  bottom: 0;
  background: white;
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
  padding: 1.5rem 0 0 0;
  border-top: 2px solid #e0e0e0;
}

.modal-content-large .modal-actions-fixed {
  margin-left: -2rem;
  margin-right: -2rem;
  padding-left: 2rem;
  padding-right: 2rem;
  padding-bottom: 1rem;
}

/* Mobile status icon */
.status-icon {
  display: none;
  font-size: 1.5rem;
  font-weight: bold;
}

.status-icon.status-not-checked-in {
  color: #cc0000;
}

.status-icon.status-checked-in {
  color: #2e7d32;
}

.status-icon.status-admin-checked-in {
  color: #1565c0;
}

.status-icon.status-wrong-location {
  color: #e65100;
}

.hide-on-mobile {
  display: table-cell;
}

.show-on-mobile {
  display: none;
}

@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .map-section {
    height: 400px;
  }

  .marshals-tab-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .marshals-table {
    font-size: 0.8rem;
  }

  .marshals-table th,
  .marshals-table td {
    padding: 0.75rem 0.5rem;
  }
}

@media (max-width: 768px) {
  .hide-on-mobile {
    display: none !important;
  }

  .show-on-mobile {
    display: inline-block !important;
  }

  .marshal-row {
    cursor: pointer;
  }

  .marshal-row:hover {
    background: #f0f0f0;
  }
}
</style>
