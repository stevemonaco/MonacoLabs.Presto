import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { eases, cubicBezier, steps, linear, spring } from 'animejs';
import { converter, toGamut } from 'culori';

const fixturesDir = join(import.meta.dirname, '..', '..', 'tests', 'Monaco.Presto.Tests', 'Fixtures');

const samples = fn => Array.from({ length: 101 }, (_, i) => fn(i / 100));

const parameterised = {
  'in(1.5)': eases.in(1.5),
  'out(3)': eases.out(3),
  'inOut(4)': eases.inOut(4),
  'outIn(2)': eases.outIn(2),
  'inBack(2.5)': eases.inBack(2.5),
  'outBack(1)': eases.outBack(1),
  'inOutBack(3)': eases.inOutBack(3),
  'outInBack(1.2)': eases.outInBack(1.2),
  'inElastic(2,.5)': eases.inElastic(2, .5),
  'outElastic(1.5,1)': eases.outElastic(1.5, 1),
  'inOutElastic(1,.3)': eases.inOutElastic(1, .3),
  'outInElastic(3,2)': eases.outInElastic(3, 2),
};

const constructed = {
  cubicBezier: {
    factory: cubicBezier,
    argSets: [[.17, .67, .83, .67], [1, 0, 1, 0], [0, 1, 0, 1], [1, 0, 0, 1], [.42, 0, .58, 1], [0, 0, 1, 1]],
  },
  steps: {
    factory: steps,
    argSets: [[4], [4, true], [10], [7, true]],
  },
  linear: {
    factory: linear,
    argSets: [[0, 1], [0, .25, 1], [0, '0.25 75%', 1], [0, .5, .5, 1], [1, 0], [0, '0.8 20%', '0.2 80%', 1]],
  },
};

const easeEntries = [];
for (const name of Object.keys(eases)) {
  const ease = eases[name];
  const fn = /^(in|out|inOut|outIn)(Back|Elastic)?$/.test(name) ? ease() : ease;
  easeEntries.push({ kind: 'string', name, samples: samples(fn) });
}
for (const [name, fn] of Object.entries(parameterised)) {
  easeEntries.push({ kind: 'string', name, samples: samples(fn) });
}
for (const [kind, { factory, argSets }] of Object.entries(constructed)) {
  for (const args of argSets) {
    easeEntries.push({ kind, name: `${kind}(${args.join(',')})`, args, samples: samples(factory(...args)) });
  }
}

const springParams = [
  {},
  { mass: 10 },
  { stiffness: 50 },
  { damping: 50 },
  { velocity: 10 },
  { velocity: -5 },
  { mass: 2, stiffness: 200, damping: 5 },
  { damping: 20 },
  { stiffness: 100, damping: 30 },
  { bounce: .3, duration: 800 },
  { bounce: -.5, duration: 1000 },
  { bounce: 0, duration: 400 },
  { bounce: 1, duration: 2000 },
];

const springEntries = springParams.map(params => {
  const s = spring(params);
  return {
    name: `spring(${JSON.stringify(params)})`,
    params,
    mass: s.mass,
    stiffness: s.stiffness,
    damping: s.damping,
    velocity: s.velocity,
    bounce: s.bounce,
    duration: s.duration,
    settlingDuration: s.settlingDuration,
    solverDuration: s.solverDuration,
    samples: samples(s.ease),
  };
});

const toRgb = converter('rgb');
const toLrgb = converter('lrgb');
const toOklab = converter('oklab');
const toOklch = converter('oklch');
const mapToGamut = toGamut('rgb', 'oklch');

const rgbOf = ([r, g, b, alpha]) => ({ mode: 'rgb', r: r / 255, g: g / 255, b: b / 255, alpha });
const clip = v => Math.min(Math.max(v, 0), 1);
const rgbaOf = color => {
  const rgb = toRgb(color);
  // Float noise (1e-15) past a gamut edge would otherwise send an in-gamut color through the chroma search and move it 1e-3.
  const inGamut = [rgb.r, rgb.g, rgb.b].every(v => v > -1e-9 && v < 1 + 1e-9);
  const { r, g, b, alpha } = inGamut ? rgb : mapToGamut(color);
  return [clip(r) * 255, clip(g) * 255, clip(b) * 255, alpha ?? 1];
};
const hex = (value, alpha = 1) => [1, 3, 5].map(i => parseInt(value.slice(i, i + 2), 16)).concat(alpha);
const fromOklch = (l, c, h) => rgbaOf({ mode: 'oklch', l, c, h });
const lerp = (a, b, t) => a + (b - a) * t;

