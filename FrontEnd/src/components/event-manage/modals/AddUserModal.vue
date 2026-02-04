<template>
  <BaseModal
    :show="show"
    title="Add event user"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction text -->
    <p class="instruction">Enter the email address and select a role for the new user</p>

    <!-- Form -->
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>Email address</label>
        <input
          v-model="email"
          type="email"
          required
          class="form-input"
          placeholder="user@example.com"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Name <span class="optional-label">(optional)</span></label>
        <input
          v-model="userName"
          type="text"
          class="form-input"
          placeholder="John Smith"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Role</label>
        <!-- Role selection grid -->
        <div class="roles-grid">
          <button
            v-for="role in availableRoles"
            :key="role"
            type="button"
            class="role-card"
            :class="{ selected: role === selectedRole }"
            @click="selectRole(role)"
          >
            <span
              class="role-badge"
              :class="getRoleBadgeClass(role)"
            >
              {{ getRoleLabel(role) }}
            </span>
            <p class="role-card-description">{{ getRoleDescription(role) }}</p>
          </button>
        </div>
      </div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button
        type="button"
        @click="handleSubmit"
        class="btn btn-primary"
        :disabled="!isValid"
      >
        Add user
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import BaseModal from '../../BaseModal.vue';
import {
  getRoleLabel,
  getRoleDescription,
  getRoleBadgeClass,
  ROLE_HIERARCHY
} from '../../../composables/useEventUsers';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
  availableRoles: {
    type: Array,
    default: () => ROLE_HIERARCHY,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty']);

const email = ref('');
const userName = ref('');
const selectedRole = ref('');

// Validation
const isValid = computed(() => {
  return email.value.trim() !== '' && selectedRole.value !== '';
});

// Reset form when modal opens/closes
watch(() => props.show, (newVal) => {
  if (!newVal) {
    email.value = '';
    userName.value = '';
    selectedRole.value = '';
  } else {
    // Set default role to the lowest privilege available (Viewer if available)
    if (props.availableRoles.length > 0) {
      const viewerIndex = props.availableRoles.indexOf('EventViewer');
      selectedRole.value = viewerIndex !== -1
        ? 'EventViewer'
        : props.availableRoles[props.availableRoles.length - 1];
    }
  }
});

const selectRole = (role) => {
  selectedRole.value = role;
  handleInput();
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleSubmit = () => {
  if (!isValid.value) return;
  emit('submit', {
    email: email.value.trim(),
    role: selectedRole.value,
    name: userName.value.trim() || null
  });
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.instruction {
  color: var(--text-secondary);
  margin-bottom: 1.5rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-dark);
}

.optional-label {
  font-weight: 400;
  color: var(--text-secondary);
  font-size: 0.85em;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.roles-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
}

.role-card {
  padding: 0.75rem;
  background: var(--bg-tertiary);
  border: 2px solid var(--border-light);
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.2s, background-color 0.2s;
  text-align: left;
  font-family: inherit;
}

.role-card:hover {
  background: var(--bg-hover);
}

.role-card.selected {
  border-color: var(--accent-primary);
  background: var(--bg-active);
}

.role-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.5rem;
  border-radius: 9999px;
  font-size: 0.65rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.025em;
  color: white;
}

.role-owner {
  background-color: #7c3aed;
}

.role-administrator {
  background-color: #2563eb;
}

.role-contributor {
  background-color: #059669;
}

.role-viewer {
  background-color: #6b7280;
}

.role-card-description {
  margin: 0.5rem 0 0 0;
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover:not(:disabled) {
  background: var(--btn-primary-hover);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

@media (max-width: 500px) {
  .roles-grid {
    grid-template-columns: 1fr;
  }
}
</style>
