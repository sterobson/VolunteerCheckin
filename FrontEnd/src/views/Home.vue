<template>
  <div class="home">
    <div class="container">
      <h1>Volunteer Check-in System </h1>
      <p class="subtitle">Manage race marshals and volunteers with ease</p>

      <div class="actions">
        <router-link to="/admin/login" class="btn btn-primary">
          Admin Portal
        </router-link>
      </div>

      <div class="features">
        <div class="feature-card">
          <h3>For Admins</h3>
          <ul>
            <li>Create and manage events</li>
            <li>Define marshal locations</li>
            <li>Assign marshals to locations</li>
            <li>Real-time check-in monitoring</li>
            <li>Map view of all locations</li>
          </ul>
        </div>

        <div class="feature-card">
          <h3>For Marshals</h3>
          <ul>
            <li>View your assigned location</li>
            <li>Check in with GPS verification</li>
            <li>Access emergency contact info</li>
            <li>View all marshal locations</li>
          </ul>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
</script>

<style scoped>
.home {
  min-height: 100vh;
  padding: 2rem;
  background: var(--brand-gradient);
  color: white;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  text-align: center;
}

h1 {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.subtitle {
  font-size: 1.25rem;
  margin-bottom: 3rem;
  opacity: 0.9;
}

.actions {
  margin-bottom: 4rem;
}

.btn {
  display: inline-block;
  padding: 1rem 2rem;
  font-size: 1.125rem;
  text-decoration: none;
  border-radius: 8px;
  transition: all 0.3s;
}

.btn-primary {
  background: white;
  color: var(--brand-primary);
  font-weight: 600;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 10px 20px rgba(0, 0, 0, 0.2);
}

.features {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 2rem;
  margin-top: 3rem;
}

.feature-card {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  padding: 2rem;
  border-radius: 12px;
  text-align: left;
}

.feature-card h3 {
  margin-bottom: 1rem;
  font-size: 1.5rem;
}

.feature-card ul {
  list-style: none;
  padding: 0;
}

.feature-card li {
  padding: 0.5rem 0;
  padding-left: 1.5rem;
  position: relative;
}

.feature-card li:before {
  content: "✓";
  position: absolute;
  left: 0;
  font-weight: bold;
}
</style>
