// Mirrors the C# classes in src/Settings.cs and src/Detector.cs (field names are PascalCase there).

export type ActionType = 'screenshot' | 'open' | 'media' | 'lock' | 'hotkey' | 'command'

export interface Binding {
  Knocks: number
  Action: ActionType
  Arg: string | null
}

export interface KnockProfile {
  BrightMean: number
  BrightStd: number
  DecayMean: number
  DecayStd: number
  LoudMin: number
  Samples: number
}

export interface Settings {
  Version: number
  Listening: boolean
  Sensitivity: number // 0 low, 1 medium, 2 high
  RhythmCheck: boolean
  IgnoreWhileTyping: boolean
  AwayMinutes: number
  Flash: boolean
  Sound: boolean
  Clipboard: boolean
  ScreenshotFolder: string
  ScreenshotName: string
  Profile: KnockProfile | null
  Bindings: Binding[]
}

export interface ScreenInfo {
  n: number
  width: number
  height: number
  primary: boolean
}

export interface ActivityEntry {
  Time: string
  Text: string
  Kind: 'ok' | 'reject' | 'info' | 'error'
}

export type Quality = 'great' | 'good' | 'inconsistent'

export interface AppState {
  settings: Settings
  startup: boolean
  listening: boolean
  screens: ScreenInfo[]
  version: string
  activity: ActivityEntry[]
  calibrationKnocks: number
}

// Messages from the app to the UI.
export type Incoming =
  | ({ type: 'state' } & AppState)
  | { type: 'level'; db: number }
  | { type: 'activity'; entry: ActivityEntry }
  | { type: 'calib.knock'; count: number }
  | { type: 'calib.done'; profile: KnockProfile; quality: Quality }
  | { type: 'browse.result'; id: number; path: string | null }
