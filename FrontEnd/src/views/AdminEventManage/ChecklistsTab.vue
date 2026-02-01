<template>
  <div class="checklists-tab">
    <!-- Row 1: View toggle + Add button + status pills -->
    <div class="tab-header">
      <div class="button-group">
        <div class="view-toggle">
          <button
            class="view-toggle-btn"
            :class="{ active: currentView === 'define' }"
            @click="setView('define')"
          >
            Define
          </button>
          <button
            class="view-toggle-btn"
            :class="{ active: currentView === 'byArea' }"
            @click="setView('byArea')"
          >
            By {{ termsLower.area }}
          </button>
          <button
            class="view-toggle-btn"
            :class="{ active: currentView === 'byCheckpoint' }"
            @click="setView('byCheckpoint')"
          >
            By {{ termsLower.checkpoint }}
          </button>
          <button
            class="view-toggle-btn"
            :class="{ active: currentView === 'byPerson' }"
            @click="setView('byPerson')"
          >
            By {{ termsLower.person }}
          </button>
          <button
            class="view-toggle-btn"
            :class="{ active: currentView === 'byTask' }"
            @click="setView('byTask')"
          >
            By {{ termsLower.checklist }}
          </button>
        </div>
        <button
          v-if="currentView === 'define'"
          @click="$emit('add-checklist-item')"
          class="btn btn-primary"
        >
          Add {{ termsLower.checklist }}
        </button>
      </div>

      <!-- Status Pills for By Area View -->
      <div class="status-pills" v-if="currentView === 'byArea' && areaStatusCounts.total > 0">
        <StatusPill
          variant="neutral"
          :active="areaStatusFilter === 'all'"
          @click="areaStatusFilter = 'all'"
        >
          Total: {{ areaStatusCounts.total }}
        </StatusPill>
        <StatusPill
          v-if="areaStatusCounts.incomplete > 0"
          variant="danger"
          :active="areaStatusFilter === 'incomplete'"
          @click="areaStatusFilter = 'incomplete'"
        >
          Not started: {{ areaStatusCounts.incomplete }}
        </StatusPill>
        <StatusPill
          v-if="areaStatusCounts.partial > 0"
          variant="warning"
          :active="areaStatusFilter === 'partial'"
          @click="areaStatusFilter = 'partial'"
        >
          Partial: {{ areaStatusCounts.partial }}
        </StatusPill>
        <StatusPill
          v-if="areaStatusCounts.complete > 0"
          variant="success"
          :active="areaStatusFilter === 'complete'"
          @click="areaStatusFilter = 'complete'"
        >
          Complete: {{ areaStatusCounts.complete }}
        </StatusPill>
      </div>

      <!-- Status Pills for By Checkpoint View -->
      <div class="status-pills" v-if="currentView === 'byCheckpoint' && checkpointStatusCounts.total > 0">
        <StatusPill
          variant="neutral"
          :active="checkpointStatusFilter === 'all'"
          @click="checkpointStatusFilter = 'all'"
        >
          Total: {{ checkpointStatusCounts.total }}
        </StatusPill>
        <StatusPill
          v-if="checkpointStatusCounts.incomplete > 0"
          variant="danger"
          :active="checkpointStatusFilter === 'incomplete'"
          @click="checkpointStatusFilter = 'incomplete'"
        >
          Not started: {{ checkpointStatusCounts.incomplete }}
        </StatusPill>
        <StatusPill
          v-if="checkpointStatusCounts.partial > 0"
          variant="warning"
          :active="checkpointStatusFilter === 'partial'"
          @click="checkpointStatusFilter = 'partial'"
        >
          Partial: {{ checkpointStatusCounts.partial }}
        </StatusPill>
        <StatusPill
          v-if="checkpointStatusCounts.complete > 0"
          variant="success"
          :active="checkpointStatusFilter === 'complete'"
          @click="checkpointStatusFilter = 'complete'"
        >
          Complete: {{ checkpointStatusCounts.complete }}
        </StatusPill>
      </div>

      <!-- Status Pills for By Person View -->
      <div class="status-pills" v-if="currentView === 'byPerson' && personStatusCounts.total > 0">
        <StatusPill
          variant="neutral"
          :active="personStatusFilter === 'all'"
          @click="personStatusFilter = 'all'"
        >
          Total: {{ personStatusCounts.total }}
        </StatusPill>
        <StatusPill
          v-if="personStatusCounts.incomplete > 0"
          variant="danger"
          :active="personStatusFilter === 'incomplete'"
          @click="personStatusFilter = 'incomplete'"
        >
          Not started: {{ personStatusCounts.incomplete }}
        </StatusPill>
        <StatusPill
          v-if="personStatusCounts.partial > 0"
          variant="warning"
          :active="personStatusFilter === 'partial'"
          @click="personStatusFilter = 'partial'"
        >
          Partial: {{ personStatusCounts.partial }}
        </StatusPill>
        <StatusPill
          v-if="personStatusCounts.complete > 0"
          variant="success"
          :active="personStatusFilter === 'complete'"
          @click="personStatusFilter = 'complete'"
        >
          Complete: {{ personStatusCounts.complete }}
        </StatusPill>
      </div>

      <!-- Status Pills for By Task View -->
      <div class="status-pills" v-if="currentView === 'byTask' && taskStatusCounts.total > 0">
        <StatusPill
          variant="neutral"
          :active="taskStatusFilter === 'all'"
          @click="taskStatusFilter = 'all'"
        >
          Total: {{ taskStatusCounts.total }}
        </StatusPill>
        <StatusPill
          v-if="taskStatusCounts.incomplete > 0"
          variant="danger"
          :active="taskStatusFilter === 'incomplete'"
          @click="taskStatusFilter = 'incomplete'"
        >
          Not started: {{ taskStatusCounts.incomplete }}
        </StatusPill>
        <StatusPill
          v-if="taskStatusCounts.partial > 0"
          variant="warning"
          :active="taskStatusFilter === 'partial'"
          @click="taskStatusFilter = 'partial'"
        >
          Partial: {{ taskStatusCounts.partial }}
        </StatusPill>
        <StatusPill
          v-if="taskStatusCounts.complete > 0"
          variant="success"
          :active="taskStatusFilter === 'complete'"
          @click="taskStatusFilter = 'complete'"
        >
          Complete: {{ taskStatusCounts.complete }}
        </StatusPill>
      </div>
    </div>

    <!-- Define View (existing) -->
    <template v-if="currentView === 'define'">
      <!-- Filters -->
      <div class="filters-section">
        <div class="search-group">
          <input
            v-model="searchQuery"
            type="text"
            class="search-input"
            :placeholder="`Search ${termsLower.checklists}...`"
          />
        </div>
      </div>

      <!-- Checklist Items List -->
      <div class="checklist-items-list">
        <div v-if="filteredItems.length === 0" class="empty-state">
          <p>{{ searchQuery ? `No ${termsLower.checklists} match your search.` : `No ${termsLower.checklists} yet. Create one to get started!` }}</p>
        </div>

        <DraggableList
          v-else
          :items="sortedItems"
          item-key="itemId"
          :disabled="!!searchQuery"
          @reorder="handleReorder"
        >
          <template #item="{ element: item }">
            <div class="checklist-item-card">
              <DragHandle v-if="!searchQuery" />
              <div class="checklist-item-content">
                <div class="checklist-item-title" @click="$emit('select-checklist-item', item, 'details')">
                  <strong>{{ item.text }}</strong>
                </div>
                <ScopedAssignmentPills
                  class="checklist-item-scopes"
                  :scope-configurations="item.scopeConfigurations"
                  :areas="areas"
                  :locations="locations"
                  :marshals="marshals"
                  @click="$emit('select-checklist-item', item, 'visibility')"
                />
              </div>
            </div>
          </template>
        </DraggableList>
      </div>
    </template>

    <!-- By Person View -->
    <template v-else-if="currentView === 'byPerson'">
      <div v-if="isLoadingDetailedReport" class="loading-state">
        <p>Loading report...</p>
      </div>
      <div v-else-if="!detailedReport" class="empty-state">
        <p>No report data available.</p>
      </div>
      <template v-else>
        <!-- Filters -->
        <div class="filters-section">
          <div class="search-group">
            <input
              v-model="personSearchQuery"
              type="text"
              class="search-input"
              :placeholder="`Search ${termsLower.people}...`"
            />
          </div>
        </div>

        <div class="report-list">
          <div v-if="filteredByPerson.length === 0" class="empty-state">
            <p>{{ personSearchQuery || personStatusFilter !== 'all' ? `No ${termsLower.people} match your filters.` : `No ${termsLower.people} with ${termsLower.checklists}.` }}</p>
          </div>
          <div
            v-for="person in filteredByPerson"
            :key="person.marshalId"
            class="report-card"
          >
            <div class="report-card-header" @click="togglePersonExpanded(person.marshalId)">
              <div class="report-card-title">
                <span class="expand-icon">{{ expandedPersons.has(person.marshalId) ? '▼' : '▶' }}</span>
                <strong>{{ person.marshalName }}</strong>
              </div>
              <div class="report-card-stats">
                <span class="stat-badge" :class="getBadgeClass(person.completedTasks, person.totalTasks)">
                  {{ person.completedTasks }} / {{ person.totalTasks }}
                </span>
              </div>
            </div>
            <div v-if="expandedPersons.has(person.marshalId)" class="report-card-body">
              <div
                v-for="task in sortedTasks(person.tasks)"
                :key="task.itemId"
                class="task-row"
              >
                <span class="task-status" :class="task.isCompleted ? 'completed' : 'pending'">
                  {{ task.isCompleted ? '✓' : '○' }}
                </span>
                <span class="task-text">{{ task.text }}</span>
                <span v-if="task.isCompleted && task.completedBy" class="task-completed-by">
                  by {{ task.completedBy }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </template>
    </template>

    <!-- By Task View -->
    <template v-else-if="currentView === 'byTask'">
      <div v-if="isLoadingDetailedReport" class="loading-state">
        <p>Loading report...</p>
      </div>
      <div v-else-if="!detailedReport" class="empty-state">
        <p>No report data available.</p>
      </div>
      <template v-else>
        <!-- Filters -->
        <div class="filters-section">
          <div class="search-group">
            <input
              v-model="taskSearchQuery"
              type="text"
              class="search-input"
              :placeholder="`Search ${termsLower.checklists}...`"
            />
          </div>
        </div>

        <div class="report-list">
          <div v-if="filteredByTask.length === 0" class="empty-state">
            <p>{{ taskSearchQuery || taskStatusFilter !== 'all' ? `No ${termsLower.checklists} match your filters.` : `No ${termsLower.checklists} defined.` }}</p>
          </div>
          <div
            v-for="task in filteredByTask"
            :key="task.itemId"
            class="report-card"
          >
            <div class="report-card-header" @click="toggleTaskExpanded(task.itemId)">
              <div class="report-card-title">
                <span class="expand-icon">{{ expandedTasks.has(task.itemId) ? '▼' : '▶' }}</span>
                <strong>{{ task.text }}</strong>
              </div>
              <div class="report-card-stats">
                <span class="stat-badge" :class="getBadgeClass(task.completedCount, task.applicableCount)">
                  {{ task.completedCount }} / {{ task.applicableCount }}
                </span>
              </div>
            </div>
            <div v-if="expandedTasks.has(task.itemId)" class="report-card-body">
              <div v-if="task.marshals.length === 0" class="empty-state small">
                <p>No {{ termsLower.people }} assigned to this {{ termsLower.checklist }}.</p>
              </div>
              <div
                v-for="marshal in task.marshals"
                :key="marshal.marshalId"
                class="task-row"
              >
                <span class="task-status" :class="marshal.isCompleted ? 'completed' : 'pending'">
                  {{ marshal.isCompleted ? '✓' : '○' }}
                </span>
                <span class="task-text">{{ marshal.marshalName }}</span>
                <span v-if="marshal.isCompleted && marshal.completedBy" class="task-completed-by">
                  by {{ marshal.completedBy }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </template>
    </template>

    <!-- By Area View (Area > Person > Job) -->
    <template v-else-if="currentView === 'byArea'">
      <div v-if="isLoadingDetailedReport" class="loading-state">
        <p>Loading report...</p>
      </div>
      <div v-else-if="!detailedReport" class="empty-state">
        <p>No report data available.</p>
      </div>
      <template v-else>
        <!-- Filters -->
        <div class="filters-section">
          <div class="search-group">
            <input
              v-model="areaSearchQuery"
              type="text"
              class="search-input"
              :placeholder="`Search ${termsLower.areas}...`"
            />
          </div>
        </div>

        <div class="report-list">
          <div v-if="filteredByArea.length === 0" class="empty-state">
            <p>{{ areaSearchQuery || areaStatusFilter !== 'all' ? `No ${termsLower.areas} match your filters.` : `No ${termsLower.areas} with ${termsLower.people}.` }}</p>
          </div>
          <div
            v-for="area in filteredByArea"
            :key="area.areaId"
            class="report-card"
          >
            <div class="report-card-header" @click="toggleAreaExpanded(area.areaId)">
              <div class="report-card-title">
                <span class="expand-icon">{{ expandedAreas.has(area.areaId) ? '▼' : '▶' }}</span>
                <strong>{{ area.areaName }}</strong>
              </div>
              <div class="report-card-stats">
                <span class="stat-badge secondary">{{ area.totalPeople }} {{ area.totalPeople === 1 ? termsLower.person : termsLower.people }}</span>
                <span class="stat-badge" :class="getBadgeClass(area.completedTasks, area.totalTasks)">
                  {{ area.completedTasks }} / {{ area.totalTasks }}
                </span>
              </div>
            </div>
            <div v-if="expandedAreas.has(area.areaId)" class="report-card-body nested-body">
              <!-- Nested person cards -->
              <div
                v-for="person in area.people"
                :key="person.marshalId"
                class="nested-card"
              >
                <div class="nested-card-header" @click.stop="toggleAreaPersonExpanded(area.areaId, person.marshalId)">
                  <div class="nested-card-title">
                    <span class="expand-icon">{{ expandedAreaPersons.has(`${area.areaId}:${person.marshalId}`) ? '▼' : '▶' }}</span>
                    <strong>{{ person.marshalName }}</strong>
                  </div>
                  <div class="nested-card-stats">
                    <span class="stat-badge small" :class="getBadgeClass(person.completedTasks, person.totalTasks)">
                      {{ person.completedTasks }} / {{ person.totalTasks }}
                    </span>
                  </div>
                </div>
                <div v-if="expandedAreaPersons.has(`${area.areaId}:${person.marshalId}`)" class="nested-card-body">
                  <div
                    v-for="task in sortedTasks(person.tasks)"
                    :key="task.itemId"
                    class="task-row"
                  >
                    <span class="task-status" :class="task.isCompleted ? 'completed' : 'pending'">
                      {{ task.isCompleted ? '✓' : '○' }}
                    </span>
                    <span class="task-text">{{ task.text }}</span>
                    <span v-if="task.isCompleted && task.completedBy" class="task-completed-by">
                      by {{ task.completedBy }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </template>

    <!-- By Checkpoint View (Checkpoint > Person > Job) -->
    <template v-else-if="currentView === 'byCheckpoint'">
      <div v-if="isLoadingDetailedReport" class="loading-state">
        <p>Loading report...</p>
      </div>
      <div v-else-if="!detailedReport" class="empty-state">
        <p>No report data available.</p>
      </div>
      <template v-else>
        <!-- Filters -->
        <div class="filters-section">
          <div class="search-group">
            <input
              v-model="checkpointSearchQuery"
              type="text"
              class="search-input"
              :placeholder="`Search ${termsLower.checkpoints}...`"
            />
          </div>
        </div>

        <div class="report-list">
          <div v-if="filteredByCheckpoint.length === 0" class="empty-state">
            <p>{{ checkpointSearchQuery || checkpointStatusFilter !== 'all' ? `No ${termsLower.checkpoints} match your filters.` : `No ${termsLower.checkpoints} with ${termsLower.people}.` }}</p>
          </div>
          <div
            v-for="checkpoint in filteredByCheckpoint"
            :key="checkpoint.checkpointId"
            class="report-card"
          >
            <div class="report-card-header" @click="toggleCheckpointExpanded(checkpoint.checkpointId)">
              <div class="report-card-title">
                <span class="expand-icon">{{ expandedCheckpoints.has(checkpoint.checkpointId) ? '▼' : '▶' }}</span>
                <div class="report-card-title-text">
                  <strong>{{ checkpoint.checkpointName }}</strong>
                  <span v-if="checkpoint.checkpointDescription" class="report-card-description">{{ checkpoint.checkpointDescription }}</span>
                </div>
              </div>
              <div class="report-card-stats">
                <span class="stat-badge secondary">{{ checkpoint.totalPeople }} {{ checkpoint.totalPeople === 1 ? termsLower.person : termsLower.people }}</span>
                <span class="stat-badge" :class="getBadgeClass(checkpoint.completedTasks, checkpoint.totalTasks)">
                  {{ checkpoint.completedTasks }} / {{ checkpoint.totalTasks }}
                </span>
              </div>
            </div>
            <div v-if="expandedCheckpoints.has(checkpoint.checkpointId)" class="report-card-body nested-body">
              <!-- Nested person cards -->
              <div
                v-for="person in checkpoint.people"
                :key="person.marshalId"
                class="nested-card"
              >
                <div class="nested-card-header" @click.stop="toggleCheckpointPersonExpanded(checkpoint.checkpointId, person.marshalId)">
                  <div class="nested-card-title">
                    <span class="expand-icon">{{ expandedCheckpointPersons.has(`${checkpoint.checkpointId}:${person.marshalId}`) ? '▼' : '▶' }}</span>
                    <strong>{{ person.marshalName }}</strong>
                  </div>
                  <div class="nested-card-stats">
                    <span class="stat-badge small" :class="getBadgeClass(person.completedTasks, person.totalTasks)">
                      {{ person.completedTasks }} / {{ person.totalTasks }}
                    </span>
                  </div>
                </div>
                <div v-if="expandedCheckpointPersons.has(`${checkpoint.checkpointId}:${person.marshalId}`)" class="nested-card-body">
                  <div
                    v-for="task in sortedTasks(person.tasks)"
                    :key="task.itemId"
                    class="task-row"
                  >
                    <span class="task-status" :class="task.isCompleted ? 'completed' : 'pending'">
                      {{ task.isCompleted ? '✓' : '○' }}
                    </span>
                    <span class="task-text">{{ task.text }}</span>
                    <span v-if="task.isCompleted && task.completedBy" class="task-completed-by">
                      by {{ task.completedBy }}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </template>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';
import ScopedAssignmentPills from '../../components/common/ScopedAssignmentPills.vue';
import DraggableList from '../../components/common/DraggableList.vue';
import DragHandle from '../../components/common/DragHandle.vue';
import StatusPill from '../../components/StatusPill.vue';
import { useTerminology } from '../../composables/useTerminology';

const { termsLower } = useTerminology();

const props = defineProps({
  checklistItems: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  completionReport: {
    type: Object,
    default: null,
  },
  detailedReport: {
    type: Object,
    default: null,
  },
  isLoadingDetailedReport: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['add-checklist-item', 'select-checklist-item', 'reorder', 'load-detailed-report']);

// View state
const currentView = ref('define');
const searchQuery = ref('');
const personSearchQuery = ref('');
const taskSearchQuery = ref('');
const checkpointSearchQuery = ref('');
const areaSearchQuery = ref('');
const personStatusFilter = ref('all');
const taskStatusFilter = ref('all');
const checkpointStatusFilter = ref('all');
const areaStatusFilter = ref('all');
const expandedPersons = ref(new Set());
const expandedTasks = ref(new Set());
const expandedCheckpoints = ref(new Set());
const expandedAreas = ref(new Set());
const expandedCheckpointPersons = ref(new Set()); // For nested persons within checkpoints
const expandedAreaPersons = ref(new Set()); // For nested persons within areas

// Load detailed report when switching to report views
const setView = (view) => {
  currentView.value = view;
  if ((view === 'byPerson' || view === 'byTask' || view === 'byCheckpoint' || view === 'byArea') && !props.detailedReport && !props.isLoadingDetailedReport) {
    emit('load-detailed-report');
  }
};

// Toggle expanded state for person cards
const togglePersonExpanded = (marshalId) => {
  if (expandedPersons.value.has(marshalId)) {
    expandedPersons.value.delete(marshalId);
  } else {
    // Close all others first (accordion behavior)
    expandedPersons.value.clear();
    expandedPersons.value.add(marshalId);
  }
  // Trigger reactivity
  expandedPersons.value = new Set(expandedPersons.value);
};

// Toggle expanded state for task cards
const toggleTaskExpanded = (itemId) => {
  if (expandedTasks.value.has(itemId)) {
    expandedTasks.value.delete(itemId);
  } else {
    // Close all others first (accordion behavior)
    expandedTasks.value.clear();
    expandedTasks.value.add(itemId);
  }
  // Trigger reactivity
  expandedTasks.value = new Set(expandedTasks.value);
};

// Toggle expanded state for checkpoint cards
const toggleCheckpointExpanded = (checkpointId) => {
  if (expandedCheckpoints.value.has(checkpointId)) {
    expandedCheckpoints.value.delete(checkpointId);
    // Clear nested expansions when collapsing
    expandedCheckpointPersons.value.clear();
  } else {
    // Close all others first (accordion behavior)
    expandedCheckpoints.value.clear();
    expandedCheckpointPersons.value.clear();
    expandedCheckpoints.value.add(checkpointId);
  }
  // Trigger reactivity
  expandedCheckpoints.value = new Set(expandedCheckpoints.value);
  expandedCheckpointPersons.value = new Set(expandedCheckpointPersons.value);
};

// Toggle expanded state for area cards
const toggleAreaExpanded = (areaId) => {
  if (expandedAreas.value.has(areaId)) {
    expandedAreas.value.delete(areaId);
    // Clear nested expansions when collapsing
    expandedAreaPersons.value.clear();
  } else {
    // Close all others first (accordion behavior)
    expandedAreas.value.clear();
    expandedAreaPersons.value.clear();
    expandedAreas.value.add(areaId);
  }
  // Trigger reactivity
  expandedAreas.value = new Set(expandedAreas.value);
  expandedAreaPersons.value = new Set(expandedAreaPersons.value);
};

// Toggle expanded state for nested person within checkpoint
const toggleCheckpointPersonExpanded = (checkpointId, marshalId) => {
  const key = `${checkpointId}:${marshalId}`;
  if (expandedCheckpointPersons.value.has(key)) {
    expandedCheckpointPersons.value.delete(key);
  } else {
    // Close all others first (accordion behavior within the checkpoint)
    const keysToRemove = [...expandedCheckpointPersons.value].filter(k => k.startsWith(`${checkpointId}:`));
    keysToRemove.forEach(k => expandedCheckpointPersons.value.delete(k));
    expandedCheckpointPersons.value.add(key);
  }
  // Trigger reactivity
  expandedCheckpointPersons.value = new Set(expandedCheckpointPersons.value);
};

// Toggle expanded state for nested person within area
const toggleAreaPersonExpanded = (areaId, marshalId) => {
  const key = `${areaId}:${marshalId}`;
  if (expandedAreaPersons.value.has(key)) {
    expandedAreaPersons.value.delete(key);
  } else {
    // Close all others first (accordion behavior within the area)
    const keysToRemove = [...expandedAreaPersons.value].filter(k => k.startsWith(`${areaId}:`));
    keysToRemove.forEach(k => expandedAreaPersons.value.delete(k));
    expandedAreaPersons.value.add(key);
  }
  // Trigger reactivity
  expandedAreaPersons.value = new Set(expandedAreaPersons.value);
};

// Search filtering for Define view
const filteredItems = computed(() => {
  if (!searchQuery.value.trim()) {
    return props.checklistItems;
  }

  const searchTerms = searchQuery.value.toLowerCase().trim().split(/\s+/);

  return props.checklistItems.filter(item => {
    const searchableText = (item.text || '').toLowerCase();
    return searchTerms.every(term => searchableText.includes(term));
  });
});

const sortedItems = computed(() => {
  return [...filteredItems.value].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.text, b.text);
  });
});

// Helper to check completion status category
const getCompletionStatus = (completed, total) => {
  if (completed === 0) return 'incomplete';
  if (completed === total) return 'complete';
  return 'partial';
};

// Helper to get badge CSS class for completion status
const getBadgeClass = (completed, total) => {
  if (completed === 0) return 'not-started';
  if (completed === total) return 'complete';
  return 'partial';
};

// Status counts for By Person view
const personStatusCounts = computed(() => {
  if (!props.detailedReport?.detailedByPerson) {
    return { total: 0, incomplete: 0, partial: 0, complete: 0 };
  }
  const data = props.detailedReport.detailedByPerson;
  return {
    total: data.length,
    incomplete: data.filter(p => getCompletionStatus(p.completedTasks, p.totalTasks) === 'incomplete').length,
    partial: data.filter(p => getCompletionStatus(p.completedTasks, p.totalTasks) === 'partial').length,
    complete: data.filter(p => getCompletionStatus(p.completedTasks, p.totalTasks) === 'complete').length,
  };
});

// Status counts for By Task view
const taskStatusCounts = computed(() => {
  if (!props.detailedReport?.detailedByTask) {
    return { total: 0, incomplete: 0, partial: 0, complete: 0 };
  }
  const data = props.detailedReport.detailedByTask;
  return {
    total: data.length,
    incomplete: data.filter(t => getCompletionStatus(t.completedCount, t.applicableCount) === 'incomplete').length,
    partial: data.filter(t => getCompletionStatus(t.completedCount, t.applicableCount) === 'partial').length,
    complete: data.filter(t => getCompletionStatus(t.completedCount, t.applicableCount) === 'complete').length,
  };
});

// Status counts for By Checkpoint view
const checkpointStatusCounts = computed(() => {
  if (!props.detailedReport?.detailedByCheckpoint) {
    return { total: 0, incomplete: 0, partial: 0, complete: 0 };
  }
  const data = props.detailedReport.detailedByCheckpoint;
  return {
    total: data.length,
    incomplete: data.filter(c => getCompletionStatus(c.completedTasks, c.totalTasks) === 'incomplete').length,
    partial: data.filter(c => getCompletionStatus(c.completedTasks, c.totalTasks) === 'partial').length,
    complete: data.filter(c => getCompletionStatus(c.completedTasks, c.totalTasks) === 'complete').length,
  };
});

// Status counts for By Area view
const areaStatusCounts = computed(() => {
  if (!props.detailedReport?.detailedByArea) {
    return { total: 0, incomplete: 0, partial: 0, complete: 0 };
  }
  const data = props.detailedReport.detailedByArea;
  return {
    total: data.length,
    incomplete: data.filter(a => getCompletionStatus(a.completedTasks, a.totalTasks) === 'incomplete').length,
    partial: data.filter(a => getCompletionStatus(a.completedTasks, a.totalTasks) === 'partial').length,
    complete: data.filter(a => getCompletionStatus(a.completedTasks, a.totalTasks) === 'complete').length,
  };
});

// Search and status filtering for By Person view
const filteredByPerson = computed(() => {
  if (!props.detailedReport?.detailedByPerson) return [];

  let data = props.detailedReport.detailedByPerson;

  // Apply status filter
  if (personStatusFilter.value !== 'all') {
    data = data.filter(person => {
      const status = getCompletionStatus(person.completedTasks, person.totalTasks);
      return status === personStatusFilter.value;
    });
  }

  // Apply search filter
  if (personSearchQuery.value.trim()) {
    const searchTerms = personSearchQuery.value.toLowerCase().trim().split(/\s+/);
    data = data.filter(person => {
      const searchableText = (person.marshalName || '').toLowerCase();
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return data;
});

// Search and status filtering for By Task view
const filteredByTask = computed(() => {
  if (!props.detailedReport?.detailedByTask) return [];

  let data = props.detailedReport.detailedByTask;

  // Apply status filter
  if (taskStatusFilter.value !== 'all') {
    data = data.filter(task => {
      const status = getCompletionStatus(task.completedCount, task.applicableCount);
      return status === taskStatusFilter.value;
    });
  }

  // Apply search filter
  if (taskSearchQuery.value.trim()) {
    const searchTerms = taskSearchQuery.value.toLowerCase().trim().split(/\s+/);
    data = data.filter(task => {
      const searchableText = (task.text || '').toLowerCase();
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return data;
});

// Search and status filtering for By Checkpoint view
const filteredByCheckpoint = computed(() => {
  if (!props.detailedReport?.detailedByCheckpoint) return [];

  let data = [...props.detailedReport.detailedByCheckpoint];

  // Apply status filter
  if (checkpointStatusFilter.value !== 'all') {
    data = data.filter(checkpoint => {
      const status = getCompletionStatus(checkpoint.completedTasks, checkpoint.totalTasks);
      return status === checkpointStatusFilter.value;
    });
  }

  // Apply search filter
  if (checkpointSearchQuery.value.trim()) {
    const searchTerms = checkpointSearchQuery.value.toLowerCase().trim().split(/\s+/);
    data = data.filter(checkpoint => {
      const searchableText = (checkpoint.checkpointName || '').toLowerCase();
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  // Sort alphanumerically
  data.sort((a, b) => alphanumericCompare(a.checkpointName, b.checkpointName));

  return data;
});

// Search and status filtering for By Area view
const filteredByArea = computed(() => {
  if (!props.detailedReport?.detailedByArea) return [];

  let data = [...props.detailedReport.detailedByArea];

  // Apply status filter
  if (areaStatusFilter.value !== 'all') {
    data = data.filter(area => {
      const status = getCompletionStatus(area.completedTasks, area.totalTasks);
      return status === areaStatusFilter.value;
    });
  }

  // Apply search filter
  if (areaSearchQuery.value.trim()) {
    const searchTerms = areaSearchQuery.value.toLowerCase().trim().split(/\s+/);
    data = data.filter(area => {
      const searchableText = (area.areaName || '').toLowerCase();
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  // Sort alphanumerically
  data.sort((a, b) => alphanumericCompare(a.areaName, b.areaName));

  return data;
});

// Sort tasks by display order for person view
const sortedTasks = (tasks) => {
  return [...tasks].sort((a, b) => {
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }
    return alphanumericCompare(a.text, b.text);
  });
};

const handleReorder = ({ changes }) => {
  emit('reorder', changes);
};
</script>

<style scoped>
.checklists-tab {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.button-group {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.view-toggle {
  display: flex;
  gap: 0;
  background: var(--bg-tertiary);
  border-radius: 6px;
  padding: 0.25rem;
}

.view-toggle-btn {
  padding: 0.5rem 1rem;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  border-radius: 4px;
  transition: all 0.2s;
  white-space: nowrap;
}

.view-toggle-btn:hover {
  color: var(--text-primary);
}

.view-toggle-btn.active {
  background: var(--accent-primary);
  color: white;
}

.status-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.filters-section {
  margin-bottom: 1rem;
  padding: 0.75rem 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
}

.search-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.search-input {
  flex: 1;
  max-width: 400px;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.search-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.search-input::placeholder {
  color: var(--text-muted);
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
  font-style: italic;
}

.empty-state.small {
  padding: 1rem;
}

.loading-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
}

.checklist-items-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.checklist-item-card {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  transition: all 0.2s;
  height: 100%;
  box-sizing: border-box;
}

.checklist-item-content {
  flex: 1;
  display: flex;
  flex-direction: row;
  gap: 1rem;
  align-items: flex-start;
  min-width: 0;
}

.checklist-item-title {
  flex: 1;
  min-width: 0;
  cursor: pointer;
  transition: color 0.2s;
}

.checklist-item-title:hover strong {
  color: var(--accent-primary);
}

.checklist-item-title strong {
  font-size: 0.95rem;
  color: var(--text-primary);
  word-wrap: break-word;
}

.checklist-item-scopes {
  flex: 1;
  min-width: 0;
  justify-content: flex-end;
}

/* Report List Styles */
.report-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.report-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  overflow: hidden;
}

.report-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: background-color 0.2s;
}

.report-card-header:hover {
  background: var(--bg-tertiary);
}

.report-card-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
}

.report-card-title strong {
  font-size: 0.95rem;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.expand-icon {
  font-size: 0.7rem;
  color: var(--text-secondary);
  flex-shrink: 0;
  width: 1em;
}

.report-card-title-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.report-card-title-text strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.report-card-description {
  font-size: 0.8rem;
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-top: 0.125rem;
}

.report-card-stats {
  flex-shrink: 0;
  display: flex;
  gap: 0.5rem;
}

.stat-badge {
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: 500;
}

.stat-badge.complete {
  background: var(--success-bg, #dcfce7);
  color: var(--success-text, #166534);
}

.stat-badge.partial {
  background: var(--warning-bg, #fef3c7);
  color: var(--warning-text, #92400e);
}

.stat-badge.not-started {
  background: var(--danger-bg, #fee2e2);
  color: var(--danger-text, #991b1b);
}

.report-card-body {
  padding: 0.5rem 1rem 1rem;
  border-top: 1px solid var(--border-color);
  background: var(--bg-secondary);
}

.report-card-body.nested-body {
  padding: 0.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

/* Nested card styles for Checkpoint > Person and Area > Person */
.nested-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  overflow: hidden;
}

.nested-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  cursor: pointer;
  transition: background-color 0.2s;
}

.nested-card-header:hover {
  background: var(--bg-tertiary);
}

.nested-card-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
}

.nested-card-title strong {
  font-size: 0.9rem;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nested-card-stats {
  flex-shrink: 0;
  display: flex;
  gap: 0.5rem;
}

.nested-card-body {
  padding: 0.5rem 0.75rem;
  border-top: 1px solid var(--border-color);
  background: var(--bg-tertiary);
}

.stat-badge.secondary {
  background: var(--bg-tertiary);
  color: var(--text-secondary);
}

.stat-badge.small {
  padding: 0.15rem 0.4rem;
  font-size: 0.75rem;
}

.task-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--border-color);
}

.task-row:last-child {
  border-bottom: none;
}

.task-status {
  flex-shrink: 0;
  width: 1.25rem;
  text-align: center;
  font-size: 0.9rem;
}

.task-status.completed {
  color: var(--success-text, #166534);
}

.task-status.pending {
  color: var(--text-muted);
}

.task-text {
  flex: 1;
  font-size: 0.9rem;
  color: var(--text-primary);
}

.task-completed-by {
  flex-shrink: 0;
  font-size: 0.8rem;
  color: var(--text-muted);
  font-style: italic;
}

.btn {
  padding: 0.6rem 1.2rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--accent-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: white;
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

/* Responsive styles for narrow screens */
@media (max-width: 600px) {
  .tab-header {
    flex-direction: column;
    align-items: stretch;
  }

  .button-group {
    flex-wrap: wrap;
  }

  .view-toggle {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0.25rem;
  }

  .view-toggle-btn {
    padding: 0.4rem 0.5rem;
    font-size: 0.8rem;
    text-align: center;
  }

  .status-pills {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
