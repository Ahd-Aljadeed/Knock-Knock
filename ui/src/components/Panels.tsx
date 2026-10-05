import { browse, send } from '../bridge'
import type { ActivityEntry, Settings } from '../types'
import { Row, Section, Segmented, Switch } from './controls'

type Patch = (p: Partial<Settings>) => void

export function Reliability({ s, patch }: { s: Settings; patch: Patch }) {
  return (
    <Section
      id="reliability"
      kicker="03 · Reliability"
      title="Only real knocks"
      intro="Filters that keep chairs, typing and sitting down from taking screenshots. Every ignored knock shows up in Activity with the reason."
    >
      <div className="card rows">
        <Row title="Rhythm check" hint="Knocks must be evenly spaced and similarly strong. Bumps and sitting down usually aren’t.">
          <Switch label="Rhythm check" checked={s.RhythmCheck} onChange={(v) => patch({ RhythmCheck: v })} />
        </Row>
        <Row title="Ignore while typing" hint="Keys and touchpad clicks sound like knocks, so nothing fires within a second of using them.">
          <Switch label="Ignore while typing" checked={s.IgnoreWhileTyping} onChange={(v) => patch({ IgnoreWhileTyping: v })} />
        </Row>
        <Row title="Ignore when you’ve been away" hint="After this long without touching the keyboard or mouse, knocks are ignored until you do. Coming back to your desk won’t set it off.">
          <Segmented
            label="Away after"
            value={s.AwayMinutes}
            onChange={(v) => patch({ AwayMinutes: v })}
            options={[{ value: 0, label: 'Off' }, { value: 3, label: '3 min' }, { value: 5, label: '5 min' }, { value: 10, label: '10 min' }, { value: 15, label: '15 min' }]}
          />
        </Row>
        <Row title="Sensitivity" hint="Calm needs firmer knocks and a closer match to your learned knock. Eager catches soft knocks but may fire by mistake.">
          <Segmented
            label="Sensitivity"
            value={s.Sensitivity}
            onChange={(v) => patch({ Sensitivity: v })}
            options={[{ value: 0, label: 'Calm' }, { value: 1, label: 'Balanced' }, { value: 2, label: 'Eager' }]}
          />
        </Row>
      </div>
    </Section>
  )
}

export function Activity({ entries }: { entries: ActivityEntry[] }) {
  const list = [...entries].reverse()
  return (
    <Section id="activity" kicker="04 · Activity" title="What it heard" intro="The last few things Knock Knock reacted to, and why it ignored the rest. Audio is never recorded or stored.">
      <div className="card log">
        {list.length === 0 && <p className="empty">Nothing yet. Try knocking.</p>}
        <ul>
          {list.map((e, i) => (
            <li key={list.length - i} className={'log-' + e.Kind}>
              <time>{e.Time}</time>
              <span className="log-dot" aria-hidden />
              <span className="log-text">{e.Text}</span>
            </li>
          ))}
        </ul>
      </div>
    </Section>
  )
}

export function General({ s, patch, startup, version }: { s: Settings; patch: Patch; startup: boolean; version: string }) {
  return (
    <Section id="general" kicker="05 · General" title="Everything else">
      <div className="card rows">
        <Row title="Start with Windows" hint="Runs quietly in the tray when you sign in.">
          <Switch label="Start with Windows" checked={startup} onChange={(v) => send({ type: 'startup', value: v })} />
        </Row>
        <Row title="Screenshot folder" hint={<span className="path">{s.ScreenshotFolder}</span>}>
          <div className="btn-pair">
            <button className="btn small ghost" onClick={() => send({ type: 'openFolder' })}>Open</button>
            <button className="btn small" onClick={async () => { const p = await browse('folder'); if (p) patch({ ScreenshotFolder: p }) }}>Change…</button>
          </div>
        </Row>
        <Row title="Flash the screen" hint="A quick white flash over what was captured.">
          <Switch label="Flash" checked={s.Flash} onChange={(v) => patch({ Flash: v })} />
        </Row>
        <Row title="Play a sound">
          <Switch label="Sound" checked={s.Sound} onChange={(v) => patch({ Sound: v })} />
        </Row>
        <Row title="Copy screenshots to the clipboard">
          <Switch label="Clipboard" checked={s.Clipboard} onChange={(v) => patch({ Clipboard: v })} />
        </Row>
      </div>
      <footer className="about">
        <span>Knock Knock {version}</span>
        <span aria-hidden>·</span>
        <span>MIT License</span>
        <span aria-hidden>·</span>
        <a href="#" onClick={(e) => { e.preventDefault(); send({ type: 'openUrl', url: 'https://github.com/Ahd-Aljadeed/knock-knock' }) }}>
          Source on GitHub
        </a>
      </footer>
    </Section>
  )
}
