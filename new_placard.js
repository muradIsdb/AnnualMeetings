// new_placard.js — Placard component with Guest Photo Toggle
// Replaces the uC() function in the bundle via inject_placard.py
// Variables from bundle scope used: Ll (useParams), qt (useNavigate),
//   ae (useQuery), Oe (guestsService), Kg (appConfigFn), N (React), s (jsx runtime)

function uC() {
  const { id: e } = Ll();
  const t = qt();
  const { data: r, isLoading: n, error: a } = ae({
    queryKey: ["guest", e],
    queryFn: () => Oe.getById(e),
    enabled: !!e,
    refetchInterval: 3e4
  });
  const { data: l } = ae({
    queryKey: ["app-config"],
    queryFn: Kg,
    refetchInterval: 6e4
  });

  // Fullscreen on mount
  N.useEffect(() => {
    const u = document.documentElement;
    return u.requestFullscreen && u.requestFullscreen().catch(() => {}),
      () => { document.exitFullscreen && document.fullscreenElement && document.exitFullscreen().catch(() => {}); };
  }, []);

  // Photo toggle state — resets whenever guest ID changes
  const [showPhoto, setShowPhoto] = N.useState(false);
  N.useEffect(() => { setShowPhoto(false); }, [e]);

  // Theme / config
  const o = ((l == null ? void 0 : l.plaCardTheme) ?? "Light") === "DarkNavy";
  const x = (l == null ? void 0 : l.eventLogoUrl) ?? "/isdb-logo.png";
  const c = (l == null ? void 0 : l.eventTitle) ?? "IsDB Annual Meetings 2026";

  // Loading state
  if (n) return s.jsx("div", {
    className: "fixed inset-0 flex items-center justify-center",
    style: { background: o ? "#0a1628" : "#ffffff" },
    children: s.jsx("div", {
      className: "text-2xl animate-pulse",
      style: { color: o ? "rgba(255,255,255,0.4)" : "#9ca3af" },
      children: "Loading..."
    })
  });

  // Error / not found state
  if (a || !r) return s.jsxs("div", {
    className: "fixed inset-0 flex flex-col items-center justify-center gap-6",
    style: { background: o ? "#0a1628" : "#ffffff" },
    children: [
      s.jsx("p", {
        className: "text-xl",
        style: { color: o ? "rgba(255,255,255,0.7)" : "#4b5563" },
        children: "Participant not found."
      }),
      s.jsx("button", {
        onClick: () => t(-1),
        className: "px-6 py-2 rounded-lg border transition",
        style: { color: o ? "rgba(255,255,255,0.7)" : "#4b5563", borderColor: o ? "rgba(255,255,255,0.2)" : "#d1d5db" },
        children: "Go Back"
      })
    ]
  });

  const d = r.isCritical === true;
  const accentColor = o ? "#dc2626" : "#3aaa35";

  // ── Photo panel (inline styles only — avoids Tailwind purge) ──
  const photoPanel = s.jsxs("div", {
    style: {
      width: "100%",
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      gap: "28px",
      padding: showPhoto ? "20px 0 4px" : "0",
      borderTop: showPhoto ? ("1px solid " + (o ? "rgba(255,255,255,0.08)" : "#f3f4f6")) : "none",
      overflow: "hidden",
      maxHeight: showPhoto ? "220px" : "0px",
      opacity: showPhoto ? 1 : 0,
      transition: "max-height 0.4s ease, opacity 0.35s ease, padding 0.3s ease"
    },
    children: [
      // Photo circle
      s.jsx("div", {
        style: {
          width: "130px", height: "130px", borderRadius: "50%",
          border: "4px solid " + accentColor,
          background: o ? "rgba(255,255,255,0.08)" : "#e5e7eb",
          overflow: "hidden", flexShrink: 0,
          display: "flex", alignItems: "center", justifyContent: "center"
        },
        children: r.photoUrl
          ? s.jsx("img", {
              src: r.photoUrl,
              alt: r.fullName,
              style: { width: "100%", height: "100%", objectFit: "cover" }
            })
          : s.jsx("span", {
              style: { fontSize: "11px", color: o ? "rgba(255,255,255,0.3)" : "#9ca3af", textAlign: "center", padding: "8px" },
              children: "No photo\non file"
            })
      }),
      // Identification badges
      s.jsxs("div", {
        style: { display: "flex", flexDirection: "column", gap: "8px" },
        children: [
          // VIP badge — only for critical guests
          d && s.jsx("span", {
            style: {
              fontSize: "11px", fontWeight: "600", letterSpacing: "0.06em",
              padding: "5px 13px", borderRadius: "20px", display: "inline-block",
              background: "#fef2f2", color: "#dc2626", border: "1px solid #fca5a5"
            },
            children: "\u2B50 VIP Critical"
          }),
          // Organisation / country / designation
          (r.organization || r.country || r.designation) && s.jsx("span", {
            style: {
              fontSize: "10px", fontWeight: "600", letterSpacing: "0.06em",
              padding: "5px 13px", borderRadius: "20px", display: "inline-block",
              background: o ? "rgba(255,255,255,0.08)" : "#eff6ff",
              color: o ? "rgba(255,255,255,0.7)" : "#1d4ed8",
              border: "1px solid " + (o ? "rgba(255,255,255,0.15)" : "transparent")
            },
            children: r.organization || r.country || r.designation
          }),
          // Flight number
          r.flightNumber && s.jsx("span", {
            style: {
              fontSize: "10px", fontWeight: "600", letterSpacing: "0.06em",
              padding: "5px 13px", borderRadius: "20px", display: "inline-block",
              background: o ? "rgba(58,170,53,0.15)" : "#f0fdf4",
              color: o ? "#86efac" : "#15803d",
              border: "1px solid " + (o ? "rgba(58,170,53,0.3)" : "transparent")
            },
            children: "\u2708 " + r.flightNumber
          })
        ]
      })
    ]
  });

  // ── Toggle button ──
  const toggleBtn = s.jsxs("button", {
    onClick: () => setShowPhoto(!showPhoto),
    style: {
      position: "absolute", bottom: "54px", right: "16px",
      display: "flex", alignItems: "center", gap: "6px",
      fontSize: "11px", fontWeight: "600", letterSpacing: "0.05em",
      padding: "7px 14px", borderRadius: "8px", cursor: "pointer",
      border: showPhoto
        ? ("1px solid " + (o ? "rgba(255,255,255,0.12)" : "#d1d5db"))
        : "none",
      background: showPhoto
        ? (o ? "rgba(255,255,255,0.06)" : "#f3f4f6")
        : (o ? "rgba(255,255,255,0.12)" : "#1a3c5e"),
      color: showPhoto
        ? (o ? "rgba(255,255,255,0.6)" : "#374151")
        : (o ? "rgba(255,255,255,0.9)" : "#ffffff"),
      zIndex: 10,
      transition: "all 0.2s"
    },
    children: [
      // Eye icon (show) or Eye-off icon (hide)
      showPhoto
        ? s.jsx("svg", {
            viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: "2",
            style: { width: "14px", height: "14px", flexShrink: 0 },
            children: s.jsxs(s.Fragment, { children: [
              s.jsx("path", { d: "M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" }),
              s.jsx("line", { x1: "1", y1: "1", x2: "23", y2: "23" })
            ]})
          })
        : s.jsx("svg", {
            viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", strokeWidth: "2",
            style: { width: "14px", height: "14px", flexShrink: 0 },
            children: s.jsxs(s.Fragment, { children: [
              s.jsx("path", { d: "M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" }),
              s.jsx("circle", { cx: "12", cy: "12", r: "3" })
            ]})
          }),
      showPhoto ? "Hide Photo" : "Show Photo"
    ]
  });

  // ── Dark Navy theme render ──
  if (o) return s.jsxs("div", {
    className: "fixed inset-0 flex flex-col overflow-hidden select-none",
    style: { background: "#0a1628" },
    children: [
      s.jsx("div", { className: "h-2.5 flex-shrink-0", style: { background: "#dc2626" } }),
      // VIP ribbon
      d && s.jsx("div", {
        className: "absolute top-0 right-0 z-20 overflow-hidden",
        style: { width: 140, height: 140 },
        children: s.jsx("div", {
          className: "absolute font-bold text-white text-sm tracking-widest uppercase text-center",
          style: { background: "#dc2626", width: 180, top: 36, right: -44, transform: "rotate(45deg)", padding: "5px 0", boxShadow: "0 2px 8px rgba(0,0,0,0.5)" },
          children: "VIP"
        })
      }),
      // Close button
      s.jsx("button", {
        onClick: () => t(-1),
        className: "absolute top-6 right-6 transition text-sm px-3 py-1 rounded z-10",
        style: { color: "rgba(255,255,255,0.5)", border: "1px solid rgba(255,255,255,0.15)" },
        title: "Close placard",
        children: "\u2715 Close"
      }),
      // Main body
      s.jsx("div", {
        className: "flex-1 flex flex-col items-center justify-start pt-4 px-12",
        children: s.jsxs("div", {
          className: "flex flex-col items-center gap-6 max-w-5xl w-full text-center",
          children: [
            // Logo
            s.jsx("div", {
              className: "flex items-center justify-center rounded-2xl px-8 py-5",
              style: { background: "rgba(255,255,255,0.07)", backdropFilter: "blur(4px)" },
              children: s.jsx("img", {
                src: x, alt: "Event Logo", className: "object-contain",
                style: { maxWidth: 600, maxHeight: 260, width: "100%" },
                onError: u => { u.target.src = "/isdb-logo.png"; }
              })
            }),
            s.jsx("div", { className: "w-24 h-1 rounded-full", style: { background: "#dc2626" } }),
            s.jsx("p", {
              className: "font-light tracking-[0.35em] uppercase",
              style: { color: "rgba(255,255,255,0.55)", fontSize: "clamp(1.2rem, 2.2vw, 2rem)" },
              children: "W E L C O M E"
            }),
            s.jsxs("div", {
              style: { marginTop: "1rem" },
              children: [
                r.title && s.jsx("p", {
                  style: { color: "#f87171", fontSize: "clamp(2rem, 4vw, 3.5rem)", fontWeight: "500", marginBottom: "0.5rem" },
                  children: r.title
                }),
                s.jsx("h1", {
                  className: "font-bold leading-tight",
                  style: { fontSize: "clamp(2.5rem, 6vw, 5rem)", color: "#ffffff" },
                  children: r.fullName
                })
              ]
            }),
            // Photo panel
            photoPanel
          ]
        })
      }),
      // Toggle button
      toggleBtn,
      // Footer
      s.jsx("div", {
        className: "flex-shrink-0 flex items-center justify-center",
        style: { background: "rgba(255,255,255,0.05)", borderTop: "1px solid rgba(255,255,255,0.08)", height: "clamp(3rem, 6vh, 4.5rem)" },
        children: s.jsx("p", {
          className: "text-xs tracking-widest uppercase",
          style: { color: "rgba(255,255,255,0.4)", letterSpacing: "0.25em" },
          children: c
        })
      })
    ]
  });

  // ── Light theme render ──
  return s.jsxs("div", {
    className: "fixed inset-0 flex flex-col overflow-hidden select-none",
    style: { background: "#ffffff" },
    children: [
      s.jsx("div", { className: "h-2.5 flex-shrink-0", style: { background: "#3aaa35" } }),
      // VIP ribbon
      d && s.jsx("div", {
        className: "absolute top-0 right-0 z-20 overflow-hidden",
        style: { width: 120, height: 120 },
        children: s.jsx("div", {
          className: "absolute font-bold text-white text-xs tracking-widest uppercase text-center",
          style: { background: "#dc2626", width: 160, top: 30, right: -36, transform: "rotate(45deg)", padding: "4px 0", boxShadow: "0 2px 6px rgba(0,0,0,0.25)" },
          children: "VIP"
        })
      }),
      // Close button
      s.jsx("button", {
        onClick: () => t(-1),
        className: "absolute top-6 right-6 text-gray-400 hover:text-gray-600 transition text-sm px-3 py-1 rounded border border-gray-200 hover:border-gray-400 z-10",
        title: "Close placard",
        children: "\u2715 Close"
      }),
      // Main body
      s.jsx("div", {
        className: "flex-1 flex flex-col items-center justify-start pt-4 px-12",
        children: s.jsxs("div", {
          className: "flex flex-col items-center gap-6 max-w-5xl w-full text-center",
          children: [
            // Logo
            s.jsx("div", {
              className: "flex items-center justify-center",
              children: s.jsx("img", {
                src: x, alt: "Event Logo", className: "object-contain",
                style: { maxWidth: 600, maxHeight: 260, width: "100%" },
                onError: u => { u.target.src = "/isdb-logo.png"; }
              })
            }),
            s.jsx("div", { className: "w-24 h-1 rounded-full", style: { background: "#3aaa35" } }),
            s.jsx("p", {
              className: "font-light tracking-[0.35em] uppercase",
              style: { color: "#3aaa35", fontSize: "clamp(1.2rem, 2.2vw, 2rem)" },
              children: "W E L C O M E"
            }),
            s.jsxs("div", {
              style: { marginTop: "1rem" },
              children: [
                r.title && s.jsx("p", {
                  style: { color: "#3aaa35", fontSize: "clamp(2rem, 4vw, 3.5rem)", fontWeight: "500", marginBottom: "0.5rem" },
                  children: r.title
                }),
                s.jsx("h1", {
                  className: "font-bold leading-tight",
                  style: { fontSize: "clamp(2.5rem, 6vw, 5rem)", color: "#1a3c5e" },
                  children: r.fullName
                })
              ]
            }),
            // Photo panel
            photoPanel
          ]
        })
      }),
      // Toggle button
      toggleBtn,
      // Footer
      s.jsx("div", {
        className: "flex-shrink-0 flex items-center justify-center",
        style: { background: "#1a3c5e", height: "clamp(3rem, 6vh, 4.5rem)" },
        children: s.jsx("p", {
          className: "text-xs tracking-widest uppercase",
          style: { color: "rgba(255,255,255,0.6)", letterSpacing: "0.25em" },
          children: c
        })
      })
    ]
  });
}
