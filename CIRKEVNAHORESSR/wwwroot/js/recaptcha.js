/* ==========================================================
   recaptcha.js (AW Contact)
   - Loads reCAPTCHA v3 dynamically (from data-sitekey)
   - Submits form via fetch to Azure Function
   - No alerts; uses template UI (.loading / .error-message / .sent-message)
   ========================================================== */

(function () {
  "use strict";

  // -----------------------------
  // Debug logger (optional)
  // -----------------------------
  window.awContactDebug = function (msg) {
    try {
      const el = document.getElementById("aw-contact-debug");
      const line = `[${new Date().toISOString()}] ${msg}`;
      if (el) {
        el.style.display = "block";
        el.textContent += line + "\n";
      }
      // keep console output too
      console.log("[AW CONTACT]", msg);
    } catch {
      // ignore
    }
  };

  // -----------------------------
  // Helpers
  // -----------------------------
  const getForm = () => document.getElementById("aw-contact-form");

  const setStartedNow = (form) => {
    const started = form.querySelector(".aw-started");
    if (started) started.value = Math.floor(Date.now() / 1000).toString();
  };

  const setState = (form, state, msg) => {
    const loadingEl = form.querySelector(".loading");
    const errorEl = form.querySelector(".error-message");
    const sentEl = form.querySelector(".sent-message");

    if (loadingEl) loadingEl.classList.toggle("d-none", state !== "loading");
    if (sentEl) sentEl.classList.toggle("d-none", state !== "sent");
    if (errorEl) {
      errorEl.classList.toggle("d-none", state !== "error");
      if (state === "error") errorEl.textContent = msg || "Send failed.";
      if (state !== "error") errorEl.textContent = "";
    }
  };

  // -----------------------------
  // Load reCAPTCHA script once
  // -----------------------------
  const loadRecaptcha = (siteKey) => {
    return new Promise((resolve, reject) => {
      if (!siteKey) {
        window.awContactDebug("❌ recaptcha: missing siteKey");
        reject(new Error("missing_sitekey"));
        return;
      }

      if (window.grecaptcha) {
        window.awContactDebug("✅ recaptcha: grecaptcha already present");
        resolve();
        return;
      }

      // avoid injecting twice
      if (document.querySelector('script[data-aw-recaptcha="1"]')) {
        window.awContactDebug("ℹ️ recaptcha: script tag already injected, waiting...");
        // wait until grecaptcha appears
        const t0 = Date.now();
        const timer = setInterval(() => {
          if (window.grecaptcha) {
            clearInterval(timer);
            resolve();
          } else if (Date.now() - t0 > 15000) {
            clearInterval(timer);
            reject(new Error("recaptcha_load_timeout"));
          }
        }, 100);
        return;
      }

      window.awContactDebug("Loading reCAPTCHA script…");

      const s = document.createElement("script");
      s.src = "https://www.google.com/recaptcha/api.js?render=" + encodeURIComponent(siteKey);
      s.async = true;
      s.defer = true;
      s.setAttribute("data-aw-recaptcha", "1");

      s.onload = () => {
        window.awContactDebug("✅ recaptcha: script loaded");
        resolve();
      };

      s.onerror = () => {
        window.awContactDebug("❌ recaptcha: FAILED to load script (adblock/CSP/network?)");
        reject(new Error("recaptcha_script_failed"));
      };

      document.head.appendChild(s);
    });
  };

  // -----------------------------
  // Get token (safe)
  // -----------------------------
  const getToken = async (siteKey) => {
    if (!window.grecaptcha) throw new Error("grecaptcha_not_loaded");

    await new Promise((resolve) => window.grecaptcha.ready(resolve));
    window.awContactDebug("Executing reCAPTCHA…");

    const token = await window.grecaptcha.execute(siteKey, { action: "contact" });

    if (!token) throw new Error("token_empty");

    window.awContactDebug("✅ Token OK");
    return token;
  };

  // -----------------------------
  // Init (set start time + load captcha)
  // -----------------------------
  const initForm = async () => {
    const form = getForm();
    if (!form) {
      window.awContactDebug("Form not found (aw-contact-form)");
      return;
    }

    window.awContactDebug("Form initialized");
    setStartedNow(form);

    const siteKey = form.getAttribute("data-sitekey");
    if (!siteKey || siteKey === "YOUR_SITE_KEY" || siteKey.startsWith("PASTE_")) {
      window.awContactDebug("❌ data-sitekey is not set (placeholder)");
      return;
    }

    try {
      await loadRecaptcha(siteKey);
      // just to log readiness
      if (window.grecaptcha) {
        await new Promise((resolve) => window.grecaptcha.ready(resolve));
        window.awContactDebug("✅ recaptcha: ready");
      }
    } catch (e) {
      window.awContactDebug("❌ recaptcha init failed: " + (e?.message || e));
    }
  };

  // -----------------------------
  // Submit handler (no alerts)
  // -----------------------------
  window.awContactSubmit = async function (ev) {
    ev.preventDefault();

    const form = ev.target;
    try {
      setState(form, "idle");
    } catch {}

    window.awContactDebug("🚀 Submit fired");

    const endpoint = form.getAttribute("data-endpoint");
    const siteKey = form.getAttribute("data-sitekey");
    const source = form.getAttribute("data-source") || "";

    window.awContactDebug("Endpoint: " + endpoint);

    // client validation
    if (!form.checkValidity()) {
      setState(form, "error", "Please fill all required fields.");
      window.awContactDebug("❌ Browser validation failed");
      return false;
    }

    // honeypot
    const honeypot = (form.querySelector('[name="honeypot"]')?.value || "").trim();
    if (honeypot) {
      setState(form, "error", "Blocked.");
      window.awContactDebug("❌ Honeypot triggered");
      return false;
    }

    // time check
    const started = parseInt(form.querySelector('[name="formStartedAtUnix"]')?.value || "0", 10);
    const now = Math.floor(Date.now() / 1000);
    if (!started || (now - started) < 4) {
      setState(form, "error", "Please wait a few seconds and try again.");
      window.awContactDebug("❌ Submitted too fast");
      return false;
    }

    // ensure captcha loaded
    try {
      await loadRecaptcha(siteKey);
    } catch (e) {
      setState(form, "error", "Captcha load failed.");
      window.awContactDebug("❌ Captcha load failed: " + (e?.message || e));
      return false;
    }

    setState(form, "loading");

    // get token
    let token = null;
    try {
      token = await getToken(siteKey);
    } catch (e) {
      setState(form, "error", "Captcha error.");
      window.awContactDebug("❌ CAPTCHA FAILED: " + (e?.message || e));
      return false;
    }

    // payload (trim)
    const payload = {
      source,
      name: (form.querySelector('input[name="name"]')?.value || "").trim(),
      email: (form.querySelector('input[name="email"]')?.value || "").trim(),
      subject: (form.querySelector('input[name="subject"]')?.value || "").trim(),
      message: (form.querySelector('textarea[name="message"]')?.value || "").trim(),
      honeypot: null,
      formStartedAtUnix: started,
      recaptchaToken: token
    };

    window.awContactDebug("Payload ready (email=" + payload.email + ")");

    // fetch
    try {
      const res = await fetch(endpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Accept": "application/json"
        },
        body: JSON.stringify(payload)
      });

      const text = await res.text();
      window.awContactDebug("HTTP: " + res.status);
      window.awContactDebug("Response: " + text);

      let json = null;
      try { json = JSON.parse(text); } catch {}

      if (!res.ok || !json || json.ok !== true) {
        const err = (json && (json.error || json.message)) ? (json.error || json.message) : `HTTP ${res.status}`;
        setState(form, "error", err);
        return false;
      }

      // success
      setState(form, "sent");
      form.reset();
      setStartedNow(form);

      // hide success after a bit
      setTimeout(() => {
        try { setState(form, "idle"); } catch {}
      }, 6000);

      return false;
    } catch (e) {
      window.awContactDebug("❌ FETCH ERROR: " + (e?.message || e));
      setState(form, "error", "Network error.");
      return false;
    }
  };

  // -----------------------------
  // Auto init
  // -----------------------------
  document.addEventListener("DOMContentLoaded", initForm);
})();
