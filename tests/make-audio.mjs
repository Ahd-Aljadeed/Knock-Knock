// Generates synthetic 16 kHz test recordings into tests/out/ for run-tests.ps1.
// Usage: node tests/make-audio.mjs
import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const R = 16000
const out = join(dirname(fileURLToPath(import.meta.url)), 'out')
mkdirSync(out, { recursive: true })

function track(seconds, build) {
  const s = new Float32Array(R * seconds)
  let seed = 7
  const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647) * 2 - 1
  for (let i = 0; i < s.length; i++) s[i] = rnd() * 0.003 // room noise
  // A knuckle knock: bright, rings ~60 ms. Tone/decay vary per kind.
  const knock = (t, a = 0.4, kind = 'knuckle') => {
    const f = kind === 'knuckle' ? 900 : 70     // a chair bump is a low thud
    const d = kind === 'knuckle' ? 90 : 14      // ...that rings much longer
    const o = Math.floor(t * R)
    for (let i = 0; i < R * 0.25 && o + i < s.length; i++) {
      const x = i / R
      s[o + i] += a * Math.exp(-x * d) * (Math.sin(2 * Math.PI * f * x) + (kind === 'knuckle' ? 0.6 * rnd() : 0.05 * rnd()))
    }
  }
  const hum = (t, len) => {
    const o = Math.floor(t * R)
    for (let i = 0; i < R * len; i++) { const x = i / R; s[o + i] += 0.2 * Math.min(1, x * 40) * Math.sin(2 * Math.PI * 220 * x) }
  }
  build(knock, hum)
  return s
}

function wav(name, s) {
  const b = Buffer.alloc(44 + s.length * 2)
  b.write('RIFF', 0); b.writeUInt32LE(36 + s.length * 2, 4); b.write('WAVEfmt ', 8); b.writeUInt32LE(16, 16)
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(R, 24); b.writeUInt32LE(R * 2, 28)
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write('data', 36); b.writeUInt32LE(s.length * 2, 40)
  for (let i = 0; i < s.length; i++) b.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(s[i] * 32767))), 44 + i * 2)
  writeFileSync(join(out, name), b)
}

// Basic: single knock, a double, talking, a slow pair, a soft double.
wav('basic.wav', track(14, (knock, hum) => {
  knock(1.0)
  knock(3.0); knock(3.25)
  hum(5.0, 0.8)
  knock(7.0); knock(8.0)
  knock(10.0, 0.15); knock(10.3, 0.15)
}))

// Counting: 2, 3 and 4 knocks.
wav('counts.wav', track(16, (knock) => {
  knock(1.0); knock(1.3)
  knock(4.0); knock(4.3); knock(4.6)
  knock(8.0); knock(8.3); knock(8.6); knock(8.9)
}))

// Rhythm: uneven spacing, then uneven strength.
wav('rhythm.wav', track(10, (knock) => {
  knock(1.0); knock(1.15); knock(1.7)
  knock(5.0, 0.5); knock(5.3, 0.03)
}))

// Profile: 6 training knocks, then a real double knock, then a double chair bump.
wav('profile.wav', track(16, (knock) => {
  for (let i = 0; i < 6; i++) knock(1 + i * 1.0, 0.3 + (i % 3) * 0.05)
  knock(9.0); knock(9.3)
  knock(12.0, 0.4, 'bump'); knock(12.3, 0.4, 'bump')
}))
console.log('wrote test audio to', out)
