import axios from 'axios'

const apiClient = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Request interceptor: attach JWT token
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Response interceptor: handle 401 by clearing auth state
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('accessToken')
      localStorage.removeItem('refreshToken')
      localStorage.removeItem('user')
      window.location.href = '/login'
    }
    return Promise.reject(error)
  }
)

/**
 * Download a file from an authenticated API endpoint.
 *
 * Uses `apiClient` so the Authorization header is injected automatically
 * by the request interceptor — no manual token handling required.
 *
 * @param url      API path relative to /api (e.g. '/departure-requests/export/csv')
 * @param filename Suggested filename for the downloaded file
 * @param params   Optional query parameters to append to the request
 */
export async function downloadBlob(
  url: string,
  filename: string,
  params?: Record<string, string>
): Promise<void> {
  const response = await apiClient.get(url, {
    responseType: 'blob',
    params,
  })
  const blob = new Blob([response.data], {
    type: (response.headers['content-type'] as string) ?? 'application/octet-stream',
  })
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  document.body.removeChild(anchor)
  URL.revokeObjectURL(objectUrl)
}

export default apiClient
