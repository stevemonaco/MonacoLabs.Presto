import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { eases, cubicBezier, steps, linear, spring } from 'animejs';

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

writeFileSync(join(fixturesDir, 'eases.json'), JSON.stringify(easeEntries, null, 2) + '\n');
writeFileSync(join(fixturesDir, 'springs.json'), JSON.stringify(springEntries, null, 2) + '\n');
console.log(`Wrote ${easeEntries.length} eases and ${springEntries.length} springs to ${fixturesDir}`);