const named = {
  red: hex('#ff0000'), lime: hex('#00ff00'), blue: hex('#0000ff'),
  yellow: hex('#ffff00'), cyan: hex('#00ffff'), magenta: hex('#ff00ff'),
  white: hex('#ffffff'), black: hex('#000000'), gray25: hex('#404040'), gray50: hex('#808080'), gray75: hex('#c0c0c0'),
  nearGrayBelow: [128, 128, 128.25, 1], nearGrayAbove: [128, 128, 128.3, 1],
  blue600: hex('#2563eb'), amber500: hex('#f59e0b'), red500: hex('#ef4444'), teal500: hex('#14b8a6'),
  slate500: hex('#64748b'), indigo500: hex('#6366f1'), rose500: hex('#f43f5e'),
  transparent: [0, 0, 0, 0], redHalf: hex('#ff0000', .5), blue600Half: hex('#2563eb', .5),
  amber500Alpha80: hex('#f59e0b', .8), redQuarter: hex('#ff0000', .25), blueThreeQuarters: hex('#0000ff', .75),
  hue350: fromOklch(.7, .1, 350), hue10: fromOklch(.7, .1, 10), hue350Dark: fromOklch(.5, .12, 350), hue10Light: fromOklch(.8, .08, 10),
};

const colorEntries = Object.entries(named).map(([name, rgba]) => {
  const rgb = rgbOf(rgba);
  const lin = toLrgb(rgb), lab = toOklab(rgb), lch = toOklch(rgb);
  return { name, rgba, linear: [lin.r, lin.g, lin.b], oklab: [lab.l, lab.a, lab.b], oklch: [lch.l, lch.c, lch.h ?? 0] };
});

const spaces = {
  srgb: [toRgb, ['r', 'g', 'b']],
  linearSrgb: [toLrgb, ['r', 'g', 'b']],
  oklab: [toOklab, ['l', 'a', 'b']],
  oklch: [toOklch, ['l', 'c']],
};

const hueFixups = {
  shorter: (h1, h2) => h2 - h1 > 180 ? [h1 + 360, h2] : h2 - h1 < -180 ? [h1, h2 + 360] : [h1, h2],
  longer: (h1, h2) => h2 - h1 > 0 && h2 - h1 < 180 ? [h1 + 360, h2] : h2 - h1 > -180 && h2 - h1 <= 0 ? [h1, h2 + 360] : [h1, h2],
  increasing: (h1, h2) => h2 < h1 ? [h1, h2 + 360] : [h1, h2],
  decreasing: (h1, h2) => h1 < h2 ? [h1 + 360, h2] : [h1, h2],
};

const mixPairs = [
  ['red', 'lime'], ['blue', 'yellow'], ['white', 'blue'], ['transparent', 'red'], ['indigo500', 'rose500'],
  ['blue600', 'amber500'], ['slate500', 'teal500'], ['hue350', 'hue10'], ['hue350Dark', 'hue10Light'],
  ['nearGrayBelow', 'blue600'], ['nearGrayAbove', 'blue600'], ['black', 'gray75'],
  ['redHalf', 'blue600Half'], ['blue600Half', 'amber500Alpha80'], ['redQuarter', 'blueThreeQuarters'],
];
const mixTimes = [0, .25, .5, .75, 1, -.25, 1.25];

