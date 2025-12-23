<template>
  <div v-if="show" class="modal" @click.self="close">
    <div class="modal-content">
      <button @click="close" class="modal-close-btn">✕</button>
      <h2>Marshal check-in link</h2>
      <p class="instruction">Share this link with marshals so they can check in:</p>

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

      <div class="modal-actions">
        <button @click="handleClose" class="btn btn-secondary">Close</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, defineProps, defineEmits } from 'vue';

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

const close = () => {
  if (props.isDirty) {
    if (confirm('You have unsaved changes. Are you sure you want to close?')) {
      emit('close');
    }
  } else {
    emit('close');
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
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
}

.modal-content {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  max-width: 600px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
  position: relative;
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: #666;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.modal-close-btn:hover {
  background: #f0f0f0;
  color: #333;
}

h2 {
  margin: 0 0 1rem 0;
  color: #333;
}

.instruction {
  color: #666;
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
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
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
