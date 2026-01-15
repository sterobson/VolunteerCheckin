<template>
  <span
    class="weather-icon"
    :style="iconStyle"
    role="img"
    :aria-label="alt"
  ></span>
</template>

<script setup>
import { computed, defineProps } from 'vue';

const props = defineProps({
  icon: {
    type: String,
    required: true,
  },
  size: {
    type: [Number, String],
    default: 20,
  },
  alt: {
    type: String,
    default: 'Weather',
  },
  color: {
    type: String,
    default: 'currentColor',
  },
});

// Map our simple icon names to Meteocons icon names
const meteoconNameMap = {
  'sun': 'clear-day',
  'cloud-sun': 'partly-cloudy-day',
  'cloud': 'cloudy',
  'fog': 'fog',
  'drizzle': 'drizzle',
  'rain': 'rain',
  'showers': 'overcast-rain',
  'snow': 'snow',
  'sleet': 'sleet',
  'thunderstorm': 'thunderstorms-rain',
};

const iconUrl = computed(() => {
  const meteoconName = meteoconNameMap[props.icon] || 'cloudy';
  return `https://bmcdn.nl/assets/weather-icons/v3.0/fill/svg/${meteoconName}.svg`;
});

const sizeWithUnit = computed(() => {
  const size = props.size;
  return typeof size === 'number' ? `${size}px` : size;
});

const iconStyle = computed(() => ({
  width: sizeWithUnit.value,
  height: sizeWithUnit.value,
  backgroundColor: props.color,
  maskImage: `url(${iconUrl.value})`,
  WebkitMaskImage: `url(${iconUrl.value})`,
  maskSize: 'contain',
  WebkitMaskSize: 'contain',
  maskRepeat: 'no-repeat',
  WebkitMaskRepeat: 'no-repeat',
  maskPosition: 'center',
  WebkitMaskPosition: 'center',
}));
</script>

<style scoped>
.weather-icon {
  display: inline-block;
  flex-shrink: 0;
  vertical-align: middle;
}
</style>
