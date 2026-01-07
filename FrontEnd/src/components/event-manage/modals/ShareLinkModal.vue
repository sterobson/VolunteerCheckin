<template>
  <BaseModal
    :show="show"
    title="Marshal check-in link"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction text -->
    <p class="instruction">Share this link with marshals so they can check in:</p>

    <!-- Share link container -->
    <div class="share-link-container">
      <input
        :value="link"
        readonly
        class="form-input"
        ref="linkInput"
      />
      <button @click="handleCopyLink" class="btn btn-primary">
        {{ linkCopied ? 'Copied!' : 'Copy' }}
      </button>
    </div>

    <!-- Action buttons -->
    <template #actions>
      <button @click="handleClose" class="btn btn-secondary">Close</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits } from 'vue';
import BaseModal from '../../BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  link: {
    type: String,
    required: true,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close']);

const linkCopied = ref(false);
const linkInput = ref(null);

const handleCopyLink = () => {
  if (linkInput.value) {
    linkInput.value.select();
    document.execCommand('copy');
    linkCopied.value = true;
    setTimeout(() => {
      linkCopied.value = false;
    }, 2000);
  }
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

.share-link-container {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
}

.form-input {
  flex: 1;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
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

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
