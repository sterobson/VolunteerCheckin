import { ref, computed } from 'vue';

// Reactive terminology settings - loaded once when event is fetched
const terminology = ref({
  people: 'Marshals',
  checkpoint: 'Checkpoints',
  area: 'Areas',
  checklist: 'Checklists',
  course: 'Course',
});

// Singular/plural mappings for each term
const termMappings = {
  people: {
    'People': { singular: 'Person', plural: 'People' },
    'Marshals': { singular: 'Marshal', plural: 'Marshals' },
    'Volunteers': { singular: 'Volunteer', plural: 'Volunteers' },
    'Helpers': { singular: 'Helper', plural: 'Helpers' },
    'Staff': { singular: 'Staff member', plural: 'Staff' },
    'Stewards': { singular: 'Steward', plural: 'Stewards' },
    'Team members': { singular: 'Team member', plural: 'Team members' },
  },
  checkpoint: {
    'Checkpoints': { singular: 'Checkpoint', plural: 'Checkpoints' },
    'Stations': { singular: 'Station', plural: 'Stations' },
    'Locations': { singular: 'Location', plural: 'Locations' },
    'Feed stations': { singular: 'Feed station', plural: 'Feed stations' },
    'Aid stations': { singular: 'Aid station', plural: 'Aid stations' },
    'Water stations': { singular: 'Water station', plural: 'Water stations' },
    // Dynamic term - resolved based on current people terminology
    'Person points': { singular: null, plural: null, dynamic: true },
  },
  area: {
    'Areas': { singular: 'Area', plural: 'Areas' },
    'Regions': { singular: 'Region', plural: 'Regions' },
    'Zones': { singular: 'Zone', plural: 'Zones' },
  },
  checklist: {
    'Checklists': { singular: 'Checklist', plural: 'Checklists' },
    'Tasks': { singular: 'Task', plural: 'Tasks' },
    'Jobs': { singular: 'Job', plural: 'Jobs' },
  },
  course: {
    'Course': { singular: 'Course', plural: 'Courses' },
    'Route': { singular: 'Route', plural: 'Routes' },
    'Track': { singular: 'Track', plural: 'Tracks' },
    'Trail': { singular: 'Trail', plural: 'Trails' },
    'Path': { singular: 'Path', plural: 'Paths' },
  },
};

// Options for each term type (for dropdowns) - sorted alphabetically
// Note: 'Person points' is a special dynamic option that uses the current people terminology
export const terminologyOptions = {
  people: ['Helpers', 'Marshals', 'People', 'Staff', 'Stewards', 'Team members', 'Volunteers'],
  checkpoint: ['Aid stations', 'Checkpoints', 'Feed stations', 'Locations', 'Person points', 'Stations', 'Water stations'],
  area: ['Areas', 'Regions', 'Zones'],
  checklist: ['Checklists', 'Jobs', 'Tasks'],
  course: ['Course', 'Path', 'Route', 'Track', 'Trail'],
};

/**
 * Get singular form of a terminology value
 * @param {string} type - The term type ('people', 'checkpoint', 'area', 'checklist', 'course')
 * @param {string} value - The plural/stored value (e.g. 'Staff', 'Checkpoints')
 * @param {string} [peopleTerm] - The current people term (for dynamic checkpoint terms like 'Person points')
 * @returns {string} The singular form
 */
export function getSingularTerm(type, value, peopleTerm = 'Marshals') {
  if (!value) return '';

  const mapping = termMappings[type]?.[value];

  // Handle dynamic 'Person points' term
  if (type === 'checkpoint' && value === 'Person points') {
    const peopleMapping = termMappings.people[peopleTerm];
    const personSingular = peopleMapping?.singular || 'Person';
    return `${personSingular} point`;
  }

  if (mapping?.singular) {
    return mapping.singular;
  }

  // Fallback: try to derive singular from plural
  if (value.endsWith('s') && !value.endsWith('ss')) {
    return value.slice(0, -1);
  }
  return value;
}

