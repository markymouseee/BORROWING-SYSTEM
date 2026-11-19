// Runs before Angular so the first paint already uses the right theme (no white flash in dark mode).
// Kept in a file instead of inline so it satisfies the Content-Security-Policy (script-src 'self').
(function () {
  var mode = 'system';
  try {
    mode = localStorage.getItem('lcc-borrowing.theme') || 'system';
  } catch (e) {}
  var dark = mode === 'dark' || (mode === 'system' && window.matchMedia('(prefers-color-scheme: dark)').matches);
  document.documentElement.dataset.theme = dark ? 'dark' : 'light';
  document.documentElement.style.colorScheme = dark ? 'dark' : 'light';
})();
