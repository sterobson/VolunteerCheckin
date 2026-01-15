<template>
  <div class="contact-item" :class="{ 'primary-contact': isPrimary }">
    <div class="contact-info">
      <div class="contact-name-row">
        <span v-if="isPrimary" class="primary-badge">&#9733;</span>
        <span class="contact-name">{{ name }}</span>
        <span v-if="allRoles.length > 0" class="contact-role-badge" :class="{ 'emergency-role': isEmergencyRole }">{{ formattedRole }}</span>
      </div>
      <div v-if="notes" class="contact-notes-text">{{ notes }}</div>
      <div v-if="phone" class="contact-detail">{{ phone }}</div>
      <div v-if="email" class="contact-detail">{{ email }}</div>
    </div>
    <div class="contact-actions">
      <a v-if="phone" :href="`tel:${phone}`" class="contact-link">
        <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z"/>
        </svg>
        Call
      </a>
      <a v-if="phone" :href="`sms:${phone}`" class="contact-link">
        <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 12H6v-2h12v2zm0-3H6V9h12v2zm0-3H6V6h12v2z"/>
        </svg>
        Text
      </a>
      <a v-if="email" :href="`mailto:${email}`" class="contact-link">
        <svg class="contact-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z"/>
        </svg>
        Email
      </a>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps } from 'vue';

const props = defineProps({
  name: {
    type: String,
    required: true,
  },
  role: {
    type: String,
    default: '',
  },
  roles: {
    type: Array,
    default: () => [],
  },
  phone: {
    type: String,
    default: '',
  },
  email: {
    type: String,
    default: '',
  },
  notes: {
    type: String,
    default: '',
  },
  isPrimary: {
    type: Boolean,
    default: false,
  },
});

// Get all roles (handles both array and legacy single role)
const allRoles = computed(() => {
  if (props.roles && props.roles.length > 0) {
    return props.roles;
  }
  if (props.role) {
    return [props.role];
  }
  return [];
});

// Emergency roles that need special highlighting
const EMERGENCY_ROLES = ['Emergency', 'Safety', 'Medical', 'First Aid'];

const isEmergencyRole = computed(() => {
  if (allRoles.value.length === 0) return false;
  return allRoles.value.some(role =>
    EMERGENCY_ROLES.some(r => role.toLowerCase().includes(r.toLowerCase()))
  );
});

const formatSingleRole = (role) => {
  if (!role) return '';
  // Convert camelCase/PascalCase to Title Case with spaces
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

const formattedRole = computed(() => {
  if (allRoles.value.length === 0) return '';
  return allRoles.value.map(formatSingleRole).join(', ');
});
</script>

<style scoped>
.contact-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background: var(--bg-muted);
  border-radius: 8px;
  gap: 1rem;
}

.contact-item.primary-contact {
  border-left: 3px solid var(--warning);
  background: var(--warning-bg-lighter);
}

.contact-info {
  flex: 1;
  min-width: 0;
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

.contact-name {
  font-weight: 500;
  color: var(--text-dark);
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

.contact-detail {
  font-size: 0.85rem;
  color: var(--text-secondary);
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

/* Responsive adjustments */
@media (max-width: 640px) {
  .contact-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.75rem;
  }

  .contact-actions {
    width: 100%;
    justify-content: flex-start;
  }
}
</style>
