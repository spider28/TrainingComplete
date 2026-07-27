import type { CompletionStatus } from './types'

export const isTerminal = (completion: CompletionStatus): boolean => {
  const certificateDone = ['Ready', 'Failed'].includes(completion.certificateStatus)
  const reportingDone = ['Completed', 'Failed'].includes(completion.reportingStatus)
  const notificationDone = ['Completed', 'Failed'].includes(completion.notificationStatus)
  return certificateDone && reportingDone && notificationDone
}

