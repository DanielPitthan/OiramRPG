using Oiram.Loot;
using static Oiram.Audio.Synth;

namespace Oiram.Audio
{
    public enum Sfx
    {
        Cursor,
        Confirm,
        Cancel,
        Jump,
        Land,
        Hit,
        HitGood,
        Perfect,
        Block,
        BlockPerfect,
        Miss,
        Magic,
        Heal,
        Buff,
        EnemyDie,
        PartyKnockOut,
        Coin,
        ChestOpen,
        LevelUp,
        Victory,
        Defeat,
        Encounter,
        Stairs,
        ChargeFull,
        Steal,
        Save,
        Bump,
    }

    /// <summary>Receitas dos efeitos sonoros (amostras geradas pelo <see cref="Synth"/>).</summary>
    public static class SoundBank
    {
        static float[] HitBody() => Mix(
            Tone(Wave.Noise, 7000f, 1500f, 0.09f, 0.32f, decay: 3f),
            Tone(Wave.Sine, 170f, 50f, 0.13f, 0.55f, decay: 2f));

        static float[] BlockBody() => Mix(
            Tone(Wave.Square, 520f, 500f, 0.11f, 0.16f, decay: 3f),
            Tone(Wave.Square, 780f, 760f, 0.11f, 0.11f, decay: 3f),
            Tone(Wave.Noise, 9000f, 9000f, 0.04f, 0.14f, decay: 4f));

        static float[] CoinBody() => Concat(
            Tone(Wave.Square, 988f, 988f, 0.05f, 0.16f, decay: 0.5f),
            Tone(Wave.Square, 1319f, 1319f, 0.2f, 0.16f, decay: 2f));

        public static float[] Build(Sfx sfx) => Limit(sfx switch
        {
            Sfx.Cursor => Tone(Wave.Square, 1400f, 1400f, 0.03f, 0.14f, decay: 2f),
            Sfx.Confirm => Concat(Tone(Wave.Square, 988f, 988f, 0.04f, 0.16f, decay: 0.7f), Tone(Wave.Square, 1319f, 1319f, 0.07f, 0.16f)),
            Sfx.Cancel => Tone(Wave.Square, 660f, 420f, 0.09f, 0.16f),
            Sfx.Jump => Tone(Wave.Square, 330f, 900f, 0.14f, 0.14f, duty: 0.25f, decay: 1f),
            Sfx.Land => Tone(Wave.Noise, 2500f, 600f, 0.07f, 0.2f, decay: 3f),
            Sfx.Hit => HitBody(),
            Sfx.HitGood => Mix(HitBody(), Delay(Notes(Wave.Triangle, 0.09f, 0.28f, 84), 0.02f)),
            Sfx.Perfect => Mix(HitBody(), Delay(Notes(Wave.Square, 0.06f, 0.2f, 88, 93), 0.02f), Delay(Notes(Wave.Triangle, 0.14f, 0.22f, 100), 0.13f)),
            Sfx.Block => BlockBody(),
            Sfx.BlockPerfect => Mix(BlockBody(), Delay(Notes(Wave.Triangle, 0.08f, 0.26f, 91, 96), 0.03f)),
            Sfx.Miss => Tone(Wave.Triangle, 260f, 140f, 0.15f, 0.3f, decay: 1.5f),
            Sfx.Magic => Mix(Tone(Wave.Saw, 300f, 1400f, 0.32f, 0.2f, decay: 1f), Tone(Wave.Noise, 2000f, 9000f, 0.32f, 0.1f, decay: 1f)),
            Sfx.Heal => Mix(Notes(Wave.Triangle, 0.065f, 0.24f, 72, 76, 79, 84), Delay(Tone(Wave.Sine, 2100f, 2600f, 0.25f, 0.05f), 0.1f)),
            Sfx.Buff => Tone(Wave.Triangle, 400f, 1250f, 0.26f, 0.24f, decay: 1f),
            Sfx.EnemyDie => Mix(Tone(Wave.Square, 700f, 90f, 0.32f, 0.15f, duty: 0.3f, decay: 1.2f), Tone(Wave.Noise, 4000f, 500f, 0.25f, 0.1f)),
            Sfx.PartyKnockOut => Tone(Wave.Triangle, 420f, 70f, 0.5f, 0.32f, decay: 1f),
            Sfx.Coin => CoinBody(),
            Sfx.ChestOpen => Concat(Tone(Wave.Saw, 160f, 260f, 0.12f, 0.1f, decay: 0.5f), Notes(Wave.Triangle, 0.07f, 0.24f, 79, 84)),
            Sfx.LevelUp => Mix(Concat(Notes(Wave.Square, 0.08f, 0.15f, 72, 76, 79), Notes(Wave.Square, 0.32f, 0.15f, 84)),
                Concat(Notes(Wave.Triangle, 0.08f, 0.18f, 60, 64, 67), Notes(Wave.Triangle, 0.32f, 0.18f, 72))),
            Sfx.Victory => Mix(
                Concat(Notes(Wave.Square, 0.11f, 0.14f, 72, 72, 72), Notes(Wave.Square, 0.33f, 0.14f, 72), Notes(Wave.Square, 0.11f, 0.14f, 68, 70), Notes(Wave.Square, 0.5f, 0.14f, 72)),
                Concat(Notes(Wave.Triangle, 0.33f, 0.2f, 48, 0), Notes(Wave.Triangle, 0.22f, 0.2f, 44, 46), Notes(Wave.Triangle, 0.5f, 0.2f, 48))),
            Sfx.Defeat => Notes(Wave.Triangle, 0.28f, 0.28f, 67, 63, 60, 55),
            Sfx.Encounter => Mix(Tone(Wave.Noise, 9000f, 800f, 0.42f, 0.13f, decay: 1f), Notes(Wave.Square, 0.07f, 0.13f, 84, 79, 76, 72, 67)),
            Sfx.Stairs => Concat(Tone(Wave.Noise, 1600f, 700f, 0.07f, 0.24f, decay: 2f), Silence(0.07f),
                Tone(Wave.Noise, 1200f, 500f, 0.07f, 0.22f, decay: 2f), Silence(0.07f), Tone(Wave.Noise, 900f, 350f, 0.08f, 0.2f, decay: 2f)),
            Sfx.ChargeFull => Notes(Wave.Square, 0.05f, 0.16f, 96, 100),
            Sfx.Steal => Concat(Tone(Wave.Saw, 900f, 300f, 0.1f, 0.13f), CoinBody()),
            Sfx.Save => Notes(Wave.Triangle, 0.09f, 0.24f, 79, 84, 88),
            Sfx.Bump => Mix(Tone(Wave.Square, 220f, 180f, 0.08f, 0.16f, decay: 2f), Tone(Wave.Noise, 3000f, 900f, 0.06f, 0.15f, decay: 3f)),
            _ => Silence(0.01f),
        });

