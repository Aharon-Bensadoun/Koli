/* ============================================================================
   Koli — Motion Design Video (LIGHT / "Daylight" — authentic Koli identity)
   Master timeline (GSAP, deterministic)
   - Single paused GSAP timeline (35s) + a frame-locked ambient timeline.
   - Exposes window.__seek(t) / window.__duration for frame-accurate capture.
   - Authentic AURORA palette, real-app screens, dynamic aurora-streak transitions,
     a custom cursor that travels precise (auto-measured) paths and triggers
     synchronized button / toggle states.
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

  /* ---- Seeded RNG so jitter keyframes are baked & reproducible ------------- */
  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  const rnd = mulberry32(0x4b4f4c49); // "KOLI" seed

  gsap.defaults({ ease: "power3.out" });

  /* ---- Generic motion helpers --------------------------------------------- */
  function pop(tl, target, at, dur = 0.5, over = 2.2) {
    tl.fromTo(target, { scale: 0, opacity: 0 },
      { scale: 1, opacity: 1, duration: dur, ease: `back.out(${over})` }, at);
  }
  function counter(el, tl, at, dur = 1.3) {
    const target = parseFloat(el.dataset.count);
    const proxy = { v: 0 };
    tl.to(proxy, { v: target, duration: dur, ease: "power2.out",
      onUpdate() { el.textContent = Math.round(proxy.v).toString(); } }, at);
  }
  function timerCount(el, tl, at, dur, toSec) {
    const proxy = { v: 0 };
    tl.to(proxy, { v: toSec, duration: dur, ease: "power1.out", onUpdate() {
      const s = Math.round(proxy.v);
      el.textContent = `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
    } }, at);
  }
  function typewriter(el, text, tl, at, dur) {
    const proxy = { i: 0 };
    tl.to(proxy, { i: text.length, duration: dur, ease: "none",
      onUpdate() { el.textContent = text.slice(0, Math.round(proxy.i)); } }, at);
  }
  function fadeScene(tl, sel, start, end, fade = 0.35) {
    const el = $(sel);
    tl.set(el, { autoAlpha: 0 }, 0);
    tl.to(el, { autoAlpha: 1, duration: fade }, start);
    tl.to(el, { autoAlpha: 0, duration: fade }, end - fade);
    return el;
  }
  function whipIn(tl, target, at, fromX = 160, dur = 0.5) {
    tl.fromTo(target, { x: fromX, opacity: 0, filter: "blur(10px)" },
      { x: 0, opacity: 1, filter: "blur(0px)", duration: dur, ease: "expo.out" }, at);
  }

  /* ---- Modern FX helpers --------------------------------------------------- */
  // Glossy light sheen passing across a glassy surface (selector or element).
  function sheenSweep(tl, sel, at, dur = 1.0) {
    const host = (typeof sel === "string") ? $(sel) : sel; if (!host) return;
    const s = document.createElement("div"); s.className = "sheen"; host.appendChild(s);
    tl.fromTo(s, { xPercent: -160, opacity: 0 },
      { xPercent: 320, opacity: 1, duration: dur, ease: "power2.inOut" }, at);
    tl.to(s, { opacity: 0, duration: 0.25 }, at + dur - 0.25);
  }
  // Depth-of-field: push the background out of focus while a UI hero is on screen.
  function focusPull(tl, start, end, amt = 4) {
    tl.to("#bg", { filter: `blur(${amt}px)`, scale: 1.05, duration: 0.7, ease: "power2.out" }, start);
    tl.to("#bg", { filter: "blur(0px)", scale: 1.0, duration: 0.7, ease: "power2.inOut" }, end - 0.7);
  }
  // Light flash at the cut.
  function flash(tl, at, peak = 0.3) {
    tl.fromTo("#flash", { opacity: 0 }, { opacity: peak, duration: 0.12 }, at - 0.04);
    tl.to("#flash", { opacity: 0, duration: 0.34 }, at + 0.08);
  }

  /* ---- A distinct, dynamic transition per scene cut ------------------------ */
  // 1) Aurora streak whip + zoom-through.
  function txStreak(tl, at, dir = 1) {
    const a = dir > 0 ? -170 : 270, b = dir > 0 ? 270 : -170;
    tl.fromTo("#camera", { scale: 1.12 }, { scale: 1.0, duration: 0.62, ease: "expo.out", immediateRender: false }, at);
    tl.fromTo("#camera", { filter: "blur(16px)" }, { filter: "blur(0px)", duration: 0.5, ease: "power2.out", immediateRender: false }, at);
    tl.set(["#fx-lead", "#fx-trail"], { opacity: 1, rotation: 0, yPercent: 0 }, at - 0.18);
    tl.fromTo("#fx-lead",  { xPercent: a }, { xPercent: b, duration: 0.62, ease: "power3.inOut" }, at - 0.18);
    tl.fromTo("#fx-trail", { xPercent: a }, { xPercent: b, duration: 0.62, ease: "power3.inOut" }, at - 0.10);
    tl.set(["#fx-lead", "#fx-trail"], { opacity: 0 }, at + 0.48);
    flash(tl, at, 0.3);
  }
  // 2) Iris close — aurora disc shrinks to reveal the scene.
  function txIris(tl, at) {
    tl.set("#fx-panel", { opacity: 1, borderRadius: "50%", left: "50%", top: "50%",
      xPercent: -50, yPercent: -50, width: 2700, height: 2700, scale: 1, rotation: 0, skewX: 0, skewY: 0 }, at - 0.08);
    tl.fromTo("#fx-panel", { scale: 1.0 }, { scale: 0, duration: 0.66, ease: "power3.inOut", immediateRender: false }, at);
    tl.set("#fx-panel", { opacity: 0 }, at + 0.66);
    tl.fromTo("#camera", { scale: 1.18, filter: "blur(10px)" }, { scale: 1.0, filter: "blur(0px)", duration: 0.72, ease: "expo.out", immediateRender: false }, at);
    flash(tl, at, 0.28);
  }
  // 3) Vertical wipe — skewed aurora panel sweeps top -> bottom.
  function txVwipe(tl, at) {
    tl.set("#fx-panel", { opacity: 1, borderRadius: 0, left: 0, top: 0, width: "100%", height: "130%",
      xPercent: 0, scale: 1, rotation: 0, skewX: 0, skewY: -5 }, at - 0.16);
    tl.fromTo("#fx-panel", { yPercent: -125 }, { yPercent: 125, duration: 0.66, ease: "power3.inOut", immediateRender: false }, at - 0.16);
    tl.set("#fx-panel", { opacity: 0, skewY: 0 }, at + 0.52);
    tl.fromTo("#camera", { y: -70, filter: "blur(12px)" }, { y: 0, filter: "blur(0px)", duration: 0.62, ease: "expo.out", immediateRender: false }, at);
    flash(tl, at, 0.26);
  }
  // 4) Spin punch — camera rotates + diagonal aurora streaks cross.
  function txSpin(tl, at, dir = 1) {
    tl.fromTo("#camera", { scale: 1.2, rotation: dir * 6, filter: "blur(14px)" },
      { scale: 1.0, rotation: 0, filter: "blur(0px)", duration: 0.74, ease: "expo.out", immediateRender: false }, at);
    tl.set(["#fx-lead", "#fx-trail"], { opacity: 1, rotation: 20 }, at - 0.16);
    tl.fromTo("#fx-lead",  { xPercent: -190 }, { xPercent: 290, duration: 0.6, ease: "power3.inOut" }, at - 0.16);
    tl.fromTo("#fx-trail", { xPercent: 290 }, { xPercent: -190, duration: 0.6, ease: "power3.inOut" }, at - 0.10);
    tl.set(["#fx-lead", "#fx-trail"], { opacity: 0, rotation: 0 }, at + 0.5);
    flash(tl, at, 0.32);
  }
  // 5) Split — two aurora panels part vertically.
  function txSplit(tl, at) {
    tl.set("#fx-panel",  { opacity: 1, borderRadius: 0, left: 0, top: 0, width: "100%", height: "51%",
      xPercent: 0, yPercent: 0, scale: 1, rotation: 0, skewX: 0, skewY: 0 }, at - 0.05);
    tl.set("#fx-panel2", { opacity: 1, borderRadius: 0, left: 0, top: "49%", width: "100%", height: "51%",
      xPercent: 0, yPercent: 0, scale: 1, rotation: 0 }, at - 0.05);
    tl.fromTo("#fx-panel",  { yPercent: 0 }, { yPercent: -108, duration: 0.62, ease: "power3.inOut" }, at - 0.05);
    tl.fromTo("#fx-panel2", { yPercent: 0 }, { yPercent: 108, duration: 0.62, ease: "power3.inOut" }, at - 0.05);
    tl.set(["#fx-panel", "#fx-panel2"], { opacity: 0 }, at + 0.6);
    tl.fromTo("#camera", { scale: 0.92, filter: "blur(8px)" }, { scale: 1.0, filter: "blur(0px)", duration: 0.7, ease: "expo.out", immediateRender: false }, at);
    flash(tl, at, 0.24);
  }
  // 6) Bloom — aurora light bursts out + big white flash + zoom settle.
  function txBloom(tl, at) {
    tl.set("#fx-panel2", { opacity: 0, borderRadius: "50%", left: "50%", top: "50%", xPercent: -50, yPercent: -50,
      width: 760, height: 760, scale: 0.2, rotation: 0,
      background: "radial-gradient(circle, rgba(167,139,250,.95), rgba(124,58,237,.5) 35%, transparent 70%)" }, at - 0.05);
    tl.to("#fx-panel2", { opacity: 1, scale: 5.2, duration: 0.5, ease: "power2.out" }, at - 0.05);
    tl.to("#fx-panel2", { opacity: 0, duration: 0.45, ease: "power2.in" }, at + 0.32);
    tl.fromTo("#camera", { scale: 1.22, filter: "blur(12px)" }, { scale: 1.0, filter: "blur(0px)", duration: 0.8, ease: "expo.out", immediateRender: false }, at);
    flash(tl, at + 0.02, 0.55);
  }

  /* ---- Custom cursor choreography (auto-measured targets) ------------------ */
  const cursorEl = $("#cursor");
  // Measure an element's centre in stage (1920x1080) coordinates, scale-aware.
  function centerOf(sel) {
    const el = (typeof sel === "string") ? $(sel) : sel;
    const r = el.getBoundingClientRect();
    const sr = stage.getBoundingClientRect();
    const sc = (sr.width / W) || 1;
    return { x: ((r.left - sr.left) + r.width / 2) / sc, y: ((r.top - sr.top) + r.height / 2) / sc };
  }
  function curTo(tl, x, y, at, dur = 0.5) {
    tl.to(cursorEl, { x: x - 3, y: y - 2, duration: dur, ease: "power2.inOut" }, at);
    return at + dur;
  }
  function curShow(tl, x, y, at) {
    tl.set(cursorEl, { x: x - 3, y: y - 2 }, at);
    tl.to(cursorEl, { opacity: 1, duration: 0.2 }, at);
  }
  function curHide(tl, at) { tl.to(cursorEl, { opacity: 0, duration: 0.2 }, at); }
  function curClick(tl, at) {
    tl.to(cursorEl, { scale: 0.82, duration: 0.08, yoyo: true, repeat: 1, ease: "power2.out" }, at);
    tl.add(() => cursorEl.classList.add("click"), at);
    tl.add(() => cursorEl.classList.remove("click"), at + 0.28);
  }

  /* ---- Ambient layer (own looping timeline, seeked in lock-step) ----------- */
  const amb = gsap.timeline({ paused: true });
  function buildAtmosphere() {
    amb.to(".bg-blob.b1", { x: 130, y: -80, scale: 1.18, duration: 17, repeat: -1, yoyo: true, ease: "sine.inOut" }, 0);
    amb.to(".bg-blob.b2", { x: -110, y: 70, scale: 1.12, duration: 21, repeat: -1, yoyo: true, ease: "sine.inOut" }, 0);
    amb.to(".bg-blob.b3", { x: 80, y: 100, scale: 1.24, duration: 14, repeat: -1, yoyo: true, ease: "sine.inOut" }, 0);
    amb.to(".bg-aurora", { rotation: 360, duration: 90, repeat: -1, ease: "none" }, 0);
    amb.to(".bg-grid", { backgroundPosition: "64px 40px", duration: 24, repeat: -1, yoyo: true, ease: "sine.inOut" }, 0);
  }
  function makeParticles() {
    const host = $("#particles"); if (!host) return;
    const N = 26;
    for (let i = 0; i < N; i++) {
      const p = document.createElement("div"); p.className = "particle";
      const sz = 3 + rnd() * 7;
      Object.assign(p.style, {
        width: sz + "px", height: sz + "px",
        left: (rnd() * W) + "px", top: (rnd() * H) + "px",
        opacity: (0.12 + rnd() * 0.28).toFixed(2)
      });
      const tint = rnd();
      if (tint > 0.66)      p.style.background = "radial-gradient(circle, rgba(34,211,238,.9), rgba(34,211,238,0) 70%)";
      else if (tint > 0.33) p.style.background = "radial-gradient(circle, rgba(244,114,182,.9), rgba(244,114,182,0) 70%)";
      host.appendChild(p);
      const dur = 9 + rnd() * 11;
      amb.to(p, { y: "-=" + (100 + rnd() * 150), x: "+=" + (rnd() * 90 - 45),
        duration: dur, repeat: -1, yoyo: true, ease: "sine.inOut" }, 0);
      amb.to(p, { opacity: (0.05 + rnd() * 0.18).toFixed(2),
        duration: 2 + rnd() * 3.5, repeat: -1, yoyo: true, ease: "sine.inOut" }, rnd() * 2);
    }
  }
  buildAtmosphere();
  makeParticles();

  /* ---- Pre-measure cursor targets in pristine layout ----------------------
     GSAP's from()/fromTo()/set() render their start state immediately (build
     time), which would shift elements before we could measure them. So capture
     every interactive target's centre now, while the DOM is untouched.        */
  const P = {
    rec:    centerOf("#dict-record"),
    rwBtn:  centerOf("#rw-btn"),
    rwLang: centerOf("#rw-lang"),
    rwFr:   centerOf($$("#rw-menu .lang-opt")[1]),
    cta:    centerOf("#cta-btn"),
  };

  /* ===========================  MASTER TIMELINE  =========================== */
  const tl = gsap.timeline({ paused: true });
  gsap.set(cursorEl, { opacity: 0 });

  /* --------------------------- SCENE 1 : INTRO (0–4) ----------------------- */
  (() => {
    fadeScene(tl, "#s-intro", 0.0, 4.0);
    tl.fromTo("#camera", { scale: 1.06 }, { scale: 1.0, duration: 4.0, ease: "power2.out" }, 0.0);
    tl.fromTo("#camera", { filter: "blur(18px)" }, { filter: "blur(0px)", duration: 0.9, ease: "power2.out" }, 0.0);

    // opening aurora swipe reveal
    tl.set(["#fx-lead", "#fx-trail"], { opacity: 1 }, 0.0);
    tl.fromTo("#fx-lead",  { xPercent: -180 }, { xPercent: 280, duration: 0.75, ease: "power3.out" }, 0.0);
    tl.fromTo("#fx-trail", { xPercent: -180 }, { xPercent: 280, duration: 0.75, ease: "power3.out" }, 0.09);
    tl.set(["#fx-lead", "#fx-trail"], { opacity: 0 }, 0.85);

    pop(tl, "#intro-badge", 0.35, 0.6, 2.0);
    tl.fromTo("#intro-badge",
      { boxShadow: "0 20px 50px rgba(16,17,34,.13), 0 50px 120px rgba(124,58,237,.14)" },
      { boxShadow: "0 20px 50px rgba(16,17,34,.16), 0 30px 90px rgba(124,58,237,.50)",
        duration: 1.1, yoyo: true, repeat: 1, ease: "sine.inOut" }, 0.8);
    tl.fromTo("#intro-ring", { scale: 1.5, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.7 }, 0.45);
    tl.to("#intro-ring", { rotation: 360, duration: 3.4, ease: "none" }, 0.6);

    // animate the whole wordmark (not per-letter) so the gradient text clip
    // is never broken by descendant transforms
    tl.from("#intro-word", { y: 64, opacity: 0, filter: "blur(14px)", duration: 0.8, ease: "expo.out" }, 0.65);
    tl.fromTo("#intro-word", { backgroundPosition: "0% 0" },
      { backgroundPosition: "260% 0", duration: 3.2, ease: "sine.inOut" }, 0.7);
    tl.from("#intro-tag", { y: 24, opacity: 0, duration: 0.5 }, 1.25);
    tl.from("#intro-kick", { opacity: 0, letterSpacing: "0.9em", duration: 0.7 }, 1.5);
    tl.from("#intro-tag .hl", { color: "#101122", duration: 0.5 }, 1.7);
  })();

  /* --------------------------- SCENE 2 : HOME / DICTATE (4–9) -------------- */
  (() => {
    const s = fadeScene(tl, "#s-dictate", 4.0, 9.0);
    const waves = $$("[data-wave]", s);

    txStreak(tl, 4.0, 1);
    focusPull(tl, 4.5, 9.0, 3);

    tl.from("#s-dictate .screen-head > *", { y: -22, opacity: 0, duration: 0.45, stagger: 0.08, ease: "back.out(1.8)" }, 4.2);

    // Koli home card + active target window fly in
    whipIn(tl, "#dict-window", 4.45, -120, 0.6);
    tl.fromTo("#dict-target", { x: 160, y: 30, opacity: 0, rotateY: 12, filter: "blur(8px)" },
      { x: 0, y: 0, opacity: 1, rotateY: 0, filter: "blur(0px)", duration: 0.65, ease: "expo.out" }, 4.7);
    tl.fromTo("#dict-flow", { opacity: 0, scale: 0.8 }, { opacity: 1, scale: 1, duration: 0.4, ease: "back.out(2)" }, 5.2);
    sheenSweep(tl, "#dict-window", 5.0, 1.1);
    sheenSweep(tl, "#dict-target", 5.3, 1.0);

    // cursor presses the record button
    const rec = P.rec;
    curShow(tl, rec.x + 220, rec.y + 120, 4.7);
    curTo(tl, rec.x, rec.y, 4.8, 0.45);
    curClick(tl, 5.05);
    curHide(tl, 5.4);

    // record button comes alive: halo breathes, ring spins
    tl.to("#dict-halo", { opacity: 0.8, duration: 0.4 }, 5.05);
    tl.to("#dict-halo", { scale: 1.12, duration: 1.0, yoyo: true, repeat: 3, ease: "sine.inOut" }, 5.1);
    tl.to("#dict-ring", { rotation: 360, duration: 4.0, ease: "none" }, 5.05);

    // REC chip blink + timer
    tl.fromTo("#dict-recchip", { opacity: 0, scale: 0.8 }, { opacity: 1, scale: 1, duration: 0.3, ease: "back.out(2)" }, 5.05);
    tl.to("#dict-recchip .rec-blob", { opacity: 0.3, duration: 0.5, repeat: 6, yoyo: true, ease: "sine.inOut" }, 5.2);
    timerCount($("#dict-timer"), tl, 5.1, 3.6, 8);

    // live waveform
    gsap.set(waves, { scaleY: 0.18, transformOrigin: "50% 50%" });
    waves.forEach((b, i) => {
      const amp = 0.4 + rnd() * 0.6;
      tl.to(b, { scaleY: amp, duration: 0.24 + rnd() * 0.16, repeat: 14, yoyo: true, ease: "sine.inOut" }, 5.1 + (i % 6) * 0.05);
    });

    // text recognised in Koli, then auto-typed into the active window
    tl.to(["#dict-caret", "#dict-caret2"], { opacity: 0, duration: 0.18, repeat: 16, yoyo: true, ease: "steps(1)" }, 5.2);
    typewriter($("#dict-live"), "We need to ship the release by Friday.", tl, 5.3, 1.8);
    typewriter($("#dict-typed"), "We need to ship the release by Friday.", tl, 5.9, 1.9);

    tl.from("#s-dictate [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 5.1);
  })();

  /* --------------------------- SCENE 3 : MEETING (9–15) -------------------- */
  (() => {
    const s = fadeScene(tl, "#s-meeting", 9.0, 15.0);
    const win = $("#meet-window");

    txIris(tl, 9.0);
    focusPull(tl, 9.2, 15.0, 3);

    tl.from("#s-meeting .screen-head > *", { y: -22, opacity: 0, duration: 0.45, stagger: 0.08, ease: "back.out(1.8)" }, 9.2);

    // window flies in with a soft isometric settle
    tl.fromTo(win,
      { rotateY: -14, rotateX: 8, y: 80, z: -240, opacity: 0, filter: "blur(8px)" },
      { rotateY: 0, rotateX: 0, y: 0, z: 0, opacity: 1, filter: "blur(0px)", duration: 0.8, ease: "expo.out" }, 9.15);
    sheenSweep(tl, "#meet-window", 9.95, 1.3);

    // live pill blink + source highlight + duration
    tl.to("#meet-window .live-pill .rec-blob", { opacity: 0.3, duration: 0.5, repeat: 8, yoyo: true, ease: "sine.inOut" }, 9.9);
    tl.fromTo("#meet-source", { scale: 0.96, boxShadow: "0 0 0 0 rgba(124,58,237,0)" },
      { scale: 1, boxShadow: "0 0 0 4px rgba(124,58,237,.16)", duration: 0.5, ease: "back.out(2.2)" }, 10.0);
    timerCount($("#meet-timer"), tl, 10.0, 3.5, 768); // 12:48

    // count badges
    $$("[data-count]", s).forEach((el) => counter(el, tl, 10.2, 1.2));

    // diarized transcript cascades (rail grows + card slides)
    $$("[data-line]", s).forEach((seg, i) => {
      const at = 10.4 + i * 0.55;
      tl.fromTo(seg, { x: 40, opacity: 0 }, { x: 0, opacity: 1, duration: 0.45, ease: "expo.out" }, at);
      tl.fromTo(seg.querySelector(".seg-rail"), { scaleY: 0 }, { scaleY: 1, duration: 0.4, ease: "power3.out" }, at + 0.05);
    });

    // participants pop in
    tl.from($$("[data-part]", s), { x: 30, opacity: 0, scale: 0.9, duration: 0.4, stagger: 0.15, ease: "back.out(1.8)" }, 10.6);

    // export chips pop
    tl.from($$("[data-exp]", s), { scale: 0, opacity: 0, duration: 0.35, stagger: 0.18, ease: "back.out(3)" }, 13.4);

    tl.to(win, { y: -10, duration: 3.5, ease: "sine.inOut" }, 10.6);
    tl.from("#s-meeting [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 10.4);
  })();

  /* --------------------------- SCENE 4 : REWRITE (15–21) ------------------- */
  (() => {
    const s = fadeScene(tl, "#s-rewrite", 15.0, 21.0);
    const badge = $("#rw-badge"), body = $("#rw-body");
    const btn = $("#rw-btn"), menu = $("#rw-menu"), langCur = $("#rw-lang-cur");

    txVwipe(tl, 15.0);
    // entrance: scale-up from centre with a slight 3D tilt
    tl.fromTo("#rw-editor",
      { scale: 0.8, opacity: 0, y: 50, rotationX: 10, transformPerspective: 900, filter: "blur(10px)" },
      { scale: 1, opacity: 1, y: 0, rotationX: 0, filter: "blur(0px)", duration: 0.7, ease: "expo.out" }, 15.15);

    // --- cursor clicks "Rewrite": raw -> polished ---
    const pBtn = P.rwBtn;
    curShow(tl, pBtn.x + 120, pBtn.y - 160, 15.7);
    curTo(tl, pBtn.x, pBtn.y, 15.85, 0.5);
    curClick(tl, 16.4);
    tl.add(() => btn.classList.add("pressed"), 16.38);
    tl.add(() => btn.classList.remove("pressed"), 16.6);
    sheenSweep(tl, "#rw-btn", 16.4, 0.7);
    tl.to(body, { opacity: 0, filter: "blur(8px)", duration: 0.22 }, 16.55);
    tl.add(() => {
      body.textContent = "So — we need to ship this by Friday.";
      badge.textContent = "Polished"; badge.className = "badge pol";
    }, 16.78);
    tl.fromTo(body, { opacity: 0, filter: "blur(8px)" }, { opacity: 1, filter: "blur(0px)", duration: 0.3 }, 16.8);
    tl.fromTo(badge, { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.4, ease: "back.out(2.4)" }, 16.8);

    // --- cursor opens language menu and picks FR ---
    const pLang = P.rwLang;
    curTo(tl, pLang.x, pLang.y, 17.4, 0.5);
    curClick(tl, 18.0);
    tl.fromTo(menu, { opacity: 0, scale: 0.9, y: -10 }, { opacity: 1, scale: 1, y: 0, duration: 0.28, ease: "back.out(2)" }, 18.05);
    const frOpt = $$(".lang-opt", menu)[1];
    const pFr = P.rwFr;
    curTo(tl, pFr.x, pFr.y, 18.3, 0.35);
    curClick(tl, 18.6);
    tl.add(() => { $$(".lang-opt", menu).forEach(o => o.classList.remove("sel")); frOpt.classList.add("sel"); }, 18.6);
    tl.to(menu, { opacity: 0, scale: 0.95, duration: 0.22 }, 18.85);
    curHide(tl, 19.0);

    // --- text translates to FR + badge -> Translated ---
    tl.to(body, { opacity: 0, filter: "blur(8px)", duration: 0.22 }, 18.9);
    tl.add(() => {
      body.textContent = "Nous visons une livraison vendredi.";
      badge.textContent = "Translated · FR"; badge.className = "badge trn";
      langCur.textContent = "FR";
    }, 19.13);
    tl.fromTo(body, { opacity: 0, filter: "blur(8px)" }, { opacity: 1, filter: "blur(0px)", duration: 0.32 }, 19.15);
    tl.fromTo(badge, { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.4, ease: "back.out(2.4)" }, 19.15);

    tl.from("#s-rewrite [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 15.9);
  })();

  /* --------------------------- SCENE 5 : ASSISTANT (21–27) -----------------
     New design: a glowing assistant orb + a live conversation (question →
     web-search status track → answer typed back).                            */
  (() => {
    const s = fadeScene(tl, "#s-assistant", 21.0, 27.0);
    txSpin(tl, 21.0, 1);

    tl.from("#s-assistant .assist-head-wrap > *", { y: -24, opacity: 0, duration: 0.5, stagger: 0.1, ease: "back.out(1.8)" }, 21.2);

    // orb assembles: core pops, rings stagger in, glow blooms
    tl.fromTo("#assist-orb .orb-core", { scale: 0, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.6, ease: "back.out(2.4)" }, 21.5);
    tl.from($$("[data-ring]", s), { scale: 0.4, opacity: 0, duration: 0.6, stagger: 0.1, ease: "back.out(2)" }, 21.6);
    tl.fromTo("#orb-glow", { opacity: 0, scale: 0.6 }, { opacity: 1, scale: 1, duration: 0.7 }, 21.6);
    // orb life — glow breathes, rings orbit, core pulses
    tl.to("#orb-glow", { opacity: 0.7, scale: 1.14, duration: 1.1, yoyo: true, repeat: 4, ease: "sine.inOut" }, 22.2);
    tl.to("#assist-orb .orb-ring.r2", { rotation: 360, duration: 9, ease: "none" }, 21.7);
    tl.to("#assist-orb .orb-ring.r1", { rotation: -360, duration: 13, ease: "none" }, 21.7);
    tl.to("#assist-orb .orb-core", { scale: 1.06, duration: 0.9, yoyo: true, repeat: 4, ease: "sine.inOut" }, 22.3);

    // conversation
    const conv  = $$("[data-conv]", s);
    const steps = $$("[data-st]", s);
    const lines = $$("[data-stline]", s);

    // user question bubble
    tl.fromTo(conv[0], { x: 50, opacity: 0, scale: 0.92 }, { x: 0, opacity: 1, scale: 1, duration: 0.5, ease: "back.out(1.7)" }, 22.2);
    // status track + steps lighting up progressively
    tl.fromTo(conv[1], { y: 18, opacity: 0 }, { y: 0, opacity: 1, duration: 0.4 }, 22.85);
    steps.forEach((st, i) => tl.fromTo(st, { scale: 0.6, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.35, ease: "back.out(2)" }, 22.9 + i * 0.42));
    lines.forEach((ln, i) => tl.fromTo(ln, { scaleX: 0 }, { scaleX: 1, duration: 0.3, ease: "power2.out" }, 23.12 + i * 0.42));
    tl.add(() => steps[0].classList.add("done"), 22.98);
    tl.add(() => steps[1].classList.add("done"), 23.55);
    tl.add(() => steps[2].classList.add("done"), 24.2);

    // assistant answer bubble + typed answer
    tl.fromTo(conv[2], { y: 24, opacity: 0, scale: 0.95 }, { y: 0, opacity: 1, scale: 1, duration: 0.5, ease: "back.out(1.7)" }, 24.2);
    sheenSweep(tl, conv[2], 24.5, 1.0);
    tl.to("#s-assistant .bubble.bot .caret", { opacity: 0, duration: 0.18, repeat: 10, yoyo: true, ease: "steps(1)" }, 24.5);
    typewriter($("#assist-answer"), "v2 adds streaming transcription + web search.", tl, 24.6, 1.7);

    // hands-free stamp
    tl.fromTo("#assist-hf", { scale: 0, opacity: 0, rotation: -18 },
      { scale: 1, opacity: 1, rotation: -3, duration: 0.5, ease: "back.out(2.6)" }, 25.1);
    tl.to("#assist-hf", { scale: 1.06, duration: 0.22, yoyo: true, repeat: 3, ease: "sine.inOut" }, 25.6);

    tl.from("#s-assistant [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 22.2);
  })();

  /* --------------------------- SCENE 6 : TRUST (27–31) --------------------- */
  (() => {
    const s = fadeScene(tl, "#s-privacy", 27.0, 31.0);
    txSplit(tl, 27.0);
    tl.from("#trust-head", { y: 30, opacity: 0, scale: 0.9, duration: 0.5, ease: "back.out(1.8)" }, 27.2);
    // cards flip in (3D rotateX) rather than sliding
    $$("[data-trust]", s).forEach((c, i) => {
      const at = 27.6 + i * 0.4;
      tl.fromTo(c, { rotationX: -82, y: 46, opacity: 0, transformPerspective: 1000, transformOrigin: "50% 100%" },
        { rotationX: 0, y: 0, opacity: 1, duration: 0.55, ease: "back.out(1.5)" }, at);
      tl.fromTo(c.querySelector(".tbox"), { scale: 0, rotation: -90 }, { scale: 1, rotation: 0, duration: 0.4, ease: "back.out(3)" }, at + 0.15);
      sheenSweep(tl, c, at + 0.25, 0.8);
    });
    tl.from("#s-privacy [data-ftag]", { y: 28, opacity: 0, duration: 0.5 }, 27.5);
  })();

  /* --------------------------- SCENE 7 : CTA (31–35) ----------------------- */
  (() => {
    fadeScene(tl, "#s-cta", 31.0, 35.0, 0.4);
    txBloom(tl, 31.0);

    pop(tl, "#cta-badge", 31.2, 0.6, 2.0);
    tl.from("#cta-name", { y: 40, opacity: 0, duration: 0.5 }, 31.5);
    tl.fromTo("#cta-name", { backgroundPosition: "0% 0" }, { backgroundPosition: "260% 0", duration: 3.0, ease: "sine.inOut" }, 31.55);
    tl.from("#cta-tag",  { y: 28, opacity: 0, duration: 0.55 }, 31.85);
    tl.from("#cta-tag .hl", { color: "#101122", duration: 0.5 }, 32.15);

    tl.from("#cta-btn", { y: 26, opacity: 0, duration: 0.5, ease: "back.out(1.8)" }, 32.15);
    const pCta = P.cta;
    curShow(tl, pCta.x + 220, pCta.y - 160, 31.95);
    curTo(tl, pCta.x, pCta.y, 32.1, 0.5);
    curClick(tl, 32.6);
    tl.add(() => $("#cta-btn").classList.add("pressed"), 32.58);
    tl.add(() => $("#cta-btn").classList.remove("pressed"), 32.82);
    curHide(tl, 33.1);
    sheenSweep(tl, "#cta-btn", 32.9, 0.9);
    tl.fromTo("#cta-btn",
      { boxShadow: "0 14px 0 #4F1FB8, 0 16px 44px rgba(124,58,237,.42)" },
      { boxShadow: "0 14px 0 #4F1FB8, 0 22px 64px rgba(124,58,237,.62)",
        duration: 0.7, yoyo: true, repeat: 1, ease: "sine.inOut" }, 32.9);

    tl.from("#platform", { opacity: 0, letterSpacing: "0.6em", duration: 0.7 }, 32.55);
    tl.to("#fade", { opacity: 1, duration: 1.0, ease: "power2.in" }, 34.0);
  })();

  // Pad to exact duration
  tl.set({}, {}, DURATION);

  /* ===========================  PUBLIC API  =============================== */
  window.__duration = DURATION;
  window.__tl = tl;
  window.__seek = (t) => {
    const c = Math.max(0, Math.min(DURATION, t));
    tl.pause();  tl.seek(c, false);
    amb.pause(); amb.seek(c, false);
  };
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
    amb.restart(); tl.restart();
  }

  if (!CAPTURE) {
    loadAudio().finally(() => {
      tl.eventCallback("onComplete", () => {
        if (musicEl) musicEl.pause();
        gsap.delayedCall(0.8, playPreview);
      });
      amb.play();
      tl.play();
      window.addEventListener("click", playPreview);
    });
  }
})();
