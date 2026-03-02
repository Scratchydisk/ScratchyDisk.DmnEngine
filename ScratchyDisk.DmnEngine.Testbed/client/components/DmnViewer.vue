<template>
  <div class="dmn-viewer">
    <div class="viewer-toolbar">
      <div class="view-tabs">
        <button
          v-for="view in availableViews"
          :key="view.id"
          :class="['tab-btn', { active: activeViewId === view.id }]"
          @click="switchView(view)"
          :title="view.name"
        >
          <span class="tab-type-icon">{{ viewTypeIcon(view.type) }}</span>
          {{ view.name }}
        </button>
      </div>
    </div>

    <ClientOnly>
      <div ref="containerRef" class="dmn-container"></div>
      <template #fallback>
        <div class="dmn-loading">
          <div class="loading-spinner"></div>
          <span>Loading DMN viewer...</span>
        </div>
      </template>
    </ClientOnly>

    <div v-if="renderError" class="render-error">
      {{ renderError }}
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'

const props = defineProps({
  dmnXml: {
    type: String,
    required: true
  },
  highlightedRules: {
    type: Array,
    default: () => []
  }
})

const emit = defineEmits(['views-ready'])

const containerRef = ref(null)
const availableViews = ref([])
const activeViewId = ref(null)
const renderError = ref(null)
let viewerInstance = null

function viewTypeIcon(type) {
  switch (type) {
    case 'drd': return '\u25C8'
    case 'decisionTable': return '\u2637'
    case 'literalExpression': return '\u2261'
    default: return '\u25A0'
  }
}

async function initViewer() {
  if (!containerRef.value || !props.dmnXml) return

  destroyViewer()
  renderError.value = null

  await nextTick()

  try {
    const { default: DmnNavigatedViewer } = await import('dmn-js/lib/NavigatedViewer')

    viewerInstance = new DmnNavigatedViewer({
      container: containerRef.value
    })

    const result = await viewerInstance.importXML(props.dmnXml)

    if (result.warnings && result.warnings.length) {
      console.warn('DMN import warnings:', result.warnings)
    }

    // Build the view tab list from discovered views
    const views = viewerInstance.getViews()
    availableViews.value = views.map(v => ({
      id: v.element?.id || v.id,
      name: v.element?.name || v.element?.id || v.type,
      type: v.type,
      _raw: v
    }))

    // Track which view was auto-opened
    const active = viewerInstance.getActiveView()
    if (active) {
      activeViewId.value = active.element?.id || active.id
    }

    if (props.highlightedRules.length > 0) {
      applyRuleHighlighting()
    }

    emit('views-ready', availableViews.value)
  } catch (err) {
    console.error('Failed to render DMN:', err)
    renderError.value = `Failed to render DMN: ${err.message}`
  }
}

async function switchView(view) {
  if (!viewerInstance || activeViewId.value === view.id) return

  try {
    await viewerInstance.open(view._raw)
    activeViewId.value = view.id

    if (view.type === 'decisionTable' && props.highlightedRules.length > 0) {
      await nextTick()
      applyRuleHighlighting()
    }
  } catch (err) {
    console.error('Failed to switch DMN view:', err)
  }
}

function applyRuleHighlighting() {
  if (!containerRef.value) return

  containerRef.value.querySelectorAll('.dmn-rule-highlight, .dmn-rule-failed').forEach(el => {
    el.classList.remove('dmn-rule-highlight', 'dmn-rule-failed')
  })

  props.highlightedRules.forEach(ruleIndex => {
    const rows = containerRef.value.querySelectorAll('.tjs-row')
    if (rows[ruleIndex]) {
      rows[ruleIndex].classList.add('dmn-rule-highlight')
    }
  })
}

function destroyViewer() {
  if (viewerInstance) {
    viewerInstance.destroy()
    viewerInstance = null
  }
  availableViews.value = []
  activeViewId.value = null
}

// Watch the container ref - <ClientOnly> delays rendering, so containerRef
// is null when onMounted fires. This watch triggers when the DOM element appears.
watch(containerRef, (el) => {
  if (el && props.dmnXml) {
    initViewer()
  }
})

watch(() => props.dmnXml, () => {
  initViewer()
})

watch(() => props.highlightedRules, () => {
  applyRuleHighlighting()
}, { deep: true })

defineExpose({
  switchToDecision(decisionId) {
    const view = availableViews.value.find(v => v.id === decisionId)
    if (view) switchView(view)
  },
  getActiveViewId() {
    return activeViewId.value
  }
})

onMounted(() => {
  initViewer()
})

onBeforeUnmount(() => {
  destroyViewer()
})
</script>

<style scoped>
.dmn-viewer {
  @apply w-full;
}

.viewer-toolbar {
  @apply flex items-center justify-between mb-3 gap-2;
}

.view-tabs {
  @apply flex gap-1 bg-gray-100 rounded-lg p-0.5 flex-wrap;
}

.tab-btn {
  @apply px-3 py-1.5 text-xs font-medium rounded-md transition-colors whitespace-nowrap;
  @apply text-gray-600 hover:text-gray-800 hover:bg-gray-50;
}

.tab-btn.active {
  @apply bg-white text-blue-700 shadow-sm;
}

.tab-type-icon {
  @apply mr-1 text-xs opacity-60;
}

.dmn-container {
  @apply w-full border border-gray-200 rounded-lg bg-white overflow-hidden;
  height: 500px;
}

.dmn-loading {
  @apply flex items-center justify-center gap-3 py-12 text-gray-500;
}

.loading-spinner {
  @apply w-5 h-5 border-2 border-gray-300 border-t-blue-600 rounded-full animate-spin;
}

.render-error {
  @apply mt-2 p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-xs;
}

/* dmn-js internal container needs explicit height */
:deep(.dmn-js-parent) {
  width: 100%;
  height: 100%;
}

/* Rule highlighting for test results */
:deep(.dmn-rule-highlight) {
  background-color: rgba(76, 175, 80, 0.15) !important;
}

:deep(.dmn-rule-failed) {
  background-color: rgba(244, 67, 54, 0.15) !important;
}
</style>

<style>
/* dmn-js global styles need to be unscoped */
@import 'dmn-js/dist/assets/dmn-js-shared.css';
@import 'dmn-js/dist/assets/dmn-js-drd.css';
@import 'dmn-js/dist/assets/dmn-js-decision-table.css';
@import 'dmn-js/dist/assets/dmn-font/css/dmn.css';
</style>