// culori's interpolate clamps t and treats a hue as missing only at exactly zero chroma, so the lerp is done by hand
// on culori's conversions, with CSS's achromatic threshold applied first (an end below it takes the other end's hue).
const mixEntries = [];
for (const [fromName, toName] of mixPairs) {
  const from = named[fromName], to = named[toName];
  for (const [space, [convert, channels]] of Object.entries(spaces)) {
    const a = convert(rgbOf(from)), b = convert(rgbOf(to));
    for (const hue of space === 'oklch' ? Object.keys(hueFixups) : ['shorter']) {
      let h1 = 0, h2 = 0;
      if (space === 'oklch') {
        h1 = a.c >= 4e-4 ? a.h : b.c >= 4e-4 ? b.h : 0;
        h2 = b.c >= 4e-4 ? b.h : h1;
        // C# deliberately makes a full turn here, where CSS makes none.
        if ((hue === 'increasing' || hue === 'decreasing') && Math.abs(h1 - h2) < 1e-9) continue;
        [h1, h2] = hueFixups[hue](h1, h2);
      }
      for (const t of mixTimes) {
        let result = t === 0 ? from : t === 1 ? to : undefined;
        if (!result) {
          const alpha = lerp(from[3], to[3], t);
          // Extrapolated alpha is unpremultiplied with its raw value, then clamped to 0-1.
          const mixed = { mode: a.mode, alpha: clip(alpha), h: lerp(h1, h2, t) };
          for (const ch of channels) mixed[ch] = lerp(a[ch] * from[3], b[ch] * to[3], t) / alpha;
          result = rgbaOf(mixed);
        }
        mixEntries.push({ from, to, space, hue, t, result });
      }
    }
  }
}

const gamutInputs = [
  [.7, .4, 0, 1], [.7, .4, 90, 1], [.5, .35, 140, 1], [.6, .3, 264, 1], [.9, .3, 200, 1], [.3, .25, 30, 1],
  [.95, .2, 100, 1], [.4, .4, 300, .5], [.8, .37, 330, .75], [.2, .15, 180, 1], [1.1, .1, 30, 1], [-.1, .1, 200, 1],
];
const gamutEntries = gamutInputs.map(([l, c, h, alpha]) => ({ oklch: [l, c, h, alpha], rgba: rgbaOf({ mode: 'oklch', l, c, h, alpha }) }));

const relativeBases = ['blue600', 'amber500', 'indigo500', 'slate500', 'gray50', 'blue600Half'];
const relativeOps = [
  { op: 'with', l: .8, c: null, h: null }, { op: 'with', l: null, c: .04, h: null }, { op: 'with', l: null, c: null, h: 120 },
  { op: 'with', l: .5, c: .3, h: 30 }, { op: 'with', l: null, c: 0, h: null },
  { op: 'shift', l: .06, c: 0, h: 0 }, { op: 'shift', l: -.08, c: 0, h: 0 }, { op: 'shift', l: 0, c: .1, h: 0 },
  { op: 'shift', l: 0, c: 0, h: 180 }, { op: 'shift', l: 0, c: -.5, h: 0 }, { op: 'shift', l: .5, c: 0, h: 0 },
];
const relativeEntries = relativeBases.flatMap(baseName => relativeOps.map(({ op, l, c, h }) => {
  const base = named[baseName];
  const o = toOklch(rgbOf(base));
  const color = op === 'with'
    ? { l: l ?? o.l, c: c ?? o.c, h: h ?? o.h ?? 0 }
    // Chroma shifted below zero clamps to 0, as CSS relative colors do.
    : { l: o.l + l, c: Math.max(o.c + c, 0), h: (o.h ?? 0) + h };
  return { base, op, l, c, h, result: rgbaOf({ mode: 'oklch', ...color, alpha: base[3] }) };
}));

const colorFixtures = { colors: colorEntries, mixes: mixEntries, gamut: gamutEntries, relative: relativeEntries };

writeFileSync(join(fixturesDir, 'eases.json'), JSON.stringify(easeEntries, null, 2) + '\n');
writeFileSync(join(fixturesDir, 'springs.json'), JSON.stringify(springEntries, null, 2) + '\n');
writeFileSync(join(fixturesDir, 'colors.json'), JSON.stringify(colorFixtures, null, 2) + '\n');
console.log(`Wrote ${easeEntries.length} eases and ${springEntries.length} springs to ${fixturesDir}`);
console.log(`Wrote ${colorEntries.length} colors, ${mixEntries.length} mixes, ${gamutEntries.length} gamut and ${relativeEntries.length} relative cases`);
