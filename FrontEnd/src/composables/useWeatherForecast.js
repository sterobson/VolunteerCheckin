import { ref, computed, watch } from 'vue';

/**
 * Composable for fetching and caching weather forecasts for events
 */
export function useWeatherForecast({ eventDate, latitude, longitude }) {
  const weather = ref(null);
  const loading = ref(false);
  const error = ref(null);

  // Countries that use Fahrenheit
  const fahrenheitCountries = ['US', 'LR', 'MM']; // USA, Liberia, Myanmar

  // Detect if user's locale uses Fahrenheit
  const usesFahrenheit = computed(() => {
    try {
      // Try to get country from locale
      const locale = navigator.language || 'en-GB';
      const country = locale.split('-')[1]?.toUpperCase();
      return fahrenheitCountries.includes(country);
    } catch {
      return false;
    }
  });

  const temperatureUnit = computed(() => usesFahrenheit.value ? 'fahrenheit' : 'celsius');

  // Calculate days until event
  const daysUntilEvent = computed(() => {
    if (!eventDate.value) return null;
    const now = new Date();
    const event = new Date(eventDate.value);
    // Reset to start of day for both
    now.setHours(0, 0, 0, 0);
    event.setHours(0, 0, 0, 0);
    return Math.floor((event - now) / (1000 * 60 * 60 * 24));
  });

  // Determine if we should show weather
  const shouldShowWeather = computed(() => {
    const days = daysUntilEvent.value;
    if (days === null) return false;
    // Show if within 7 days in future or up to 1 day in past
    return days >= -1 && days <= 7;
  });

  // Get cache TTL based on days until event
  const getCacheTtlMs = (days) => {
    if (days > 3) return 12 * 60 * 60 * 1000; // 12 hours
    if (days > 1) return 3 * 60 * 60 * 1000;  // 3 hours
    return 1 * 60 * 60 * 1000;                 // 1 hour
  };

  // Cache key for localStorage
  const getCacheKey = (lat, lng, date) => {
    return `weather_${lat.toFixed(2)}_${lng.toFixed(2)}_${date}`;
  };

  // Get cached weather if valid
  const getCachedWeather = (lat, lng, date, days) => {
    try {
      const key = getCacheKey(lat, lng, date);
      const cached = localStorage.getItem(key);
      if (!cached) return null;

      const { data, timestamp } = JSON.parse(cached);
      const ttl = getCacheTtlMs(days);
      const age = Date.now() - timestamp;

      if (age < ttl) {
        return data;
      }
      // Cache expired
      localStorage.removeItem(key);
      return null;
    } catch {
      return null;
    }
  };

  // Save weather to cache
  const setCachedWeather = (lat, lng, date, data) => {
    try {
      const key = getCacheKey(lat, lng, date);
      localStorage.setItem(key, JSON.stringify({
        data,
        timestamp: Date.now(),
      }));
    } catch {
      // Ignore storage errors
    }
  };

  // Map WMO weather codes to simple icon names and descriptions
  const weatherCodeMap = {
    0: { icon: 'sun', description: 'Clear' },
    1: { icon: 'sun', description: 'Mainly clear' },
    2: { icon: 'cloud-sun', description: 'Partly cloudy' },
    3: { icon: 'cloud', description: 'Overcast' },
    45: { icon: 'fog', description: 'Fog' },
    48: { icon: 'fog', description: 'Icy fog' },
    51: { icon: 'drizzle', description: 'Light drizzle' },
    53: { icon: 'drizzle', description: 'Drizzle' },
    55: { icon: 'drizzle', description: 'Heavy drizzle' },
    56: { icon: 'drizzle', description: 'Freezing drizzle' },
    57: { icon: 'drizzle', description: 'Heavy freezing drizzle' },
    61: { icon: 'rain', description: 'Light rain' },
    63: { icon: 'rain', description: 'Rain' },
    65: { icon: 'rain', description: 'Heavy rain' },
    66: { icon: 'rain', description: 'Freezing rain' },
    67: { icon: 'rain', description: 'Heavy freezing rain' },
    71: { icon: 'snow', description: 'Light snow' },
    73: { icon: 'snow', description: 'Snow' },
    75: { icon: 'snow', description: 'Heavy snow' },
    77: { icon: 'snow', description: 'Snow grains' },
    80: { icon: 'showers', description: 'Light showers' },
    81: { icon: 'showers', description: 'Showers' },
    82: { icon: 'showers', description: 'Heavy showers' },
    85: { icon: 'snow', description: 'Light snow showers' },
    86: { icon: 'snow', description: 'Heavy snow showers' },
    95: { icon: 'thunderstorm', description: 'Thunderstorm' },
    96: { icon: 'thunderstorm', description: 'Thunderstorm with hail' },
    99: { icon: 'thunderstorm', description: 'Severe thunderstorm' },
  };

  // Fetch weather from Open-Meteo
  const fetchWeather = async () => {
    const lat = latitude.value;
    const lng = longitude.value;
    const date = eventDate.value;
    const days = daysUntilEvent.value;

    if (!lat || !lng || !date || !shouldShowWeather.value) {
      weather.value = null;
      return;
    }

    // Format event date as YYYY-MM-DD
    const eventDateStr = new Date(date).toISOString().split('T')[0];

    // Check cache first
    const cached = getCachedWeather(lat, lng, eventDateStr, days);
    if (cached) {
      weather.value = cached;
      return;
    }

    loading.value = true;
    error.value = null;

    try {
      const unit = usesFahrenheit.value ? 'fahrenheit' : 'celsius';
      const url = `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lng}&daily=temperature_2m_max,temperature_2m_min,weathercode&temperature_unit=${unit}&timezone=auto`;

      const response = await fetch(url);
      if (!response.ok) {
        throw new Error('Weather API request failed');
      }

      const data = await response.json();

      // Find the forecast for our event date
      const dateIndex = data.daily.time.indexOf(eventDateStr);
      if (dateIndex === -1) {
        // Event date not in forecast range
        weather.value = null;
        return;
      }

      const weatherCode = data.daily.weathercode[dateIndex];
      const tempMax = Math.round(data.daily.temperature_2m_max[dateIndex]);
      const tempMin = Math.round(data.daily.temperature_2m_min[dateIndex]);
      const weatherInfo = weatherCodeMap[weatherCode] || { icon: 'cloud', description: 'Unknown' };

      const result = {
        tempMax,
        tempMin,
        unit: usesFahrenheit.value ? 'F' : 'C',
        icon: weatherInfo.icon,
        description: weatherInfo.description,
        weatherCode,
      };

      // Cache the result
      setCachedWeather(lat, lng, eventDateStr, result);
      weather.value = result;
    } catch (err) {
      console.warn('Failed to fetch weather:', err);
      error.value = err.message;
      weather.value = null;
    } finally {
      loading.value = false;
    }
  };

  // Watch for changes and refetch
  watch(
    [eventDate, latitude, longitude, shouldShowWeather],
    () => {
      if (shouldShowWeather.value && latitude.value && longitude.value) {
        fetchWeather();
      } else {
        weather.value = null;
      }
    },
    { immediate: true }
  );

  return {
    weather,
    loading,
    error,
    shouldShowWeather,
    daysUntilEvent,
    temperatureUnit,
    fetchWeather,
  };
}
