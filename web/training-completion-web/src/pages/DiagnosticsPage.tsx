import { useQuery } from '@tanstack/react-query'
import { trainingApi } from '../api'

export function DiagnosticsPage() {
  const diagnostics = useQuery({
    queryKey: ['diagnostics'],
    queryFn: trainingApi.diagnostics,
    refetchInterval: 5000,
  })

  if (diagnostics.isLoading) return <p className="notice">Loading diagnostics…</p>
  if (diagnostics.error) {
    return <p className="notice notice--error">{diagnostics.error.message}</p>
  }

  const data = diagnostics.data!
  return (
    <section>
      <p className="eyebrow">Operational view · refreshes every 5 seconds</p>
      <h1>Diagnostics</h1>
      <div className="metric">
        <strong>{data.pendingOutboxCount}</strong>
        <span>pending outbox messages</span>
      </div>
      <FailureTable
        title="Outbox failures"
        rows={data.recentOutboxFailures.map((x) => ({
          key: x.eventId,
          source: 'publisher',
          attempts: x.attempts,
          error: x.error,
        }))}
      />
      <FailureTable
        title="Consumer failures"
        rows={data.recentConsumerFailures.map((x) => ({
          key: `${x.eventId}-${x.consumer}`,
          source: x.consumer,
          attempts: x.attempts,
          error: x.error,
        }))}
      />
    </section>
  )
}

function FailureTable({
  title,
  rows,
}: {
  title: string
  rows: Array<{ key: string; source: string; attempts: number; error: string }>
}) {
  return (
    <div className="table-wrap">
      <h2>{title}</h2>
      {rows.length === 0 ? (
        <p className="empty">No unresolved failures.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Source</th>
              <th>Attempts</th>
              <th>Sanitized error</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.key}>
                <td>{row.source}</td>
                <td>{row.attempts}</td>
                <td>{row.error}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

