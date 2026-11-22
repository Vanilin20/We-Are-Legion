using UnityEngine;

// Одне завдання для мурахи: дійти до клітинки (або стати поруч) у певній локації й попрацювати там.
public abstract class AntTask
{
    private readonly GridManager grid;
    private readonly Vector3Int cell;

    protected AntTask(GridManager grid, Vector3Int cell)
    {
        this.grid = grid;
        this.cell = cell;
    }

    public virtual GridManager Grid => grid;
    public virtual Vector3Int Cell => cell;

    // true — стати на сусідню клітинку (стіну не можна зайняти), false — на саму клітинку.
    public virtual bool StandNextTo => false;

    // Ціль рухається (жук): мураха перераховує маршрут, коли ціль зміщується.
    public virtual bool TargetMoves => false;

    // Повторювана робота (удари): Complete викликається раз на WorkTime, доки завдання дійсне.
    public virtual bool Repeating => false;

    public abstract bool IsValid(AntAgent ant);
    public abstract float WorkTime(AntAgent ant);
    public abstract void Complete(AntAgent ant);
}

// Прокопати клітинку стіни.
public class DigTask : AntTask
{
    public DigTask(GridManager grid, Vector3Int cell) : base(grid, cell) { }

    public override bool StandNextTo => true;

    public override bool IsValid(AntAgent ant)
    {
        return Grid != null && Grid.HasWall(Cell) && Grid.GetDigTask(Cell) == this;
    }

    public override float WorkTime(AntAgent ant) => ant.DigTime;

    public override void Complete(AntAgent ant) => Grid.Dig(Cell);
}

// Просто дійти до клітинки (ручна команда гравця або відійти вбік).
public class MoveTask : AntTask
{
    public MoveTask(GridManager grid, Vector3Int cell) : base(grid, cell) { }

    public override bool IsValid(AntAgent ant) => Grid != null && Grid.IsWalkable(Cell);

    public override float WorkTime(AntAgent ant) => 0f;

    public override void Complete(AntAgent ant) { }
}

// Підібрати їжу (черга робітників).
public class PickupTask : AntTask
{
    public FoodItem Food { get; }

    public PickupTask(FoodItem food) : base(food.Grid, food.Cell) { Food = food; }

    public override bool IsValid(AntAgent ant) => Food != null && ant.CanCarryMore;

    public override float WorkTime(AntAgent ant) => 0.3f;

    public override void Complete(AntAgent ant) => ant.AddCargo(Food.Take());
}

// Віднести вантаж на склад (мураха ставить собі сама, коли руки повні).
public class DeliverTask : AntTask
{
    public Storage Storage { get; }

    public DeliverTask(Storage storage) : base(storage.Grid, storage.Cell) { Storage = storage; }

    public override bool StandNextTo => true;

    public override bool IsValid(AntAgent ant) => Storage != null && ant.CargoCount > 0;

    public override float WorkTime(AntAgent ant) => 0.3f;

    public override void Complete(AntAgent ant) => Storage.Add(ant.UnloadCargo());
}

// Атакувати жука (ручна команда гравця). Мураха стає поруч і б'є раз на AttackInterval, доки жук живий.
public class AttackTask : AntTask
{
    public EnemyBeetle Target { get; }

    public AttackTask(EnemyBeetle target) : base(target.Grid, target.Cell) { Target = target; }

    public override GridManager Grid => Target != null ? Target.Grid : null;
    public override Vector3Int Cell => Target != null ? Target.Cell : base.Cell;

    public override bool StandNextTo => true;
    public override bool TargetMoves => true;
    public override bool Repeating => true;

    public override bool IsValid(AntAgent ant) => Target != null && Target.IsAlive && Target.Grid != null;

    public override float WorkTime(AntAgent ant) => ant.AttackInterval;

    public override void Complete(AntAgent ant) => Target.Damage(ant.AttackDamage, ant);
}
