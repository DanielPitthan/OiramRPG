using System;
using UnityEngine;

namespace Oiram.Core
{
    /// <summary>Opções do jogador (aba Opções do menu), guardadas em PlayerPrefs.</summary>
    public static class GameSettings
    {
        const string MusicKey = "oiram.musicVolume";
        const string SfxKey = "oiram.sfxVolume";
        const string RingKey = "oiram.timingRing";
        const string MsKey = "oiram.timingMs";

        static bool loaded;
        static float musicVolume = 0.5f;
        static float sfxVolume = 0.8f;
        static bool timingRing = true;
        static bool showMilliseconds;

        /// <summary>False nos testes/tour: as mudanças valem só para a sessão.</summary>
        public static bool Persist = true;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Persist = true;
            Changed = null;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            musicVolume = PlayerPrefs.GetFloat(MusicKey, musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxKey, sfxVolume);
            timingRing = PlayerPrefs.GetInt(RingKey, timingRing ? 1 : 0) != 0;
            showMilliseconds = PlayerPrefs.GetInt(MsKey, 0) != 0;
        }

        public static float MusicVolume
        {
            get { Load(); return musicVolume; }
            set { Load(); musicVolume = Mathf.Clamp01(value); Save(); }
        }

        public static float SfxVolume
        {
            get { Load(); return sfxVolume; }
            set { Load(); sfxVolume = Mathf.Clamp01(value); Save(); }
        }

        /// <summary>Anel que fecha no instante do impacto (ajuda a aprender o ritmo dos timed hits).</summary>
        public static bool TimingRing
        {
            get { Load(); return timingRing; }
            set { Load(); timingRing = value; Save(); }
        }

        /// <summary>Mostra o desvio em ms de cada timed hit (também alternado com F6 nas builds de desenvolvimento).</summary>
        public static bool ShowMilliseconds
        {
            get { Load(); return showMilliseconds; }
            set { Load(); showMilliseconds = value; Save(); }
        }

        static void Save()
        {
            if (Persist)
            {
                PlayerPrefs.SetFloat(MusicKey, musicVolume);
                PlayerPrefs.SetFloat(SfxKey, sfxVolume);
                PlayerPrefs.SetInt(RingKey, timingRing ? 1 : 0);
                PlayerPrefs.SetInt(MsKey, showMilliseconds ? 1 : 0);
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
        }

        /// <summary>Volume em passos de 10% para os menus (0..10).</summary>
        public static int ToSteps(float volume) => Mathf.RoundToInt(volume * 10f);
        public static float FromSteps(int steps) => Mathf.Clamp(steps, 0, 10) / 10f;
    }
}
