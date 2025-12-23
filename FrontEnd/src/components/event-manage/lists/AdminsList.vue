<template>
  <div>
    <h3>Event administrators</h3>
    <button @click="$emit('add-admin')" class="btn btn-small btn-primary">
      Add administrator
    </button>

    <div class="admins-list">
      <div
        v-for="admin in admins"
        :key="admin.userEmail"
        class="admin-item"
      >
        <div class="admin-info">
          <strong>{{ admin.userEmail }}</strong>
          <span class="admin-role">{{ admin.role }}</span>
        </div>
        <button
          v-if="admins.length > 1"
          @click="$emit('remove-admin', admin.userEmail)"
          class="btn btn-small btn-danger"
        >
          Remove
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';

defineProps({
  admins: {
    type: Array,
    required: true,
  },
});

defineEmits(['add-admin', 'remove-admin']);
</script>

<style scoped>
h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.btn-danger {
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

.admins-list {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
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
</style>
