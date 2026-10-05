import { useEffect, useRef, useState } from 'react'
import type { AppState, Incoming } from './types'

// Talks to the C# host through WebView2 messages. In a plain browser (npm run dev)
// a small mock stands in, so the UI can be worked on without the app.

type Listener = (msg: Incoming) => void
const listeners = new Set<Listener>()
const host = (window as unknown as { chrome?: { webview?: WebViewHost } }).chrome?.webview

interface WebViewHost {
  postMessage(message: string): void
  addEventListener(type: 'message', fn: (e: { data: Incoming }) => void): void
}

function dispatch(msg: Incoming) {
  listeners.forEach((l) => l(msg))
}

if (host) host.addEventListener('message', (e) => dispatch(e.data))

export function send(msg: Record<string, unknown>) {
  if (host) host.postMessage(JSON.stringify(msg))
  else mock(msg)
}

export function subscribe(fn: Listener) {
  listeners.add(fn)
  return () => void listeners.delete(fn)
}

export function useMessage<T extends Incoming['type']>(type: T, fn: (msg: Extract<Incoming, { type: T }>) => void) {
  const ref = useRef(fn)
  ref.current = fn
  useEffect(() => subscribe((m) => m.type === type && ref.current(m as Extract<Incoming, { type: T }>)), [type])
}

// Current mic level in dB above the noise floor (updated ~15x per second).
let level = 0
subscribe((m) => {
  if (m.type === 'level') level = m.db
})
export function useLevel(): number {
  const [v, setV] = useState(0)
  useEffect(() => {
    let raf = 0
    const tick = () => {
      setV((prev) => prev + (level - prev) * 0.35)
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [])
  return v
}

let browseId = 0
export function browse(kind: 'file' | 'folder'): Promise<string | null> {
  const id = ++browseId
  return new Promise((resolve) => {
    const off = subscribe((m) => {
      if (m.type === 'browse.result' && m.id === id) {
        off()
        resolve(m.path)
      }
    })
    send({ type: 'browse', kind, id })
  })
}

// ---- Browser mock ----

function mock(msg: Record<string, unknown>) {
  const now = () => new Date().toTimeString().slice(0, 8)
  setTimeout(() => {
    switch (msg.type) {
      case 'ready': {
        const state: AppState = {
          settings: {
            Version: 1, Listening: true, Sensitivity: 1, RhythmCheck: true, IgnoreWhileTyping: true, AwayMinutes: 3,
            Flash: true, Sound: true, Clipboard: true, ScreenshotFolder: 'C:\\Users\\you\\Pictures\\Screenshots', Profile: null,
            Bindings: [
              { Knocks: 2, Action: 'screenshot', Arg: '1' },
              { Knocks: 3, Action: 'screenshot', Arg: '2' },
              { Knocks: 4, Action: 'screenshot', Arg: 'all' },
            ],
          },
          startup: true, listening: true, version: '1.0.0', calibrationKnocks: 6,
          screens: [
            { n: 1, width: 1366, height: 768, primary: true },
            { n: 2, width: 1920, height: 1080, primary: false },
          ],
          activity: [
            { Time: '16:02:11', Text: '2 knocks: Screenshot of screen 1', Kind: 'ok' },
            { Time: '16:05:40', Text: '2 knocks ignored: you were typing or using the mouse', Kind: 'reject' },
            { Time: '16:07:02', Text: 'Ignored a sound: rings too long or too short for your knock', Kind: 'reject' },
          ],
        }
        dispatch({ type: 'state', ...state })
        setInterval(() => dispatch({ type: 'level', db: Math.random() < 0.04 ? 18 + Math.random() * 12 : Math.random() * 3 }), 66)
        break
      }
      case 'calibrate.start': {
        let n = 0
        const t = setInterval(() => {
          n++
          dispatch({ type: 'level', db: 26 })
          dispatch({ type: 'calib.knock', count: n })
          if (n === 6) {
            clearInterval(t)
            dispatch({ type: 'calib.done', quality: 'great', profile: { BrightMean: -1.2, BrightStd: 0.12, DecayMean: 6, DecayStd: 1.1, LoudMin: 140, Samples: 6 } })
          }
        }, 900)
        break
      }
      case 'test':
        dispatch({ type: 'activity', entry: { Time: now(), Text: 'Test: ' + JSON.stringify(msg.binding), Kind: 'ok' } })
        break
      case 'browse':
        dispatch({ type: 'browse.result', id: msg.id as number, path: msg.kind === 'folder' ? 'D:\\Shots' : 'C:\\Windows\\notepad.exe' })
        break
    }
  }, 30)
}
