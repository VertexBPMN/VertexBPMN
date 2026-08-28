import { readFileSync, writeFileSync } from 'node:fs';

const sourcePath = new URL('./node_modules/feelin/dist/index.js', import.meta.url);
const targetPath = new URL('./feelin.strict.mjs', import.meta.url);
let source = readFileSync(sourcePath, 'utf8');

const replacements = [
  [
    `if (args.length === 0) {
            return null;
        }`,
    `if (args.length === 0) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (!args.every(arg => tester(arg) !== FALSE)) {
            return null;
        }`,
    `if (!args.every(arg => tester(arg) !== FALSE)) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `if (!convertedArgs) {
            return null;
        }`,
    `if (!convertedArgs) {
            return FUNCTION_PARAMETER_MISSMATCH;
        }`
  ],
  [
    `return d && ifValid(d.setZone('utc').startOf('day')) || null;`,
    `return d && ifValid(d.setZone('utc', { keepLocalTime: true }).startOf('day')) || null;`
  ],
  [
    `if (offset) {
            throw notImplemented('time(..., offset)');
        }`,
    `let zone = null;
        if (offset) {
            if (!isDuration(offset)) return null;
            const minutes = offset.as('minutes');
            if (!Number.isInteger(minutes) || Math.abs(minutes) > 14 * 60) return null;
            zone = FixedOffsetZone.instance(minutes);
        }`
  ],
  [
    `t = date().set({
                hour,`,
    `t = date().setZone(zone || SystemZone.instance).set({
                hour,`
  ],
  [
    `const dLocal = d.toLocal();`,
    `const dLocal = d;`
  ],
  [
    `t = date().setZone(zone || SystemZone.instance).set({
                hour,
                minute,
                second
            }).set({
                year: 1900,
                month: 1,
                day: 1,
                millisecond: 0
            });`,
    `t = date().setZone(zone || SystemZone.instance).set({
                hour,
                minute,
                second: Math.trunc(second),
                millisecond: Math.round((second - Math.trunc(second)) * 1000)
            }).set({
                year: 1900,
                month: 1,
                day: 1
            });`
  ]
];

for (const [original, replacement] of replacements) {
  const first = source.indexOf(original);
  if (first < 0 || source.indexOf(original, first + original.length) >= 0) {
    throw new Error('Pinned feelin source no longer matches the strict argument patch.');
  }
  source = source.replace(original, replacement);
}

writeFileSync(targetPath, source, 'utf8');
