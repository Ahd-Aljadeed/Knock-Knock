import type { ReactNode } from 'react'

export function Switch({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      className={'switch' + (checked ? ' on' : '')}
      onClick={() => onChange(!checked)}
    >
      <span className="switch-thumb" />
    </button>
  )
}

export function Segmented<T extends string | number>({
  value, options, onChange, label,
}: { value: T; options: { value: T; label: string }[]; onChange: (v: T) => void; label: string }) {
  return (
    <div className="segmented" role="radiogroup" aria-label={label}>
      {options.map((o) => (
        <button
          key={String(o.value)}
          type="button"
          role="radio"
          aria-checked={o.value === value}
          className={o.value === value ? 'active' : ''}
          onClick={() => onChange(o.value)}
        >
          {o.label}
        </button>
      ))}
    </div>
  )
}

// The knock count drawn as dots: the visual motif used everywhere.
export function KnockDots({ n, lit = n, size = 'md' }: { n: number; lit?: number; size?: 'sm' | 'md' | 'lg' }) {
  return (
    <span className={'dots dots-' + size} aria-label={n + ' knocks'}>
      {Array.from({ length: n }, (_, i) => (
        <span key={i} className={'dot' + (i < lit ? ' lit' : '')} style={{ animationDelay: i * 70 + 'ms' }} />
      ))}
    </span>
  )
}

export function KnockStepper({ value, onChange, taken }: { value: number; onChange: (v: number) => void; taken: (n: number) => boolean }) {
  const next = (dir: number) => {
    let v = value
    do v += dir
    while (v >= 2 && v <= 8 && taken(v))
    if (v >= 2 && v <= 8) onChange(v)
  }
  return (
    <div className="stepper">
      <button type="button" aria-label="Fewer knocks" onClick={() => next(-1)} disabled={value <= 2}>−</button>
      <div className="stepper-value">
        <KnockDots n={value} />
        <span className="stepper-num">{value}</span>
      </div>
      <button type="button" aria-label="More knocks" onClick={() => next(1)} disabled={value >= 8}>+</button>
    </div>
  )
}

export function Row({ title, hint, children }: { title: string; hint?: ReactNode; children: ReactNode }) {
  return (
    <div className="row">
      <div className="row-text">
        <div className="row-title">{title}</div>
        {hint && <div className="row-hint">{hint}</div>}
      </div>
      <div className="row-control">{children}</div>
    </div>
  )
}

export function Section({ id, kicker, title, intro, children }: { id: string; kicker: string; title: string; intro?: ReactNode; children: ReactNode }) {
  return (
    <section id={id} className="section">
      <header className="section-head">
        <span className="kicker">{kicker}</span>
        <h2>{title}</h2>
        {intro && <p className="intro">{intro}</p>}
      </header>
      {children}
    </section>
  )
}
