<template>
  <div class="about-view">
    <div class="content-container">
      <h1>About OnTheDay</h1>
      <p class="tagline">Confidence for your team, visibility for your event</p>

      <div class="about-section">
        <h2>For marshals and staff</h2>
        <p>
          Give your marshals and event staff the confidence to be at the correct place
          at the correct time. With clear checkpoint information, interactive maps, and
          easy check-in, everyone knows exactly where they need to be and what they need to do.
        </p>
      </div>

      <div class="about-section">
        <h2>For area leads and event managers</h2>
        <p>
          See where your team members are in real-time. Know at a glance who has checked in,
          who's on their way, and who might need a reminder. Be confident that all necessary
          tasks have been completed before the event begins.
        </p>
      </div>

      <div class="features-section">
        <h2>Everything you need</h2>
        <div class="features-grid">
          <div class="feature-item">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
              </svg>
            </div>
            <div class="feature-content">
              <h3>Track lead cars</h3>
              <p>Monitor the position of lead vehicles as they progress through your route</p>
            </div>
          </div>

          <div class="feature-item">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
              </svg>
            </div>
            <div class="feature-content">
              <h3>Task management</h3>
              <p>Create checklists and track completion across checkpoints, areas, and individuals</p>
            </div>
          </div>

          <div class="feature-item">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z"/>
              </svg>
            </div>
            <div class="feature-content">
              <h3>Incident reporting</h3>
              <p>Report and track incidents in real-time with location data and status updates</p>
            </div>
          </div>

          <div class="feature-item">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM9 17H7v-7h2v7zm4 0h-2V7h2v10zm4 0h-2v-4h2v4z"/>
              </svg>
            </div>
            <div class="feature-content">
              <h3>Flexible management</h3>
              <p>Organise by areas, customise terminology, and adapt to your event's unique needs</p>
            </div>
          </div>
        </div>
      </div>

      <div class="cta-section">
        <router-link to="/?login=true" class="btn btn-primary">Get started</router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
</script>

<style scoped>
.about-view {
  min-height: 100vh;
  background: linear-gradient(180deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
  color: white;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 1rem;
  padding-top: 5rem;
  padding-bottom: 3rem;
}

.content-container {
  max-width: 800px;
  width: 100%;
}

h1 {
  font-size: 2rem;
  margin-bottom: 0.5rem;
  text-align: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.tagline {
  font-size: 1.1rem;
  color: rgba(255, 255, 255, 0.7);
  text-align: center;
  margin-bottom: 2.5rem;
}

.about-section {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 1.5rem;
  margin-bottom: 1.5rem;
}

.about-section h2 {
  font-size: 1.1rem;
  color: #667eea;
  margin-bottom: 0.75rem;
}

.about-section p {
  color: rgba(255, 255, 255, 0.85);
  line-height: 1.6;
  margin: 0;
}

.features-section {
  margin-top: 2rem;
}

.features-section h2 {
  font-size: 1.25rem;
  text-align: center;
  margin-bottom: 1.5rem;
  color: white;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.feature-item {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 1.25rem;
  display: flex;
  align-items: center;
  gap: 1rem;
}

/* Odd items: icon on left */
.feature-item:nth-child(odd) {
  flex-direction: row;
}

/* Even items: icon on right */
.feature-item:nth-child(even) {
  flex-direction: row-reverse;
}

.feature-content {
  flex: 1;
  text-align: left;
}

.feature-icon {
  width: 48px;
  height: 48px;
  min-width: 48px;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.2) 0%, rgba(118, 75, 162, 0.2) 100%);
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.feature-icon svg {
  width: 24px;
  height: 24px;
  color: #667eea;
}

.feature-item h3 {
  font-size: 1rem;
  color: white;
  margin-bottom: 0.25rem;
}

.feature-item p {
  font-size: 0.85rem;
  color: rgba(255, 255, 255, 0.7);
  line-height: 1.5;
  margin: 0;
}

.cta-section {
  margin-top: 2.5rem;
  text-align: center;
}

.btn {
  display: inline-block;
  padding: 0.875rem 2rem;
  font-size: 1rem;
  text-decoration: none;
  border-radius: 50px;
  transition: all 0.3s;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  font-weight: 600;
  box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(102, 126, 234, 0.5);
}

@media (max-width: 600px) {
  h1 {
    font-size: 1.75rem;
  }

  .features-grid {
    grid-template-columns: 1fr;
  }

  .about-section {
    padding: 1.25rem;
  }

  .feature-item {
    padding: 1rem;
  }
}
</style>