/**
 * Get plural form of a terminology value
 * @param {string} type - The term type ('people', 'checkpoint', 'area', 'checklist', 'course')
 * @param {string} value - The value (could be singular or plural stored form)
 * @param {string} [peopleTerm] - The current people term (for dynamic checkpoint terms like 'Person points')
 * @returns {string} The plural form
 */
export function getPluralTerm(type, value, peopleTerm = 'Marshals') {
  if (!value) return '';

  const mapping = termMappings[type]?.[value];

  // Handle dynamic 'Person points' term
  if (type === 'checkpoint' && value === 'Person points') {
    const peopleMapping = termMappings.people[peopleTerm];
    const personSingular = peopleMapping?.singular || 'Person';
    return `${personSingular} points`;
  }

  if (mapping?.plural) {
    return mapping.plural;
  }

  // Return as-is (assuming it's already plural)
  return value;
}

/**
 * Get the display label for a checkpoint option, resolving dynamic terms
 * @param {string} option - The checkpoint option value
 * @param {string} peopleTerm - The current people terminology
 * @returns {string} The display label
 */
export function getCheckpointOptionLabel(option, peopleTerm) {
  if (option === 'Person points') {
    // Get the singular form of the people term for the label
    const peopleMapping = termMappings.people[peopleTerm];
    const personSingular = peopleMapping?.singular || 'Person';
    return `${personSingular} points`;
  }
  return option;
}

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
    course: event.courseTerm || 'Course',
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
    courseTerm: terminology.value.course,
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

    // Handle dynamic "Person points" term for checkpoints
    if (type === 'checkpoint' && term === 'Person points') {
      const peopleTerm = terminology.value.people;
      const peopleMapping = termMappings.people[peopleTerm];
      const personSingular = peopleMapping?.singular || 'Person';
      return `${personSingular} point`;
    }

    return mapping?.singular || term;
  };

  // Get plural form of a term
  const plural = (type) => {
    const term = terminology.value[type];
    const mapping = termMappings[type]?.[term];

    // Handle dynamic "Person points" term for checkpoints
    if (type === 'checkpoint' && term === 'Person points') {
      const peopleTerm = terminology.value.people;
      const peopleMapping = termMappings.people[peopleTerm];
      const personSingular = peopleMapping?.singular || 'Person';
      return `${personSingular} points`;
    }

    return mapping?.plural || term;
  };

  // Computed properties for easy access - Title Case (default)
  const terms = computed(() => ({
    // Singular forms
    person: singular('people'),
    checkpoint: singular('checkpoint'),
    area: singular('area'),
    checklist: singular('checklist'),
    course: singular('course'),
    // Plural forms
    people: plural('people'),
    checkpoints: plural('checkpoint'),
    areas: plural('area'),
    checklists: plural('checklist'),
    courses: plural('course'),
  }));

  // Lowercase versions
  const termsLower = computed(() => ({
    // Singular forms
    person: toLowerCase(singular('people')),
    checkpoint: toLowerCase(singular('checkpoint')),
    area: toLowerCase(singular('area')),
    checklist: toLowerCase(singular('checklist')),
    course: toLowerCase(singular('course')),
    // Plural forms
    people: toLowerCase(plural('people')),
    checkpoints: toLowerCase(plural('checkpoint')),
    areas: toLowerCase(plural('area')),
    checklists: toLowerCase(plural('checklist')),
    courses: toLowerCase(plural('course')),
  }));

  // Sentence case versions (first letter capitalized, rest lowercase)
  const termsSentence = computed(() => ({
    // Singular forms
    person: toSentenceCase(singular('people')),
    checkpoint: toSentenceCase(singular('checkpoint')),
    area: toSentenceCase(singular('area')),
    checklist: toSentenceCase(singular('checklist')),
    course: toSentenceCase(singular('course')),
    // Plural forms
    people: toSentenceCase(plural('people')),
    checkpoints: toSentenceCase(plural('checkpoint')),
    areas: toSentenceCase(plural('area')),
    checklists: toSentenceCase(plural('checklist')),
    courses: toSentenceCase(plural('course')),
  }));

  // Tab labels with correct terminology
  const tabLabels = computed(() => ({
    course: terms.value.course,
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
