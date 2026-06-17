/* ============================================================================
   Koli — Motion Design Video  |  Master timeline (GSAP, deterministic)
   - Builds a single paused GSAP timeline (34s).
   - Exposes window.__seek(t) / window.__duration for frame-accurate capture.
   - Plays a live preview loop (with optional SFX) when opened in a browser.
   All animation is time-based (no Date.now / free rAF) => seek is reproducible.
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

  /* ---- Seeded RNG so the camera-shake keyframes are baked & reproducible --- */
  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  const rnd = mulberry32(0x4b4f4c49); // "KOLI" seed — fixed for reproducible motion

  // Build a fixed array of shake keyframes (deterministic chaos / "frustration")
  function shakeKeyframes(count, amp, rot) {
    const kf = [];
    for (let i = 0; i < count; i++) {
      kf.push({
        x: (rnd() * 2 - 1) * amp,
        y: (rnd() * 2 - 1) * amp,
        rotation: (rnd() * 2 - 1) * rot,
      });
    }
    kf.push({ x: 0, y: 0, rotation: 0 });
    return kf;
  }

  /* ---- Helpers ------------------------------------------------------------- */
  gsap.defaults({ ease: "power3.out" });

  // Elastic "bounce pop": 0 -> 110% -> 98% -> 100%
  function bouncePop(tl, target, at, dur = 0.5) {
    tl.fromTo(target, { scale: 0, opacity: 0 }, {
      scale: 1, opacity: 1, duration: dur, ease: "back.out(2.4)",
    }, at);
  }

  // Animated number counter (runs correctly on seek)
  function counter(el, tl, at, dur = 1.4) {
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

  // Typewriter (runs correctly on seek)
  function typewriter(el, text, tl, at, dur) {
    const proxy = { i: 0 };
    tl.to(proxy, {
      i: text.length, duration: dur, ease: "none",
      onUpdate() { el.textContent = text.slice(0, Math.round(proxy.i)); },
    }, at);
  }

  // Scene cross-fade wrapper: fades a scene in at `start`, out before `end`.
  function fadeScene(tl, sel, start, end, fade = 0.35) {
    const el = $(sel);
    tl.set(el, { autoAlpha: 0 }, 0);
    tl.to(el, { autoAlpha: 1, duration: fade }, start);
    tl.to(el, { autoAlpha: 0, duration: fade }, end - fade);
    return el;
  }

  /* ===========================  MASTER TIMELINE  =========================== */
  const tl = gsap.timeline({ paused: true });

  /* --------------------------- SCENE 1 : HOOK (0–4) ------------------------ */
  (() => {
    const s = fadeScene(tl, "#s-hook", 0.0, 4.0);
    const chips = $$("[data-chip]", s);
    const asks  = $$("[data-ask]", s);
    const l1 = $("#hook-l1"), l2 = $("#hook-l2");

    // chips snap in, jittering
    tl.from(chips, { scale: 0.4, opacity: 0, duration: 0.4, stagger: 0.06, ease: "back.out(2)" }, 0.15);
    chips.forEach((c, i) => {
      tl.to(c, { x: "+=" + ((i % 2 ? -1 : 1) * 10), y: "+=8", duration: 0.18,
        repeat: 5, yoyo: true, ease: "sine.inOut" }, 0.6);
    });

    // questions pop chaotically
    tl.from(asks, { scale: 0, opacity: 0, rotation: -8, duration: 0.3,
      stagger: 0.12, ease: "back.out(3)" }, 0.5);
    asks.forEach((a) => tl.to(a, { opacity: 0.35, duration: 0.12, repeat: 7, yoyo: true }, 1.0));

    // line 1 word-by-word burst
    tl.from($$(".word", l1), { y: 60, opacity: 0, scale: 0.6, rotationX: -40,
      duration: 0.5, stagger: 0.06, ease: "back.out(2)" }, 0.35);

    // camera shake builds as "frustration" peaks
    tl.to("#camera", { keyframes: shakeKeyframes(26, 14, 0.7), duration: 1.6,
      ease: "none" }, 1.9);

    // declutter: fade out the scattered questions + dim chips so the focused
    // question line below never overlaps them
    tl.to(asks,  { opacity: 0, duration: 0.3 }, 2.35);
    tl.to(chips, { opacity: 0.12, duration: 0.3 }, 2.35);

    // swap to line 2 (the painful questions)
    tl.to(l1, { y: -30, opacity: 0, scale: 0.92, duration: 0.4 }, 2.55);
    tl.set(l2, { opacity: 1 }, 2.6);
    tl.from($$(".word", l2), { y: 40, opacity: 0, scale: 0.6, duration: 0.4,
      stagger: 0.05, ease: "back.out(2.2)" }, 2.6);
  })();

  /* --------------------------- SCENE 2 : LOGO (4–7) ------------------------ */
  (() => {
    fadeScene(tl, "#s-reveal", 4.0, 7.0);
    tl.set("#camera", { x: 0, y: 0, rotation: 0 }, 4.0);
    bouncePop(tl, "#logo-badge", 4.1, 0.6);
    tl.fromTo("#logo-ring", { scale: 1.4, opacity: 0 }, { scale: 1, opacity: 1, duration: 0.6 }, 4.2);
    tl.to("#logo-ring", { rotation: 180, duration: 2.4, ease: "none" }, 4.4);
    tl.to("#logo-badge", { boxShadow: "0 0 40px rgba(124,58,237,.85), 0 0 110px rgba(124,58,237,.5)",
      duration: 0.8, yoyo: true, repeat: 2, ease: "sine.inOut" }, 4.6);
    tl.from("#brand-name", { y: 40, opacity: 0, duration: 0.5 }, 4.55);
    tl.from("#brand-sub",  { y: 20, opacity: 0, letterSpacing: "1.2em", duration: 0.6 }, 4.8);
    tl.from("#reveal-tag", { y: 20, opacity: 0, duration: 0.5 }, 5.1);
  })();

  /* ----------------------- SCENE 3 : DICTATION (7–12) ---------------------- */
  (() => {
    const s = fadeScene(tl, "#s-dictation", 7.0, 12.0);
    const waves   = $$("[data-wave]", s);
    const engines = $$("[data-engine]", s);

    // press F9
    bouncePop(tl, "#f9-key", 7.15, 0.5);
    tl.to("#f9-key .cap", { y: 6, boxShadow: "0 4px 0 #0c0c16, 0 10px 26px rgba(0,0,0,.6)",
      duration: 0.1, yoyo: true, repeat: 1 }, 7.5);

    // recording indicator + waveform comes alive
    tl.from("#dict-rec", { opacity: 0, x: -20, duration: 0.35 }, 7.4);
    tl.from("#dict-rec .blob", { scale: 0.4, duration: 0.4, repeat: 8, yoyo: true, ease: "sine.inOut" }, 7.5);
    gsap.set(waves, { scaleY: 0.16, transformOrigin: "50% 50%" });
    waves.forEach((b, i) => {
      const amp = 0.4 + rnd() * 0.6;
      tl.to(b, { scaleY: amp, duration: 0.26 + rnd() * 0.18, repeat: 12, yoyo: true,
        ease: "sine.inOut" }, 7.4 + (i % 6) * 0.05);
    });

    // active app window slides in + Koli types into it
    tl.from("#dict-window", { x: 80, opacity: 0, duration: 0.5 }, 7.5);
    tl.to("#dict-caret", { opacity: 0, duration: 0.18, repeat: 14, yoyo: true, ease: "steps(1)" }, 7.9);
    typewriter($("#dict-typed"), "Koli types straight into the active window.", tl, 8.0, 2.0);

    // engine chips light up in a staggered cascade
    engines.forEach((e, i) => {
      const at = 9.5 + i * 0.32;
      tl.from(e, { y: 30, opacity: 0, scale: 0.8, duration: 0.4, ease: "back.out(1.8)" }, at);
      tl.add(() => e.classList.add("lit"), at + 0.12);
      tl.to(e, { scale: 1.06, duration: 0.12, yoyo: true, repeat: 1 }, at + 0.12);
    });

    tl.from("#s-dictation [data-ftag]", { y: 30, opacity: 0, duration: 0.5 }, 8.2);
  })();

  /* ------------------- SCENE 4 : MEETING ISOMETRIC (12–17) ----------------- */
  (() => {
    const s = fadeScene(tl, "#s-meeting", 12.0, 17.0);
    const win = $("#meet-window");
    // fly in + lock to isometric tilt
    tl.fromTo(win,
      { rotateY: -42, rotateX: 18, y: 120, z: -400, opacity: 0 },
      { rotateY: -20, rotateX: 8, y: 0, z: 0, opacity: 1, duration: 0.9, ease: "power3.out" }, 12.1);
    // audio-source toggles + stat cards cascade
    tl.from($$(".src-toggle", s), { x: -40, opacity: 0, duration: 0.4, stagger: 0.12, ease: "back.out(1.7)" }, 12.7);
    tl.from($$("[data-mstat]", s), { y: 40, opacity: 0, scale: 0.8, duration: 0.5,
      stagger: 0.12, ease: "back.out(1.7)" }, 13.0);
    $$("[data-count]", s).forEach((el) => counter(el, tl, 13.2, 1.4));
    // colour-coded speaker transcript cascades in
    tl.from($$("[data-line]", s), { x: 40, opacity: 0, duration: 0.45,
      stagger: 0.28, ease: "power3.out" }, 13.2);
    // export chips pop
    tl.from($$("[data-exp]", s), { scale: 0, opacity: 0, duration: 0.35,
      stagger: 0.1, ease: "back.out(3)" }, 15.0);
    // slow parallax drift for life
    tl.to(win, { rotateY: -16, y: -14, duration: 3.0, ease: "sine.inOut" }, 13.4);
    tl.from("#s-meeting [data-ftag]", { y: 30, opacity: 0, duration: 0.5 }, 13.0);
  })();

  /* ----------------- SCENE 5 : REWRITE & TRANSLATE (17–22) ----------------- */
  (() => {
    const s = fadeScene(tl, "#s-rewrite", 17.0, 22.0);
    // swipe & blur through rewrite cards (xPercent/yPercent keep them centered
    // while x slides horizontally — GSAP manages the full transform matrix)
    const cards = $$("[data-rw]", s);
    gsap.set(cards, { xPercent: -50, yPercent: -50, x: 1200, opacity: 0, filter: "blur(0px)" });
    cards.forEach((c, i) => {
      const at = 17.5 + i * 0.92;
      // incoming from right
      tl.fromTo(c, { x: 1200, opacity: 0, filter: "blur(14px)" },
        { x: 0, opacity: 1, filter: "blur(0px)", duration: 0.45, ease: "power4.out" }, at);
      tl.add(() => c.classList.add("active"), at + 0.2);
      if (i < cards.length - 1) {
        // outgoing to left with motion blur
        tl.to(c, { x: -1200, opacity: 0, filter: "blur(14px)", duration: 0.4, ease: "power3.in",
          onComplete: () => c.classList.remove("active") }, at + 0.62);
      }
    });
    // last card (Translated): pop the language pills
    tl.from($$(".rw-card:last-child .lang-pill", s), { scale: 0, opacity: 0, duration: 0.35,
      stagger: 0.12, ease: "back.out(3)" }, 21.0);
    tl.from("#s-rewrite [data-ftag]", { y: 30, opacity: 0, duration: 0.5 }, 17.6);
  })();

  /* --------------- SCENE 6 : VOICE ASSISTANT (Alt Gr) (22–26) -------------- */
  (() => {
    const s = fadeScene(tl, "#s-assistant", 22.0, 26.0);
    const nodes = $$("[data-anode]", s);
    const flows = $$("[data-aflow]", s);

    tl.from("#assist-kicker", { y: -22, opacity: 0, duration: 0.4 }, 22.1);
    tl.from("#assist-head", { y: 30, opacity: 0, scale: 0.8, duration: 0.5, ease: "back.out(2)" }, 22.2);
    tl.from("#assist-sub", { y: 18, opacity: 0, duration: 0.4 }, 22.5);

    bouncePop(tl, "[data-ahub]", 22.45, 0.5);
    // draw the connectors
    flows.forEach((p) => {
      const len = p.getTotalLength();
      gsap.set(p, { strokeDasharray: len, strokeDashoffset: len });
    });
    tl.to(flows, { strokeDashoffset: 0, duration: 0.5, stagger: 0.16, ease: "power2.inOut" }, 22.75);
    // flow nodes pop + light up in sequence (question -> STT -> search -> answer)
    nodes.forEach((n, i) => {
      const at = 22.95 + i * 0.22;
      tl.from(n, { scale: 0.5, opacity: 0, duration: 0.4, ease: "back.out(2)" }, at);
      tl.add(() => n.classList.add("lit"), at + 0.12);
      tl.to(n, { scale: 1.06, duration: 0.12, yoyo: true, repeat: 1 }, at + 0.12);
    });
    tl.to("[data-ahub]", { scale: 1.05, duration: 0.2, yoyo: true, repeat: 3, ease: "sine.inOut" }, 24.2);
    // badge slams in and pulses (fromTo keeps the -5° tilt)
    tl.fromTo("#assist-gc", { scale: 0, opacity: 0, rotation: -22 },
      { scale: 1, opacity: 1, rotation: -5, duration: 0.5, ease: "back.out(2.6)" }, 24.0);
    tl.to("#assist-gc", { scale: 1.08, duration: 0.25, yoyo: true, repeat: 3, ease: "sine.inOut" }, 24.5);
    tl.from("#s-assistant [data-ftag]", { y: 30, opacity: 0, duration: 0.5 }, 23.2);
  })();

  /* ------------------ SCENE 7 : PRIVACY & RESILIENCE (26–30) --------------- */
  (() => {
    const s = fadeScene(tl, "#s-privacy", 26.0, 30.0);
    tl.fromTo("#shield", { scale: 0.4, opacity: 0, rotationY: -60 },
      { scale: 1, opacity: 1, rotationY: 0, duration: 0.7, ease: "back.out(1.8)" }, 26.1);
    tl.to("#shield", { boxShadow: "0 0 40px rgba(167,139,250,.9), 0 0 120px rgba(124,58,237,.5)",
      duration: 0.9, yoyo: true, repeat: 2, ease: "sine.inOut" }, 26.4);
    // checklist cascade — one "ding" per check
    $$("[data-check]", s).forEach((c, i) => {
      const at = 26.6 + i * 0.5;
      tl.from(c, { x: 60, opacity: 0, duration: 0.4, ease: "power3.out" }, at);
      tl.from(c.querySelector(".box"), { scale: 0, rotation: -90, duration: 0.35, ease: "back.out(3)" }, at + 0.05);
    });
    tl.from("#s-privacy [data-ftag]", { y: 30, opacity: 0, duration: 0.5 }, 26.4);
  })();

  /* --------------------------- SCENE 8 : CTA (30–34) ----------------------- */
  (() => {
    fadeScene(tl, "#s-cta", 30.0, 34.0, 0.4);
    // white impact flash on cut
    tl.fromTo("#flash", { opacity: 0.0 }, { opacity: 0.55, duration: 0.12 }, 30.0);
    tl.to("#flash", { opacity: 0, duration: 0.45 }, 30.12);
    bouncePop(tl, "#cta-badge", 30.15, 0.6);
    tl.to("#cta-badge", { boxShadow: "0 0 50px rgba(124,58,237,.9), 0 0 130px rgba(34,211,238,.4)",
      duration: 1.1, yoyo: true, repeat: 1, ease: "sine.inOut" }, 30.5);
    tl.from("#cta-name", { y: 40, opacity: 0, duration: 0.5 }, 30.5);
    tl.from("#cta-tag",  { y: 30, opacity: 0, duration: 0.6 }, 30.9);
    tl.from(".cta-tag .neon", { color: "#ffffff", textShadow: "0 0 0 rgba(0,0,0,0)", duration: 0.5 }, 31.4);
    tl.from("#platform", { opacity: 0, letterSpacing: "0.6em", duration: 0.7 }, 31.4);
    // final fade to black
    tl.to("#fade", { opacity: 1, duration: 1.0, ease: "power2.in" }, 33.0);
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
