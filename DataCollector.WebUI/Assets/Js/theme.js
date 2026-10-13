// Тема інтерфейсу: "light", "dark" або "system" (як в операційній системі). Вибір - у localStorage
// (datacollector.theme), застосування - атрибут data-theme на <html>, від якого залежать CSS-змінні (theme.scss).
// Перший раз атрибут ставить вбудований скрипт у <head> Views/App.razor - ще до показу сторінки, без «спалаху».
// Після зміни надсилається подія datacollector:themechange - charts.js перемальовує діаграми новими кольорами.
(function () {
  var ns = window.dataCollector = window.dataCollector || {};
  var storageKey = 'datacollector.theme';
  var media = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  function readMode() {
    try {
      var mode = localStorage.getItem(storageKey);
      return mode === 'light' || mode === 'dark' ? mode : 'system';
    } catch (e) {
      return 'system';
    }
  }

  function resolve(mode) {
    return mode === 'dark' || (mode === 'system' && media && media.matches) ? 'dark' : 'light';
  }

  function apply(mode, animate) {
    var root = document.documentElement;
    var theme = resolve(mode);

    if (root.getAttribute('data-theme') === theme) {
      return;
    }

    // Плавний перехід кольорів лише на час перемикання (див. .theme-switching у theme.scss).
    if (animate) {
      root.classList.add('theme-switching');
      window.setTimeout(function () { root.classList.remove('theme-switching'); }, 300);
    }

    root.setAttribute('data-theme', theme);
    document.dispatchEvent(new CustomEvent('datacollector:themechange', { detail: { theme: theme } }));
  }

  // Режим «як у системі»: тема змінюється разом з налаштуванням ОС (наприклад, автоматично ввечері).
  if (media) {
    var onSystemChange = function () {
      if (readMode() === 'system') {
        apply('system', true);
      }
    };

    if (media.addEventListener) {
      media.addEventListener('change', onSystemChange);
    } else if (media.addListener) {
      media.addListener(onSystemChange);
    }
  }

  apply(readMode(), false);

  ns.theme = {
    /** Вибраний режим: "light", "dark" або "system". */
    getMode: function () {
      return readMode();
    },

    /** Змінити режим, запам'ятати в браузері і застосувати. */
    setMode: function (mode) {
      mode = mode === 'light' || mode === 'dark' ? mode : 'system';

      try {
        if (mode === 'system') {
          localStorage.removeItem(storageKey);
        } else {
          localStorage.setItem(storageKey, mode);
        }
      } catch (e) { /* сховище недоступне - тема діє до перезавантаження */ }

      apply(mode, true);
      return mode;
    }
  };
})();
