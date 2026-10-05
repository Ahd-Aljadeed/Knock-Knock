import { useEffect, useRef, useState } from 'react'
import { useLevel, useMessage } from '../bridge'
import type { ActivityEntry } from '../types'

// The hero: a top-down "desk" whose rings breathe with the mic level and ripple
// outwards whenever something sharp is heard. Shows the latest thing that happened.
export function Desk({ listening, last, learned }: { listening: boolean; last?: ActivityEntry; learned: boolean }) {
  const level = useLevel()
  const [ripples, setRipples] = useState<number[]>([])
  const lastSpike = useRef(0)
  const id = useRef(0)

  useEffect(() => {
    const now = performance.now()
    if (listening && level > 14 && now - lastSpike.current > 140) {
      lastSpike.current = now
      const r = ++id.current
      setRipples((rs) => [...rs.slice(-5), r])
      setTimeout(() => setRipples((rs) => rs.filter((x) => x !== r)), 1300)
    }
  }, [level, listening])

  // Flash the caption whenever a new entry arrives.
  const [fresh, setFresh] = useState(false)
  useMessage('activity', () => {
    setFresh(true)
    setTimeout(() => setFresh(false), 900)
  })

  const t = listening ? Math.min(1, Math.max(0, level / 30)) : 0
  return (
    <div className={'desk' + (listening ? '' : ' off')}>
      <div className="desk-grain" aria-hidden />
      <svg className="desk-rings" viewBox="-100 -100 200 200" aria-hidden>
        <defs>
          <radialGradient id="glow">
            <stop offset="0%" stopColor="var(--accent)" stopOpacity="0.55" />
            <stop offset="100%" stopColor="var(--accent)" stopOpacity="0" />
          </radialGradient>
        </defs>
        <circle r={34 + t * 30} fill="url(#glow)" />
        {[46, 64, 84].map((r, i) => (
          <circle key={r} r={r + t * (6 + i * 4)} className="ring" style={{ opacity: 0.18 + t * 0.5 - i * 0.04 }} />
        ))}
        {ripples.map((r) => (
          <circle key={r} r={14} className="ripple" />
        ))}
        <circle r={13 + t * 3} className="knuckle" />
      </svg>
      <div className="desk-copy">
        <div className="desk-status">
          <span className={'pulse' + (listening ? ' live' : '')} />
          {listening ? 'Listening for knocks' : 'Microphone off'}
        </div>
        <p className={'desk-last' + (fresh ? ' fresh' : '')}>
          {last ? last.Text : learned ? 'Knock on your desk to try it.' : 'Start by teaching Knock Knock your knock.'}
        </p>
        {last && <span className="desk-time">{last.Time}</span>}
      </div>
    </div>
  )
}
