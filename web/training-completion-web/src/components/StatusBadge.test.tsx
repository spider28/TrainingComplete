import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatusBadge } from './StatusBadge'

describe('StatusBadge', () => {
  it('renders a status with a normalized class', () => {
    render(<StatusBadge status="Processing" />)
    expect(screen.getByText('Processing')).toHaveClass('status--processing')
  })
})
