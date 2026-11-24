import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const WEB_DIR = path.resolve(__dirname, '..');
const ROOT_DIR = path.resolve(WEB_DIR, '..');
const OUT_MODELS = path.join(WEB_DIR, 'public', 'models');
const OUT_THUMBS = path.join(WEB_DIR, 'public', 'thumbs');
const OUT_MARKERS = path.join(WEB_DIR, 'public', 'markers');
const OUT_SCREENS = path.join(WEB_DIR, 'public', 'screens');

for (const d of [OUT_MODELS, OUT_THUMBS, OUT_MARKERS, OUT_SCREENS]) {
  fs.mkdirSync(d, { recursive: true });
}

const MODELS = [
  { id: 'golok_betawi', src: 'Assets/Art/Golok/golok.glb', texSize: 1024 },
  { id: 'keris_bali', src: 'Assets/Art/KerisBali/keris_bali.glb', texSize: 1024 },
  { id: 'keris_sumatra', src: 'Assets/Art/KerisSumatra/keris_sumatra.glb', texSize: 1024 },
  { id: 'karambit', src: 'Assets/Art/Karambit/karambit.glb', texSize: 1024 },
  { id: 'komodo', src: 'Assets/Art/Komodo/komodo.glb', texSize: 1024 },
  { id: 'candi_borobudur', src: 'Assets/Art/CandiBorobudur/candi_borobudur.glb', texSize: 1024 },
];

const THUMBS = [
  ['GOLOK_BETAWI_01', 'golok_betawi.png'],
  ['KERIS_BALI_01', 'keris_bali.png'],
  ['KERIS_SUMATRA_01', 'keris_sumatra.png'],
  ['KARAMBIT_01', 'karambit.png'],
  ['KOMODO_01', 'komodo.png'],
  ['BOROBUDUR_01', 'candi_borobudur.png'],
];

for (const [id, outName] of THUMBS) {
  const src = path.join(ROOT_DIR, 'Assets', 'Content', id, `${id}_thumb.png`);
  if (fs.existsSync(src)) {
    fs.copyFileSync(src, path.join(OUT_THUMBS, outName));
  }
}

for (const fn of fs.readdirSync(path.join(ROOT_DIR, 'Markers'))) {
  if (fn.startsWith('KartuQR_') && (fn.endsWith('.pdf') || fn.endsWith('.png'))) {
    fs.copyFileSync(path.join(ROOT_DIR, 'Markers', fn), path.join(OUT_MARKERS, fn));
  }
}

const SCREENS = [
  ['Builds/QA/Captures/BlackBox/BB07_GOLOK_BETAWI_01.png', 'app_detail_golok.png'],
  ['Builds/QA/Captures/BlackBox/BB14_GOLOK_BETAWI_01_hunus.png', 'app_hunus_golok.png'],
  ['Builds/QA/Captures/BlackBox/BB12_GOLOK_BETAWI_01_tahap4.png', 'app_explode_golok.png'],
  ['Builds/QA/Captures/BlackBox/BB05_1_katalog_atas.png', 'app_catalog.png'],
];
for (const [rel, outName] of SCREENS) {
  const src = path.join(ROOT_DIR, rel);
  if (fs.existsSync(src)) {
    fs.copyFileSync(src, path.join(OUT_SCREENS, outName));
  }
}

console.log('Optimizing 6 GLB assets (Meshopt + WebP 1024px, preserving modular part nodes)...');
const gltfBin = process.platform === 'win32'
  ? path.join(WEB_DIR, 'node_modules', '.bin', 'gltf-transform.cmd')
  : path.join(WEB_DIR, 'node_modules', '.bin', 'gltf-transform');

for (const m of MODELS) {
  const srcPath = path.join(ROOT_DIR, m.src);
  const dstPath = path.join(OUT_MODELS, `${m.id}.glb`);
  const beforeMB = (fs.statSync(srcPath).size / 1e6).toFixed(2);
  // --flatten false --join false menjaga nama node bagian (Bilah, Hulu, Sarung, dll.) agar fitur Bongkar 3D jalan di web!
  execFileSync(
    gltfBin,
    [
      'optimize',
      srcPath,
      dstPath,
      '--compress', 'meshopt',
      '--texture-compress', 'webp',
      '--texture-size', String(m.texSize),
      '--flatten', 'false',
      '--join', 'false',
      '--simplify', 'false',
    ],
    { stdio: 'inherit', shell: process.platform === 'win32' }
  );
  const afterMB = (fs.statSync(dstPath).size / 1e6).toFixed(2);
  console.log(`  ${m.id}.glb : ${beforeMB} MB -> ${afterMB} MB`);
}
