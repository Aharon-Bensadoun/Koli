/* ============================================================================
   Koli — Motion Design Video v2  |  Master timeline (GSAP, deterministic)
   AURORA GLASSMORPHISM edition. Smooth / liquid motion language:
     - glassReveal : filter blur(20px)->0 + scale 0.96->1 + autoAlpha 0->1
     - float       : gentle breathing y/rotation loops (finite, seek-safe)
     - parallax    : aurora blobs drift slower than foreground (time-based)
     - gradientFlow : connectors draw in with a moving gradient stroke
   No camera shake, no aggressive bounce. Eases lean on power2/power3/expo.
   Builds a single paused 34s timeline and exposes window.__seek(t) /
   window.__duration for frame-accurate capture. ALL visible motion (incl. the
   living background) is on the timeline — no Date.now / free rAF — so every
   seek is reproducible by the renderer.
   ========================================================================== */
(() => {
  "use strict";

  const DURATION = 34;          // seconds (keep in sync with cues.json)
  const W = 1920, H = 1080;
  const $  = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));

  /* ---- Fit the fixed 1920x1080 stage into the viewport --------------------- */
  const stage = $("#stage");
  function fit() {
    const s = Math.min(window.innerWidth / W, window.innerHeight / H);
    stage.style.transform = `scale(${s})`;
  }
  window.addEventListener("resize", fit);
  fit();

  /* ---- Seeded RNG so all "organic" motion is baked & reproducible ---------- */
  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  const rnd = mulberry32(0x4b4f4c49); // "KOLI" seed — fixed for reproducible motion

  /* ---- Defaults & timeline ------------------------------------------------- */
  gsap.defaults({ ease: "power3.out" });
  const tl = gsap.timeline({ paused: true });

  /* ===========================  MOTION PRIMITIVES  ========================= */

  // Liquid "glass reveal": blur-in + soft scale + fade. Supports stagger.
  function glassReveal(target, at, opt = {}) {
    const dur     = opt.dur ?? 0.7;
    const y       = opt.y ?? 24;
    const x       = opt.x ?? 0;
    const blur    = opt.blur ?? 18;
    const scale   = opt.scale ?? 0.96;
    const ease    = opt.ease ?? "power3.out";
    const stagger = opt.stagger ?? 0;
    return tl.fromTo(target,
      { autoAlpha: 0, x, y, scale, filter: `blur(${blur}px)` },
      { autoAlpha: 1, x: 0, y: 0, scale: 1, filter: "blur(0px)", duration: dur, ease, stagger },
      at);
  }

  // Gentle breathing float (finite repeats keep the timeline duration finite).
  function float(target, at, span, opt = {}) {
    const ampY  = opt.y ?? 12;
    const rot   = opt.rotation ?? 0;
    const cycle = opt.cycle ?? 2.2;
    const reps  = Math.max(1, Math.round(span / cycle));
    tl.to(target, { y: `+=${ampY}`, rotation: `+=${rot}`, duration: cycle,
      ease: "sine.inOut", yoyo: true, repeat: reps }, at);
  }

  // Connector stroke that draws in along its gradient, then breathes in width.
  function gradientFlow(paths, at, opt = {}) {
    const dur     = opt.dur ?? 0.6;
    const stagger = opt.stagger ?? 0.14;
    paths.forEach((p) => {
      const len = p.getTotalLength();
      gsap.set(p, { strokeDasharray: len, strokeDashoffset: len });
    });
    tl.to(paths, { strokeDashoffset: 0, duration: dur, stagger, ease: "power2.inOut" }, at);
    tl.to(paths, { strokeWidth: 5, duration: 0.9, yoyo: true, repeat: 2, ease: "sine.inOut" }, at + dur + 0.1);
  }

  // Animated number counter (runs correctly on seek).
  function counter(el, at, dur = 1.4) {
    const target = parseFloat(el.dataset.count);
    const dec = parseInt(el.dataset.dec || "0", 10);
    const suffix = el.dataset.suffix || "";
    const proxy = { v: 0 };
    tl.to(proxy, {
      v: target, duration: dur, ease: "power2.out",
      onUpdate() {
        let n;
        if (suffix) n = dec ? proxy.v.toFixed(dec) : Math.round(proxy.v).toString();
        else n = Math.round(proxy.v).toLocaleString("en-US");
        el.textContent = n + suffix;
      },
    }, at);
  }

  // Typewriter (runs correctly on seek).
  function typewriter(el, text, at, dur) {
    const proxy = { i: 0 };
    tl.to(proxy, {
      i: text.length, duration: dur, ease: "none",
      onUpdate() { el.textContent = text.slice(0, Math.round(proxy.i)); },
    }, at);
  }

  // Scene cross-fade wrapper: fades a scene in at `start`, out before `end`.
  function fadeScene(sel, start, end, fade = 0.45) {
    const el = $(sel);
    tl.set(el, { autoAlpha: 0 }, 0);
    tl.to(el, { autoAlpha: 1, duration: fade, ease: "power2.out" }, start);
    tl.to(el, { autoAlpha: 0, duration: fade, ease: "power2.in" }, end - fade);
    return el;
  }

  /* =====================  LIVING AURORA BACKGROUND  ======================== */
  // Each blob drifts/scales over the whole timeline. Amplitude scales with the
  // element's data-depth (deeper = slower/smaller => parallax). Deterministic.
  (() => {
    const blobs = $$("[data-blob]");
    blobs.forEach((b, i) => {
      const depth = parseFloat(b.dataset.depth || "0.4");
      const A = 40 + depth * 150;          // px of travel
      const rs = mulberry32(0x42000 + i * 977);
      const seg = DURATION / 3;
      tl.to(b, { keyframes: [
        { x: (rs() * 2 - 1) * A, y: (rs() * 2 - 1) * A, scale: 1 + rs() * 0.14, duration: seg, ease: "sine.inOut" },
        { x: (rs() * 2 - 1) * A, y: (rs() * 2 - 1) * A, scale: 1 + rs() * 0.14, duration: seg, ease: "sine.inOut" },
        { x: 0, y: 0, scale: 1, duration: seg, ease: "sine.inOut" },
      ]}, 0);
    });
    // Foreground drifts subtly the other way for depth parallax.
    tl.to("#parallax", { keyframes: [
      { x: -16, y: 9,  duration: DURATION / 2, ease: "sine.inOut" },
      { x: 0,   y: 0,  duration: DURATION / 2, ease: "sine.inOut" },
    ]}, 0);
  })();

  /* --------------------------- SCENE 1 : HOOK (0–4) ------------------------ */
  (() => {
    const s = fadeScene("#s-hook", 0.0, 4.0);
    const tiles = $$("[data-tile]", s);
    const asks  = $$("[data-ask]", s);
    const l1 = $("#hook-l1"), l2 = $("#hook-l2");

    // glass tiles drift in through the aurora haze
    glassReveal(tiles, 0.15, { stagger: 0.1, y: 30, blur: 22, dur: 0.8, ease: "power3.out" });
    tiles.forEach((t, i) => float(t, 0.9, 3.0, { y: 10 + (i % 3) * 4, cycle: 2.0 + i * 0.15 }));

    // soft frustration micro-copy fades in, then recedes
    glassReveal(asks, 0.5, { stagger: 0.12, y: 14, blur: 10, dur: 0.6 });

    // headline blur-in, word by word
    glassReveal($$(".word", l1), 0.5, { stagger: 0.08, y: 44, blur: 14, dur: 0.7, ease: "power3.out" });

    // declutter: gently fade the questions away & soften tiles so the focus line is clean
    tl.to(asks,  { autoAlpha: 0, filter: "blur(8px)", duration: 0.5 }, 2.3);
    tl.to(tiles, { autoAlpha: 0.18, duration: 0.5 }, 2.3);

    // swap to line 2 (the painful questions) with a liquid blur-in
    tl.to(l1, { autoAlpha: 0, y: -26, filter: "blur(10px)", duration: 0.45, ease: "power2.in" }, 2.5);
    tl.set(l2, { opacity: 1 }, 2.55);
    glassReveal($$(".word", l2), 2.6, { stagger: 0.06, y: 34, blur: 12, dur: 0.55 });
  })();

  /* --------------------------- SCENE 2 : LOGO (4–7) ------------------------ */
  (() => {
    fadeScene("#s-reveal", 4.0, 7.0);

    // soft aurora bloom instead of a hard white flash
    tl.fromTo("#bloom", { opacity: 0 }, { opacity: 0.7, duration: 0.6, ease: "power2.out" }, 4.0);
    tl.to("#bloom", { opacity: 0, duration: 1.1, ease: "power2.in" }, 4.55);

    glassReveal("#logo-orb", 4.1, { y: 0, blur: 26, scale: 0.9, dur: 0.9, ease: "expo.out" });
    tl.fromTo("#logo-ring", { scale: 1.35, autoAlpha: 0 },
      { scale: 1, autoAlpha: 1, duration: 0.9, ease: "expo.out" }, 4.2);
    tl.to("#logo-ring", { rotation: 180, duration: 2.6, ease: "none" }, 4.4);
    tl.to("#logo-orb", { boxShadow: "0 40px 90px rgba(0,0,0,.45), inset 0 1px 0 rgba(255,255,255,.25), 0 0 90px rgba(124,58,237,.7)",
      duration: 1.0, yoyo: true, repeat: 1, ease: "sine.inOut" }, 4.6);
    float("#logo-orb", 4.9, 2.0, { y: 10, cycle: 2.0 });

    glassReveal("#brand-name", 4.6, { y: 36, blur: 14, dur: 0.7 });
    glassReveal("#brand-sub",  4.85, { y: 20, blur: 10, dur: 0.7 });
    glassReveal("#reveal-tag", 5.1, { y: 18, blur: 10, dur: 0.7 });
  })();

  /* ----------------------- SCENE 3 : DICTATION (7–12) ---------------------- */
  (() => {
    const s = fadeScene("#s-dictation", 7.0, 12.0);
    const waves   = $$("[data-wave]", s);
    const engines = $$("[data-engine]", s);

    // press F9 (soft depress, no hard bounce)
    glassReveal("#f9-key", 7.15, { y: 20, blur: 16, scale: 0.9, dur: 0.6, ease: "expo.out" });
    tl.to("#f9-key .cap", { y: 8, duration: 0.16, yoyo: true, repeat: 1, ease: "power2.inOut" }, 7.6);

    // recording indicator + flowing waveform
    tl.fromTo("#dict-rec", { autoAlpha: 0, x: -16 }, { autoAlpha: 1, x: 0, duration: 0.4 }, 7.4);
    tl.to("#dict-rec .blob-dot", { scale: 0.55, duration: 0.5, repeat: 7, yoyo: true, ease: "sine.inOut" }, 7.5);

    gsap.set(waves, { scaleY: 0.16, transformOrigin: "50% 50%" });
    waves.forEach((b, i) => {
      const amp = 0.45 + rnd() * 0.55;
      tl.to(b, { scaleY: amp, duration: 0.32 + rnd() * 0.2, repeat: 11, yoyo: true,
        ease: "sine.inOut" }, 7.4 + (i % 6) * 0.05);
    });

    // translucent active-app window glides in + Koli types into it
    glassReveal("#dict-window", 7.5, { x: 70, y: 0, blur: 20, dur: 0.8, ease: "power3.out" });
    tl.to("#dict-caret", { opacity: 0, duration: 0.5, repeat: 8, yoyo: true, ease: "steps(1)" }, 7.9);
    typewriter($("#dict-typed"), "Koli types straight into the active window.", 8.0, 2.0);

    // engine pills light up in a smooth staggered cascade
    engines.forEach((e, i) => {
      const at = 9.5 + i * 0.32;
      glassReveal(e, at, { y: 26, blur: 12, scale: 0.92, dur: 0.5 });
      tl.add(() => e.classList.add("lit"), at + 0.18);
    });

    glassReveal("#s-dictation [data-ftag]", 8.2, { y: 24, blur: 12, dur: 0.6 });
  })();

  /* ------------------- SCENE 4 : MEETING (soft float) (12–17) -------------- */
  (() => {
    const s = fadeScene("#s-meeting", 12.0, 17.0);
    const win = $("#meet-window");

    // gentle float-in with a subtle (not hard) tilt
    tl.fromTo(win,
      { autoAlpha: 0, y: 90, scale: 0.95, rotationY: -12, filter: "blur(18px)" },
      { autoAlpha: 1, y: 0, scale: 1, rotationY: -3, filter: "blur(0px)", duration: 1.0, ease: "power3.out" }, 12.1);

    glassReveal($$(".src-toggle", s), 12.7, { x: -34, y: 0, blur: 10, dur: 0.5, stagger: 0.14 });
    glassReveal($$("[data-mstat]", s), 13.0, { y: 34, blur: 12, dur: 0.55, stagger: 0.14 });
    $$("[data-count]", s).forEach((el) => counter(el, 13.2, 1.4));
    glassReveal($$("[data-line]", s), 13.2, { x: 34, y: 0, blur: 10, dur: 0.55, stagger: 0.28 });
    glassReveal($$("[data-exp]", s), 15.0, { y: 18, blur: 8, scale: 0.85, dur: 0.45, stagger: 0.12 });

    // continuous gentle parallax float for life
    tl.to(win, { rotationY: 0, y: -14, duration: 2.2, yoyo: true, repeat: 1, ease: "sine.inOut" }, 13.4);

    glassReveal("#s-meeting [data-ftag]", 13.0, { y: 24, blur: 12, dur: 0.6 });
  })();

  /* ----------------- SCENE 5 : REWRITE & TRANSLATE (17–22) ----------------- */
  (() => {
    const s = fadeScene("#s-rewrite", 17.0, 22.0);
    const cards = $$("[data-rw]", s);
    gsap.set(cards, { autoAlpha: 0 });
    cards.forEach((c, i) => {
      const at = 17.5 + i * 0.95;
      // liquid blur-in + vertical rise (no horizontal swipe-blur)
      tl.fromTo(c, { autoAlpha: 0, y: 70, scale: 0.95, filter: "blur(16px)" },
        { autoAlpha: 1, y: 0, scale: 1, filter: "blur(0px)", duration: 0.55, ease: "power3.out" }, at);
      if (i < cards.length - 1) {
        // smooth cross-fade upward to the next card
        tl.to(c, { autoAlpha: 0, y: -60, scale: 0.97, filter: "blur(14px)", duration: 0.5, ease: "power2.in" }, at + 0.7);
      }
    });
    // last card (Translated): soft-pop the language pills
    tl.fromTo($$(".rw-card:last-child [data-lang]", s), { scale: 0.5, autoAlpha: 0 },
      { scale: 1, autoAlpha: 1, duration: 0.45, stagger: 0.12, ease: "power3.out" }, 21.0);

    glassReveal("#s-rewrite [data-ftag]", 17.6, { y: 24, blur: 12, dur: 0.6 });
  })();

  /* --------------- SCENE 6 : VOICE ASSISTANT (Alt Gr) (22–26) -------------- */
  (() => {
    const s = fadeScene("#s-assistant", 22.0, 26.0);
    const nodes = $$("[data-anode]", s);
    const flows = $$("[data-aflow]", s);

    glassReveal("#assist-kicker", 22.1, { y: -20, blur: 8, dur: 0.5 });
    glassReveal("#assist-head", 22.2, { y: 26, blur: 12, dur: 0.6 });
    glassReveal("#assist-sub", 22.5, { y: 16, blur: 8, dur: 0.5 });

    glassReveal("[data-ahub]", 22.45, { scale: 0.7, blur: 16, y: 0, dur: 0.6, ease: "expo.out" });
    gradientFlow(flows, 22.75, { dur: 0.55, stagger: 0.16 });

    // flow nodes blur-in + light up in sequence (question -> STT -> search -> answer)
    nodes.forEach((n, i) => {
      const at = 22.95 + i * 0.26;
      glassReveal(n, at, { scale: 0.7, blur: 12, y: 0, dur: 0.5, ease: "power3.out" });
      tl.add(() => n.classList.add("lit"), at + 0.18);
    });
    tl.to("[data-ahub]", { scale: 1.04, duration: 0.5, yoyo: true, repeat: 2, ease: "sine.inOut" }, 24.2);

    // game-changer badge: soft scale-in + gentle breathing (no slam)
    tl.fromTo("#assist-gc", { scale: 0.6, autoAlpha: 0, filter: "blur(14px)" },
      { scale: 1, autoAlpha: 1, filter: "blur(0px)", duration: 0.6, ease: "expo.out" }, 24.0);
    tl.to("#assist-gc", { scale: 1.06, duration: 0.5, yoyo: true, repeat: 2, ease: "sine.inOut" }, 24.6);

    glassReveal("#s-assistant [data-ftag]", 23.2, { y: 24, blur: 12, dur: 0.6 });
  })();

  /* ------------------ SCENE 7 : PRIVACY & RESILIENCE (26–30) --------------- */
  (() => {
    const s = fadeScene("#s-privacy", 26.0, 30.0);

    tl.fromTo("#shield", { scale: 0.6, autoAlpha: 0, rotationY: -40, filter: "blur(20px)" },
      { scale: 1, autoAlpha: 1, rotationY: 0, filter: "blur(0px)", duration: 0.9, ease: "expo.out" }, 26.1);
    // aurora sheen sweep across the shield
    tl.fromTo("#shield-sheen", { xPercent: -120, autoAlpha: 0 },
      { xPercent: 120, autoAlpha: 1, duration: 1.6, ease: "power2.inOut" }, 26.5);
    float("#shield", 27.0, 2.6, { y: 12, cycle: 2.6 });

    // checklist cascade — soft glass rows fading/sliding in
    $$("[data-check]", s).forEach((c, i) => {
      const at = 26.6 + i * 0.5;
      glassReveal(c, at, { x: 40, y: 0, blur: 12, dur: 0.55 });
      tl.fromTo(c.querySelector(".box"), { scale: 0, filter: "blur(6px)" },
        { scale: 1, filter: "blur(0px)", duration: 0.45, ease: "power3.out" }, at + 0.08);
    });

    glassReveal("#s-privacy [data-ftag]", 26.4, { y: 24, blur: 12, dur: 0.6 });
  })();

  /* --------------------------- SCENE 8 : CTA (30–34) ----------------------- */
  (() => {
    fadeScene("#s-cta", 30.0, 34.0, 0.5);

    // soft aurora bloom on the cut (not a white flash)
    tl.fromTo("#bloom", { opacity: 0 }, { opacity: 0.65, duration: 0.5, ease: "power2.out" }, 30.0);
    tl.to("#bloom", { opacity: 0, duration: 1.2, ease: "power2.in" }, 30.5);

    glassReveal("#cta-orb", 30.15, { scale: 0.8, blur: 22, y: 0, dur: 0.8, ease: "expo.out" });
    tl.to("#cta-orb", { boxShadow: "0 40px 90px rgba(0,0,0,.45), inset 0 1px 0 rgba(255,255,255,.25), 0 0 110px rgba(34,211,238,.5)",
      duration: 1.2, yoyo: true, repeat: 1, ease: "sine.inOut" }, 30.6);
    float("#cta-orb", 31.0, 2.5, { y: 9, cycle: 2.5 });

    glassReveal("#cta-name", 30.55, { y: 36, blur: 14, dur: 0.7 });
    glassReveal("#cta-tag",  30.9, { y: 28, blur: 12, dur: 0.7 });
    tl.fromTo("#platform", { autoAlpha: 0, letterSpacing: "0.6em" },
      { autoAlpha: 1, letterSpacing: "0.28em", duration: 0.8, ease: "power2.out" }, 31.4);

    // final soft fade
    tl.to("#fade", { opacity: 1, duration: 1.2, ease: "power2.in" }, 32.8);
  })();

  // Pad to exact duration
  tl.set({}, {}, DURATION);

  /* ===========================  PUBLIC API  =============================== */
  window.__duration = DURATION;
  window.__tl = tl;
  window.__seek = (t) => { tl.pause(); tl.seek(Math.max(0, Math.min(DURATION, t)), false); };
  window.__ready = true;

  /* ===================  LIVE PREVIEW (browser only)  ====================== */
  // Loads cues.json for SFX if served over http(s); silently skips on file://.
  const sfxBank = {};
  let musicEl = null, cues = null;
  const CAPTURE = new URLSearchParams(location.search).has("capture");
  window.__CAPTURE = CAPTURE;

  async function loadAudio() {
    try {
      const res = await fetch("cues.json", { cache: "no-store" });
      cues = await res.json();
      (cues.audio?.sfx || []).forEach((c) => {
        if (!sfxBank[c.file]) {
          const a = new Audio(c.file); a.preload = "auto"; sfxBank[c.file] = a;
        }
      });
      if (cues.audio?.music?.file) {
        musicEl = new Audio(cues.audio.music.file);
        musicEl.volume = cues.audio.music.gain ?? 0.5;
        musicEl.loop = false;
      }
      // schedule SFX + voice-over as timeline callbacks (preview only)
      const schedule = (list, defGain) => (list || []).forEach((c) => {
        if (!sfxBank[c.file]) { const a = new Audio(c.file); a.preload = "auto"; sfxBank[c.file] = a; }
        tl.call(() => {
          if (window.__CAPTURE) return;
          const base = sfxBank[c.file]; if (!base) return;
          const n = base.cloneNode(); n.volume = c.gain ?? defGain;
          n.play().catch(() => {});
        }, null, c.time);
      });
      schedule(cues.audio?.voice, 1.0);
      schedule(cues.audio?.sfx, 0.8);
    } catch (_) { /* file:// or no audio yet — preview stays silent */ }
  }

  function playPreview() {
    if (musicEl) { musicEl.currentTime = 0; musicEl.play().catch(() => {}); }
    tl.restart();
  }

  if (!CAPTURE) {
    loadAudio().finally(() => {
      // autoplay visual loop; click to (re)start with sound (browser autoplay policy)
      tl.eventCallback("onComplete", () => {
        if (musicEl) musicEl.pause();
        gsap.delayedCall(0.8, playPreview);
      });
      tl.play();
      window.addEventListener("click", playPreview);
    });
  }
})();
