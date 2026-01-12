<template>
  <div class="hero-animation-container">
    <svg
      viewBox="0 0 400 350"
      class="hero-map-svg"
      :class="{ 'animate': shouldAnimate && animate, 'no-animate': shouldAnimate && !animate }"
    >
      <defs>
        <!-- Clip path for the perspective map shape (more extreme) -->
        <clipPath id="mapClip">
          <polygon points="80,0 320,0 420,350 -20,350" />
        </clipPath>
      </defs>

      <!-- Map background with perspective -->
      <g class="map-group">
        <!-- Map base (green field with more extreme perspective) -->
        <polygon
          points="80,0 320,0 420,350 -20,350"
          fill="#c8e6c0"
          stroke="#8fbc8f"
          stroke-width="3"
        />

        <!-- Contour lines -->
        <g clip-path="url(#mapClip)" class="contours">
          <ellipse cx="300" cy="60" rx="35" ry="12" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="300" cy="60" rx="25" ry="8" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="300" cy="60" rx="15" ry="5" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="80" cy="220" rx="80" ry="40" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="80" cy="220" rx="55" ry="28" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="80" cy="220" rx="30" ry="15" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="320" cy="180" rx="50" ry="25" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
          <ellipse cx="320" cy="180" rx="30" ry="15" fill="none" stroke="#b8d4b0" stroke-width="1.5" />
        </g>

        <!-- Roads and River -->
        <g clip-path="url(#mapClip)" class="roads">
          <!-- Blue river (thick, winding) -->
          <path
            d="M 30,350 Q 80,300 100,250 C 120,200 90,150 110,100 Q 130,50 160,-10"
            fill="none"
            stroke="#1a5276"
            stroke-width="18"
            stroke-linecap="round"
          />
          <path
            d="M 30,350 Q 80,300 100,250 C 120,200 90,150 110,100 Q 130,50 160,-10"
            fill="none"
            stroke="#5dade2"
            stroke-width="12"
            stroke-linecap="round"
          />

          <!-- Yellow road (B-road style) -->
          <path
            d="M -20,180 Q 80,160 160,150 T 300,100 Q 340,80 380,50"
            fill="none"
            stroke="#cc8800"
            stroke-width="10"
            stroke-linecap="round"
          />
          <path
            d="M -20,180 Q 80,160 160,150 T 300,100 Q 340,80 380,50"
            fill="none"
            stroke="#ffcc00"
            stroke-width="6"
            stroke-linecap="round"
          />
        </g>

        <!-- Hidden path for arrow to follow (zigzag with 3 direction changes) -->
        <path
          id="routePath"
          d="M 60,360
             L 200,250
             L 150,150
             L 280,100
             L 450,60"
          fill="none"
          stroke="none"
        />

        <!-- Trail with variable width (drawn as 4 segments) - zigzag -->
        <g class="trail-segments">
          <path class="trail-seg trail-seg-1" d="M 60,360 L 200,250" fill="none" stroke="#dc3545" stroke-width="12" stroke-linecap="round" stroke-linejoin="round" />
          <path class="trail-seg trail-seg-2" d="M 200,250 L 150,150" fill="none" stroke="#dc3545" stroke-width="9" stroke-linecap="round" stroke-linejoin="round" />
          <path class="trail-seg trail-seg-3" d="M 150,150 L 280,100" fill="none" stroke="#dc3545" stroke-width="6" stroke-linecap="round" stroke-linejoin="round" />
          <path class="trail-seg trail-seg-4" d="M 280,100 L 450,60" fill="none" stroke="#dc3545" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" />
        </g>

        <!-- Checkpoint 1 at first turn (200, 250) - larger due to perspective -->
        <g class="checkpoint checkpoint-1" transform="translate(200, 250)">
          <ellipse cx="0" cy="8" rx="40" ry="12" fill="#4a5aba" opacity="0.4" />
          <ellipse cx="0" cy="4" rx="30" ry="10" fill="#667eea" />
          <ellipse cx="0" cy="0" rx="25" ry="8" fill="#8b9ff0" />
        </g>

        <!-- Checkpoint 2 at second turn (100, 150) - medium size -->
        <g class="checkpoint checkpoint-2" transform="translate(150, 150)">
          <ellipse cx="0" cy="6" rx="18" ry="8" fill="#4a5aba" opacity="0.4" />
          <ellipse cx="0" cy="3" rx="15" ry="6" fill="#667eea" />
          <ellipse cx="0" cy="0" rx="12" ry="5" fill="#8b9ff0" />
        </g>

        <!-- Checkpoint 3 at third turn (280, 80) - smallest due to perspective -->
        <g class="checkpoint checkpoint-3" transform="translate(280, 100)">
          <ellipse cx="0" cy="4" rx="12" ry="5" fill="#4a5aba" opacity="0.4" />
          <ellipse cx="0" cy="2" rx="10" ry="4" fill="#667eea" />
          <ellipse cx="0" cy="0" rx="8" ry="3" fill="#8b9ff0" />
        </g>

        <!-- Marshal 1 at first turn (200, 250) -->
        <g class="marshal marshal-1" transform="translate(200, 250)">
          <circle cx="0" cy="-28" r="12" fill="#444" />
          <path d="M 20,0 v-8 a12 12 0 0 0-12-12 h-16 a12 12 0 0 0-12 12 v8" fill="#444" />
        </g>

        <!-- Marshal 2 at second turn (100, 150) -->
        <g class="marshal marshal-2" transform="translate(150, 150)">
          <circle cx="0" cy="-18" r="8" fill="#444" />
          <path d="M 12,0 v-5 a8 8 0 0 0-8-8 h-8 a8 8 0 0 0-8 8 v5" fill="#444" />
        </g>

        <!-- Marshal 3 at third turn (280, 80) -->
        <g class="marshal marshal-3" transform="translate(280, 100)">
          <circle cx="0" cy="-12" r="6" fill="#444" />
          <path d="M 9,0 v-4 a6 6 0 0 0-6-6 h-6 a6 6 0 0 0-6 6 v4" fill="#444" />
        </g>

        <!-- Pine trees (on top, fatter) -->
        <g clip-path="url(#mapClip)" class="trees">
          <!-- Tree 1 (bottom left, larger) -->
          <g transform="translate(45, 280)">
            <polygon points="0,-20 12,0 -12,0" fill="#2d5a2d" />
            <polygon points="0,-34 10,-12 -10,-12" fill="#2d5a2d" />
            <polygon points="0,-46 8,-26 -8,-26" fill="#2d5a2d" />
            <rect x="-3" y="0" width="6" height="8" fill="#5d4037" />
          </g>
          <!-- Tree 2 -->
          <g transform="translate(75, 255)">
            <polygon points="0,-18 10,0 -10,0" fill="#3d6a3d" />
            <polygon points="0,-30 8,-10 -8,-10" fill="#3d6a3d" />
            <polygon points="0,-40 6,-22 -6,-22" fill="#3d6a3d" />
            <rect x="-2.5" y="0" width="5" height="6" fill="#5d4037" />
          </g>
          <!-- Tree 3 (right side) -->
          <g transform="translate(350, 230)">
            <polygon points="0,-20 11,0 -11,0" fill="#2d5a2d" />
            <polygon points="0,-33 9,-11 -9,-11" fill="#2d5a2d" />
            <polygon points="0,-44 7,-24 -7,-24" fill="#2d5a2d" />
            <rect x="-2.5" y="0" width="5" height="7" fill="#5d4037" />
          </g>
          <!-- Tree 4 (smaller, higher up) -->
          <g transform="translate(60, 140)">
            <polygon points="0,-12 7,0 -7,0" fill="#3d6a3d" />
            <polygon points="0,-20 5.5,-8 -5.5,-8" fill="#3d6a3d" />
            <polygon points="0,-27 4,-15 -4,-15" fill="#3d6a3d" />
            <rect x="-1.5" y="0" width="3" height="4" fill="#5d4037" />
          </g>
          <!-- Tree 5 (top area, smallest) -->
          <g transform="translate(320, 70)">
            <polygon points="0,-9 5,0 -5,0" fill="#2d5a2d" />
            <polygon points="0,-15 4,-6 -4,-6" fill="#2d5a2d" />
            <polygon points="0,-20 3,-11 -3,-11" fill="#2d5a2d" />
            <rect x="-1" y="0" width="2" height="3" fill="#5d4037" />
          </g>
          <!-- Tree 6 -->
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
import { ref, onMounted } from 'vue';

