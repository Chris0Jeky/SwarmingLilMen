namespace SwarmSim.Core.Canonical;

public readonly struct Boid
{
    public Vec2 Position { get; }
    public Vec2 Velocity { get; }
    public byte Group { get; }

    public Boid(Vec2 position, Vec2 velocity, byte group = 0)
    {
        Position = position;
        Velocity = velocity;
        Group = group;
    }

    public Vec2 Forward => Velocity.IsNearlyZero() ? new Vec2(1f, 0f) : Velocity.Normalized;

    public bool IsMoving => !Velocity.IsNearlyZero();

    public Boid WithVelocity(Vec2 velocity) => new(Position, velocity, Group);

    public Boid WithPosition(Vec2 position) => new(position, Velocity, Group);

    public Boid WithGroup(byte group) => new(Position, Velocity, group);
}
