import { useState, type KeyboardEvent } from 'react'
import { browse, send } from '../bridge'
import type { ActionType, Binding, ScreenInfo } from '../types'
import { KnockStepper, Section, Segmented } from './controls'

const ACTIONS: { value: ActionType; label: string; blurb: string }[] = [
  { value: 'screenshot', label: 'Take a screenshot', blurb: 'Saved as PNG and copied to the clipboard' },
  { value: 'open', label: 'Open an app, file or website', blurb: 'Anything you could double-click' },
  { value: 'media', label: 'Media control', blurb: 'Play/pause, skip, volume' },
  { value: 'hotkey', label: 'Press a keyboard shortcut', blurb: 'Sends the keys to the active window' },
  { value: 'lock', label: 'Lock the PC', blurb: 'Same as Windows + L' },
  { value: 'command', label: 'Run a command', blurb: 'Runs silently with cmd.exe' },
]

const MEDIA = [
  { value: 'playpause', label: 'Play / pause' },
  { value: 'next', label: 'Next track' },
  { value: 'prev', label: 'Previous track' },
  { value: 'mute', label: 'Mute' },
  { value: 'volup', label: 'Volume up' },
  { value: 'voldown', label: 'Volume down' },
]

const DEFAULT_ARG: Record<ActionType, string | null> = {
  screenshot: 'all', open: '', media: 'playpause', hotkey: '', lock: null, command: '',
}

