# Koli — Motion Design Video (HTML → MP4)

A self-contained, dark-mode/aurora **motion-design promo** for **Koli** (real-time speech
transcription & meeting assistant for Windows), built as a deterministic HTML composition
and rendered frame-by-frame to a clean 1920×1080 MP4.

Style follows Koli's signature **violet → indigo → cyan → rose** aurora palette: dark canvas +
grid, glow highlights, kinetic typography (bounce pop, capsule highlights), living UI mockups,
isometric meeting window, staggered cascades, and swipe-&-blur transitions.

- **Format:** 16:9, 1920×1080, ~34s, 60 fps (configurable)
- **Visuals:** all UI is recreated in pure HTML/CSS/SVG — **no screenshots, no AI images**
- **Only real asset:** `assets/logo.jpg` (copied from `Koli.WinUI/Assets/Koli.jpg`)
- **On-screen text:** English throughout

> This is a standalone marketing asset. It does **not** touch the Koli app code, and mirrors the
> `motion-video/` ("AI Nexus") pipeline so the two stay easy to maintain side by side.

---

## 1. Preview (no build needed)

Just open **`index.html`** in a browser. The timeline auto-plays and loops.

> 🔊 **Sound in preview:** because of browser autoplay rules, **click once** on the page to
> (re)start *with* audio. Audio in the live preview only works when the page is served over
> http (not `file://`). For a quick local server:
> ```
> npx serve .        # then open the printed http://localhost:PORT
> ```
> The **final MP4 audio is added at render time** (below) and does not depend on this.

---

## 2. Add your audio (optional but recommended)

Drop your files into **`motion-video-koli/audio/`** with these exact names:

| File          | Used for                                  |
|---------------|-------------------------------------------|
| `music.mp3`   | background music bed (full ~34s)          |
| `riser.mp3`   | tension risers (hook + CTA)               |
| `impact.mp3`  | hard hits on cuts / logo reveal           |
| `whoosh.mp3`  | transitions, swipes, node fly-ins         |
| `pop.mp3`     | small element pops (cascades)             |
| `ding.mp3`    | validations / checkmarks                  |

Voice-over (optional, one clip per scene): `voice_1_hook.mp3`, `voice_2_reveal.mp3`,
`voice_3_dictation.mp3`, `voice_4_meeting.mp3`, `voice_5_rewrite.mp3`, `voice_6_assistant.mp3`,
`voice_7_privacy.mp3`, `voice_8_cta.mp3`.

Exact trigger timings live in **`cues.json`** (`audio.voice[]` / `audio.sfx[]`, in seconds).
Missing files are skipped gracefully; if no audio is present, the final MP4 is silent.

> `.mp3`, `.wav`, `.m4a`, etc. all work (ffmpeg decodes them).

---

## 3. Render to MP4

**Requirements:** [Node.js 18+](https://nodejs.org) and [ffmpeg](https://ffmpeg.org) on your `PATH`
(`winget install Gyan.FFmpeg` on Windows). Puppeteer (headless Chromium) is installed on first run.

**Windows (PowerShell):**
```powershell
cd motion-video-koli/render
./render.ps1
```

**macOS / Linux:**
```bash
cd motion-video-koli/render
chmod +x render.sh && ./render.sh
```

Pipeline: capture every frame via `window.__seek(t)` → encode silent H.264 → mux music + SFX
from `cues.json`. Output: **`motion-video-koli/out/koli-promo.mp4`**.

Useful flags: `--only-capture` (frames only) · `--skip-capture` (re-encode existing frames) ·
`--fps 30` (override frame rate).

---

## 4. Tuning

- **Duration / fps / size:** edit `cues.json` (`duration`, `fps`, `width`, `height`).
- **Scene timing & content:** scene boundaries are in `cues.json`; the animations live in
  `timeline.js` (one IIFE per scene, positioned at absolute times).
- **Colors / look:** CSS variables at the top of `styles.css` (`--brand`, `--cyan`, `--bg`, …).
- **Copy / text:** edit directly in `index.html`.

## 5. Files

```
motion-video-koli/
├── index.html      # 1920x1080 stage + all 8 scenes (DOM)
├── styles.css      # dark/aurora visual system + vector UI mockups
├── timeline.js     # GSAP master timeline; exposes window.__seek(t)
├── cues.json       # SINGLE SOURCE OF TRUTH: fps/duration + scene & audio timing
├── vendor/gsap.min.js
├── assets/logo.jpg
├── audio/          # drop music.mp3 / whoosh.mp3 / ... here
├── render/         # render.js (Puppeteer+ffmpeg) + render.ps1 / render.sh
└── out/            # generated frames + koli-promo.mp4
```

### Storyboard (≈34s)
1. **0–4s Hook** — scattered app chips (Email, Word, Slack, Teams, Notes, Chat) + "typing all day? / too slow", "You speak faster than you type." (camera shake)
2. **4–7s Reveal** — Koli logo bounce-pop + glow ring, "Speak. It types itself."
3. **7–12s Dictation** — press **F9** → waveform → typewriter transcript auto-typed into the active app; engine chips Whisper / gpt-4o-transcribe / gpt-realtime / Azure / On-prem light up · *Dictate into any app*
4. **12–17s Meeting mode** — isometric window, mic + system-audio capture, counting stats, colour-coded multi-speaker transcript, export TXT/MD/JSON · *Multi-speaker meetings, diarized*
5. **17–22s Rewrite & Translate** — swipe-&-blur cards Raw → Polished → Professional → Translated (EN/FR/HE) · *Rewrite & translate on the fly*
6. **22–26s Voice assistant** *(Alt Gr)* — radial flow: spoken question → speech-to-text → web search → answer typed back · *Voice assistant, web-aware*
7. **26–30s Privacy & resilience** — shield + checklist (DPAPI-encrypted keys, runs locally, hallucination filter, failed-recording recovery) · *Private. Resilient. Yours.*
8. **30–34s CTA** — logo recompose, "Speak. It types itself." + "Windows 10 | 11"
