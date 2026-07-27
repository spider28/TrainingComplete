import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { trainingApi } from '../api'
import { StatusBadge } from '../components/StatusBadge'

export function CoursesPage({ navigate }: { navigate: (path: string) => void }) {
  const queryClient = useQueryClient()
  const courses = useQuery({ queryKey: ['courses'], queryFn: trainingApi.courses })
  const enroll = useMutation({
    mutationFn: trainingApi.enroll,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['courses'] }),
  })
  const complete = useMutation({
    mutationFn: trainingApi.complete,
    onSuccess: (result) => navigate(`/completions/${result.completionId}`),
  })

  if (courses.isLoading) return <p className="notice">Loading courses…</p>
  if (courses.error) return <p className="notice notice--error">{courses.error.message}</p>

  return (
    <section>
      <header className="page-heading">
        <div>
          <p className="eyebrow">Demo learner · learner-1001</p>
          <h1>Available courses</h1>
          <p>Enroll, complete, and watch the event-driven workflow progress.</p>
        </div>
      </header>

      {(enroll.error || complete.error) && (
        <p className="notice notice--error">
          {(enroll.error ?? complete.error)?.message}
        </p>
      )}

      <div className="course-grid">
        {courses.data?.map((course) => (
          <article className="card course-card" key={course.id}>
            <div className="card__topline">
              <span className="course-id">{course.id}</span>
              {course.enrollmentStatus && <StatusBadge status={course.enrollmentStatus} />}
            </div>
            <h2>{course.title}</h2>
            <p>{course.description}</p>
            <p className="capacity">
              {course.remainingCapacity} of {course.capacity} seats remaining
            </p>
            <div className="card__actions">
              {!course.enrollmentStatus && (
                <button
                  onClick={() => enroll.mutate(course.id)}
                  disabled={!course.isActive || course.remainingCapacity === 0 || enroll.isPending}
                >
                  Enroll
                </button>
              )}
              {course.enrollmentStatus === 'Enrolled' && (
                <button onClick={() => complete.mutate(course.id)} disabled={complete.isPending}>
                  Complete course
                </button>
              )}
              {course.completionId && (
                <a
                  className="button button--secondary"
                  href={`/completions/${course.completionId}`}
                  onClick={(event) => {
                    event.preventDefault()
                    navigate(`/completions/${course.completionId}`)
                  }}
                >
                  View completion
                </a>
              )}
            </div>
          </article>
        ))}
      </div>
    </section>
  )
}
