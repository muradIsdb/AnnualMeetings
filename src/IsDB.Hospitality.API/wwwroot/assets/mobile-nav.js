/**
 * IsDB Hospitality Platform — Mobile & Tablet Navigation Injection
 * Injects a hamburger button and slide-out drawer for screens < 1024px
 */
(function () {
  'use strict';

  // Only run on mobile/tablet
  if (window.innerWidth >= 1024) return;

  // Navigation structure mirroring the sidebar
  const NAV_SECTIONS = [
    {
      label: null,
      items: [
        { to: '/airport', label: 'Airport', icon: '✈️' },
        { to: '/transport', label: 'Transport', icon: '🚗' },
        { to: '/departure-stats', label: 'Departure Shuttle', icon: '🚌' },
        { to: '/hotel', label: 'Hotel', icon: '🏨' },
        { to: '/control-room', label: 'Control Room', icon: '🖥️' },
      ],
    },
    {
      label: 'FLEET',
      items: [
        { to: '/fleet', label: 'Fleet', icon: '🚙' },
        { to: '/fleet/car-classes', label: 'Car Classes', icon: '🏷️' },
        { to: '/fleet/guest-car-class', label: 'Guest Car Class Assignment', icon: '👤' },
      ],
    },
    {
      label: 'ADMINISTRATION',
      items: [
        { to: '/staff', label: 'Staff Management', icon: '👥' },
        { to: '/integrations/eventsair', label: 'EventsAir Config', icon: '🔗' },
        { to: '/integrations/field-mappings', label: 'Field Mappings', icon: '📋' },
      ],
    },
    {
      label: 'SETTINGS',
      items: [
        { to: '/settings', label: 'Platform Settings', icon: '⚙️' },
        { to: '/notification-templates', label: 'Notification Templates', icon: '🔔' },
        { to: '/notification-history', label: 'Notification History', icon: '📜' },
      ],
    },
  ];

  const ISDB_GREEN = '#1a7a4a';

  function injectStyles() {
    const style = document.createElement('style');
    style.id = 'mobile-nav-styles';
    style.textContent = `
      #mbl-hamburger {
        display: none;
        align-items: center;
        justify-content: center;
        width: 40px;
        height: 40px;
        border: none;
        background: transparent;
        cursor: pointer;
        border-radius: 8px;
        padding: 0;
        flex-shrink: 0;
        order: -1;
      }
      #mbl-hamburger:hover { background: #f3f4f6; }
      #mbl-hamburger svg { width: 22px; height: 22px; color: #374151; }

      #mbl-overlay {
        display: none;
        position: fixed;
        inset: 0;
        background: rgba(0,0,0,0.4);
        z-index: 9998;
        opacity: 0;
        transition: opacity 0.25s ease;
      }
      #mbl-overlay.open { opacity: 1; }

      #mbl-drawer {
        position: fixed;
        top: 0;
        left: 0;
        bottom: 0;
        width: 280px;
        background: #fff;
        z-index: 9999;
        transform: translateX(-100%);
        transition: transform 0.25s ease;
        display: flex;
        flex-direction: column;
        box-shadow: 4px 0 20px rgba(0,0,0,0.15);
        overflow-y: auto;
        -webkit-overflow-scrolling: touch;
      }
      #mbl-drawer.open { transform: translateX(0); }

      #mbl-drawer-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 16px 20px;
        border-bottom: 1px solid #f3f4f6;
        flex-shrink: 0;
      }
      #mbl-drawer-brand {
        display: flex;
        align-items: center;
        gap: 10px;
      }
      #mbl-drawer-logo {
        width: 36px;
        height: 36px;
        border-radius: 8px;
        background: ${ISDB_GREEN};
        display: flex;
        align-items: center;
        justify-content: center;
        color: white;
        font-weight: 700;
        font-size: 13px;
        flex-shrink: 0;
      }
      #mbl-drawer-brand-text p:first-child {
        font-weight: 600;
        font-size: 14px;
        color: #111827;
        margin: 0;
      }
      #mbl-drawer-brand-text p:last-child {
        font-size: 12px;
        color: #6b7280;
        margin: 0;
      }
      #mbl-close-btn {
        width: 32px;
        height: 32px;
        border: none;
        background: transparent;
        cursor: pointer;
        border-radius: 6px;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #6b7280;
        padding: 0;
      }
      #mbl-close-btn:hover { background: #f3f4f6; }
      #mbl-close-btn svg { width: 18px; height: 18px; }

      #mbl-drawer-nav {
        flex: 1;
        padding: 8px 0;
        overflow-y: auto;
      }

      .mbl-section-label {
        font-size: 11px;
        font-weight: 600;
        color: #9ca3af;
        letter-spacing: 0.08em;
        padding: 12px 20px 4px;
        text-transform: uppercase;
      }

      .mbl-nav-item {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 10px 20px;
        text-decoration: none;
        color: #374151;
        font-size: 14px;
        font-weight: 500;
        border-radius: 0;
        transition: background 0.15s;
        cursor: pointer;
        border: none;
        background: transparent;
        width: 100%;
        text-align: left;
      }
      .mbl-nav-item:hover { background: #f9fafb; }
      .mbl-nav-item.active {
        background: #f0fdf4;
        color: ${ISDB_GREEN};
        font-weight: 600;
      }
      .mbl-nav-item .mbl-icon {
        font-size: 16px;
        width: 20px;
        text-align: center;
        flex-shrink: 0;
      }

      #mbl-drawer-footer {
        border-top: 1px solid #f3f4f6;
        padding: 12px 20px;
        flex-shrink: 0;
      }
      #mbl-user-info {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px 0 12px;
      }
      #mbl-user-avatar {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        background: #e5e7eb;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 14px;
        color: #6b7280;
        flex-shrink: 0;
      }
      #mbl-user-name {
        font-size: 13px;
        font-weight: 600;
        color: #111827;
        margin: 0;
      }
      #mbl-user-role {
        font-size: 12px;
        color: #6b7280;
        margin: 0;
      }

      @media (max-width: 1023px) {
        #mbl-hamburger { display: flex !important; }
        #mbl-overlay.open { display: block; }
      }
    `;
    document.head.appendChild(style);
  }

  function createHamburgerButton() {
    const btn = document.createElement('button');
    btn.id = 'mbl-hamburger';
    btn.setAttribute('aria-label', 'Open navigation menu');
    btn.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
      <path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16"/>
    </svg>`;
    btn.addEventListener('click', openDrawer);
    return btn;
  }

  function createDrawer() {
    // Overlay
    const overlay = document.createElement('div');
    overlay.id = 'mbl-overlay';
    overlay.addEventListener('click', closeDrawer);

    // Drawer
    const drawer = document.createElement('div');
    drawer.id = 'mbl-drawer';
    drawer.setAttribute('role', 'dialog');
    drawer.setAttribute('aria-modal', 'true');
    drawer.setAttribute('aria-label', 'Navigation menu');

    // Header
    const header = document.createElement('div');
    header.id = 'mbl-drawer-header';
    header.innerHTML = `
      <div id="mbl-drawer-brand">
        <div id="mbl-drawer-logo">IsDB</div>
        <div id="mbl-drawer-brand-text">
          <p>Hospitality</p>
          <p>Guest Management</p>
        </div>
      </div>
      <button id="mbl-close-btn" aria-label="Close navigation menu">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    `;

    // Nav
    const nav = document.createElement('nav');
    nav.id = 'mbl-drawer-nav';

    const currentPath = window.location.pathname;

    NAV_SECTIONS.forEach(section => {
      if (section.label) {
        const label = document.createElement('div');
        label.className = 'mbl-section-label';
        label.textContent = section.label;
        nav.appendChild(label);
      }
      section.items.forEach(item => {
        const link = document.createElement('a');
        link.className = 'mbl-nav-item' + (currentPath === item.to ? ' active' : '');
        link.href = item.to;
        link.innerHTML = `<span class="mbl-icon">${item.icon}</span><span>${item.label}</span>`;
        link.addEventListener('click', (e) => {
          e.preventDefault();
          closeDrawer();
          // Use React Router's history if available, otherwise navigate directly
          setTimeout(() => { window.location.href = item.to; }, 200);
        });
        nav.appendChild(link);
      });
    });

    // Footer
    const footer = document.createElement('div');
    footer.id = 'mbl-drawer-footer';
    footer.innerHTML = `
      <div id="mbl-user-info">
        <div id="mbl-user-avatar">👤</div>
        <div>
          <p id="mbl-user-name">Loading...</p>
          <p id="mbl-user-role"></p>
        </div>
      </div>
    `;

    drawer.appendChild(header);
    drawer.appendChild(nav);
    drawer.appendChild(footer);

    document.body.appendChild(overlay);
    document.body.appendChild(drawer);

    // Wire close button
    drawer.querySelector('#mbl-close-btn').addEventListener('click', closeDrawer);

    // Keyboard: close on Escape
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') closeDrawer();
    });

    return { overlay, drawer };
  }

  function openDrawer() {
    const overlay = document.getElementById('mbl-overlay');
    const drawer = document.getElementById('mbl-drawer');
    overlay.style.display = 'block';
    requestAnimationFrame(() => {
      overlay.classList.add('open');
      drawer.classList.add('open');
    });
    document.body.style.overflow = 'hidden';

    // Update active state
    const currentPath = window.location.pathname;
    drawer.querySelectorAll('.mbl-nav-item').forEach(link => {
      link.classList.toggle('active', link.getAttribute('href') === currentPath);
    });

    // Try to get user info from the page
    tryPopulateUserInfo();
  }

  function closeDrawer() {
    const overlay = document.getElementById('mbl-overlay');
    const drawer = document.getElementById('mbl-drawer');
    if (!overlay || !drawer) return;
    overlay.classList.remove('open');
    drawer.classList.remove('open');
    document.body.style.overflow = '';
    setTimeout(() => { overlay.style.display = 'none'; }, 250);
  }

  function tryPopulateUserInfo() {
    // Read user info from Zustand persisted auth store ('auth-storage')
    try {
      const stored = localStorage.getItem('auth-storage');
      if (stored) {
        const parsed = JSON.parse(stored);
        const user = parsed?.state?.user;
        const nameEl = document.getElementById('mbl-user-name');
        const roleEl = document.getElementById('mbl-user-role');
        if (user && nameEl) {
          nameEl.textContent = user.name || user.fullName || user.email || 'User';
        }
        if (user && roleEl) {
          const roles = user.roles || (user.role ? [user.role] : []);
          roleEl.textContent = roles.map(r => r.replace(/([A-Z])/g, ' $1').trim()).join(', ');
        }
      }
    } catch (e) { /* ignore */ }
  }

  function injectHamburger() {
    // Find the top bar: div with class "flex items-center justify-end"
    const topBar = document.querySelector('main > div.flex.items-center.justify-end');
    if (topBar) {
      topBar.style.justifyContent = 'space-between';
      const hamburger = createHamburgerButton();
      topBar.insertBefore(hamburger, topBar.firstChild);
      return true;
    }
    return false;
  }

  function init() {
    injectStyles();
    createDrawer();

    // Try to inject hamburger immediately
    if (!injectHamburger()) {
      // React hasn't rendered yet — observe DOM mutations
      const observer = new MutationObserver(() => {
        if (injectHamburger()) {
          observer.disconnect();
        }
      });
      observer.observe(document.body, { childList: true, subtree: true });
      // Fallback: stop observing after 10 seconds
      setTimeout(() => observer.disconnect(), 10000);
    }

    // Re-check on resize
    window.addEventListener('resize', () => {
      const hamburger = document.getElementById('mbl-hamburger');
      if (hamburger) {
        hamburger.style.display = window.innerWidth < 1024 ? 'flex' : 'none';
      }
    });
  }

  // Run after DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
