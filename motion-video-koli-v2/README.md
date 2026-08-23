# Koli — Motion Design Video v2 · Aurora Glass (HTML → MP4)

A **second, visually distinct** motion-design promo for **Koli** (real-time speech
transcription & meeting assistant for Windows), built as a deterministic HTML composition
and rendered frame-by-frame to a clean 1920×1080 MP4.

Where [v1](../motion-video-koli) is a **dark-neon-grid SaaS** look, **v2 is aurora
glassmorphism**: a living animated gradient-mesh background, frosted glass surfaces, soft
glows and depth parallax, with **smooth / liquid motion** instead of snappy bounce-pop.

- **Format:** 16:9, 1920×1080, ~34s, 60 fps (configurable)
- **Visuals:** all UI recreated in pure HTML/CSS/SVG — **no screenshots, no AI images**
- **Only real asset:** `assets/logo.jpg`
- **On-screen text:** English throughout

> Standalone marketing asset. It does **not** touch the Koli app code, and shares the same
> deterministic GSAP + Puppeteer/ffmpeg pipeline as v1 so the two stay easy to maintain.

---

## What makes v2 different from v1

| | **v1 — dark neon grid** | **v2 — aurora glass** |
|---|---|---|
| Background | near-black canvas, rigid grid + dot matrix, neon glows | living **gradient-mesh blobs** (violet → indigo → cyan → rose) drifting with parallax + film grain |
| Surfaces | flat opaque panels, neon borders | **frosted glass** cards (`backdrop-filter: blur(40px) saturate(180%)`), 1px inset highlight, soft large-radius shadows |
| Motion | snappy `back.out` bounce-pop, camera shake, hard horizontal swipe-blur, hard isometric tilt | **liquid** blur-in reveals, gentle parallax drift, vertical card shuffle, breathing float loops, drawing gradient connectors |
| Eases | `back.out`, shake keyframes | `power2 / power3 / expo`, `sine.inOut` — **no shake** |
| Sound | hard impacts / pops | softer **risers / swells / glass chimes / shimmers** |

### Palette (aurora)
violet `#7C3AED` · indigo `#5B5EE3` · cyan `#22D3EE` · rose `#F472B6` · deep `#4F1FB8` ·
glow `#A78BFA` · base bg `#0A0A12` · success `#4ADE80` · recording `#FF4D6A`.
Glass fills `rgba(255,255,255,.06–.10)`, inset highlight `rgba(255,255,255,.25)`.

---

## 1. Preview (no build needed)

Open **`index.html`** in a browser. The timeline auto-plays and loops.

> 🔊 **Sound in preview:** because of browser autoplay rules, **click once** on the page to
> (re)start *with* audio. Audio in the live preview only works when served over http (not
> `file://`). For a quick local server:
> ```
> npx serve .        # then open the printed http://localhost:PORT
> ```
> The **final MP4 audio is added at render time** (below) and does not depend on this.

---

## 2. Add your audio (optional but recommended)

Drop your files into **`motion-video-koli-v2/audio/`** with these exact names — v2 uses a
**softer, liquid** sound set:

| File          | Used for                                       |
|---------------|------------------------------------------------|
| `music.mp3`   | ambient pad / music bed (full ~34s)            |
| `riser.mp3`   | soft tension risers (hook + CTA build-ups)     |
| `swell.mp3`   | airy swells for reveals / card morphs          |
| `whoosh.mp3`  | soft scene-change / fly-in transitions         |
| `shimmer.mp3` | light shimmer for element appearances          |
| `chime.mp3`   | soft glass chime for validations / checkmarks  |
| `bloom.mp3`   | gentle bloom for logo reveal / CTA             |

Voice-over (optional, one clip per scene): `voice_1_hook.mp3`, `voice_2_reveal.mp3`,
`voice_3_dictation.mp3`, `voice_4_meeting.mp3`, `voice_5_rewrite.mp3`, `voice_6_assistant.mp3`,
`voice_7_privacy.mp3`, `voice_8_cta.mp3`.

