<template>
  <div class="pricing-view">
    <div class="content-container">
      <h1>Simple, transparent pricing</h1>
      <p class="subtitle">Pay per event, scale as you grow</p>

      <div class="pricing-card">
        <div class="price-header">
          <span class="price">£10</span>
          <span class="per">per event</span>
        </div>

        <ul class="features-list">
          <li>
            <svg class="check-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z"/>
            </svg>
            Includes 10 marshals
          </li>
          <li>
            <svg class="check-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z"/>
            </svg>
            Unlimited checkpoints
          </li>
          <li>
            <svg class="check-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z"/>
            </svg>
            Real-time check-in tracking
          </li>
          <li>
            <svg class="check-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z"/>
            </svg>
            Incident reporting
          </li>
          <li>
            <svg class="check-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z"/>
            </svg>
            Route and checkpoint maps
          </li>
        </ul>

        <div class="additional-pricing">
          <div class="additional-header">Need more marshals?</div>
          <div class="additional-price">
            <span class="price-small">£0.50</span>
            <span class="per-small">per additional marshal</span>
          </div>
        </div>
      </div>

      <div class="pricing-examples">
        <h3>Example pricing</h3>
        <div class="examples-grid">
          <div class="example">
            <span class="example-size">10 marshals</span>
            <span class="example-price">£10</span>
          </div>
          <div class="example">
            <span class="example-size">50 marshals</span>
            <span class="example-price">£30</span>
          </div>
          <div class="example">
            <span class="example-size">100 marshals</span>
            <span class="example-price">£55</span>
          </div>
          <div class="example">
            <span class="example-size">200 marshals</span>
            <span class="example-price">£105</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
</script>

<style scoped>
.pricing-view {
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
  max-width: 600px;
  width: 100%;
  text-align: center;
}

h1 {
  font-size: 2rem;
  margin-bottom: 0.5rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.subtitle {
  font-size: 1.1rem;
  color: rgba(255, 255, 255, 0.7);
  margin-bottom: 2rem;
}

.pricing-card {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  padding: 2rem;
  margin-bottom: 2rem;
}

.price-header {
  margin-bottom: 1.5rem;
}

.price {
  font-size: 3.5rem;
  font-weight: 700;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.per {
  display: block;
  font-size: 1rem;
  color: rgba(255, 255, 255, 0.6);
  margin-top: 0.25rem;
}

.features-list {
  list-style: none;
  padding: 0;
  margin: 0 0 2rem 0;
  text-align: left;
}

.features-list li {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.6rem 0;
  color: rgba(255, 255, 255, 0.9);
  font-size: 1rem;
}

.check-icon {
  width: 20px;
  height: 20px;
  color: #667eea;
  flex-shrink: 0;
}

.additional-pricing {
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  padding-top: 1.5rem;
}

.additional-header {
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.6);
  margin-bottom: 0.5rem;
}

.additional-price {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 0.5rem;
}

.price-small {
  font-size: 1.75rem;
  font-weight: 600;
  color: white;
}

.per-small {
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.6);
}

.pricing-examples {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
  padding: 1.5rem;
}

.pricing-examples h3 {
  font-size: 1rem;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.7);
  margin-bottom: 1rem;
}

.examples-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.example {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 8px;
}

.example-size {
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.8);
}

.example-price {
  font-size: 1rem;
  font-weight: 600;
  color: #667eea;
}

@media (max-width: 480px) {
  h1 {
    font-size: 1.75rem;
  }

  .price {
    font-size: 3rem;
  }

  .pricing-card {
    padding: 1.5rem;
  }

  .examples-grid {
    grid-template-columns: 1fr;
  }
}
</style>
