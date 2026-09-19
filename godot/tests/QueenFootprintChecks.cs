using Godot;
using Godot.Collections;
using System.Reflection;
using System.Collections.Generic;

public partial class QueenFootprintChecks : RefCounted
{
    public Array<string> Run(Unit queen, Unit hero, Godot.Collections.Dictionary mapData)
    {
        var failures = new Array<string>();
        void Check(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        var queenSnapshot = queen.BuildRuntimeSnapshot();
        var heroSnapshot = hero.BuildRuntimeSnapshot();
        var controller = new BattleController();
        var director = new AiDirector();
        try
        {
            Check(queen.FootprintSize == 2, "Queen must occupy a 2x2 footprint");
            Check(hero.FootprintSize == 1, "Normal units must remain 1x1");
            var sprite = queen.GetNode<Sprite2D>("Sprite2D");
            Check(sprite.Texture.GetSize() == new Vector2(128, 128) && !sprite.RegionEnabled, "Queen must use the dedicated 128px texture");
            Check(sprite.Texture.GetSize() * sprite.Scale == new Vector2(128, 128), "Queen must render at exactly 2x2 tiles");
            Check(queen.GetTurnOrderIcon() == sprite.Texture, "Queen portrait must use her dedicated texture");
            var mapWalls = mapData["walls"].AsGodotArray<Vector2I>();
            var mapCells = mapData["walkable_cells"].AsGodotArray<Vector2I>();
            foreach (var cell in queen.GetOccupiedCellsAt(queen.GridPos))
            {
                Check(mapCells.Contains(cell) && !mapWalls.Contains(cell), "Queen's complete spawn footprint must be walkable");
                foreach (var encounter in mapData["encounters"].AsGodotArray<Godot.Collections.Dictionary>())
                {
                    foreach (var enemy in encounter["enemies"].AsGodotArray<Godot.Collections.Dictionary>())
                    {
                        if (enemy["name"].AsString() != "Spider Queen")
                            Check(enemy["grid_pos"].AsVector2I() != cell, "Queen must not overlap a guard at spawn");
                    }
                }
            }

            var walkable = Field<HashSet<Vector2I>>(controller, "_walkableCells");
            var walls = Field<HashSet<Vector2I>>(controller, "_wallCellSet");
            var units = Field<Array<Unit>>(controller, "_allUnits");
            var doors = Field<System.Collections.Generic.Dictionary<Vector2I, Godot.Collections.Dictionary>>(controller, "_mapDoorByCell");
            units.Add(queen);
            units.Add(hero);
            for (var row = 0; row < 10; row++)
                for (var column = 0; column < 10; column++)
                    walkable.Add(new Vector2I(column, row));
            queen.SetGridPos(new Vector2I(1, 1));
            hero.SetGridPos(new Vector2I(8, 8));
            Check(queen.Position == new Vector2(128, 128), "Queen must be centered over four cells");
            Check(Stand(controller, queen, queen.GridPos), "Queen must not collide with herself");
            Check(!Stand(controller, queen, new Vector2I(9, 9)), "Footprint cannot extend outside map bounds");
            foreach (var cell in queen.GetOccupiedCellsAt(new Vector2I(3, 3)))
            {
                walls.Add(cell);
                Check(!Stand(controller, queen, new Vector2I(3, 3)), "Every footprint corner must reject walls");
                walls.Clear();
            }
            doors[new Vector2I(4, 4)] = new Godot.Collections.Dictionary { { "is_open", false } };
            Check(!Stand(controller, queen, new Vector2I(3, 3)), "Non-anchor corner must collide with closed doors");
            doors[new Vector2I(4, 4)]["is_open"] = true;
            Check(Stand(controller, queen, new Vector2I(3, 3)), "Open doors must allow the footprint");
            doors.Clear();
            hero.SetGridPos(new Vector2I(4, 4));
            Check(!Stand(controller, queen, new Vector2I(3, 3)), "Non-anchor corner must collide with another unit");
            foreach (var cell in queen.GetOccupiedCellsAt(queen.GridPos))
            {
                Check(!Stand(controller, hero, cell), "Small units must not enter any queen cell");
                Check(ReferenceEquals(Call(controller, "GetLivingEnemyAtCell", "player", cell), queen), "All queen cells must be targetable");
            }
            foreach (var position in new[] { new Vector2I(0, 1), new Vector2I(1, 0), new Vector2I(3, 2), new Vector2I(2, 3) })
            {
                hero.SetGridPos(position);
                Check(queen.CanAttackTarget(hero, 1, units) && hero.CanAttackTarget(queen, 1, units), "Melee must work from every side");
            }
            hero.SetGridPos(new Vector2I(4, 2));
            Check(!queen.CanAttackTarget(hero, 1, units), "Large footprint must not grant extra melee reach");
            Check(Unit.IsWithinRange(new Vector2I(2, 2), queen.GetClosestCell(new Vector2I(2, 2)), 0), "Area attacks must include non-anchor queen cells");

            hero.SetGridPos(new Vector2I(8, 8));
            for (var row = 0; row < 10; row++)
                if (row != 4) walls.Add(new Vector2I(4, row));
            var goal = new Vector2I(6, 1);
            Check(Path(controller, queen, goal).Count == 0, "Queen must not path through a one-cell opening");
            Check(Path(controller, hero, new Vector2I(1, 5)).Count > 0, "Normal units must still fit through a one-cell opening");
            walls.Remove(new Vector2I(4, 5));
            var path = Path(controller, queen, goal);
            Check(path.Count > 0, "Queen must fit through a two-cell opening");
            var previous = queen.GridPos;
            foreach (var cell in path)
            {
                Check(Stand(controller, queen, cell), "Every path step must fit all four cells");
                Check((cell - previous).Abs().X + (cell - previous).Abs().Y == 1, "Queen movement must use adjacent anchor steps");
                previous = cell;
            }
            var finder = (System.Func<Vector2I, Array<Vector2I>>)Call(controller, "CreatePathFinder", queen, queen.GridPos, 100);
            Check(finder(goal).Count == path.Count, "AI BFS must obey the same footprint rules");
            walls.Clear();
            hero.SetGridPos(new Vector2I(7, 2));
            var options = new[] { new AiDirector.ActionOption("melee", "attack", 1, true, true) };
            finder = (System.Func<Vector2I, Array<Vector2I>>)Call(controller, "CreatePathFinder", queen, queen.GridPos, 100);
            var choseStep = director.TryChooseStepTowardActionRange(queen, new Array<Unit> { hero }, options,
                target => target == hero, cell => Stand(controller, queen, cell), (_, _) => true, finder, out var step);
            Check(choseStep && Stand(controller, queen, step), "Queen AI must choose a legal movement step");
            queen.ResetTurnResources();
            var movement = queen.RemainingMovement;
            Check(queen.TrySpendMovement() && queen.RemainingMovement == movement - 1, "Moving a large unit costs one movement per step");
            var snapshot = queen.BuildRuntimeSnapshot();
            queen.SetGridPos(new Vector2I(6, 6));
            queen.ApplyRuntimeSnapshot(snapshot);
            Check(queen.FootprintSize == 2 && queen.Position == queen.GetWorldCenterAt(queen.GridPos), "Save/load must preserve footprint and centering");
        }
        finally
        {
            queen.ApplyRuntimeSnapshot(queenSnapshot);
            hero.ApplyRuntimeSnapshot(heroSnapshot);
            director.Free();
            controller.Free();
        }
        return failures;
    }

    private static T Field<T>(BattleController controller, string name) =>
        (T)typeof(BattleController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

    private static object Call(BattleController controller, string name, params object[] arguments) =>
        typeof(BattleController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, arguments);

    private static bool Stand(BattleController controller, Unit unit, Vector2I cell) =>
        (bool)Call(controller, "CanUnitStandAt", unit, cell);

    private static Array<Vector2I> Path(BattleController controller, Unit unit, Vector2I goal) =>
        (Array<Vector2I>)Call(controller, "FindPath", unit, unit.GridPos, goal, 100);
}