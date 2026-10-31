using System;
using LowObservable.Core.Maths;
using LowObservable.Core.Stages;
using Xunit;

namespace LowObservable.Core.Tests
{
    /// <summary>Random play with every tool on every level must never throw (guards the paths bots never take).</summary>
    public class StageFuzzTests
    {
        [Fact]
        public void RandomInputsNeverThrow()
        {
            foreach (var def in BuiltInStages.LoadAll())
            {
                for (ulong seed = 1; seed <= 2; seed++)
                {
                    var stage = new Stage(def, new StageConfig { Seed = seed, UnlockedTools = ToolInfo.All });
                    var rng = new Rng(seed * 31 + 7);
                    var move = new Vec2(1, 0);
                    for (int k = 0; k < 4000; k++)
                    {
                        if (k % 25 == 0) move = Vec2.FromAngle(rng.Range(0, 2 * Math.PI));
                        var input = new StageInput
                        {
                            Move = move,
                            Mode = (MoveMode)(k / 200 % 3),
                            InteractPressed = rng.Range(0, 1) < 0.05,
                            InteractHeld = rng.Range(0, 1) < 0.3,
                            UsePressed = rng.Range(0, 1) < 0.02,
                            Tool = ToolInfo.All[(int)rng.Range(0, 4.999)],
                            Aim = stage.Player + Vec2.FromAngle(rng.Range(0, 6.28), rng.Range(0, 12)),
                            MirrorAngle = rng.Range(0, 6.28),
                        };
                        try { stage.Step(input); }
                        catch (Exception e) { throw new Exception($"{def.Id} seed {seed} step {k}: {e}"); }
                        if (stage.Outcome != StageOutcome.Running) break;
                    }
                }
            }
        }
    }
}
