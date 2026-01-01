import { ref, computed } from 'vue';

// Reactive terminology settings - loaded once when event is fetched
const terminology = ref({
  people: 'Marshals',
  checkpoint: 'Checkpoints',
  area: 'Areas',
  checklist: 'Checklists',
});

// Singular/plural mappings for each term
const termMappings = {
  people: {
    'People': { singular: 'Person', plural: 'People' },
    'Marshals': { singular: 'Marshal', plural: 'Marshals' },
    'Volunteers': { singular: 'Volunteer', plural: 'Volunteers' },
    'Helpers': { singular: 'Helper', plural: 'Helpers' },
    'Staff': { singular: 'Staff member', plural: 'Staff' },
  },
  checkpoint: {
    'Checkpoints': { singular: 'Checkpoint', plural: 'Checkpoints' },
    'Stations': { singular: 'Station', plural: 'Stations' },
    'Locations': { singular: 'Location', plural: 'Locations' },
  },
  area: {
    'Areas': { singular: 'Area', plural: 'Areas' },
    'Regions': { singular: 'Region', plural: 'Regions' },
    'Zones': { singular: 'Zone', plural: 'Zones' },
  },
  checklist: {
    'Checklists': { singular: 'Checklist', plural: 'Checklists' },
    'Tasks': { singular: 'Task', plural: 'Tasks' },
  },
};

// Options for each term type (for dropdowns) - sorted alphabetically
export const terminologyOptions = {
  people: ['Helpers', 'Marshals', 'People', 'Staff', 'Volunteers'],
  checkpoint: ['Checkpoints', 'Locations', 'Stations'],
  area: ['Areas', 'Regions', 'Zones'],
  checklist: ['Checklists', 'Tasks'],
};

/**
 * Set terminology from event data
 * Call this once when the event is loaded or after saving
 */
export function setTerminology(event) {
  if (!event) return;

  terminology.value = {
    people: event.peopleTerm || 'Marshals',
    checkpoint: event.checkpointTerm || 'Checkpoints',
    area: event.areaTerm || 'Areas',
    checklist: event.checklistTerm || 'Checklists',
  };
}

/**
 * Get the current terminology settings
 */
export function getTerminologySettings() {
  return {
    peopleTerm: terminology.value.people,
    checkpointTerm: terminology.value.checkpoint,
    areaTerm: terminology.value.area,
    checklistTerm: terminology.value.checklist,
  };
}

// Case conversion helpers
const toTitleCase = (str) => str.charAt(0).toUpperCase() + str.slice(1).toLowerCase();
const toLowerCase = (str) => str.toLowerCase();
const toSentenceCase = (str) => str.charAt(0).toUpperCase() + str.slice(1).toLowerCase();

/**
 * Composable for using terminology in components
 */
export function useTerminology() {
  // Get singular form of a term
  const singular = (type) => {
    const term = terminology.value[type];
    const mapping = termMappings[type]?.[term];
    return mapping?.singular || term;
  };

  // Get plural form of a term
  const plural = (type) => {
    const term = terminology.value[type];
    const mapping = termMappings[type]?.[term];
    return mapping?.plural || term;
  };

  // Computed properties for easy access - Title Case (default)
  const terms = computed(() => ({
    // Singular forms
    person: singular('people'),
    checkpoint: singular('checkpoint'),
    area: singular('area'),
    checklist: singular('checklist'),
    // Plural forms
    people: plural('people'),
    checkpoints: plural('checkpoint'),
    areas: plural('area'),
    checklists: plural('checklist'),
  }));

  // Lowercase versions
  const termsLower = computed(() => ({
    // Singular forms
    person: toLowerCase(singular('people')),
    checkpoint: toLowerCase(singular('checkpoint')),
    area: toLowerCase(singular('area')),
    checklist: toLowerCase(singular('checklist')),
    // Plural forms
    people: toLowerCase(plural('people')),
    checkpoints: toLowerCase(plural('checkpoint')),
    areas: toLowerCase(plural('area')),
    checklists: toLowerCase(plural('checklist')),
  }));

  // Sentence case versions (first letter capitalized, rest lowercase)
  const termsSentence = computed(() => ({
    // Singular forms
    person: toSentenceCase(singular('people')),
    checkpoint: toSentenceCase(singular('checkpoint')),
    area: toSentenceCase(singular('area')),
    checklist: toSentenceCase(singular('checklist')),
    // Plural forms
    people: toSentenceCase(plural('people')),
    checkpoints: toSentenceCase(plural('checkpoint')),
    areas: toSentenceCase(plural('area')),
    checklists: toSentenceCase(plural('checklist')),
  }));

  // Tab labels with correct terminology
  const tabLabels = computed(() => ({
    course: 'Course',
    checkpoints: terms.value.checkpoints,
    areas: terms.value.areas,
    marshals: terms.value.people,
    checklists: terms.value.checklists,
    notes: 'Notes',
    details: 'Event details',
  }));

  // Helper to format count with correct singular/plural
  const formatCount = (type, count) => {
    if (count === 1) {
      return `1 ${singular(type)}`;
    }
    return `${count} ${plural(type)}`;
  };

  // Case conversion functions for dynamic use
  const lower = (type, isPlural = true) => {
    return toLowerCase(isPlural ? plural(type) : singular(type));
  };

  const title = (type, isPlural = true) => {
    return isPlural ? plural(type) : singular(type);
  };

  const sentence = (type, isPlural = true) => {
    return toSentenceCase(isPlural ? plural(type) : singular(type));
  };

  return {
    terms,
    termsLower,
    termsSentence,
    tabLabels,
    singular,
    plural,
    formatCount,
    lower,
    title,
    sentence,
    terminology, // Expose raw terminology for settings UI
  };
}

export default useTerminology;
