# Monaco.Presto specs

One spec per feature; format and usage in [../README.md](../README.md). Find a type's spec by its \	ypes\ list, or search this folder for the type name.

## Timing

| Spec | Id | Owns |
|---|---|---|
| [Engine, clock and frame sources](engine.md) | CORE-ENGINE | `Engine`, `Clock`, `IFrameSource`, `ManualFrameSource`, `StopwatchFrameSource`, `TimeUnit` |
| [Engine instancing and threading](threading.md) | CORE-THREADING | `EngineScope` |
| [Timer timing and playback controls](timer.md) | CORE-TIMER | `Timer`, `TimerParams`, `LoopCount` |
| [Callbacks and Then()](callbacks.md) | CORE-CALLBACKS | — |

## Eases

| Spec | Id | Owns |
|---|---|---|
| [Eases](eases.md) | CORE-EASES | `Ease`, `Eases`, `EaseValue`, `LinearStop`, `RoughTaper`, `WiggleType` |
| [Ease strings](ease-parser.md) | CORE-EASEPARSER | `EaseParser` |
| [Spring](spring.md) | CORE-SPRING | `Spring`, `Springs` |

## Values and targets

| Spec | Id | Owns |
|---|---|---|
| [Values, parsing and unit conversion](values.md) | CORE-VALUES | `TweenValue`, `ValueKind`, `ValueParser`, `UnitConverter`, `DecomposedValue`, `FunctionValue` |
| [Colors and color spaces](colors.md) | CORE-COLORS | `PrestoColor`, `OklabColor`, `OklchColor`, `ColorSpace`, `HueInterpolation` |
| [Targets, adapters and codecs](targets.md) | CORE-TARGETS | `ITargetAdapter`, `IPropertyAccessor`, `IValueCodec`, `IReconcilingValueCodec`, `ObjectTargetAdapter`, `TargetParser`, `DiscreteCodec`, `DiscreteRule`, `StructCodec` |

## Animation and timelines

| Spec | Id | Owns |
|---|---|---|
| [Animation and tweens](animation.md) | CORE-ANIMATION | `Animation`, `AnimationParams`, `Tween`, `TweenParams`, `TweenSnapshot`, `TimerTweens` |
| [Keyframes](keyframes.md) | CORE-KEYFRAMES | `Keyframe`, `Keyframes`, `KeyframeBuilder`, `KeyframesBuilder`, `TweenKeyframesBuilder` |
| [Composition and additive animation](composition.md) | CORE-COMPOSITION | `Composition` |
| [Timeline](timeline.md) | CORE-TIMELINE | `Timeline`, `TimelineParams`, `TimelinePosition`, `TimerChildren` |
| [Stagger](stagger.md) | CORE-STAGGER | `Stagger`, `StaggerParams`, `StaggerFrom`, `StaggerAxis` |

## Entry points, defaults and builders

| Spec | Id | Owns |
|---|---|---|
| [Presto facade](presto.md) | CORE-PRESTO | `Presto` |
| [Scope and defaults](scope-and-defaults.md) | CORE-SCOPE | `Scope`, `ScopeParams`, `PrestoDefaults`, `DefaultsParams`, `IRevertible` |
| [Reduced motion](reduced-motion.md) | CORE-REDUCED | — |
| [Animation builder](animation-builder.md) | CORE-BUILDER | `AnimationBuilder`, `PropertyBuilder` |
| [Utilities](utils.md) | CORE-UTILS | `Utils`, `ChainedUtil`, `ChainedFunctionValue`, `RandomGenerator` |
| [Text scrambling](scramble.md) | CORE-SCRAMBLE | `ScrambleTextParams`, `ScrambleFrom` |

## Physics, values over time and simulation

| Spec | Id | Owns |
|---|---|---|
| [Animatable](animatable.md) | CORE-ANIMATABLE | `Animatable`, `AnimatableParams`, `AnimatableProperty` |
| [Inertia, velocity tracking and projectiles](inertia.md) | CORE-INERTIA | `Inertia`, `VelocityTracker`, `Projectile` |
| [Motion values](motion-values.md) | CORE-MOTIONVALUE | `MotionValue`, `MotionObjectValue` |
| [Motion path through points](motion-path.md) | CORE-MOTIONPATH | `MotionPath` |
| [Particle simulation](particles.md) | CORE-PARTICLES | `ParticleSystem`, `EmitterParams`, `ParticleState`, `ParticleCurve` |

## Diagnostics

| Spec | Id | Owns |
|---|---|---|
| [Engine diagnostics](diagnostics.md) | CORE-DIAGNOSTICS | `EngineDiagnostics`, `FrameStats`, `OriginScope`, `PrestoDiagnostics` |

