// Режим перегляду дашборду без меню: клавіша Esc і (за бажанням) повноекранний режим браузера.
// Esc у повноекранному режимі браузер забирає собі (виходить з повного екрана) - тоді спрацьовує fullscreenchange,
// і режим без меню вимикається теж: один Esc повертає звичайний вигляд.
(function () {
  var ns = window.dataCollector = window.dataCollector || {};
  var state = null;

  function onKey(e) {
    if (e.key === 'Escape' && state) {
      state.dotnet.invokeMethodAsync('ExitFocusFromBrowser');
    }
  }

  function onFullscreenChange() {
    if (!document.fullscreenElement && state && state.fullscreen) {
      state.fullscreen = false;
      state.dotnet.invokeMethodAsync('ExitFocusFromBrowser');
    }
  }

  /** Плавно прокрутити до елемента (наприклад, щойно доданого віджета). */
  ns.scrollIntoView = function (selector) {
    var element = document.querySelector(selector);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  };

  ns.focus = {
    /** Слухати Esc; fullscreen - ще й розгорнути вікно на весь екран (лише у відповідь на дію користувача). */
    enter: function (dotnet, fullscreen) {
      ns.focus.exit();
      state = { dotnet: dotnet, fullscreen: false };
      document.addEventListener('keydown', onKey);
      document.addEventListener('fullscreenchange', onFullscreenChange);

      if (fullscreen && document.documentElement.requestFullscreen && !document.fullscreenElement) {
        document.documentElement.requestFullscreen().then(function () {
          if (state) {
            state.fullscreen = true;
          }
        }).catch(function () { /* браузер не дозволив - лишаємось у вікні */ });
      }
    },

    exit: function () {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('fullscreenchange', onFullscreenChange);

      var wasFullscreen = state && state.fullscreen;
      state = null;

      if (wasFullscreen && document.fullscreenElement && document.exitFullscreen) {
        document.exitFullscreen().catch(function () { });
      }
    }
  };
})();
