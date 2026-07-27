import { describe, expect, it } from 'vitest'
import { isTerminal } from './status'
import type { CompletionStatus } from './types'

const completion: CompletionStatus = {
  completionId: '00000000-0000-0000-0000-000000000001',
  courseId: 'course-2001',
  learnerId: 'learner-1001',
  coreStatus: 'Completed',
  certificateStatus: 'Pending',
  reportingStatus: 'Pending',
  notificationStatus: 'Pending',
  version: 1,
  certificateAvailable: false,
}

describe('isTerminal', () => {
  it('keeps polling while work is pending', () => {
    expect(isTerminal(completion)).toBe(false)
  })

  it('stops when each consumer has a terminal status', () => {
    expect(
      isTerminal({
        ...completion,
        certificateStatus: 'Ready',
        reportingStatus: 'Completed',
        notificationStatus: 'Failed',
      }),
    ).toBe(true)
  })
})

