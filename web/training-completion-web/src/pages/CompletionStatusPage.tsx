import { useQuery } from '@tanstack/react-query'
import { trainingApi } from '../api'
import { StatusBadge } from '../components/StatusBadge'
import { isTerminal } from '../status'

export function CompletionStatusPage({
  completionId,
  navigate,
}: {
  completionId: string
  navigate: (path: string) => void
}) {
  const completion = useQuery({
    queryKey: ['completion', completionId],
    queryFn: () => trainingApi.completion(completionId),
    enabled: Boolean(completionId),
    refetchInterval: (query) => {
      const data = query.state.data
      return data && isTerminal(data) ? false : 2500
    },
  })

  if (completion.isLoading) return <p className="notice">Loading workflow status…</p>
  if (completion.error) return <p className="notice notice--error">{completion.error.message}</p>
  if (!completion.data) return null

  const data = completion.data
  return (
    <section className="narrow">
      <p className="eyebrow">Completion {data.completionId}</p>
      <h1>Course completed</h1>
      <p>
        The core write is committed. Secondary work continues independently through
        Azure Service Bus.
      </p>
      <div className="workflow">
        <WorkflowRow label="Core transaction" status={data.coreStatus} />
        <WorkflowRow label="Certificate" status={data.certificateStatus} />
        <WorkflowRow label="Reporting" status={data.reportingStatus} />
        <WorkflowRow label="Notification" status={data.notificationStatus} />
      </div>
      <div className="page-actions">
        {data.certificateAvailable && (
          <a className="button" href={trainingApi.certificateUrl(data.completionId)}>
            Download certificate
          </a>
        )}
        <a
          className="button button--secondary"
          href="/courses"
          onClick={(event) => {
            event.preventDefault()
            navigate('/courses')
          }}
        >
          Back to courses
        </a>
      </div>
    </section>
  )
}

function WorkflowRow({ label, status }: { label: string; status: string }) {
  return (
    <div className="workflow__row">
      <span>{label}</span>
      <StatusBadge status={status} />
    </div>
  )
}
