/* ============================================================================
   Koli — Motion Design Video (LIGHT / "Gojiberry" corporate style)
   Master timeline (GSAP, deterministic)
   - Builds a single paused GSAP timeline (35s).
   - Exposes window.__seek(t) / window.__duration for frame-accurate capture.
   - Plays a live preview loop (with optional SFX) when opened in a browser.
   Signature of this cut vs the dark version: a custom mouse cursor that travels
   along precise paths and triggers synchronized button / toggle states, snappy
   easing, staggered cascades and short blur "whip" entrances.
   All animation is time-based (no Date.now / free rAF) => seek is reproducible.
   ========================================================================== */
(() => {
  "use strict";

  const DURATION = 35;          // seconds (keep in sync with cues.json)
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

  /* ---- Seeded RNG so any jitter keyframes are baked & reproducible --------- */
  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  const rnd = mulberry32(0x4b4f4c49); // "KOLI" seed

  /* ---- Defaults: snappy, dampened easing ----------------------------------- */
  gsap.defaults({ ease: "power3.out" });

  // Elastic "pop": 0 -> overshoot -> settle
  function pop(tl, target, at, dur = 0.5, over = 2.2) {
    tl.fromTo(target, { scale: 0, opacity: 0 },
      { scale: 1, opacity: 1, duration: dur, ease: `back.out(${over})` }, at);
  }

  // Animated number counter (correct on seek)
  function counter(el, tl, at, dur = 1.3) {
    const target = parseFloat(el.dataset.count);
    const proxy = { v: 0 };
    tl.to(proxy, { v: target, duration: dur, ease: "power2.out",
      onUpdate() { el.textContent = Math.round(proxy.v).toString(); } }, at);
  }

  // Typewriter (correct on seek)
  function typewriter(el, text, tl, at, dur) {
    const proxy = { i: 0 };
    tl.to(proxy, { i: text.length, duration: dur, ease: "none",
      onUpdate() { el.textContent = text.slice(0, Math.round(proxy.i)); } }, at);
  }

  // Scene cross-fade wrapper
  function fadeScene(tl, sel, start, end, fade = 0.35) {
    const el = $(sel);
    tl.set(el, { autoAlpha: 0 }, 0);
    tl.to(el, { autoAlpha: 1, duration: fade }, start);
    tl.to(el, { autoAlpha: 0, duration: fade }, end - fade);
    return el;
  }

  // Short blur "whip" entrance for a scene's hero element (camera stays calm)
  function whipIn(tl, target, at, fromX = 160, dur = 0.5) {
    tl.fromTo(target, { x: fromX, opacity: 0, filter: "blur(10px)" },
      { x: 0, opacity: 1, filter: "blur(0px)", duration: dur, ease: "power3.out" }, at);
  }

  /* ---- Custom cursor choreography ------------------------------------------ */
  const cursorEl = $("#cursor");
  // place cursor (stage coords -> screen-space transform); offset so the SVG tip
  // (top-left of the arrow) lands on the target point.
  function curTo(tl, x, y, at, dur = 0.5) {
    tl.to(cursorEl, { x: x - 3, y: y - 2, duration: dur, ease: "power2.inOut" }, at);
    return at + dur;
  }
  function curShow(tl, x, y, at) {
    tl.set(cursorEl, { x: x - 3, y: y - 2 }, at);
    tl.to(cursorEl, { opacity: 1, duration: 0.2 }, at);
  }
  function curHide(tl, at) { tl.to(cursorEl, { opacity: 0, duration: 0.2 }, at); }
  // a tactile click: little dip + orange ring ping
  function curClick(tl, at) {
    tl.to(cursorEl, { scale: 0.82, duration: 0.08, yoyo: true, repeat: 1, ease: "power2.out" }, at);
    tl.add(() => cursorEl.classList.add("click"), at);
    tl.add(() => cursorEl.classList.remove("click"), at + 0.28);
  }

  /* ===========================  MASTER TIMELINE  =========================== */
  const tl = gsap.timeline({ paused: true });
  gsap.set(cursorEl, { opacity: 0 });

  /* --------------------------- SCENE 1 : INTRO (0–4) ----------------------- */
  (() => {
    fadeScene(tl, "#s-intro", 0.0, 4.0);
    // subtle camera push-in for premium feel
    tl.fromTo("#camera", { scale: 1.06 }, { scale: 1.0, duration: 4.0, ease: "power2.out" }, 0.0);

    pop(tl, "#intro-badge", 0.25, 0.6, 2.0);
    tl.fromTo("#intro-ring", { scale: 1.5, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.7 }, 0.35);
    tl.to("#intro-ring", { rotation: 180, duration: 3.0, ease: "none" }, 0.5);

    // wordmark letters cascade up with overshoot
    tl.from($$("#intro-word .word"), { y: 70, opacity: 0, scale: 0.5, rotationX: -50,
      duration: 0.5, stagger: 0.08, ease: "back.out(2.2)" }, 0.55);
    tl.from("#intro-tag", { y: 24, opacity: 0, duration: 0.5 }, 1.15);
    tl.from("#intro-kick", { opacity: 0, letterSpacing: "0.9em", duration: 0.7 }, 1.4);
    // tagline highlight color sweep
    tl.from("#intro-tag .hl", { color: "#0E1726", duration: 0.5 }, 1.6);
  })();

  /* --------------------------- SCENE 2 : DICTATE (4–9) --------------------- */
  (() => {
    const s = fadeScene(tl, "#s-dictate", 4.0, 9.0);
    const waves = $$("[data-wave]", s);

    tl.from("#s-dictate [data-eyebrow]", { y: -20, opacity: 0, duration: 0.4 }, 4.15);
    tl.from($$("#dict-line .word, #dict-line"), { y: 36, opacity: 0, duration: 0.5,
      stagger: 0.05, ease: "back.out(1.8)" }, 4.25);

    // app window whips in from the right (2.5D float)
    whipIn(tl, "#dict-window", 4.45, 220, 0.55);

    // recording row + live waveform
    tl.from("#dict-rec", { opacity: 0, x: -20, duration: 0.4 }, 4.7);
    tl.from("#dict-rec .rec-blob", { scale: 0.4, duration: 0.45, repeat: 8, yoyo: true, ease: "sine.inOut" }, 4.8);
    gsap.set(waves, { scaleY: 0.18, transformOrigin: "50% 50%" });
    waves.forEach((b, i) => {
      const amp = 0.4 + rnd() * 0.6;
      tl.to(b, { scaleY: amp, duration: 0.24 + rnd() * 0.16, repeat: 14, yoyo: true,
        ease: "sine.inOut" }, 4.7 + (i % 6) * 0.05);
    });

    // caret blink + Koli auto-types into the active window
    tl.to("#dict-caret", { opacity: 0, duration: 0.18, repeat: 16, yoyo: true, ease: "steps(1)" }, 5.0);
    typewriter($("#dict-typed"), "Koli types straight into the active window.", tl, 5.1, 2.0);

    tl.from("#s-dictate [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 5.0);
  })();

  /* --------------------------- SCENE 3 : MEETING (9–15) -------------------- */
  (() => {
    const s = fadeScene(tl, "#s-meeting", 9.0, 15.0);
    const win = $("#meet-window");
    const mic = $("#meet-mic"), sys = $("#meet-sys");

    // toggles start OFF; cursor will switch them on
    tl.add(() => { mic.classList.remove("on"); sys.classList.remove("on"); }, 9.0);

    // window flies in with a soft isometric settle
    tl.fromTo(win,
      { rotateY: -16, rotateX: 9, y: 90, z: -260, opacity: 0, filter: "blur(8px)" },
      { rotateY: 0, rotateX: 0, y: 0, z: 0, opacity: 1, filter: "blur(0px)", duration: 0.8, ease: "power3.out" }, 9.05);

    // cursor enters and clicks both source toggles ON
    curShow(tl, 980, 300, 9.45);
    curTo(tl, 470, 372, 9.55, 0.5);
    curClick(tl, 9.7);
    tl.add(() => mic.classList.add("on"), 9.72);
    tl.fromTo(mic, { scale: 0.97 }, { scale: 1, duration: 0.25, ease: "back.out(2.5)" }, 9.72);
    curTo(tl, 470, 424, 9.9, 0.35);
    curClick(tl, 10.05);
    tl.add(() => sys.classList.add("on"), 10.07);
    tl.fromTo(sys, { scale: 0.97 }, { scale: 1, duration: 0.25, ease: "back.out(2.5)" }, 10.07);
    curHide(tl, 10.35);

    // stat cards + counters
    tl.from($$("[data-mstat]", s), { y: 36, opacity: 0, scale: 0.85, duration: 0.45,
      stagger: 0.12, ease: "back.out(1.8)" }, 10.7);
    $$("[data-count]", s).forEach((el) => counter(el, tl, 10.9, 1.3));

    // colour-coded transcript cascades in
    tl.from($$("[data-line]", s), { x: 40, opacity: 0, duration: 0.45,
      stagger: 0.5, ease: "power3.out" }, 10.9);

    // export chips pop
    tl.from($$("[data-exp]", s), { scale: 0, opacity: 0, duration: 0.35,
      stagger: 0.2, ease: "back.out(3)" }, 13.4);

    // slow parallax drift for life
    tl.to(win, { y: -10, duration: 3.5, ease: "sine.inOut" }, 11.0);
    tl.from("#s-meeting [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 10.7);
  })();

  /* --------------------------- SCENE 4 : REWRITE (15–21) ------------------- */
  (() => {
    const s = fadeScene(tl, "#s-rewrite", 15.0, 21.0);
    const badge = $("#rw-badge"), body = $("#rw-body");
    const btn = $("#rw-btn"), menu = $("#rw-menu"), langCur = $("#rw-lang-cur");

    whipIn(tl, "#rw-editor", 15.1, 180, 0.55);

    // --- cursor clicks "Rewrite": raw -> polished ---
    curShow(tl, 980, 250, 15.7);
    curTo(tl, 1130, 432, 15.85, 0.5);
    curClick(tl, 16.4);
    tl.add(() => btn.classList.add("pressed"), 16.38);
    tl.add(() => btn.classList.remove("pressed"), 16.6);
    // text morphs (blur swap) + badge swaps to Polished
    tl.to(body, { opacity: 0, filter: "blur(8px)", duration: 0.22 }, 16.55);
    tl.add(() => {
      body.textContent = "So — we need to ship this by Friday.";
      badge.textContent = "Polished"; badge.className = "badge pol";
    }, 16.78);
    tl.fromTo(body, { opacity: 0, filter: "blur(8px)" },
      { opacity: 1, filter: "blur(0px)", duration: 0.3 }, 16.8);
    tl.fromTo(badge, { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.4, ease: "back.out(2.4)" }, 16.8);

    // --- cursor opens language menu and picks FR ---
    curTo(tl, 1340, 432, 17.4, 0.5);
    curClick(tl, 18.0);
    tl.fromTo(menu, { opacity: 0, scale: 0.9, y: -10 },
      { opacity: 1, scale: 1, y: 0, duration: 0.28, ease: "back.out(2)" }, 18.05);
    curTo(tl, 1360, 548, 18.3, 0.35);
    curClick(tl, 18.6);
    tl.add(() => { $$(".lang-opt", menu).forEach(o => o.classList.remove("sel"));
      $$(".lang-opt", menu)[1].classList.add("sel"); }, 18.6);
    tl.to(menu, { opacity: 0, scale: 0.95, duration: 0.22 }, 18.85);
    curHide(tl, 19.0);

    // --- text translates to FR + badge -> Translated ---
    tl.to(body, { opacity: 0, filter: "blur(8px)", duration: 0.22 }, 18.9);
    tl.add(() => {
      body.textContent = "Nous visons une livraison vendredi.";
      badge.textContent = "Translated · FR"; badge.className = "badge trn";
      langCur.textContent = "FR";
    }, 19.13);
    tl.fromTo(body, { opacity: 0, filter: "blur(8px)" },
      { opacity: 1, filter: "blur(0px)", duration: 0.32 }, 19.15);
    tl.fromTo(badge, { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.4, ease: "back.out(2.4)" }, 19.15);

    tl.from("#s-rewrite [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 15.8);
  })();

  /* --------------------------- SCENE 5 : ASSISTANT (21–27) ----------------- */
  (() => {
    const s = fadeScene(tl, "#s-assistant", 21.0, 27.0);
    const nodes  = $$("[data-pnode]", s);
    const arrows = $$("[data-parrow]", s);

    tl.from("#s-assistant [data-eyebrow]", { y: -20, opacity: 0, scale: 0.9, duration: 0.4, ease: "back.out(2)" }, 21.1);
    tl.from("#assist-head", { y: 30, opacity: 0, scale: 0.85, duration: 0.5, ease: "back.out(2)" }, 21.25);

    // pipeline nodes pop left->right, arrows draw between
    nodes.forEach((n, i) => {
      const at = 21.9 + i * 0.7;
      tl.fromTo(n, { y: 40, opacity: 0, scale: 0.85, filter: "blur(6px)" },
        { y: 0, opacity: 1, scale: 1, filter: "blur(0px)", duration: 0.5, ease: "back.out(1.8)" }, at);
      if (i < arrows.length) {
        tl.fromTo(arrows[i], { opacity: 0, x: -16 }, { opacity: 1, x: 0, duration: 0.35 }, at + 0.45);
      }
    });

    // hands-free stamp slams in
    tl.fromTo("#assist-hf", { scale: 0, opacity: 0, rotation: -18 },
      { scale: 1, opacity: 1, rotation: -3, duration: 0.5, ease: "back.out(2.6)" }, 24.7);
    tl.to("#assist-hf", { scale: 1.07, duration: 0.22, yoyo: true, repeat: 3, ease: "sine.inOut" }, 25.2);

    tl.from("#s-assistant [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 22.0);
  })();

  /* --------------------------- SCENE 6 : TRUST (27–31) --------------------- */
  (() => {
    const s = fadeScene(tl, "#s-privacy", 27.0, 31.0);
    tl.from("#trust-head", { y: 30, opacity: 0, scale: 0.9, duration: 0.5, ease: "back.out(1.8)" }, 27.1);
    // cards cascade in; each checkmark pops with a "ding"
    $$("[data-trust]", s).forEach((c, i) => {
      const at = 27.55 + i * 0.45;
      tl.fromTo(c, { y: 36, opacity: 0, scale: 0.9 },
        { y: 0, opacity: 1, scale: 1, duration: 0.45, ease: "back.out(1.7)" }, at);
      tl.fromTo(c.querySelector(".tbox"), { scale: 0, rotation: -90 },
        { scale: 1, rotation: 0, duration: 0.4, ease: "back.out(3)" }, at + 0.12);
    });
    tl.from("#s-privacy [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 27.4);
  })();

  /* --------------------------- SCENE 7 : CTA (31–35) ----------------------- */
  (() => {
    fadeScene(tl, "#s-cta", 31.0, 35.0, 0.4);
    // soft white flash on cut
    tl.fromTo("#flash", { opacity: 0 }, { opacity: 0.6, duration: 0.12 }, 31.0);
    tl.to("#flash", { opacity: 0, duration: 0.5 }, 31.12);

    pop(tl, "#cta-badge", 31.15, 0.6, 2.0);
    tl.from("#cta-name", { y: 40, opacity: 0, duration: 0.5 }, 31.45);
    tl.from("#cta-tag",  { y: 28, opacity: 0, duration: 0.55 }, 31.8);
    tl.from("#cta-tag .hl", { color: "#0E1726", duration: 0.5 }, 32.1);

    // cursor moves to the button and presses it (tactile state)
    tl.from("#cta-btn", { y: 26, opacity: 0, duration: 0.5, ease: "back.out(1.8)" }, 32.1);
    curShow(tl, 1180, 560, 31.9);
    curTo(tl, 960, 726, 32.05, 0.5);
    curClick(tl, 32.6);
    tl.add(() => $("#cta-btn").classList.add("pressed"), 32.58);
    tl.add(() => $("#cta-btn").classList.remove("pressed"), 32.82);
    curHide(tl, 33.1);

    tl.from("#platform", { opacity: 0, letterSpacing: "0.6em", duration: 0.7 }, 32.5);
    // final soft fade to white
    tl.to("#fade", { opacity: 1, duration: 1.0, ease: "power2.in" }, 34.0);
  })();

  // Pad to exact duration
  tl.set({}, {}, DURATION);

  /* ===========================  PUBLIC API  =============================== */
  window.__duration = DURATION;
  window.__tl = tl;
  window.__seek = (t) => { tl.pause(); tl.seek(Math.max(0, Math.min(DURATION, t)), false); };
  window.__ready = true;

  /* ===================  LIVE PREVIEW (browser only)  ====================== */
  const sfxBank = {};
  let musicEl = null, cues = null;
  const CAPTURE = new URLSearchParams(location.search).has("capture");
  window.__CAPTURE = CAPTURE;

  async function loadAudio() {
    try {
      const res = await fetch("cues.json", { cache: "no-store" });
      cues = await res.json();
      if (cues.audio?.music?.file) {
        musicEl = new Audio(cues.audio.music.file);
        musicEl.volume = cues.audio.music.gain ?? 0.5;
        musicEl.loop = false;
      }
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
      tl.eventCallback("onComplete", () => {
        if (musicEl) musicEl.pause();
        gsap.delayedCall(0.8, playPreview);
      });
      tl.play();
      window.addEventListener("click", playPreview);
    });
  }
})();
