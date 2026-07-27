export type EnrollmentStatus = 'Enrolled' | 'Completed'
export type CertificateStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed'
export type WorkflowStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed'

export interface Course {
  id: string
  title: string
  description: string
  capacity: number
  remainingCapacity: number
  isActive: boolean
  enrollmentStatus: EnrollmentStatus | null
  completionId: string | null
}

export interface CompletionStatus {
  completionId: string
  courseId: string
  learnerId: string
  coreStatus: 'Completed'
  certificateStatus: CertificateStatus
  reportingStatus: WorkflowStatus
  notificationStatus: WorkflowStatus
  version: number
  certificateAvailable: boolean
}

export interface Diagnostics {
  pendingOutboxCount: number
  recentOutboxFailures: Array<{
    eventId: string
    attempts: number
    error: string
    nextAttemptAt: string | null
  }>
  recentConsumerFailures: Array<{
    eventId: string
    completionId: string
    consumer: string
    attempts: number
    error: string
    lastFailedAt: string
  }>
}

