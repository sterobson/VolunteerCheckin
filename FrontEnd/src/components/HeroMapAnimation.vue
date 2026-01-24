<template>
  <div class="hero-animation-container" :style="cssVars">
    <svg
      viewBox="0 0 400 200"
      class="hero-map-svg"
      :class="{ 'animate': shouldAnimate && animate, 'no-animate': shouldAnimate && !animate }"
    >
      <defs>
        <!-- Clip path for the perspective map shape -->
        <clipPath id="mapClip">
          <polygon points="100,0 300,0 450,200 -50,200" />
        </clipPath>
      </defs>

      <!-- Map background with perspective -->
      <g class="map-group">
        <!-- Map base (green field with perspective) -->
        <polygon
          points="100,0 300,0 450,200 -50,200"
          fill="#c8e6c0"
          stroke="#8fbc8f"
          stroke-width="3"
        />

        <!-- Grid lines (perspective-aligned) -->
        <g clip-path="url(#mapClip)" class="gridlines">
          <!-- Horizontal gridlines -->
          <line x1="50" y1="50" x2="350" y2="50" stroke="#5a7a5a" stroke-width="1" opacity="0.3" />
          <line x1="25" y1="100" x2="375" y2="100" stroke="#5a7a5a" stroke-width="1" opacity="0.3" />
          <line x1="0" y1="150" x2="400" y2="150" stroke="#5a7a5a" stroke-width="1" opacity="0.3" />
          <!-- Vertical gridlines (converging to perspective) -->
          <line x1="150" y1="0" x2="75" y2="200" stroke="#5a7a5a" stroke-width="1" opacity="0.3" />
          <line x1="200" y1="0" x2="200" y2="200" stroke="#5a7a5a" stroke-width="1" opacity="0.3" />
          <line x1="250" y1="0" x2="325" y2="200" stroke="#5a7a5a" stroke-width="1" opacity="0.3" />
        </g>

        <!-- Contour lines -->
        <g clip-path="url(#mapClip)" class="contours">
          <ellipse cx="100" cy="120" rx="70" ry="35" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="100" cy="120" rx="45" ry="22" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="100" cy="120" rx="20" ry="10" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="320" cy="60" rx="40" ry="18" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="320" cy="60" rx="22" ry="10" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
        </g>

        <!-- Roads and River -->
        <g clip-path="url(#mapClip)" class="roads">
          <!-- Blue river (smooth sweeping curve) -->
          <path
            d="M 180,210 C 240,150 220,60 170,-10"
            fill="none"
            stroke="#1a5276"
            stroke-width="18"
            stroke-linecap="round"
          />
          <path
            d="M 180,210 C 240,150 220,60 170,-10"
            fill="none"
            stroke="#5dade2"
            stroke-width="12"
            stroke-linecap="round"
          />

          <!-- Yellow road (B-road style) -->
          <path
            d="M -20,100 Q 60,85 140,75 T 280,40 Q 320,25 380,0"
            fill="none"
            stroke="#cc8800"
            stroke-width="10"
            stroke-linecap="round"
          />
          <path
            d="M -20,100 Q 60,85 140,75 T 280,40 Q 320,25 380,0"
            fill="none"
            stroke="#ffcc00"
            stroke-width="6"
            stroke-linecap="round"
          />
        </g>

        <!-- Trail/route -->
        <g class="trail-segments">
          <path class="trail-seg trail-seg-1" :d="trailPath1" fill="none" stroke="#dc3545" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" />
          <path class="trail-seg trail-seg-2" :d="trailPath2" fill="none" stroke="#dc3545" stroke-width="8" stroke-linecap="round" stroke-linejoin="round" />
          <path class="trail-seg trail-seg-3" :d="trailPath3" fill="none" stroke="#dc3545" stroke-width="6" stroke-linecap="round" stroke-linejoin="round" />
          <path class="trail-seg trail-seg-4" :d="trailPath4" fill="none" stroke="#dc3545" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" />
        </g>

        <!-- Checkpoint 1 (foreground, larger) -->
        <g class="checkpoint checkpoint-1">
          <ellipse cx="0" cy="8" rx="35" ry="11" fill="#4a5aba" opacity="0.4" />
          <ellipse cx="0" cy="4" rx="26" ry="9" fill="#667eea" />
          <ellipse cx="0" cy="0" rx="21" ry="7" fill="#8b9ff0" />
        </g>

        <!-- Checkpoint 2 (middle distance) -->
        <g class="checkpoint checkpoint-2">
          <ellipse cx="0" cy="5" rx="16" ry="6" fill="#4a5aba" opacity="0.4" />
          <ellipse cx="0" cy="2" rx="12" ry="5" fill="#667eea" />
          <ellipse cx="0" cy="0" rx="9" ry="4" fill="#8b9ff0" />
        </g>

        <!-- Marshal 1 (foreground) -->
        <g class="marshal marshal-1">
          <circle cx="0" cy="-24" r="10" fill="#444" />
          <path d="M 16,0 v-6 a10 10 0 0 0-10-10 h-12 a10 10 0 0 0-10 10 v6" fill="#444" />
        </g>

        <!-- Marshal 2 (middle distance) -->
        <g class="marshal marshal-2">
          <circle cx="0" cy="-14" r="6" fill="#444" />
          <path d="M 9,0 v-4 a6 6 0 0 0-6-6 h-6 a6 6 0 0 0-6 6 v4" fill="#444" />
        </g>

        <!-- Checkpoint 3 (on the road, far distance) -->
        <g class="checkpoint checkpoint-3">
          <ellipse cx="0" cy="4" rx="12" ry="5" fill="#4a5aba" opacity="0.4" />
          <ellipse cx="0" cy="2" rx="9" ry="4" fill="#667eea" />
          <ellipse cx="0" cy="0" rx="7" ry="3" fill="#8b9ff0" />
        </g>

        <!-- Marshal 3 (on the road, far distance) -->
        <g class="marshal marshal-3">
          <circle cx="0" cy="-10" r="5" fill="#444" />
          <path d="M 7,0 v-3 a5 5 0 0 0-5-5 h-4 a5 5 0 0 0-5 5 v3" fill="#444" />
        </g>

        <!-- Pine trees -->
        <g clip-path="url(#mapClip)" class="trees">
          <!-- Tree 1 (bottom left, larger) -->
          <g transform="translate(55, 170)">
            <polygon points="0,-18 11,0 -11,0" fill="#2d5a2d" />
            <polygon points="0,-30 9,-10 -9,-10" fill="#2d5a2d" />
            <polygon points="0,-40 7,-22 -7,-22" fill="#2d5a2d" />
            <rect x="-2.5" y="0" width="5" height="7" fill="#5d4037" />
          </g>
          <!-- Tree 2 -->
          <g transform="translate(85, 150)">
            <polygon points="0,-15 9,0 -9,0" fill="#3d6a3d" />
            <polygon points="0,-25 7,-9 -7,-9" fill="#3d6a3d" />
            <polygon points="0,-33 5,-18 -5,-18" fill="#3d6a3d" />
            <rect x="-2" y="0" width="4" height="5" fill="#5d4037" />
          </g>
          <!-- Tree 3 (right side) -->
          <g transform="translate(340, 130)">
            <polygon points="0,-16 9,0 -9,0" fill="#2d5a2d" />
            <polygon points="0,-27 7,-10 -7,-10" fill="#2d5a2d" />
            <polygon points="0,-36 5,-20 -5,-20" fill="#2d5a2d" />
            <rect x="-2" y="0" width="4" height="5" fill="#5d4037" />
          </g>
          <!-- Tree 4 (upper area) -->
          <g transform="translate(70, 70)">
            <polygon points="0,-10 6,0 -6,0" fill="#3d6a3d" />
            <polygon points="0,-17 5,-7 -5,-7" fill="#3d6a3d" />
            <polygon points="0,-23 4,-13 -4,-13" fill="#3d6a3d" />
            <rect x="-1.5" y="0" width="3" height="4" fill="#5d4037" />
          </g>
          <!-- Tree 5 (top right) -->
          <g transform="translate(330, 50)">
            <polygon points="0,-8 5,0 -5,0" fill="#2d5a2d" />
            <polygon points="0,-14 4,-5 -4,-5" fill="#2d5a2d" />
            <polygon points="0,-19 3,-10 -3,-10" fill="#2d5a2d" />
            <rect x="-1" y="0" width="2" height="3" fill="#5d4037" />
          </g>
          <g transform="translate(320, 140)">
            <polygon points="0,-14 8,0 -8,0" fill="#3d6a3d" />
            <polygon points="0,-24 6,-9 -6,-9" fill="#3d6a3d" />
            <polygon points="0,-32 5,-18 -5,-18" fill="#3d6a3d" />
            <rect x="-1.5" y="0" width="3" height="5" fill="#5d4037" />
          </g>
        </g>
      </g>
    </svg>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';

