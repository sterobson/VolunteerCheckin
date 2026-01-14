<template>
  <div class="checklist-preview">
    <!-- Header with add button -->
    <div class="preview-actions">
      <p class="preview-header">
        Add new {{ termsLower.checklist }} for this {{ termsLower.area }}:
      </p>
      <button
        type="button"
        class="add-item-btn"
        @click="showAddForm = !showAddForm"
      >
        {{ showAddForm ? 'Cancel' : `Add ${termsLower.checklist}` }}
      </button>
    </div>

    <!-- Add new item form -->
    <div v-if="showAddForm" class="add-item-form">
      <input
        v-model="newItemText"
        type="text"
        :placeholder="`Enter ${termsLower.checklist} text...`"
        class="add-item-input"
        @keyup.enter="addNewItem"
      />
      <button
        type="button"
        class="save-item-btn"
        :disabled="!newItemText.trim()"
        @click="addNewItem"
      >
        Add
      </button>
    </div>

    <!-- Pending new items -->
    <div v-if="pendingNewItems.length > 0" class="pending-items">
      <div
        v-for="(item, index) in pendingNewItems"
        :key="`pending-${index}`"
        class="checklist-item pending"
      >
        <div class="item-checkbox">
          <input
            type="checkbox"
            :checked="false"
            disabled
          />
        </div>
        <div class="item-content">
          <div class="item-header">
            <div class="item-text">{{ item.text }}</div>
            <div class="item-scope">
              <span class="scope-pill new-pill" title="Will be created when saved">
                New
              </span>
              <button
                type="button"
                class="remove-item-btn"
                @click="removeNewItem(index)"
                title="Remove"
              >
                &times;
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty state when no pending items -->
    <div v-if="pendingNewItems.length === 0 && !showAddForm" class="empty-state">
      <p>No new {{ termsLower.checklists }} to add. Click the button above to create one.</p>
    </div>
  </div>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import { useTerminology } from '../composables/useTerminology';

const { termsLower } = useTerminology();

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['update:modelValue', 'change']);

// Local state
const showAddForm = ref(false);
const newItemText = ref('');
const pendingNewItems = ref([...props.modelValue]);

// Sync with v-model
watch(() => props.modelValue, (newVal) => {
  pendingNewItems.value = [...newVal];
}, { deep: true });

const addNewItem = () => {
  if (!newItemText.value.trim()) return;

  pendingNewItems.value.push({
    text: newItemText.value.trim(),
    isNew: true,
  });

  newItemText.value = '';
  showAddForm.value = false;
  emitChanges();
};

const removeNewItem = (index) => {
  pendingNewItems.value.splice(index, 1);
  emitChanges();
};

const emitChanges = () => {
  emit('update:modelValue', [...pendingNewItems.value]);
  emit('change');
};
</script>

<style scoped>
.checklist-preview {
  padding: 1rem 0;
}

.preview-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.preview-header {
  margin: 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.add-item-btn {
  padding: 0.4rem 0.75rem;
  background: var(--brand-primary);
  color: var(--card-bg);
  border: none;
  border-radius: 4px;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background-color 0.2s;
  white-space: nowrap;
  flex-shrink: 0;
}

.add-item-btn:hover {
  background: var(--brand-primary-hover);
}

.add-item-form {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: var(--brand-primary-light);
  border-radius: 6px;
}

.add-item-input {
  flex: 1;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.add-item-input:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.save-item-btn {
  padding: 0.5rem 1rem;
  background: var(--success);
  color: var(--card-bg);
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
}

.save-item-btn:disabled {
  background: var(--border-dark);
  cursor: not-allowed;
}

.save-item-btn:not(:disabled):hover {
  background: var(--success-hover);
}

.pending-items {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  color: var(--text-secondary);
}

.empty-state p {
  margin: 0;
}

.checklist-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 6px;
}

.checklist-item.pending {
  background: var(--brand-primary-light);
  border-color: var(--brand-primary);
}

.item-checkbox {
  display: flex;
  align-items: flex-start;
  padding-top: 0.25rem;
}

.item-checkbox input[type="checkbox"] {
  width: 1.2rem;
  height: 1.2rem;
  flex-shrink: 0;
  opacity: 0.5;
  cursor: not-allowed;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  flex-wrap: wrap;
}

.item-text {
  font-size: 0.95rem;
  color: var(--text-dark);
  word-wrap: break-word;
  flex: 1;
  min-width: 150px;
}

.item-scope {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.scope-pill {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  background: var(--info-bg);
  color: var(--info-blue);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
}

.scope-pill.new-pill {
  background: var(--success-bg-light);
  color: var(--success-dark);
}

.remove-item-btn {
  background: none;
  border: none;
  color: var(--danger);
  font-size: 1.2rem;
  line-height: 1;
  cursor: pointer;
  padding: 0 0.25rem;
}

.remove-item-btn:hover {
  color: var(--danger-hover);
}
</style>
