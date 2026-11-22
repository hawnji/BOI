namespace Hollowcrawl.Enemies
{
    /// <summary>
    /// A difficulty setting. Harder settings change behaviour, not just numbers: shorter telegraphs and
    /// cooldowns, extra projectiles, extra attacks (AttackSpec.MinDifficulty), more elites, fewer rewards.
    /// </summary>
    public sealed class DifficultyDefinition
    {
        public string Id = "normal";
        public string Name = "Normal";
        public int Level = 1;                     // compared with AttackSpec.MinDifficulty
        public float EnemyHealth = 1f;
        public float EnemyDamageBonusChance = 0f; // chance for a hit to deal +1 half point
        public float EnemySpeed = 1f;
        public float CooldownMultiplier = 1f;
        public float TelegraphMultiplier = 1f;
        public int ExtraProjectiles = 0;
        public float ProjectileSpeed = 1f;
        public float BossHealth = 1f;
        public float EliteChanceMultiplier = 1f;
        public float RewardMultiplier = 1f;
        public float RoomModifierChance = 0.1f;
        public float CurseChance = 0.15f;

        public static DifficultyDefinition Story() => new DifficultyDefinition { Id = "story", Name = "Story", Level = 0, EnemyHealth = 0.75f, EnemySpeed = 0.9f, CooldownMultiplier = 1.3f, TelegraphMultiplier = 1.4f, ProjectileSpeed = 0.85f, BossHealth = 0.75f, EliteChanceMultiplier = 0.5f, RewardMultiplier = 1.2f, RoomModifierChance = 0.05f, CurseChance = 0.05f };
        public static DifficultyDefinition Normal() => new DifficultyDefinition();
        public static DifficultyDefinition Hard() => new DifficultyDefinition { Id = "hard", Name = "Hard", Level = 2, EnemyHealth = 1.15f, EnemyDamageBonusChance = 0.15f, EnemySpeed = 1.1f, CooldownMultiplier = 0.85f, TelegraphMultiplier = 0.85f, ExtraProjectiles = 1, ProjectileSpeed = 1.1f, BossHealth = 1.2f, EliteChanceMultiplier = 1.8f, RewardMultiplier = 0.9f, RoomModifierChance = 0.2f, CurseChance = 0.3f };
        public static DifficultyDefinition Nightmare() => new DifficultyDefinition { Id = "nightmare", Name = "Nightmare", Level = 3, EnemyHealth = 1.3f, EnemyDamageBonusChance = 0.35f, EnemySpeed = 1.2f, CooldownMultiplier = 0.7f, TelegraphMultiplier = 0.75f, ExtraProjectiles = 2, ProjectileSpeed = 1.2f, BossHealth = 1.4f, EliteChanceMultiplier = 2.5f, RewardMultiplier = 0.8f, RoomModifierChance = 0.35f, CurseChance = 0.45f };
    }

    /// <summary>Resolved numbers for one spawned enemy (definition x floor x difficulty x elite).</summary>
    public struct EnemyScaling
    {
        public float MaxHealth;
        public float SpeedMultiplier;
        public float CooldownMultiplier;
        public float TelegraphMultiplier;
        public float ProjectileSpeedMultiplier;
        public int ExtraProjectiles;
        public int DifficultyLevel;
        public bool Elite;

        public const float EliteHealth = 1.6f;
        public const float EliteSpeed = 1.15f;

        public static EnemyScaling Compute(EnemyDefinition def, int floorIndex, DifficultyDefinition diff, bool elite)
        {
            diff = diff ?? DifficultyDefinition.Normal();
            float hp = def.Health * (1f + def.HealthPerFloor * floorIndex) * (def.IsBoss ? diff.BossHealth : diff.EnemyHealth);
            if (elite) hp *= EliteHealth;
            return new EnemyScaling
            {
                MaxHealth = hp,
                SpeedMultiplier = diff.EnemySpeed * (elite ? EliteSpeed : 1f),
                CooldownMultiplier = diff.CooldownMultiplier * (elite ? 0.85f : 1f),
                TelegraphMultiplier = diff.TelegraphMultiplier,
                ProjectileSpeedMultiplier = diff.ProjectileSpeed,
                ExtraProjectiles = diff.ExtraProjectiles + (elite ? 1 : 0),
                DifficultyLevel = diff.Level,
                Elite = elite,
            };
        }
    }
}