const props = defineProps({
  animate: {
    type: Boolean,
    default: true,
  },
});

// Checkpoint/Marshal positions (at grid intersections)
const positions = {
  start: { x: 94, y: 150 }, // Trail start (left-bottom intersection)
  cp1: { x: 180, y: 150 },  // Center-bottom intersection
  cp2: { x: 170, y: 72 },  // Center-middle intersection
  cp3: { x: 287, y: 40 },  // Right-middle intersection
  end: { x: 350, y: 10 },   // Trail end (right-top intersection)
};

// Generate trail path from positions
const trailPath1 = computed(() => `M ${positions.start.x},${positions.start.y + 60} L ${positions.cp1.x},${positions.cp1.y}`);
const trailPath2 = computed(() => `M ${positions.cp1.x},${positions.cp1.y} L ${positions.cp2.x},${positions.cp2.y}`);
const trailPath3 = computed(() => `M ${positions.cp2.x},${positions.cp2.y} L ${positions.cp3.x},${positions.cp3.y}`);
const trailPath4 = computed(() => `M ${positions.cp3.x},${positions.cp3.y} L ${positions.end.x},${positions.end.y}`);

// CSS custom properties for animations
const cssVars = computed(() => ({
  '--cp1-x': `${positions.cp1.x}px`,
  '--cp1-y': `${positions.cp1.y}px`,
  '--cp2-x': `${positions.cp2.x}px`,
  '--cp2-y': `${positions.cp2.y}px`,
  '--cp3-x': `${positions.cp3.x}px`,
  '--cp3-y': `${positions.cp3.y}px`,
}));

