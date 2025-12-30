<template>
  <div class="tab-content">
    <div class="form-group">
      <label>Name *</label>
      <input
        :value="form.name"
        @input="handleInput('name', $event.target.value)"
        type="text"
        required
        class="form-input"
      />
    </div>

    <div class="form-group">
      <label>Email</label>
      <input
        :value="form.email"
        @input="handleInput('email', $event.target.value)"
        type="email"
        class="form-input"
      />
    </div>

    <div class="form-group">
      <label>Phone number</label>
      <input
        :value="form.phoneNumber"
        @input="handleInput('phoneNumber', $event.target.value)"
        type="tel"
        class="form-input"
      />
    </div>

    <div class="form-group">
      <label>Notes</label>
      <textarea
        :value="form.notes"
        @input="handleInput('notes', $event.target.value)"
        class="form-input"
        rows="3"
        placeholder="e.g., Needs to leave by 11am"
      ></textarea>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
});

const emit = defineEmits(['update:form', 'input']);

const handleInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: value });
  emit('input');
};
</script>

<style scoped>
.tab-content {
  padding-top: 0.5rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  font-family: inherit;
}

textarea.form-input {
  resize: vertical;
}
</style>
