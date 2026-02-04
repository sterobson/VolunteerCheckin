import { ref, onUnmounted } from 'vue';
import { useTerminology } from './useTerminology';

const TOAST_COUNT = 4;

/**
 * Time-based toast sequence for demo/sample events.
 * Shows guided prompts at timed intervals to help users explore.
 * Persists progress per user in localStorage so dismissed toasts never reappear.
 *
 * @param {() => string|null} getEmail - Getter returning the user email for per-user persistence
 */
export function useDemoToasts(getEmail) {
  const { termsLower } = useTerminology();

  const currentToast = ref(null);
  const stopped = ref(false);
  const timers = [];

  function getStorageKey() {
    const email = getEmail();
    return email ? `demo-toasts-${email}` : null;
  }

  function loadState() {
    const key = getStorageKey();
    if (!key) return { nextIndex: 0, hiddenAll: false };
    try {
      const raw = localStorage.getItem(key);
      if (raw) {
        const parsed = JSON.parse(raw);
        return {
          nextIndex: parsed.nextIndex ?? 0,
          hiddenAll: parsed.hiddenAll ?? false,
        };
      }
    } catch {
      // Corrupted — start fresh
    }
    return { nextIndex: 0, hiddenAll: false };
  }

  function saveState(state) {
    const key = getStorageKey();
    if (!key) return;
    try {
      localStorage.setItem(key, JSON.stringify(state));
    } catch {
      // localStorage full or unavailable
    }
  }

  function getToasts() {
    const t = termsLower.value;
    return [
      {
        delay: 10000,
        title: 'Welcome to the demo!',
        body: 'Have a look around and play with the features — nothing here is permanent.',
      },
      {
        delay: 60000,
        title: 'Try changing the route colour',
        body: `Head to the ${t.course} tab, click the route settings icon, and pick a new colour.`,
      },
      {
        delay: 120000,
        title: `Unstaffed ${t.checkpoints}`,
        body: `Uh oh! It looks like some ${t.checkpoints} don't have anyone assigned. See if you can find one and assign a ${t.person} to it.`,
      },
      {
        delay: 120000,
        title: `Get a ${t.person}'s unique link`,
        body: `Open a ${t.person} and find their unique link — it's what they'll use on the day. Try it out for yourself!`,
      },
    ];
  }

  function scheduleNext(index) {
    if (stopped.value) return;
    const toasts = getToasts();
    if (index >= toasts.length) return;

    const toast = toasts[index];
    const timer = setTimeout(() => {
      if (stopped.value) return;
      currentToast.value = {
        title: toast.title,
        body: toast.body,
        index,
      };
    }, toast.delay);
    timers.push(timer);
  }

  function dismiss() {
    if (!currentToast.value) return;
    const nextIndex = currentToast.value.index + 1;
    currentToast.value = null;
    saveState({ nextIndex, hiddenAll: false });
    scheduleNext(nextIndex);
  }

  function hideAll() {
    stopped.value = true;
    const nextIndex = currentToast.value ? currentToast.value.index + 1 : TOAST_COUNT;
    currentToast.value = null;
    saveState({ nextIndex: TOAST_COUNT, hiddenAll: true });
    timers.forEach(clearTimeout);
    timers.length = 0;
  }

  function start() {
    const state = loadState();
    if (state.hiddenAll || state.nextIndex >= TOAST_COUNT) return;

    stopped.value = false;
    currentToast.value = null;
    scheduleNext(state.nextIndex);
  }

  onUnmounted(() => {
    timers.forEach(clearTimeout);
    timers.length = 0;
  });

  return {
    currentToast,
    dismiss,
    hideAll,
    start,
  };
}

export default useDemoToasts;
