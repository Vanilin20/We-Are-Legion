using UnityEngine;

// Одне завдання для мурахи: дійти до клітинки (або стати поруч) і попрацювати там.
public abstract class AntTask
{
    public Vector3Int Cell { get; }

    protected AntTask(Vector3Int cell) { Cell = cell; }

    // true — стати на сусідню клітинку (стіну не можна зайняти), false — на саму клітинку.
    public virtual bool StandNextTo => false;

    public abstract bool IsValid(GridManager grid);
    public abstract float WorkTime(AntAgent ant);
    public abstract void Complete(AntAgent ant, GridManager grid);
}

// Прокопати клітинку стіни.
public class DigTask : AntTask
{
    public DigTask(Vector3Int cell) : base(cell) { }

    public override bool StandNextTo => true;

    public override bool IsValid(GridManager grid)
    {
        return grid.HasWall(Cell) && grid.GetDigTask(Cell) == this;
    }

    public override float WorkTime(AntAgent ant) => ant.DigTime;

    public override void Complete(AntAgent ant, GridManager grid) => grid.Dig(Cell);
}

// Просто дійти до клітинки (ручна команда гравця).
public class MoveTask : AntTask
{
    public MoveTask(Vector3Int cell) : base(cell) { }

    public override bool IsValid(GridManager grid) => grid.IsWalkable(Cell);

    public override float WorkTime(AntAgent ant) => 0f;

    public override void Complete(AntAgent ant, GridManager grid) { }
}
