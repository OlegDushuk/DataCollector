// Збереження файлу, отриманого з сервера Blazor (експорт): потік -> Blob -> тимчасове посилання з атрибутом download.
// Без async/await, щоб gulp-uglify стиснув файл без додаткових налаштувань.
window.dataCollector = window.dataCollector || {};

window.dataCollector.downloadFile = function (fileName, contentType, streamReference) {
  return streamReference.arrayBuffer().then(function (buffer) {
    var blob = new Blob([buffer], { type: contentType || 'application/octet-stream' });
    var url = URL.createObjectURL(blob);

    var link = document.createElement('a');
    link.href = url;
    link.download = fileName || 'export';
    document.body.appendChild(link);
    link.click();
    link.remove();

    // Посилання вже спрацювало - звільняємо пам'ять трохи згодом.
    setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
  });
};
