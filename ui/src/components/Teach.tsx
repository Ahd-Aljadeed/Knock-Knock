import { useState } from 'react'
import { send, useLevel, useMessage } from '../bridge'
import type { KnockProfile, Quality } from '../types'
import { Section } from './controls'

type Phase = 'idle' | 'listening' | 'done'

const qualityCopy: Record<Quality, { title: string; body: string }> = {
  great: { title: 'Got it. That’s a clean knock.', body: 'Your knocks were very consistent. Other thumps on the desk will be ignored.' },
  good: { title: 'Learned, with a bit of variation.', body: 'It will work well. Retry if you notice missed knocks.' },
  inconsistent: { title: 'Those knocks varied a lot.', body: 'Try again knocking the same way each time: same spot, same knuckle, same strength.' },
}

export function Teach({ profile, needed, listening }: { profile: KnockProfile | null; needed: number; listening: boolean }) {
  const [phase, setPhase] = useState<Phase>('idle')
  const [count, setCount] = useState(0)
  const [result, setResult] = useState<{ profile: KnockProfile; quality: Quality } | null>(null)
  const level = useLevel()

  useMessage('calib.knock', (m) => setCount(m.count))
  useMessage('calib.done', (m) => {
    setResult({ profile: m.profile, quality: m.quality })
    setPhase('done')
  })

  const start = () => {
    setCount(0)
    setResult(null)
    setPhase('listening')
    send({ type: 'calibrate.start' })
  }
  const cancel = () => {
    send({ type: 'calibrate.cancel' })
    setPhase('idle')
  }
  const keep = () => {
    if (result) send({ type: 'profile.save', profile: result.profile })
    setPhase('idle')
  }

  return (
    <Section
      id="teach"
      kicker="01 · Teach"
      title="Teach it your knock"
      intro="Every desk and every laptop sounds different. Knock a few times so Knock Knock learns what yours sounds like, then everything else gets ignored."
    >
      <div className={'teach card phase-' + phase}>
        {phase === 'idle' && (
          <div className="teach-idle">
            <div className="teach-badge" data-learned={!!profile}>
              {profile ? 'Learned' : 'Not learned yet'}
            </div>
            <p className="teach-copy">
              {profile
                ? `Knock Knock knows your knock from ${profile.Samples} samples. Teach it again if you move to another desk.`
                : `You’ll knock ${needed} times, one at a time, about a second apart, wherever you normally will.`}
            </p>
            <div className="teach-actions">
              <button className="btn primary" onClick={start} disabled={!listening}>
                {profile ? 'Teach again' : 'Start teaching'}
              </button>
              {profile && (
                <button className="btn ghost" onClick={() => send({ type: 'profile.clear' })}>
                  Forget it
                </button>
              )}
            </div>
            {!listening && <p className="hint warn">Turn listening on first.</p>}
          </div>
        )}

        {phase === 'listening' && (
          <div className="teach-live">
            <p className="teach-prompt">
              {count === 0 ? 'Knock once on your desk.' : count < needed ? 'Again…' : 'Done!'}
              <span className="teach-sub">Separate knocks, about a second apart</span>
            </p>
            <div className="teach-slots">
              {Array.from({ length: needed }, (_, i) => (
                <span key={i} className={'slot' + (i < count ? ' hit' : '') + (i === count ? ' next' : '')}>
                  <span className="slot-core" />
                </span>
              ))}
            </div>
            <div className="meter" aria-label="Microphone level">
              <span style={{ width: Math.min(100, Math.max(2, (level / 35) * 100)) + '%' }} />
            </div>
            <button className="btn ghost" onClick={cancel}>Cancel</button>
          </div>
        )}

        {phase === 'done' && result && (
          <div className="teach-done">
            <div className={'quality q-' + result.quality}>{result.quality}</div>
            <h3>{qualityCopy[result.quality].title}</h3>
            <p className="teach-copy">{qualityCopy[result.quality].body}</p>
            <dl className="teach-stats">
              <div><dt>Tone spread</dt><dd>{result.profile.BrightStd.toFixed(2)}</dd></div>
              <div><dt>Ring time</dt><dd>{(result.profile.DecayMean * 10).toFixed(0)} ms</dd></div>
              <div><dt>Samples</dt><dd>{result.profile.Samples}</dd></div>
            </dl>
            <div className="teach-actions">
              <button className="btn primary" onClick={keep}>Use this knock</button>
              <button className="btn ghost" onClick={start}>Try again</button>
            </div>
          </div>
        )}
      </div>
    </Section>
  )
}
