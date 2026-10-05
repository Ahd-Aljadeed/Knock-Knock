import { useEffect, useRef, useState } from 'react'
import { send, useMessage } from './bridge'
import type { AppState, Settings } from './types'
import { Desk } from './components/Desk'
import { Teach } from './components/Teach'
import { ActionsPanel } from './components/ActionsPanel'
import { Activity, General, Reliability } from './components/Panels'
import { Switch } from './components/controls'

const NAV = [
  { id: 'teach', label: 'Teach' },
  { id: 'actions', label: 'Actions' },
  { id: 'reliability', label: 'Reliability' },
  { id: 'activity', label: 'Activity' },
  { id: 'general', label: 'General' },
]

export function App() {
  const [state, setState] = useState<AppState | null>(null)
  const [saved, setSaved] = useState<'idle' | 'saving' | 'saved'>('idle')
  const [active, setActive] = useState('teach')
  const saveTimer = useRef<number>(0)
  const main = useRef<HTMLElement>(null)

  useMessage('state', (m) => {
    const { type, ...s } = m
    void type
    setState(s)
  })
  useMessage('activity', (m) => setState((s) => (s ? { ...s, activity: [...s.activity.slice(-59), m.entry] } : s)))
  useEffect(() => send({ type: 'ready' }), [])

  // Highlight the section in view.
  useEffect(() => {
    const root = main.current
    if (!root || !state) return
    const io = new IntersectionObserver(
      (es) => es.forEach((e) => e.isIntersecting && setActive(e.target.id)),
      { root, rootMargin: '-35% 0px -60% 0px' },
    )
    NAV.forEach((n) => { const el = document.getElementById(n.id); if (el) io.observe(el) })
    return () => io.disconnect()
  }, [!!state])

  if (!state) return <div className="boot"><span className="pulse live" /></div>

  const s = state.settings
  // Edits apply instantly in the UI and are saved shortly after.
  const patch = (p: Partial<Settings>) => {
    const next = { ...s, ...p }
    setState({ ...state, settings: next })
    setSaved('saving')
    clearTimeout(saveTimer.current)
    saveTimer.current = window.setTimeout(() => {
      send({ type: 'save', settings: next })
      setSaved('saved')
      window.setTimeout(() => setSaved('idle'), 1400)
    }, 350)
  }
  const setListening = (v: boolean) => {
    setState({ ...state, listening: v, settings: { ...s, Listening: v } })
    send({ type: 'listening', value: v })
  }
  const last = state.activity[state.activity.length - 1]

  return (
    <div className="shell">
      <aside className="rail">
        <div className="brand">
          <svg className="logo" viewBox="0 0 32 32" aria-hidden>
            <defs>
              <linearGradient id="lg" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0" stopColor="#ff7a45" />
                <stop offset="1" stopColor="#e23d5a" />
              </linearGradient>
            </defs>
            <rect width="32" height="32" rx="8" fill="url(#lg)" />
            <circle cx="11.5" cy="16" r="3.2" fill="#fff" />
            <path d="M15.5 11.4a7 7 0 0 1 0 9.2" stroke="#fff" strokeWidth="2.4" fill="none" strokeLinecap="round" />
            <path d="M19.4 8.2a11.5 11.5 0 0 1 0 15.6" stroke="#fff" strokeOpacity=".65" strokeWidth="2.4" fill="none" strokeLinecap="round" />
          </svg>
          <span className="brand-name">Knock<br />Knock</span>
        </div>
        <nav>
          {NAV.map((n, i) => (
            <a
              key={n.id}
              href={'#' + n.id}
              className={active === n.id ? 'active' : ''}
              onClick={(e) => { e.preventDefault(); document.getElementById(n.id)?.scrollIntoView({ behavior: 'smooth' }) }}
            >
              <span className="nav-num">0{i + 1}</span>
              {n.label}
            </a>
          ))}
        </nav>
        <div className="rail-foot">
          <div className="listen">
            <div>
              <div className="listen-title">{state.listening ? 'Listening' : s.Listening ? 'Paused while locked' : 'Mic off'}</div>
              <div className="listen-hint">{state.listening ? 'Mic in use' : 'Nothing is heard'}</div>
            </div>
            <Switch label="Listening" checked={s.Listening} onChange={setListening} />
          </div>
          <div className={'saved ' + saved} aria-live="polite">{saved === 'saving' ? 'Saving…' : saved === 'saved' ? 'Saved' : ''}</div>
        </div>
      </aside>

      <main ref={main}>
        <Desk listening={state.listening} last={last} learned={!!s.Profile} />
        <Teach profile={s.Profile} needed={state.calibrationKnocks} listening={state.listening} />
        <ActionsPanel bindings={s.Bindings} screens={state.screens} onChange={(Bindings) => patch({ Bindings })} />
        <Reliability s={s} patch={patch} />
        <Activity entries={state.activity} />
        <General s={s} patch={patch} startup={state.startup} version={state.version} />
      </main>
    </div>
  )
}