        /// <summary>Jingle do loot: quanto mais rara, mais notas (Relíquia ganha um brilho extra).</summary>
        public static float[] Loot(Rarity rarity) => Limit(rarity switch
        {
            Rarity.Common => Notes(Wave.Triangle, 0.09f, 0.22f, 79),
            Rarity.Uncommon => Notes(Wave.Triangle, 0.08f, 0.24f, 76, 79),
            Rarity.Rare => Mix(Notes(Wave.Square, 0.08f, 0.14f, 72, 76, 79), Notes(Wave.Triangle, 0.08f, 0.16f, 60, 64, 67)),
            Rarity.Epic => Mix(Notes(Wave.Square, 0.08f, 0.15f, 72, 76, 79, 84), Notes(Wave.Triangle, 0.08f, 0.18f, 60, 64, 67, 72)),
            Rarity.Legendary => Mix(Concat(Notes(Wave.Square, 0.08f, 0.15f, 72, 76, 79, 84, 88), Notes(Wave.Square, 0.35f, 0.15f, 91)),
                Concat(Notes(Wave.Triangle, 0.08f, 0.18f, 60, 64, 67, 72, 76), Notes(Wave.Triangle, 0.35f, 0.18f, 79))),
            _ => Mix(Concat(Notes(Wave.Square, 0.08f, 0.15f, 72, 76, 79, 84, 88), Notes(Wave.Square, 0.4f, 0.15f, 91)),
                Concat(Notes(Wave.Triangle, 0.08f, 0.18f, 60, 64, 67, 72, 76), Notes(Wave.Triangle, 0.4f, 0.18f, 79)),
                Delay(Notes(Wave.Sine, 0.04f, 0.12f, 96, 100, 103, 108, 103, 108, 112), 0.4f)),
        });
    }
}
