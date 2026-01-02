import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './styles/common.css'
import { initDb } from './services/offlineDb'

// Initialize IndexedDB for offline support
initDb().then(() => {
  console.log('Offline database initialized');
}).catch((error) => {
  console.warn('Failed to initialize offline database:', error);
});

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')
