import { build } from 'esbuild';
import { copyFile, cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const studioRoot = join(scriptDir, '..', '..');
const srcRoot = join(scriptDir, 'src');
const outRoot = join(studioRoot, 'wwwroot', 'lib');

const bundles = [
  ['bpmn-modeler.entry.js', 'bpmn-js/bpmn-modeler.js', 'BpmnModelerBundle'],
  ['bpmn-viewer.entry.js', 'bpmn-js/bpmn-viewer.js', 'BpmnViewerBundle'],
  ['bpmn-properties-panel.entry.js', 'bpmn-js-properties-panel/properties-panel.js', 'BpmnPropertiesPanelBundle'],
  ['dmn-modeler.entry.js', 'dmn-js/dmn-modeler.js', 'DmnModelerBundle'],
  ['dmn-viewer.entry.js', 'dmn-js/dmn-viewer.js', 'DmnViewerBundle'],
  ['cmmn-modeler.entry.js', 'cmmn-js/cmmn-modeler.js', 'CmmnModelerBundle'],
  ['cmmn-viewer.entry.js', 'cmmn-js/cmmn-viewer.js', 'CmmnViewerBundle'],
  ['form-editor.entry.js', 'form-js/form-editor.js', 'FormEditorBundle'],
  ['form-viewer.entry.js', 'form-js/form-viewer.js', 'FormViewerBundle']
];

const assets = [
  ['node_modules/bpmn-js/dist/assets/diagram-js.css', 'bpmn-js/diagram-js.css'],
  ['node_modules/bpmn-js/dist/assets/bpmn-js.css', 'bpmn-js/bpmn.css'],
  ['node_modules/@bpmn-io/properties-panel/dist/assets/properties-panel.css', 'bpmn-js-properties-panel/properties-panel.css'],
  ['node_modules/dmn-js/dist/assets/diagram-js.css', 'dmn-js/diagram-js.css'],
  ['node_modules/dmn-js/dist/assets/dmn-js-decision-table.css', 'dmn-js/dmn-js-decision-table.css'],
  ['node_modules/dmn-js/dist/assets/dmn-js-drd.css', 'dmn-js/dmn-js-drd.css'],
  ['node_modules/dmn-js/dist/assets/dmn-js-literal-expression.css', 'dmn-js/dmn-js-literal-expression.css'],
  ['node_modules/dmn-js/dist/assets/dmn-js-shared.css', 'dmn-js/dmn.css'],
  ['node_modules/cmmn-js/dist/assets/diagram-js.css', 'cmmn-js/diagram-js.css'],
  ['node_modules/cmmn-js/dist/assets/cmmn-font/css/cmmn.css', 'cmmn-js/cmmn.css'],
  ['node_modules/@bpmn-io/form-js/dist/assets/form-js.css', 'form-js/form-js.css'],
  ['node_modules/@bpmn-io/form-js/dist/assets/form-js-editor.css', 'form-js/form-js-editor.css']
];

async function ensureParent(path) {
  await mkdir(dirname(path), { recursive: true });
}

async function copyAsset([from, to]) {
  const target = join(outRoot, to);
  await ensureParent(target);
  await copyFile(join(studioRoot, from), target);
}

await rm(join(outRoot, 'bpmn-js-properties-panel'), { recursive: true, force: true });

for (const [entryPoint, outfile, globalName] of bundles) {
  const target = join(outRoot, outfile);
  await ensureParent(target);
  await build({
    entryPoints: [join(srcRoot, entryPoint)],
    outfile: target,
    bundle: true,
    format: 'iife',
    globalName,
    platform: 'browser',
    target: ['es2020'],
    legalComments: 'eof',
    sourcemap: false,
    minify: true,
    logLevel: 'info'
  });
}

await Promise.all(assets.map(copyAsset));

await cp(
  join(studioRoot, 'node_modules/cmmn-js/dist/assets/cmmn-font/font'),
  join(outRoot, 'cmmn-js/cmmn-font/font'),
  { recursive: true }
);

const cmmnCss = join(outRoot, 'cmmn-js/cmmn.css');
const cmmnCssContent = await readFile(cmmnCss, 'utf8');
await writeFile(cmmnCss, cmmnCssContent.replaceAll('../font/', 'cmmn-font/font/'));

const vertexModdleDir = join(outRoot, 'vertex-bpmn-moddle');
await mkdir(vertexModdleDir, { recursive: true });
await copyFile(join(srcRoot, 'vertex.json'), join(vertexModdleDir, 'vertex.json'));

console.log(`Built ${bundles.length} bpmn.io bundles and copied ${assets.length} stylesheet assets, CMMN font assets, and vertex-bpmn-moddle.`);
