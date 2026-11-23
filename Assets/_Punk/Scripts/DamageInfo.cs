using UnityEngine;

namespace PunkGame
{
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Team Team;
        public readonly HitStrength Strength;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitDirection;
        public readonly float Knockback;

        public DamageInfo(float amount, Team team, HitStrength strength, GameObject source, Vector3 hitPoint, Vector3 hitDirection, float knockback)
        {
            Amount = amount;
            Team = team;
            Strength = strength;
            Source = source;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
            Knockback = knockback;
        }
    }
}