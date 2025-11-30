// Theme management for dark mode
(function() {
    'use strict';

    const THEME_KEY = 'sheetlink-theme';
    const THEME_ATTR = 'data-theme';

    // Get stored theme or detect system preference
    function getInitialTheme() {
        const stored = localStorage.getItem(THEME_KEY);
        if (stored) {
            return stored;
        }

        // Check system preference
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            return 'dark';
        }

        return 'light';
    }

    // Apply theme to document
    function applyTheme(theme) {
        document.documentElement.setAttribute(THEME_ATTR, theme);
        localStorage.setItem(THEME_KEY, theme);

        // Update toggle button icon if it exists
        const toggleBtn = document.querySelector('.theme-toggle');
        if (toggleBtn) {
            toggleBtn.setAttribute('aria-label', theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
            toggleBtn.textContent = theme === 'dark' ? '☀️' : '🌙';
        }
    }

    // Toggle between light and dark
    function toggleTheme() {
        const current = document.documentElement.getAttribute(THEME_ATTR) || 'light';
        const next = current === 'dark' ? 'light' : 'dark';
        applyTheme(next);
    }

    // Ensure the toggle icon is updated after the DOM is ready (button exists)
    function refreshToggleIcon() {
        const current = document.documentElement.getAttribute(THEME_ATTR) || getInitialTheme();
        applyTheme(current);
    }

    // Initialize theme on page load
    function initTheme() {
        const theme = getInitialTheme();
        applyTheme(theme);
    }

    // Listen for system theme changes
    if (window.matchMedia) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
            // Only auto-switch if user hasn't manually set a preference
            if (!localStorage.getItem(THEME_KEY)) {
                applyTheme(e.matches ? 'dark' : 'light');
            }
        });
    }

    // Initialize immediately (before DOM ready to prevent flash)
    initTheme();
    document.addEventListener('DOMContentLoaded', refreshToggleIcon);

    // Expose toggle function globally for button click
    window.toggleTheme = toggleTheme;

    // Re-apply theme after Blazor reconnects (in case of SignalR reconnection)
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', initTheme);
        window.Blazor.addEventListener('afterWebStarted', refreshToggleIcon);
    }
})();
