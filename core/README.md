# Low Observable Core

The game's brain with no engine attached: the player's EKF, the Seeker's particle filter, sensors,
data association, the floor plan, and the `Episode` simulation loop.

It is two things at once:

- A **Unity package** (`com.brennandrake.lowobservable.core`, this folder). `noEngineReferences` in the
  asmdef enforces that nothing here touches `UnityEngine`.
- A **.NET library** built from these same sources by `dotnet/LowObservable.Core/LowObservable.Core.csproj`,
  so it can be unit-tested and simulated headless with `make test` and `make sim`.

Keep it at C# 9 (Unity's language level): no file-scoped namespaces, no `record struct`, no global usings.
The `dotnet` build enforces this with `<LangVersion>9.0</LangVersion>` and treats warnings as errors.

See `docs/architecture.md` for how the pieces fit and `docs/estimation-primer.md` for the math.
