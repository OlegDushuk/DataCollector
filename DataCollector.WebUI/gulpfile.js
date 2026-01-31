const { src, dest, watch, series, parallel } = require('gulp');

const sass = require('gulp-sass')(require('sass'));
const cleanCSS = require('gulp-clean-css');
const concat = require('gulp-concat');
const uglify = require('gulp-uglify');
const sourcemaps = require('gulp-sourcemaps');
const autoprefixer = require('gulp-autoprefixer');

const paths = {
  scss: {
    src: 'Assets/Scss/**/*.scss',
    dest: 'wwwroot/dist'
  },
  js: {
    src: 'Assets/Js/**/*.js',
    dest: 'wwwroot/dist'
  }
};

function buildScss() {
  return src(paths.scss.src)
    .pipe(sourcemaps.init())
    .pipe(sass().on('error', sass.logError))
    .pipe(autoprefixer({
      cascade: false
    }))
    .pipe(cleanCSS({ level: 2 }))
    .pipe(concat('app.min.css'))
    .pipe(sourcemaps.write('.'))
    .pipe(dest(paths.scss.dest));
}

function buildJs() {
  return src(paths.js.src)
    .pipe(sourcemaps.init())
    .pipe(concat('app.min.js'))
    .pipe(uglify())
    .pipe(sourcemaps.write('.'))
    .pipe(dest(paths.js.dest));
}

function watcher() {
  watch(paths.scss.src, buildScss);
  watch(paths.js.src, buildJs);
}

exports.scss = buildScss;
exports.js = buildJs;
exports.build = parallel(buildScss, buildJs);
exports.watch = series(exports.build, watcher);
exports.default = exports.build;
