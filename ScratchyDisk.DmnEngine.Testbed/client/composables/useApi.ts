/**
 * API client composable for the DMN Testbed backend.
 * All endpoints are relative — dev proxy handles forwarding to the .NET backend.
 */

export function useApi() {
  async function listFiles(): Promise<any[]> {
    return await $fetch('/api/dmn')
  }

  async function getXml(name: string): Promise<string> {
    return await $fetch(`/api/dmn/xml/${name}`, { responseType: 'text' })
  }

  async function getInfo(name: string): Promise<any> {
    return await $fetch(`/api/dmn/info/${name}`)
  }

  async function execute(name: string, decisionName: string, inputs: Record<string, any>): Promise<any> {
    return await $fetch(`/api/dmn/execute/${name}`, {
      method: 'POST',
      body: { decisionName, inputs }
    })
  }

  async function loadTests(name: string): Promise<any> {
    return await $fetch(`/api/dmn/tests/${name}`)
  }

  async function saveTests(name: string, suite: any): Promise<any> {
    return await $fetch(`/api/dmn/tests/${name}`, {
      method: 'PUT',
      body: suite
    })
  }

  async function runTests(name: string, testCaseIds?: string[]): Promise<any> {
    return await $fetch(`/api/dmn/tests/run/${name}`, {
      method: 'POST',
      body: { testCaseIds: testCaseIds || null }
    })
  }

  async function uploadDmn(name: string, file: File): Promise<any> {
    const formData = new FormData()
    formData.append('file', file)
    return await $fetch(`/api/dmn/upload/${name}`, {
      method: 'POST',
      body: formData
    })
  }

  async function batchTestCsv(name: string, decisionName: string, file: File): Promise<any> {
    const formData = new FormData()
    formData.append('file', file)
    return await $fetch(`/api/dmn/batch-test-csv/${name}?decisionName=${encodeURIComponent(decisionName)}`, {
      method: 'POST',
      body: formData
    })
  }

  async function importCsv(name: string, decisionName: string, file: File, mode: 'replace' | 'append' = 'append'): Promise<any> {
    const formData = new FormData()
    formData.append('file', file)
    return await $fetch(`/api/dmn/tests/import-csv/${name}?decisionName=${encodeURIComponent(decisionName)}&mode=${mode}`, {
      method: 'POST',
      body: formData
    })
  }

  return {
    listFiles,
    getXml,
    getInfo,
    execute,
    loadTests,
    saveTests,
    runTests,
    uploadDmn,
    batchTestCsv,
    importCsv
  }
}
