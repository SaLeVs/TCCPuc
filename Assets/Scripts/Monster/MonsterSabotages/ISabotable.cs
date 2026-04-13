namespace Monster.MonsterSabotages
{
    public interface ISabotageable
    {
        SabotageType SabotageType { get; }
        
        public bool IsSabotaged { get; }
        public void Sabotage();
        public void Restore();
    }
}