const shouldAnimate = ref(false);

onMounted(() => {
  if (props.animate) {
    // Small delay to ensure CSS is ready, then trigger animation
    requestAnimationFrame(() => {
      shouldAnimate.value = true;
    });
  } else {
    // Skip animation, show final state immediately
    shouldAnimate.value = true;
  }
});
</script>

<style scoped>
.hero-animation-container {
  width: 100%;
  max-width: 500px;
  margin: 0 auto 2rem;
}

.hero-map-svg {
  width: 100%;
  height: auto;
  overflow: visible;
}

/* Map entrance animation */
.map-group {
  transform: translateY(250px);
  opacity: 0;
}

.animate .map-group {
  animation: mapRise 1s ease-out forwards;
}

@keyframes mapRise {
  0% {
    transform: translateY(250px);
    opacity: 0;
  }
  100% {
    transform: translateY(0);
    opacity: 1;
  }
}

/* Trail segments animation - draw each segment sequentially */
.trail-seg {
  stroke-dasharray: 250;
  stroke-dashoffset: 250;
  opacity: 0;
}

.animate .trail-seg-1 {
  animation: drawSegment 0.7s linear 0.5s forwards;
}
.animate .trail-seg-2 {
    animation: drawSegment 0.7s linear 0.9s forwards;
}
.animate .trail-seg-3 {
    animation: drawSegment 0.7s linear 1.3s forwards;
}
.animate .trail-seg-4 {
    animation: drawSegment 0.7s linear 1.7s forwards;
}

@keyframes drawSegment {
  0% {
    stroke-dashoffset: 250;
    opacity: 1;
  }
  100% {
    stroke-dashoffset: 0;
    opacity: 1;
  }
}

/* Arrow head - hidden until animation starts */
.arrow-head-group {
  opacity: 0;
}

.animate .arrow-head-group {
  animation: showArrow 0.1s ease-out 0.5s forwards;
}

@keyframes showArrow {
  0% { opacity: 0; }
  100% { opacity: 1; }
}

/* Checkpoint animations - scale from 0 */
.checkpoint {
  transform-origin: center;
  opacity: 0;
}

.checkpoint-1 {
  transform: translate(var(--cp1-x), var(--cp1-y)) scale(0);
}

.checkpoint-2 {
  transform: translate(var(--cp2-x), var(--cp2-y)) scale(0);
}

.checkpoint-3 {
  transform: translate(var(--cp3-x), var(--cp3-y)) scale(0);
}

.animate .checkpoint-1 {
  animation: checkpointGrow1 0.4s ease-out 0.9s forwards;
}

.animate .checkpoint-2 {
  animation: checkpointGrow2 0.4s ease-out 1.3s forwards;
}

@keyframes checkpointGrow1 {
  0% {
    transform: translate(var(--cp1-x), var(--cp1-y)) scale(0);
    opacity: 1;
  }
  70% {
    transform: translate(var(--cp1-x), var(--cp1-y)) scale(1.2);
    opacity: 1;
  }
  100% {
    transform: translate(var(--cp1-x), var(--cp1-y)) scale(1);
    opacity: 1;
  }
}