export function ActionsPanel({ bindings, screens, onChange }: { bindings: Binding[]; screens: ScreenInfo[]; onChange: (b: Binding[]) => void }) {
  const sorted = [...bindings].sort((a, b) => a.Knocks - b.Knocks)
  const update = (i: number, patch: Partial<Binding>) => onChange(sorted.map((b, j) => (j === i ? { ...b, ...patch } : b)))
  const remove = (i: number) => onChange(sorted.filter((_, j) => j !== i))
  const free = [2, 3, 4, 5, 6, 7, 8].find((n) => !sorted.some((b) => b.Knocks === n))
  const max = Math.max(2, ...sorted.map((b) => b.Knocks))

  return (
    <Section
      id="actions"
      kicker="02 · Actions"
      title="What each knock does"
      intro={
        <>
          Pick an action, then how many knocks trigger it.
          {max > 2 && <> With up to {max} knocks set, Knock Knock waits about half a second after your last knock to be sure you’re done.</>}
        </>
      }
    >
      <ol className="bindings">
        {sorted.map((b, i) => (
          <li key={b.Knocks + ':' + i} className="binding card" style={{ animationDelay: i * 60 + 'ms' }}>
            <KnockStepper value={b.Knocks} onChange={(v) => update(i, { Knocks: v })} taken={(n) => n !== b.Knocks && sorted.some((x) => x.Knocks === n)} />
            <div className="binding-body">
              <label className="select">
                <span className="sr-only">Action</span>
                <select value={b.Action} onChange={(e) => {
                  const a = e.target.value as ActionType
                  update(i, { Action: a, Arg: DEFAULT_ARG[a] })
                }}>
                  {ACTIONS.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
                </select>
              </label>
              <ArgEditor binding={b} screens={screens} onChange={(Arg) => update(i, { Arg })} />
            </div>
            <div className="binding-tools">
              <button className="btn small" onClick={() => send({ type: 'test', binding: b })} title="Run it now">Test</button>
              <button className="icon-btn" onClick={() => remove(i)} aria-label={`Remove ${b.Knocks}-knock action`} title="Remove">
                <svg viewBox="0 0 16 16" aria-hidden><path d="M4 4l8 8M12 4l-8 8" /></svg>
              </button>
            </div>
          </li>
        ))}
      </ol>
      {sorted.length === 0 && <p className="empty">No actions yet. Knocks will be heard but nothing will happen.</p>}
      {free && (
        <button className="btn add" onClick={() => onChange([...sorted, { Knocks: free, Action: 'screenshot', Arg: 'all' }])}>
          <span aria-hidden>+</span> Add an action for {free} knocks
        </button>
      )}
    </Section>
  )
}

function ArgEditor({ binding, screens, onChange }: { binding: Binding; screens: ScreenInfo[]; onChange: (v: string | null) => void }) {
  const blurb = ACTIONS.find((a) => a.value === binding.Action)?.blurb
  switch (binding.Action) {
    case 'screenshot':
      if (screens.length < 2) return <p className="arg-note">Captures your whole screen. {blurb}.</p>
      return (
        <Segmented
          label="Which screen"
          value={binding.Arg ?? 'all'}
          onChange={onChange}
          options={[
            ...screens.map((s) => ({ value: String(s.n), label: `Screen ${s.n} · ${s.width}×${s.height}` })),
            { value: 'all', label: 'All screens' },
          ]}
        />
      )
    case 'open':
      return (
        <div className="arg-line">
          <input className="input" value={binding.Arg ?? ''} placeholder="C:\path\to\app.exe or https://…" onChange={(e) => onChange(e.target.value)} spellCheck={false} />
          <button className="btn small ghost" onClick={async () => { const p = await browse('file'); if (p) onChange(p) }}>Browse…</button>
        </div>
      )
    case 'media':
      return <Segmented label="Media key" value={binding.Arg ?? 'playpause'} onChange={onChange} options={MEDIA} />
    case 'hotkey':
      return <HotkeyRecorder value={binding.Arg ?? ''} onChange={onChange} />
    case 'command':
      return <input className="input mono" value={binding.Arg ?? ''} placeholder="e.g. shutdown /h" onChange={(e) => onChange(e.target.value)} spellCheck={false} />
    default:
      return <p className="arg-note">{blurb}.</p>
  }
}

// Records a combination like Ctrl+Shift+S using the names the app's key parser understands.
function HotkeyRecorder({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const [recording, setRecording] = useState(false)
  const onKey = (e: KeyboardEvent) => {
    e.preventDefault()
    const key = keyName(e.code)
    if (!key) return // a modifier on its own: keep waiting
    const parts = [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Win', key].filter(Boolean)
    onChange(parts.join('+'))
    setRecording(false)
    ;(e.target as HTMLElement).blur()
  }
  return (
    <button
      type="button"
      className={'hotkey' + (recording ? ' recording' : '')}
      onFocus={() => setRecording(true)}
      onBlur={() => setRecording(false)}
      onKeyDown={onKey}
    >
      {recording ? 'Press the keys…' : value ? value.split('+').map((k) => <kbd key={k}>{k}</kbd>) : 'Click, then press a shortcut'}
    </button>
  )
}

function keyName(code: string): string | null {
  if (/^(Control|Shift|Alt|Meta|OS)/.test(code)) return null
  if (code.startsWith('Key')) return code.slice(3)
  if (code.startsWith('Digit')) return code.slice(5)
  if (code.startsWith('Arrow')) return code.slice(5)
  if (code.startsWith('Numpad')) return /^\d$/.test(code.slice(6)) ? 'NumPad' + code.slice(6) : code.slice(6) // Add, Subtract, Enter…
  const map: Record<string, string> = { Escape: 'Escape', Backspace: 'Back', ContextMenu: 'Apps', Backquote: 'Oemtilde', Minus: 'OemMinus', Equal: 'Oemplus', Comma: 'Oemcomma', Period: 'OemPeriod', Slash: 'OemQuestion', BracketLeft: 'OemOpenBrackets', BracketRight: 'OemCloseBrackets', Semicolon: 'OemSemicolon', Quote: 'OemQuotes', Backslash: 'OemPipe' }
  return map[code] ?? code // F1-F24, Space, Enter, Tab, Home, End, PageUp, PageDown, Insert, Delete, PrintScreen…
}
