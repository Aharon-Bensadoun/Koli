/* ============================================================================
   Koli — Motion Design Video  |  Deterministic HTML -> MP4 renderer
   Phases:
     1. capture  — Puppeteer drives window.__seek(t) and screenshots every frame
     2. encode   — ffmpeg assembles the PNG frames into a silent H.264 MP4
     3. mux       — ffmpeg overlays music + SFX (from cues.json) onto the video
   Requirements: Node 18+, ffmpeg on PATH, puppeteer (npm install in this folder).
   Usage:  node render.js [--only-capture] [--skip-capture] [--fps N]
   ========================================================================== */
"use strict";
const fs = require("fs");
const path = require("path");
const { execFileSync, spawnSync } = require("child_process");

const ROOT   = path.resolve(__dirname, "..");
const OUT     = path.join(ROOT, "out");
const FRAMES  = path.join(OUT, "frames");
const INDEX   = "file://" + path.join(ROOT, "index.html").replace(/\\/g, "/") + "?capture";
const cues    = JSON.parse(fs.readFileSync(path.join(ROOT, "cues.json"), "utf8"));

const argv    = process.argv.slice(2);
const has     = (f) => argv.includes(f);
const argVal  = (f, d) => { const i = argv.indexOf(f); return i >= 0 ? argv[i + 1] : d; };

const FPS      = parseInt(argVal("--fps", cues.fps), 10);
const DURATION = cues.duration;
const W = cues.width, H = cues.height;
const TOTAL    = Math.round(DURATION * FPS);

const SILENT   = path.join(OUT, "video_silent.mp4");
const FINAL    = path.join(OUT, "koli-promo.mp4");

function log(m) { process.stdout.write(`[render] ${m}\n`); }
function ffmpegPath() {
  const probe = spawnSync(process.platform === "win32" ? "where" : "which", ["ffmpeg"]);
  if (probe.status !== 0) {
    console.error("\n[render] ERROR: ffmpeg not found on PATH.\n" +
      "  Windows: winget install Gyan.FFmpeg   (or: choco install ffmpeg)\n" +
      "  macOS:   brew install ffmpeg\n  Linux:   sudo apt install ffmpeg\n");
    process.exit(1);
  }
  return "ffmpeg";
}

/* ----------------------------- Phase 1: capture --------------------------- */
async function capture() {
  let puppeteer;
  try { puppeteer = require("puppeteer"); }
  catch { console.error("[render] puppeteer missing — run `npm install` in motion-video-koli/render first."); process.exit(1); }

  fs.rmSync(FRAMES, { recursive: true, force: true });
  fs.mkdirSync(FRAMES, { recursive: true });

  log(`Launching headless Chromium @ ${W}x${H}, ${FPS}fps, ${DURATION}s = ${TOTAL} frames`);
  const browser = await puppeteer.launch({
    headless: "new",
    args: [`--window-size=${W},${H}`, "--no-sandbox", "--force-color-profile=srgb",
           "--hide-scrollbars", "--disable-gpu-vsync"],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: W, height: H, deviceScaleFactor: 1 });
  await page.goto(INDEX, { waitUntil: "networkidle0" });
  await page.waitForFunction("window.__ready === true", { timeout: 15000 });
  await page.evaluate(async () => { await document.fonts.ready; });

  const pad = (n) => String(n).padStart(5, "0");
  const t0 = Date.now();
  for (let f = 0; f < TOTAL; f++) {
    const t = f / FPS;
    await page.evaluate((tt) => window.__seek(tt), t);
    await page.screenshot({
      path: path.join(FRAMES, `frame_${pad(f)}.png`),
      clip: { x: 0, y: 0, width: W, height: H },
      optimizeForSpeed: true,
    });
    if (f % 60 === 0 || f === TOTAL - 1) {
      const pct = (((f + 1) / TOTAL) * 100).toFixed(0);
      log(`frame ${f + 1}/${TOTAL} (${pct}%)`);
    }
  }
  await browser.close();
  log(`Captured ${TOTAL} frames in ${((Date.now() - t0) / 1000).toFixed(1)}s`);
}

/* ----------------------------- Phase 2: encode ---------------------------- */
function encode() {
  const ff = ffmpegPath();
  log("Encoding silent video…");
  execFileSync(ff, [
    "-y", "-framerate", String(FPS),
    "-start_number", "0", "-i", path.join(FRAMES, "frame_%05d.png"),
    "-c:v", "libx264", "-preset", "slow", "-crf", "16",
    "-pix_fmt", "yuv420p", "-r", String(FPS),
    "-vf", "format=yuv420p", SILENT,
  ], { stdio: "inherit" });
  log(`Silent video -> ${SILENT}`);
}

/* ------------------------------ Phase 3: mux ------------------------------ */
function mux() {
  const ff = ffmpegPath();
  const A = cues.audio || {};
  const abs = (p) => path.join(ROOT, p);
  const exists = (p) => p && fs.existsSync(abs(p));

  const sources = [];   // { file, delayMs, gain, isMusic }
  if (exists(A.music?.file)) sources.push({ file: A.music.file, delayMs: 0, gain: A.music.gain ?? 0.5, isMusic: true });
  (A.voice || []).forEach((c) => { if (exists(c.file)) sources.push({ file: c.file, delayMs: Math.round(c.time * 1000), gain: c.gain ?? 1.0 }); });
  (A.sfx || []).forEach((c) => { if (exists(c.file)) sources.push({ file: c.file, delayMs: Math.round(c.time * 1000), gain: c.gain ?? 0.8 }); });

  if (sources.length === 0) {
    log("No audio files found in motion-video-koli/audio — exporting silent final video.");
    fs.copyFileSync(SILENT, FINAL);
    log(`Final (silent) -> ${FINAL}`);
    return;
  }

  log(`Muxing ${sources.length} audio source(s)…`);
  const inputs = ["-y", "-i", SILENT];
  sources.forEach((s) => inputs.push("-i", abs(s.file)));

  const labels = [];
  const filters = sources.map((s, i) => {
    const idx = i + 1;                       // 0 is the video
    const out = `a${i}`;
    labels.push(`[${out}]`);
    const delay = s.delayMs > 0 ? `adelay=${s.delayMs}:all=1,` : "";
    const fade = s.isMusic && A.music?.fadeOut
      ? `,afade=t=out:st=${(DURATION - A.music.fadeOut).toFixed(2)}:d=${A.music.fadeOut}` : "";
    return `[${idx}:a]${delay}volume=${s.gain}${fade}[${out}]`;
  });
  filters.push(`${labels.join("")}amix=inputs=${sources.length}:normalize=0:dropout_transition=0[mix]`);

  execFileSync(ff, [
    ...inputs,
    "-filter_complex", filters.join(";"),
    "-map", "0:v", "-map", "[mix]",
    "-c:v", "copy", "-c:a", "aac", "-b:a", "256k",
    "-t", String(DURATION), FINAL,
  ], { stdio: "inherit" });
  log(`Final (with audio) -> ${FINAL}`);
}

/* -------------------------------- main ------------------------------------ */
(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  if (!has("--skip-capture")) await capture();
  if (has("--only-capture")) { log("Done (capture only)."); return; }
  encode();
  mux();
  log("✅  Render complete.");
})().catch((e) => { console.error(e); process.exit(1); });
