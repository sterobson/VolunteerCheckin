import js from '@eslint/js';
import pluginVue from 'eslint-plugin-vue';
import eslintConfigPrettier from 'eslint-config-prettier';

export default [
  // Base JavaScript recommended rules
  js.configs.recommended,

  // Vue.js recommended rules (includes vue3-essential and vue3-strongly-recommended)
  ...pluginVue.configs['flat/recommended'],

  // Prettier config (disables formatting rules that conflict with Prettier)
  eslintConfigPrettier,

  // Custom configuration
  {
    files: ['**/*.{js,vue}'],
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: {
        // Browser globals
        window: 'readonly',
        document: 'readonly',
        alert: 'readonly',
        confirm: 'readonly',
        prompt: 'readonly',
        navigator: 'readonly',
        console: 'readonly',
        fetch: 'readonly',
        setTimeout: 'readonly',
        setInterval: 'readonly',
        clearTimeout: 'readonly',
        clearInterval: 'readonly',
        localStorage: 'readonly',
        sessionStorage: 'readonly',
        URL: 'readonly',
        URLSearchParams: 'readonly',
        FormData: 'readonly',
        Blob: 'readonly',
        File: 'readonly',
        FileReader: 'readonly',
        Image: 'readonly',
        Event: 'readonly',
        CustomEvent: 'readonly',
        AbortController: 'readonly',
        requestAnimationFrame: 'readonly',
        cancelAnimationFrame: 'readonly',
        MutationObserver: 'readonly',
        ResizeObserver: 'readonly',
        IntersectionObserver: 'readonly',
        requestIdleCallback: 'readonly',
        cancelIdleCallback: 'readonly',
        Notification: 'readonly',
        indexedDB: 'readonly',
        IDBKeyRange: 'readonly',
        // Node.js globals for config files
        process: 'readonly',
      },
    },
    rules: {
      // Code quality rules
      'no-unused-vars': ['warn', { argsIgnorePattern: '^_', varsIgnorePattern: '^_|^props$|^emit$|^terms$' }],
      'no-console': ['warn', { allow: ['warn', 'error', 'log'] }], // Allow console.log for now
      'no-debugger': 'warn',
      'no-duplicate-imports': 'error',
      'no-template-curly-in-string': 'warn',
      'prefer-const': 'warn',
      'no-var': 'error',
      eqeqeq: ['warn', 'smart'],

      // Vue-specific rules
      'vue/no-unused-vars': 'warn',
      'vue/no-unused-components': 'warn',
      'vue/require-default-prop': 'off', // Can be noisy
      'vue/multi-word-component-names': 'off', // Allow single-word component names
      'vue/attribute-hyphenation': ['warn', 'always'],
      'vue/v-on-event-hyphenation': ['warn', 'always'],
      'vue/prop-name-casing': ['warn', 'camelCase'],
      'vue/component-definition-name-casing': ['warn', 'PascalCase'],
      'vue/no-v-html': 'off', // Allow v-html (needed for SVG rendering)
      'vue/max-attributes-per-line': 'off', // Let Prettier handle this
      'vue/html-self-closing': 'off', // Let Prettier handle this
      'vue/singleline-html-element-content-newline': 'off', // Let Prettier handle this
      'vue/attributes-order': 'off', // Stylistic preference, not a code smell
    },
  },

  // Ignore patterns
  {
    ignores: ['dist/**', 'node_modules/**', '*.min.js'],
  },
];