const props = defineProps({
  animate: {
    type: Boolean,
    default: true,
  },
});

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
  transform: translateY(400px);
  opacity: 0;
}

.animate .map-group {
  animation: mapRise 1s ease-out forwards;
}

@keyframes mapRise {
  0% {
    transform: translateY(400px);
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
  transform: scale(0);
  opacity: 0;
}

.animate .checkpoint-1 {
  animation: checkpointGrow1 0.4s ease-out 0.9s forwards;
}

.animate .checkpoint-2 {
  animation: checkpointGrow2 0.4s ease-out 1.3s forwards;
}

.animate .checkpoint-3 {
  animation: checkpointGrow3 0.4s ease-out 1.7s forwards;
}

@keyframes checkpointGrow1 {
  0% {
    transform: translate(200px, 250px) scale(0);
    opacity: 1;
  }
  70% {
    transform: translate(200px, 250px) scale(1.2);
    opacity: 1;
  }
  100% {
    transform: translate(200px, 250px) scale(1);
    opacity: 1;
  }
}

@keyframes checkpointGrow2 {
  0% {
    transform: translate(150px, 150px) scale(0);
    opacity: 1;
  }
  70% {
    transform: translate(150px, 150px) scale(1.2);
    opacity: 1;
  }
  100% {
    transform: translate(150px, 150px) scale(1);
    opacity: 1;
  }
}

@keyframes checkpointGrow3 {
  0% {
    transform: translate(280px, 100px) scale(0);
    opacity: 1;
  }
  70% {
    transform: translate(280px, 100px) scale(1.2);
    opacity: 1;
  }
  100% {
    transform: translate(280px, 100px) scale(1);
    opacity: 1;
  }
}

/* Marshal drop animations */
.marshal {
  opacity: 0;
}

.animate .marshal-1 {
  animation: marshalDrop1 0.5s ease-in 1.5s forwards;
}

.animate .marshal-2 {
  animation: marshalDrop2 0.5s ease-in 1.9s forwards;
}

.animate .marshal-3 {
  animation: marshalDrop3 0.5s ease-in 2.3s forwards;
}

@keyframes marshalDrop1 {
  0% {
    transform: translate(200px, 250px) translateY(-350px);
    opacity: 1;
  }
  70% {
    transform: translate(200px, 250px) translateY(8px);
    opacity: 1;
  }
  85% {
    transform: translate(200px, 250px) translateY(-5px);
  }
  100% {
    transform: translate(200px, 250px) translateY(0);
    opacity: 1;
  }
}

@keyframes marshalDrop2 {
  0% {
    transform: translate(150px, 150px) translateY(-350px);
    opacity: 1;
  }
  70% {
    transform: translate(150px, 150px) translateY(6px);
    opacity: 1;
  }
  85% {
    transform: translate(150px, 150px) translateY(-4px);
  }
  100% {
    transform: translate(150px, 150px) translateY(0);
    opacity: 1;
  }
}

@keyframes marshalDrop3 {
  0% {
    transform: translate(280px, 100px) translateY(-350px);
    opacity: 1;
  }
  70% {
    transform: translate(280px, 100px) translateY(4px);
    opacity: 1;
  }
  85% {
    transform: translate(280px, 100px) translateY(-3px);
  }
  100% {
    transform: translate(280px, 100px) translateY(0);
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
  transform: translate(200px, 250px) scale(1);
  opacity: 1;
}

.no-animate .checkpoint-2 {
  transform: translate(150px, 150px) scale(1);
  opacity: 1;
}

.no-animate .checkpoint-3 {
  transform: translate(280px, 100px) scale(1);
  opacity: 1;
}

.no-animate .marshal-1 {
  transform: translate(200px, 250px) translateY(0);
  opacity: 1;
}

.no-animate .marshal-2 {
  transform: translate(150px, 150px) translateY(0);
  opacity: 1;
}

.no-animate .marshal-3 {
  transform: translate(280px, 100px) translateY(0);
  opacity: 1;
}
</style>