Exact trigger timings live in **`cues.json`** (`audio.voice[]` / `audio.sfx[]`, in seconds).
Missing files are skipped gracefully; if no audio is present, the final MP4 is silent.

---

## 3. Render to MP4

**Requirements:** [Node.js 18+](https://nodejs.org) and [ffmpeg](https://ffmpeg.org) on your `PATH`
(`winget install Gyan.FFmpeg` on Windows). Puppeteer (headless Chromium) installs on first run.
`backdrop-filter` renders correctly in headless Chromium, so the glass captures cleanly.

**Windows (PowerShell):**
```powershell
cd motion-video-koli-v2/render
./render.ps1
```

**macOS / Linux:**
```bash
cd motion-video-koli-v2/render
chmod +x render.sh && ./render.sh
```

Pipeline: capture every frame via `window.__seek(t)` → encode silent H.264 → mux music + SFX
from `cues.json`. Output: **`motion-video-koli-v2/out/koli-v2-promo.mp4`**.

Useful flags: `--only-capture` (frames only) · `--skip-capture` (re-encode existing frames) ·
`--fps 30` (override frame rate).

---

## 4. Tuning

- **Duration / fps / size:** edit `cues.json` (`duration`, `fps`, `width`, `height`).
- **Scene timing & content:** scene boundaries are in `cues.json`; the animations live in
  `timeline.js` (one IIFE per scene, positioned at absolute times).
- **Colors / look:** CSS variables at the top of `styles.css` (`--violet`, `--cyan`, `--glass`, …).
- **Copy / text:** edit directly in `index.html`.
- **Motion primitives:** `glassReveal`, `float`, `gradientFlow`, plus reused `counter` /
  `typewriter` at the top of `timeline.js`. The aurora background drift is also on the master
  timeline (deterministic, frame-accurate on seek).

## 5. Files

```
motion-video-koli-v2/
├── index.html      # 1920x1080 stage + aurora mesh + 8 glass scenes (DOM)
├── styles.css      # aurora-glass visual system + vector UI mockups
├── timeline.js     # GSAP master timeline; exposes window.__seek(t)
├── cues.json       # SINGLE SOURCE OF TRUTH: fps/duration + scene & audio timing
├── vendor/gsap.min.js
├── assets/logo.jpg
├── audio/          # drop music.mp3 / swell.mp3 / shimmer.mp3 / ... here
├── render/         # render.js (Puppeteer+ffmpeg) + render.ps1 / render.sh
└── out/            # generated frames + koli-v2-promo.mp4
```

### Storyboard (≈34s) — same Koli content, restaged in glass
1. **0–4s Hook** — floating glass app-tiles (Email, Word, Slack, Teams, Notes) drift in aurora haze; soft frustration micro-copy; headline blur-in "You speak faster than you type."
2. **4–7s Reveal** — Koli logo inside a frosted glass orb with an aurora bloom ring; "Speak. It types itself."
3. **7–12s Dictation** — glass console, **F9** keycap, flowing gradient waveform, typewriter transcript auto-typed into a translucent app window; engine pills Whisper / gpt-4o-transcribe / gpt-realtime / Azure / On-prem light up · *Dictate into any app*
4. **12–17s Meeting mode** — glass window, color-coded diarized transcript, mic + system-audio pills, counting stats, export TXT/MD/JSON; gentle floating parallax tilt · *Multi-speaker meetings, diarized*
5. **17–22s Rewrite & Translate** — stacked glass cards morph Raw → Polished → Professional → Translated (EN/FR/HE) via smooth cross-fade + vertical shuffle · *Rewrite & translate on the fly*
6. **22–26s Voice assistant** *(Alt Gr)* — radial constellation of glass nodes around a central orb; animated gradient connectors: spoken question → speech-to-text → web search → answer typed · *Voice assistant, web-aware*
7. **26–30s Privacy & resilience** — frosted shield with aurora sheen + glass checklist rows (DPAPI-encrypted keys, runs locally, hallucination filter, failed-recording recovery) · *Private. Resilient. Yours.*
8. **30–34s CTA** — logo recompose in glass, "Speak. It types itself." + "Windows 10 | 11"; final aurora bloom + soft fade.
