// Redirect www to apex domain to ensure consistent localStorage/IndexedDB
if (window.location.hostname.startsWith('www.')) {
  window.location.replace(window.location.href.replace(window.location.hostname, window.location.hostname.slice(4)))
}

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './styles/common.css'
import './styles/themes.css'
import './styles/marshal-shared.css'
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
