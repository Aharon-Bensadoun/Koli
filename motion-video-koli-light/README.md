# Koli — Motion Design Video · **Light / Corporate** cut (HTML → MP4)

A second, **stylistically different** promo for **Koli** (real-time speech transcription &
meeting assistant for Windows), built as a deterministic HTML composition and rendered
frame-by-frame to a clean 1920×1080 MP4.

Where `motion-video-koli/` is the **dark / aurora** cut, this is the **light, premium B2B
"product-tour"** cut in the *Gojiberry* style:

- **Clean white canvas** + subtle blue grid, soft **2.5D drop shadows** on floating UI layers.
- **Tech blue** (`#2563EB`) as the base, with **vivid orange/red** pops (`#FB6514`).
- **Bold geometric kinetic typography** (letters cascade, colour sweeps on key words).
- A signature **custom mouse cursor** that travels along precise paths and triggers
  synchronized **button / toggle states** (micro-interactions), plus snappy easing,
  staggered cascades and short blur **whip** entrances.

- **Format:** 16:9, 1920×1080, **~35s**, 60 fps (configurable)
- **Visuals:** all UI is recreated in pure HTML/CSS/SVG — **no screenshots, no AI images**
- **Only real asset:** `assets/logo.jpg` (copied from `Koli.WinUI/Assets/Koli.jpg`)
- **On-screen text:** English throughout

> Standalone marketing asset. It does **not** touch the Koli app code, and mirrors the
> `motion-video-koli/` pipeline so the two stay easy to maintain side by side.

---

## 1. Preview (no build needed)

Open **`index.html`** in a browser. The timeline auto-plays and loops.

> 🔊 **Sound in preview:** because of browser autoplay rules, **click once** on the page to
> (re)start *with* audio. Audio in the live preview only works when served over http
> (not `file://`):
> ```
> npx serve .        # then open the printed http://localhost:PORT
> ```
> The **final MP4 audio is added at render time** (below) and does not depend on this.

---

## 2. Add your audio (optional but recommended)

Drop your files into **`motion-video-koli-light/audio/`** with these exact names:

| File         | Used for                                              |
|--------------|-------------------------------------------------------|
| `music.mp3`  | background music bed (full ~35s)                      |
| `whoosh.mp3` | whip pans / scene transitions / window fly-ins        |
| `click.mp3`  | clean digital UI clicks (cursor on buttons / toggles) |
| `pop.mp3`    | small tactile pops (cards, chips, stat cascades)      |
| `type.mp3`   | mechanical keyboard typing (text reveals)             |
| `ding.mp3`   | validations / checkmarks                              |
| `impact.mp3` | accent hits (HANDS-FREE stamp, CTA cut)               |

Voice-over (optional, one clip per scene): `voice_1_intro.mp3` … `voice_7_cta.mp3`.
Exact trigger timings live in **`cues.json`**. Missing files are skipped gracefully;
if no audio is present, the final MP4 is silent.

---

## 3. Render to MP4

**Requirements:** [Node.js 18+](https://nodejs.org) and [ffmpeg](https://ffmpeg.org) on your `PATH`.
Puppeteer (headless Chromium) is installed on first run.

**Windows (PowerShell):**
```powershell
cd motion-video-koli-light/render
./render.ps1
```

**macOS / Linux:**
```bash
cd motion-video-koli-light/render
chmod +x render.sh && ./render.sh
```

Pipeline: capture every frame via `window.__seek(t)` → encode silent H.264 → mux music + SFX
from `cues.json`. Output: **`motion-video-koli-light/out/koli-promo-light.mp4`**.

Useful flags: `--only-capture` · `--skip-capture` · `--fps 30`.

---

## 4. Tuning

- **Duration / fps / size:** edit `cues.json` (`duration`, `fps`, `width`, `height`).
- **Scene timing:** scene boundaries are in `cues.json`; animations live in `timeline.js`
  (one IIFE per scene, positioned at absolute times).
- **Colors / look:** CSS variables at the top of `styles.css` (`--blue`, `--orange`, `--bg`, …).
- **Copy / text:** edit directly in `index.html`.

## 5. Files

```
motion-video-koli-light/
├── index.html      # 1920x1080 stage + 7 scenes (DOM) + custom cursor
├── styles.css      # light/corporate "Gojiberry" visual system + vector UI mockups
├── timeline.js     # GSAP master timeline + cursor choreography; exposes window.__seek(t)
├── cues.json       # SINGLE SOURCE OF TRUTH: fps/duration + scene & audio timing
├── vendor/gsap.min.js
├── assets/logo.jpg
├── audio/          # drop music.mp3 / click.mp3 / type.mp3 / ... here
├── render/         # render.js (Puppeteer+ffmpeg) + render.ps1 / render.sh
└── out/            # generated frames + koli-promo-light.mp4
```

### Storyboard (≈35s)

| Scene | Time | Visuals & UI actions | Motion & transitions | Sound cues |
|---|---|---|---|---|
| **1 · Intro** | 0–4s | Logo badge + rotating ring, **Koli** wordmark, *"Your voice, instantly at work."* | Camera push-in, letters cascade w/ overshoot, colour sweep | whoosh, pops, soft click |
| **2 · Dictate** | 4–9s | Press **F9**, live waveform + Recording, text auto-types into a floating Notes window · *Dictate into any app* | Window whip-in (blur), typewriter, waveform | whoosh, click, key taps, ding |
| **3 · Meeting** | 9–15s | Cursor flips **Microphone** + **System audio** toggles on, speaker-coloured transcript, Speakers/Minutes counters, export TXT/MD/JSON · *Multi-speaker, diarized* | Isometric settle, tactile switch knobs, staggered cards | whoosh, 2× click, pops |
| **4 · Rewrite** | 15–21s | Cursor clicks **Rewrite** (Raw→Polished), opens language menu, picks **FR** (→ Translated) · *Rewrite & translate on the fly* | Button press state, blur text swaps, dropdown pop | whoosh, clicks, ding |
| **5 · Assistant** | 21–27s | *Press Alt Gr* — pipeline **Spoken question → Web search → Answer typed back**, ⚡ HANDS-FREE stamp · *Voice assistant, web-aware* | Nodes pop L→R, arrows draw, stamp slam | whoosh, pops, impact |
| **6 · Trust** | 27–31s | *Private by design* — 4 trust cards (encrypted keys, runs locally, hallucination filter, recovery) | Cards cascade, checkmarks pop | whoosh, dings |
| **7 · CTA** | 31–35s | Logo recompose, *"Speak. It types itself."*, cursor presses **Get Koli — free**, Windows 10 \| 11 | White flash cut, button press, fade to white | impact, whoosh, click, ding |
