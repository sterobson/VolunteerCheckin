<template>
  <BaseModal
    :show="show"
    :title="`${terms.person} already assigned`"
    size="medium"
    :z-index="1100"
    @close="handleCancel"
  >
    <!-- Conflict message -->
    <p>
      <strong>{{ conflictData.marshalName }}</strong> is currently assigned to:
    </p>
    <ul class="locations-list">
      <li v-for="loc in conflictData.locations" :key="loc">{{ loc }}</li>
    </ul>
    <p>What would you like to do?</p>

    <!-- Action buttons (vertical layout) -->
    <template #actions>
      <div class="conflict-actions">
        <button @click="handleChoice('move')" class="btn btn-primary btn-full">
          Move to this {{ termsLower.checkpoint }} (remove from others)
        </button>
        <button @click="handleChoice('both')" class="btn btn-secondary btn-full">
          Assign to both (keep existing assignments)
        </button>
        <button @click="handleChoice('choose-other')" class="btn btn-secondary btn-full">
          Choose someone else
        </button>
        <button @click="handleChoice('cancel')" class="btn btn-secondary btn-full">
          Cancel
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  conflictData: {
    type: Object,
    default: () => ({ marshalName: '', locations: [] }),
  },
});

const emit = defineEmits(['close', 'choice']);

const handleChoice = (choice) => {
  emit('choice', choice);
};

const handleCancel = () => {
  emit('close');
};
</script>

<style scoped>
p {
  margin: 0 0 1rem 0;
  color: #333;
}

p strong {
  color: #007bff;
}

.locations-list {
  margin: 1rem 0;
  padding-left: 1.5rem;
  color: #333;
}

.locations-list li {
  margin-bottom: 0.5rem;
}

.conflict-actions {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: 100%;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-full {
  width: 100%;
}

.btn-primary {
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
