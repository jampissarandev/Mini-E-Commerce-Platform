import { describe, expect, it } from 'vitest'
import { cn } from './utils'

describe('cn()', () => {
  it('merges multiple class names into a single space-separated string', () => {
    expect(cn('foo', 'bar')).toBe('foo bar')
  })

  it('drops falsy values (null, undefined, false, empty string)', () => {
    expect(cn('foo', null, 'bar', undefined, false, '')).toBe('foo bar')
  })

  it('returns an empty string when called with no arguments', () => {
    expect(cn()).toBe('')
  })

  it('keeps the last value when two classes conflict (tailwind-merge behavior)', () => {
    // px-4 and px-2 both apply horizontal padding. tailwind-merge drops the
    // earlier one so the later class wins — this is the whole point of cn().
    expect(cn('px-4', 'px-2')).toBe('px-2')
  })

  it('preserves non-conflicting tailwind classes', () => {
    expect(cn('text-sm', 'font-bold')).toBe('text-sm font-bold')
  })

  it('handles arrays of class names', () => {
    expect(cn(['foo', 'bar'], 'baz')).toBe('foo bar baz')
  })

  it('handles object-style class names from clsx', () => {
    expect(cn('foo', { bar: true, baz: false })).toBe('foo bar')
  })
})
