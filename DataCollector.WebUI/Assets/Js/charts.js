// Тонка обгортка над Apache ECharts для Blazor: C# готує опції (дані, кольори, підписи),
// а малювання, підказки, масштабування і зміна розміру працюють у браузері без запитів до сервера.
// Без async/await і стрілкових функцій - щоб gulp-uglify стиснув файл без налаштувань.
(function () {
  var ns = window.dataCollector = window.dataCollector || {};
  var numbers = new Intl.NumberFormat('uk-UA', { maximumFractionDigits: 2 });

  function formatValue(value) {
    return value === null || value === undefined || value === '-' ? '—' : numbers.format(value);
  }

  // Текст з даних (назви категорій) у власних підказках - лише екранований: це HTML.
  function escape(text) {
    return window.echarts.format.encodeHTML(String(text));
  }

  // Функції не передаються з C# (JSON), тож форматування чисел і власні підказки додаються тут.
  function addFormatters(option) {
    option.tooltip = option.tooltip || {};
    option.tooltip.valueFormatter = formatValue;

    // Числа - на осі значень (у смуг це вісь X, у решти - Y); вісь категорій не чіпаємо.
    [option.xAxis, option.yAxis].forEach(function (axis) {
      if (axis && axis.type === 'value') {
        axis.axisLabel = axis.axisLabel || {};
        axis.axisLabel.formatter = function (value) { return numbers.format(value); };
      }
    });

    (option.series || []).forEach(function (series) {
      if (series.type !== 'heatmap') {
        return;
      }

      // Клітинка: [x, y, значення]. Підпис - відформатоване число, підказка - рядок, стовпець і значення.
      series.label = series.label || {};
      series.label.formatter = function (params) {
        return params.value[2] === '-' ? '' : numbers.format(params.value[2]);
      };

      option.tooltip.formatter = function (params) {
        var x = option.xAxis.data[params.value[0]];
        var y = option.yAxis.data[params.value[1]];
        return escape(y) + '<br/>' + params.marker + escape(x) + ': <b>' + formatValue(params.value[2]) + '</b>';
      };
    });
  }

  function chartOf(element) {
    return element && window.echarts ? window.echarts.getInstanceByDom(element) : null;
  }

  ns.charts = {
    /** Малює або оновлює діаграму в елементі. false - бібліотека ECharts не завантажена. */
    render: function (element, option) {
      if (!element || !window.echarts) {
        return false;
      }

      var chart = chartOf(element) || window.echarts.init(element, null, { renderer: 'canvas' });

      // Панель поруч може відкритися чи закритися - діаграма підлаштовується під новий розмір.
      if (!element.dcResizeObserver && window.ResizeObserver) {
        element.dcResizeObserver = new ResizeObserver(function () { chart.resize(); });
        element.dcResizeObserver.observe(element);
      }

      addFormatters(option);

      chart.setOption(option, { notMerge: true });
      return true;
    },

    /** Зберігає діаграму як PNG (подвійна щільність - чітко і на друк, і в записку). */
    savePng: function (element, fileName) {
      var chart = chartOf(element);
      if (!chart) {
        return;
      }

      var link = document.createElement('a');
      link.href = chart.getDataURL({ type: 'png', pixelRatio: 2, backgroundColor: '#ffffff' });
      link.download = fileName || 'chart.png';
      document.body.appendChild(link);
      link.click();
      link.remove();
    },

    /** Звільняє діаграму, коли компонент зникає зі сторінки (інакше лишаються обробники і canvas). */
    dispose: function (element) {
      if (!element) {
        return;
      }

      if (element.dcResizeObserver) {
        element.dcResizeObserver.disconnect();
        element.dcResizeObserver = null;
      }

      var chart = chartOf(element);
      if (chart) {
        chart.dispose();
      }
    }
  };
})();
