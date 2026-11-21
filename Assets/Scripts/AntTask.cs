using UnityEngine;

// Одне завдання для мурахи: дійти до клітинки (або стати поруч) у певній локації й попрацювати там.
public abstract class AntTask
{
    public GridManager Grid { get; }
    public Vector3Int Cell { get; }

    protected AntTask(GridManager grid, Vector3Int cell)
    {
        Grid = grid;
        Cell = cell;
    }

    // true — стати на сусідню клітинку (стіну не можна зайняти), false — на саму клітинку.
    public virtual bool StandNextTo => false;

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