@keyframes checkpointGrow2 {
  0% {
    transform: translate(var(--cp2-x), var(--cp2-y)) scale(0);
    opacity: 1;
  }
  70% {
    transform: translate(var(--cp2-x), var(--cp2-y)) scale(1.2);
    opacity: 1;
  }
  100% {
    transform: translate(var(--cp2-x), var(--cp2-y)) scale(1);
    opacity: 1;
  }
}

.animate .checkpoint-3 {
  animation: checkpointGrow3 0.4s ease-out 1.7s forwards;
}

@keyframes checkpointGrow3 {
  0% {
    transform: translate(var(--cp3-x), var(--cp3-y)) scale(0);
    opacity: 1;
  }
  70% {
    transform: translate(var(--cp3-x), var(--cp3-y)) scale(1.2);
    opacity: 1;
  }
  100% {
    transform: translate(var(--cp3-x), var(--cp3-y)) scale(1);
    opacity: 1;
  }
}

/* Marshal drop animations */
.marshal {
  opacity: 0;
}

.marshal-1 {
  transform: translate(var(--cp1-x), var(--cp1-y)) translateY(0);
}

.marshal-2 {
  transform: translate(var(--cp2-x), var(--cp2-y)) translateY(0);
}

.marshal-3 {
  transform: translate(var(--cp3-x), var(--cp3-y)) translateY(0);
}

.animate .marshal-1 {
  animation: marshalDrop1 0.5s ease-in 1.5s forwards;
}

.animate .marshal-2 {
  animation: marshalDrop2 0.5s ease-in 1.9s forwards;
}

@keyframes marshalDrop1 {
  0% {
    transform: translate(var(--cp1-x), var(--cp1-y)) translateY(-250px);
    opacity: 1;
  }
  70% {
    transform: translate(var(--cp1-x), var(--cp1-y)) translateY(8px);
    opacity: 1;
  }
  85% {
    transform: translate(var(--cp1-x), var(--cp1-y)) translateY(-5px);
  }
  100% {
    transform: translate(var(--cp1-x), var(--cp1-y)) translateY(0);
    opacity: 1;
  }
}

@keyframes marshalDrop2 {
  0% {
    transform: translate(var(--cp2-x), var(--cp2-y)) translateY(-250px);
    opacity: 1;
  }
  70% {
    transform: translate(var(--cp2-x), var(--cp2-y)) translateY(6px);
    opacity: 1;
  }
  85% {
    transform: translate(var(--cp2-x), var(--cp2-y)) translateY(-4px);
  }
  100% {
    transform: translate(var(--cp2-x), var(--cp2-y)) translateY(0);
    opacity: 1;
  }
}

.animate .marshal-3 {
  animation: marshalDrop3 0.5s ease-in 2.3s forwards;
}

@keyframes marshalDrop3 {
  0% {
    transform: translate(var(--cp3-x), var(--cp3-y)) translateY(-250px);
    opacity: 1;
  }
  70% {
    transform: translate(var(--cp3-x), var(--cp3-y)) translateY(4px);
    opacity: 1;
  }
  85% {
    transform: translate(var(--cp3-x), var(--cp3-y)) translateY(-3px);
  }
  100% {
    transform: translate(var(--cp3-x), var(--cp3-y)) translateY(0);
    opacity: 1;
  }
}

/* No animation - show final state immediately */
.no-animate .map-group {
  transform: translateY(0);
  opacity: 1;
}

.no-animate .trail-seg {
  stroke-dashoffset: 0;
  opacity: 1;
}

.no-animate .arrow-head-group {
  opacity: 1;
}

.no-animate .checkpoint-1 {
  transform: translate(var(--cp1-x), var(--cp1-y)) scale(1);
  opacity: 1;
}

.no-animate .checkpoint-2 {
  transform: translate(var(--cp2-x), var(--cp2-y)) scale(1);
  opacity: 1;
}

.no-animate .checkpoint-3 {
  transform: translate(var(--cp3-x), var(--cp3-y)) scale(1);
  opacity: 1;
}

.no-animate .marshal-1 {
  transform: translate(var(--cp1-x), var(--cp1-y)) translateY(0);
  opacity: 1;
}

.no-animate .marshal-2 {
  transform: translate(var(--cp2-x), var(--cp2-y)) translateY(0);
  opacity: 1;
}

.no-animate .marshal-3 {
  transform: translate(var(--cp3-x), var(--cp3-y)) translateY(0);
  opacity: 1;
}
</style>
