namespace Monster.MonsterSabotages
{
    public abstract class Sabotage
    {
        public abstract SabotageType Type { get; }
        public abstract void Execute(SabotageTarget target, MonsterBrain brain);
        public abstract void Restore(SabotageTarget target, MonsterBrain brain);
    }
}