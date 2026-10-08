using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oiram.Core;
using Oiram.Loot;
using UnityEngine;

namespace Oiram.Audio
{
    /// <summary>
    /// Toca efeitos e música gerados pelo <see cref="Synth"/>. Criado sob demanda (DontDestroyOnLoad) com o próprio
    /// AudioListener — as cenas não têm listener. Músicas são compostas em segundo plano na primeira vez.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int Voices = 12;
        const float RepeatGuard = 0.035f;

        static AudioManager instance;
        static readonly MusicTrack[] PrecomposeOrder =
        {
            MusicTrack.Title, MusicTrack.Field, MusicTrack.Battle, MusicTrack.WorldMap,
            MusicTrack.Town, MusicTrack.Dungeon, MusicTrack.Boss,
        };

        readonly Dictionary<Sfx, AudioClip> sfxClips = new();
        readonly Dictionary<Rarity, AudioClip> lootClips = new();
        readonly Dictionary<MusicTrack, AudioClip> musicClips = new();
        readonly Dictionary<Sfx, float> lastPlayed = new();
        readonly Dictionary<MusicTrack, Lazy<float[]>> composed = new();

        AudioSource[] voices;
        int nextVoice;

        sealed class MusicVoice
        {
            public AudioSource Source;
            public MusicTrack Track;
            public float Level;
            public float Target;
            public float Speed;
        }

        MusicVoice current, previous;
        float duck = 1f, duckUntil;

        public static MusicTrack CurrentTrack => instance != null && instance.current != null ? instance.current.Track : MusicTrack.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        static AudioManager Instance
        {
            get
            {
                if (instance != null) return instance;
                if (!Application.isPlaying) return null;
                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
                return instance;
            }
        }

        void Awake()
        {
            gameObject.AddComponent<AudioListener>();
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++) voices[i] = NewSource(false);
            current = new MusicVoice { Source = NewSource(true) };
            previous = new MusicVoice { Source = NewSource(true) };

            foreach (MusicTrack track in Enum.GetValues(typeof(MusicTrack)))
            {
                var t = track;
                composed[t] = new Lazy<float[]>(() => MusicComposer.Compose(t), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            }
#if !UNITY_WEBGL
            // Compõe tudo fora da thread principal; quem pedir antes de ficar pronto espera só pela própria faixa.
            Task.Run(() =>
            {
                foreach (var t in PrecomposeOrder) _ = composed[t].Value;
            });
#endif
        }

        AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        static AudioClip Clip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, Math.Max(1, samples.Length), 1, Synth.Rate, false);
            if (samples.Length > 0) clip.SetData(samples, 0);
            return clip;
        }

        // ---------- efeitos ----------

        /// <summary>Toca um efeito. <paramref name="volume"/> multiplica o volume de efeitos das opções.</summary>
        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f) => Instance?.PlayInternal(sfx, volume, pitch);

        public static void PlayLoot(Rarity rarity)
        {
            var self = Instance;
            if (self == null) return;
            if (!self.lootClips.TryGetValue(rarity, out var clip))
                self.lootClips[rarity] = clip = Clip("loot_" + rarity, SoundBank.Loot(rarity));
            self.PlayClip(clip, 1f, 1f);
            if (rarity >= Rarity.Legendary) self.Duck(1.6f);
        }

        void PlayInternal(Sfx sfx, float volume, float pitch)
        {
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(sfx, out float last) && now - last < RepeatGuard) return;
            lastPlayed[sfx] = now;
            if (!sfxClips.TryGetValue(sfx, out var clip)) sfxClips[sfx] = clip = Clip("sfx_" + sfx, SoundBank.Build(sfx));
            PlayClip(clip, volume, pitch);
            if (sfx is Sfx.Victory or Sfx.LevelUp or Sfx.Defeat) Duck(clip.length);
        }

        void PlayClip(AudioClip clip, float volume, float pitch)
        {
            AudioSource voice = null;
            for (int i = 0; i < voices.Length && voice == null; i++)
            {
                var candidate = voices[(nextVoice + i) % voices.Length];
                if (!candidate.isPlaying) voice = candidate;
            }
            voice ??= voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = Mathf.Clamp01(volume * GameSettings.SfxVolume);
            voice.Play();
        }

        /// <summary>Abaixa a música enquanto um jingle importante toca.</summary>
        void Duck(float seconds)
        {
            duck = Mathf.Min(duck, 0.25f);
            duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);
        }

        // ---------- música ----------

        /// <summary>Troca de música com crossfade. Pedir a faixa que já toca não reinicia.</summary>
        public static void PlayMusic(MusicTrack track, float fade = 0.8f) => Instance?.PlayMusicInternal(track, fade);

        public static void StopMusic(float fade = 0.5f) => Instance?.PlayMusicInternal(MusicTrack.None, fade);

        void PlayMusicInternal(MusicTrack track, float fade)
        {
            if (current.Track == track && (track == MusicTrack.None || current.Target > 0f)) return;
            float speed = fade <= 0f ? 1000f : 1f / fade;

            // A faixa atual vira a "anterior" e some; a nova entra no lugar.
            (previous, current) = (current, previous);
            previous.Target = 0f;
            previous.Speed = speed;

            current.Source.Stop();
            current.Source.clip = null;
            current.Track = track;
            current.Level = 0f;
            current.Target = track == MusicTrack.None ? 0f : 1f;
            current.Speed = speed;
            if (track == MusicTrack.None || StartIfReady(current)) return;
#if UNITY_WEBGL
            _ = composed[track].Value;
            StartIfReady(current);
#else
            // Ainda compondo: termina numa thread de fundo e começa a tocar quando ficar pronta (sem travar a tela).
            var lazy = composed[track];
            Task.Run(() => _ = lazy.Value);
#endif
        }

        bool StartIfReady(MusicVoice voice)
        {
            if (!musicClips.TryGetValue(voice.Track, out var clip))
            {
                var lazy = composed[voice.Track];
                if (!lazy.IsValueCreated) return false;
                musicClips[voice.Track] = clip = Clip("music_" + voice.Track, lazy.Value);
            }
            voice.Source.clip = clip;
            voice.Source.Play();
            return true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime > duckUntil) duck = Mathf.MoveTowards(duck, 1f, dt * 1.5f);
            if (current.Track != MusicTrack.None && current.Source.clip == null) StartIfReady(current);
            Apply(current, dt);
            Apply(previous, dt);
        }

        void Apply(MusicVoice voice, float dt)
        {
            if (voice.Source.clip == null) return;
            voice.Level = Mathf.MoveTowards(voice.Level, voice.Target, voice.Speed * dt);
            voice.Source.volume = voice.Level * duck * GameSettings.MusicVolume;
            if (voice.Level <= 0f && voice.Target <= 0f && voice.Source.isPlaying) voice.Source.Stop();
        }
    }
}
