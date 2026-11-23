namespace PunkGame
{
    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        DamageResult TakeDamage(in DamageInfo info);
    }
}