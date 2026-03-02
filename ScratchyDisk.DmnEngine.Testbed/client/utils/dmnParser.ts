/**
 * Client-side DMN XML parser utility.
 * Extracts input/output column definitions for auto-generating test input forms.
 * No network round-trip needed - parses directly in the browser using DOMParser.
 */

export interface DmnColumn {
  id: string
  label: string
  typeRef: string
  allowedValues: string[]
}

export interface DmnParseResult {
  decisionName: string | null
  hitPolicy: string
  ruleCount: number
  inputs: DmnColumn[]
  outputs: DmnColumn[]
}

/**
 * A single decision within a DRD, with full schema information.
 */
export interface DmnDecision {
  id: string
  name: string
  hitPolicy: string
  ruleCount: number
  inputs: DmnColumn[]
  outputs: DmnColumn[]
  /** IDs of upstream decisions this decision depends on */
  informationRequirements: string[]
}

/**
 * Complete parsed model containing all decisions from a DMN file.
 */
export interface DmnModelInfo {
  decisions: DmnDecision[]
}

/**
 * Parse DMN XML and extract schema information.
 */
export function parseDmnXml(xml: string): DmnParseResult {
  const parser = new DOMParser()
  const doc = parser.parseFromString(xml, 'application/xml')

  // Check for parsing errors
  const parseError = doc.querySelector('parsererror')
  if (parseError) {
    console.error('DMN XML parse error:', parseError.textContent)
    return emptyResult()
  }

  // Find the decision element (namespace-agnostic)
  const decision = doc.querySelector('decision')
  if (!decision) {
    return emptyResult()
  }

  const decisionName = decision.getAttribute('name') || decision.getAttribute('id') || null

  // Find the decision table
  const decisionTable = decision.querySelector('decisionTable')
  if (!decisionTable) {
    return {
      decisionName,
      hitPolicy: 'UNIQUE',
      ruleCount: 0,
      inputs: [],
      outputs: []
    }
  }

  const hitPolicy = decisionTable.getAttribute('hitPolicy') || 'UNIQUE'

  // Parse input columns
  const inputs: DmnColumn[] = Array.from(decisionTable.querySelectorAll(':scope > input')).map(input => {
    const inputExpression = input.querySelector('inputExpression')
    const inputValues = input.querySelector('inputValues')

    return {
      id: input.getAttribute('id') || '',
      label: input.getAttribute('label') || inputExpression?.getAttribute('label') || '',
      typeRef: inputExpression?.getAttribute('typeRef') || '',
      allowedValues: parseAllowedValues(inputValues)
    }
  })

  // Parse output columns
  const outputs: DmnColumn[] = Array.from(decisionTable.querySelectorAll(':scope > output')).map(output => {
    const outputValues = output.querySelector('outputValues')

    return {
      id: output.getAttribute('id') || '',
      label: output.getAttribute('label') || output.getAttribute('name') || '',
      typeRef: output.getAttribute('typeRef') || '',
      allowedValues: parseAllowedValues(outputValues)
    }
  })

  // Count rules
  const ruleCount = decisionTable.querySelectorAll(':scope > rule').length

  return {
    decisionName,
    hitPolicy,
    ruleCount,
    inputs,
    outputs
  }
}

/**
 * Get the appropriate HTML input type for a DMN type reference.
 */
export function getInputType(typeRef: string): 'number' | 'text' | 'checkbox' | 'date' {
  const normalised = typeRef.toLowerCase()
  if (['number', 'integer', 'long', 'double', 'float', 'decimal'].includes(normalised)) {
    return 'number'
  }
  if (normalised === 'boolean') {
    return 'checkbox'
  }
  if (['date', 'dateTime', 'time'].includes(normalised)) {
    return 'date'
  }
  return 'text'
}

/**
 * Parse DMN XML and extract ALL decisions with their schema information.
 * Use this for the Decision Laboratory where users need to select and test individual decisions.
 */
export function parseDmnModel(xml: string): DmnModelInfo {
  const parser = new DOMParser()
  const doc = parser.parseFromString(xml, 'application/xml')

  const parseError = doc.querySelector('parsererror')
  if (parseError) {
    console.error('DMN XML parse error:', parseError.textContent)
    return { decisions: [] }
  }

  const decisionElements = doc.querySelectorAll('decision')
  const decisions: DmnDecision[] = []

  decisionElements.forEach(decision => {
    const id = decision.getAttribute('id') || ''
    const name = decision.getAttribute('name') || id

    // Parse information requirements (upstream decision dependencies)
    const informationRequirements: string[] = []
    decision.querySelectorAll(':scope > informationRequirement').forEach(req => {
      const requiredDecision = req.querySelector('requiredDecision')
      if (requiredDecision) {
        const href = requiredDecision.getAttribute('href') || ''
        // href is like "#Decision_Eligibility" — strip the leading #
        informationRequirements.push(href.replace(/^#/, ''))
      }
    })

    const decisionTable = decision.querySelector('decisionTable')
    if (!decisionTable) {
      // Decision exists but has no table (e.g. literal expression) — include with empty schema
      decisions.push({
        id,
        name,
        hitPolicy: 'UNIQUE',
        ruleCount: 0,
        inputs: [],
        outputs: [],
        informationRequirements
      })
      return
    }

    const hitPolicy = decisionTable.getAttribute('hitPolicy') || 'UNIQUE'

    const inputs: DmnColumn[] = Array.from(decisionTable.querySelectorAll(':scope > input')).map(input => {
      const inputExpression = input.querySelector('inputExpression')
      const inputValues = input.querySelector('inputValues')
      return {
        id: input.getAttribute('id') || '',
        label: input.getAttribute('label') || inputExpression?.getAttribute('label') || '',
        typeRef: inputExpression?.getAttribute('typeRef') || '',
        allowedValues: parseAllowedValues(inputValues)
      }
    })

    const outputs: DmnColumn[] = Array.from(decisionTable.querySelectorAll(':scope > output')).map(output => {
      const outputValues = output.querySelector('outputValues')
      return {
        id: output.getAttribute('id') || '',
        label: output.getAttribute('label') || output.getAttribute('name') || '',
        typeRef: output.getAttribute('typeRef') || '',
        allowedValues: parseAllowedValues(outputValues)
      }
    })

    const ruleCount = decisionTable.querySelectorAll(':scope > rule').length

    decisions.push({
      id,
      name,
      hitPolicy,
      ruleCount,
      inputs,
      outputs,
      informationRequirements
    })
  })

  return { decisions }
}

function parseAllowedValues(element: Element | null): string[] {
  if (!element) return []

  const text = element.querySelector('text')?.textContent
  if (!text) return []

  return text.split(',')
    .map(v => v.trim().replace(/^"|"$/g, ''))
    .filter(v => v.length > 0)
}

function emptyResult(): DmnParseResult {
  return {
    decisionName: null,
    hitPolicy: 'UNIQUE',
    ruleCount: 0,
    inputs: [],
    outputs: []
  }
}
