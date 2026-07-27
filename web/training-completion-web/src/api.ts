import type { CompletionStatus, Course, Diagnostics } from './types'

export const apiBaseUrl =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ??
  'http://localhost:5000'

interface ProblemDetails {
  title?: string
  detail?: string
  code?: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    throw new Error(problem.detail ?? problem.title ?? `Request failed (${response.status})`)
  }

  return (await response.json()) as T
}

export const learnerId = 'learner-1001'

export const trainingApi = {
  courses: () =>
    request<Course[]>(`/api/courses?learnerId=${encodeURIComponent(learnerId)}`),

  enroll: (courseId: string) =>
    request(`/api/courses/${encodeURIComponent(courseId)}/enrollments`, {
      method: 'POST',
      body: JSON.stringify({ learnerId }),
    }),

  complete: (courseId: string) =>
    request<CompletionStatus>('/api/course-completions', {
      method: 'POST',
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({ learnerId, courseId }),
    }),

  completion: (completionId: string) =>
    request<CompletionStatus>(
      `/api/course-completions/${encodeURIComponent(completionId)}`,
    ),

  diagnostics: () => request<Diagnostics>('/api/admin/diagnostics'),

  certificateUrl: (completionId: string) =>
    `${apiBaseUrl}/api/course-completions/${encodeURIComponent(completionId)}/certificate`,
}

