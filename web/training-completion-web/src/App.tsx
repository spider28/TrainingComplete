import { useEffect, useState, type ReactNode } from 'react'
import { CompletionStatusPage } from './pages/CompletionStatusPage'
import { CoursesPage } from './pages/CoursesPage'
import { DiagnosticsPage } from './pages/DiagnosticsPage'

export function App() {
  const [path, setPath] = useState(window.location.pathname)
  useEffect(() => {
    const onPopState = () => setPath(window.location.pathname)
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  const navigate = (target: string) => {
    window.history.pushState({}, '', target)
    setPath(target)
  }

  const completionMatch = /^\/completions\/([^/]+)$/.exec(path)
  const page = completionMatch ? (
    <CompletionStatusPage completionId={completionMatch[1]} navigate={navigate} />
  ) : path === '/diagnostics' ? (
    <DiagnosticsPage />
  ) : (
    <CoursesPage navigate={navigate} />
  )

  return (
    <>
      <header className="site-header">
        <a className="brand" href="/courses" onClick={(event) => {
          event.preventDefault()
          navigate('/courses')
        }}>
          <span className="brand__mark">TC</span>
          <span>Training Completion</span>
        </a>
        <nav>
          <NavItem path="/courses" currentPath={path} navigate={navigate}>Courses</NavItem>
          <NavItem path="/diagnostics" currentPath={path} navigate={navigate}>Diagnostics</NavItem>
        </nav>
      </header>
      <main>{page}</main>
    </>
  )
}

function NavItem({
  path,
  currentPath,
  navigate,
  children,
}: {
  path: string
  currentPath: string
  navigate: (path: string) => void
  children: ReactNode
}) {
  return (
    <a
      className={currentPath === path ? 'active' : undefined}
      href={path}
      onClick={(event) => {
        event.preventDefault()
        navigate(path)
      }}
    >
      {children}
    </a>
  )
}
