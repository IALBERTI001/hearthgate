// Hearthgate 3D sound (game-polish rule 15: every event has a sound and a sight; Nov 14 2026). The rules are the 2D game's
// (js/audio.js): the same effect files, each effect at most every 70 ms so a busy wave never turns to noise, pitch jitter
// 0.94 to 1.06, a calm playlist at low volume that starts only after the player's first tap (platform rule) with quiet town
// ambience between songs, and the battle theme on boss waves. Effects ship in Resources/Sfx (about 150 KB); the music is
// streamed from StreamingAssets/Music when it is first needed, so it never counts toward the first download.
// The button click, the door knock and footsteps are made in code (short shaped noise and tone), like the 2D game's knock.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Hearthgate.Game
{
    public class Sound : MonoBehaviour
    {
        public static Sound I;
        static readonly string[] SFX = { "fire_launch_0", "ice_launch_0", "lightning_launch_0", "fire_contact_0", "ice_contact_0", "lightning_contact_0",
            "shatter_combo", "freeze_combo", "conduction_combo", "tier_up", "golem_death", "ascension", "buy", "feat", "surge", "claim", "boss_horn", "level_up" };
        static readonly string[] SONGS = { "hearthfire", "lantern-walk", "starlit-wall", "rebuilding" };
        const float VOL = 0.16f, FADE = 1.2f, SFX_VOL = 0.7f;

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(), music = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> last = new Dictionary<string, float>();
        AudioSource[] pool; int next;
        AudioSource song, amb;   // two music players so songs, ambience and the battle theme cross-fade
        AudioClip click, dud, knock, step;
        float songEnd = -1; bool unlocked, boss; string lastSong = ""; float gapUntil = -1; string mode = "idle";
        public static bool MusicOn = true, SfxOn = true;

        public static void Init() { if (I) return; var go = new GameObject("Sound"); DontDestroyOnLoad(go); I = go.AddComponent<Sound>(); }

        void Awake()
        {
            MusicOn = PlayerPrefs.GetInt("hg_music", 1) == 1; SfxOn = PlayerPrefs.GetInt("hg_sfx", 1) == 1;
            pool = new AudioSource[10];
            for (int i = 0; i < pool.Length; i++) { pool[i] = gameObject.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
            song = gameObject.AddComponent<AudioSource>(); amb = gameObject.AddComponent<AudioSource>();
            song.playOnAwake = amb.playOnAwake = false; song.volume = amb.volume = 0;
            foreach (var n in SFX) { var c = Resources.Load<AudioClip>("Sfx/" + n); if (c) clips[n] = c; }
            click = Make("click", 0.045f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * 1250f) * 0.6f + Noise() * 0.4f) * Mathf.Pow(1 - k, 4));
            dud = Make("dud", 0.07f, (t, k) => Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(260f, 180f, k)) * Mathf.Pow(1 - k, 3) * 0.7f);
            knock = Make("knock", 0.09f, (t, k) => (Mathf.Sin(t * 2f * Mathf.PI * Mathf.Lerp(200f, 90f, k)) * 0.7f + Noise() * 0.35f) * Mathf.Pow(1 - k, 3));
            step = Make("step", 0.06f, (t, k) => (Noise() * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 110f) * 0.4f) * Mathf.Pow(1 - k, 5));
        }
        static System.Random rnd = new System.Random(7);
        static float Noise() => (float)(rnd.NextDouble() * 2 - 1);
        static AudioClip Make(string name, float len, System.Func<float, float, float> f)
        {
            int rate = 44100, n = Mathf.CeilToInt(rate * len); var d = new float[n];
            for (int i = 0; i < n; i++) { float k = i / (float)n; d[i] = f(i / (float)rate, k) * Mathf.Min(1f, i / 40f); }   // a 1 ms fade-in: no click at the start
            var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
        }

        // ---------- effects ----------
        public static void Play(string name, float vol = 0.4f) { if (I && I.clips.TryGetValue(name, out var c)) I.Shot(name, c, vol, true); }
        public static void Click() { if (I) I.Shot("click", I.click, 0.32f, true); }
        public static void Dud() { if (I) I.Shot("dud", I.dud, 0.3f, false); }
        public static void Knock() { if (I) I.Shot("knock", I.knock, 0.4f, true); }
        public static void Step() { if (I) I.Shot("step", I.step, 0.13f, true); }
        void Shot(string name, AudioClip c, float vol, bool jitter)
        {
            if (!SfxOn || c == null) return;
            float now = Time.unscaledTime; if (last.TryGetValue(name, out var t) && now - t < 0.07f) return; last[name] = now;
            var s = pool[next = (next + 1) % pool.Length];
            s.pitch = jitter ? 0.94f + (float)rnd.NextDouble() * 0.12f : 1f; s.PlayOneShot(c, vol * SFX_VOL);
        }

        // ---------- music ----------
        public static void Boss(bool on) { if (I) I.SetBoss(on); }
        public static void SetMusic(bool on) { MusicOn = on; PlayerPrefs.SetInt("hg_music", on ? 1 : 0); if (I) I.Apply(); }
        public static void SetSfx(bool on) { SfxOn = on; PlayerPrefs.SetInt("hg_sfx", on ? 1 : 0); }

        void Update()
        {
            if (!unlocked && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)) { unlocked = true; if (boss) StartCoroutine(Battle()); else NextSong(); }
            if (!unlocked) return;
            if (mode == "song" && songEnd > 0 && MusicOn) { songEnd -= Time.unscaledDeltaTime; if (songEnd <= 0) Gap(30 + (float)rnd.NextDouble() * 30); }
            if (mode == "gap" && gapUntil > 0 && Time.unscaledTime > gapUntil) NextSong();
        }
        void Apply()
        {
            if (!MusicOn) { song.Pause(); amb.Pause(); return; }
            if (!unlocked) return;
            var el = mode == "gap" ? amb : song; if (el.clip && !el.isPlaying) { el.UnPause(); if (!el.isPlaying) el.Play(); }
            el.volume = VOL;
        }
        void NextSong()
        {
            gapUntil = -1; songEnd = -1; if (boss) return;
            var pool = System.Array.FindAll(SONGS, n => n != lastSong);
            string n = lastSong == "" ? SONGS[0] : pool[rnd.Next(pool.Length)]; lastSong = n; mode = "song";
            StartCoroutine(PlayOn(song, n, false, FADE, amb));
        }
        void Gap(float secs) { if (boss) return; mode = "gap"; gapUntil = Time.unscaledTime + secs; StartCoroutine(PlayOn(amb, "town", true, 2f, null)); }
        void SetBoss(bool on)
        {
            if (boss == on) return; boss = on; if (!unlocked) return;
            if (on) StartCoroutine(Battle()); else { Fade(song, 0, FADE); Gap(10); }   // a breath of quiet after the fight, then the calm songs again
        }
        IEnumerator Battle() { mode = "boss"; gapUntil = -1; songEnd = -1; yield return PlayOn(song, "battle", true, 0.6f, amb); }
        IEnumerator PlayOn(AudioSource src, string n, bool loop, float fade, AudioSource other)
        {
            if (other) Fade(other, 0, FADE);
            if (!music.TryGetValue(n, out var clip))
            {
                using (var rq = UnityWebRequestMultimedia.GetAudioClip(Application.streamingAssetsPath + "/Music/" + n + ".mp3", AudioType.MPEG))
                {
                    yield return rq.SendWebRequest();
                    if (rq.result != UnityWebRequest.Result.Success) yield break;
                    clip = DownloadHandlerAudioClip.GetContent(rq); clip.name = n; music[n] = clip;
                }
            }
            if ((mode == "boss") != (n == "battle") && src == song) yield break;   // the mood changed while it loaded
            src.Stop(); src.clip = clip; src.loop = loop; src.volume = 0;
            if (MusicOn) { src.Play(); Fade(src, VOL, fade); }
            if (src == song && !loop) songEnd = clip.length;
        }
        readonly Dictionary<AudioSource, Coroutine> fades = new Dictionary<AudioSource, Coroutine>();
        void Fade(AudioSource s, float to, float secs) { if (fades.TryGetValue(s, out var c) && c != null) StopCoroutine(c); fades[s] = StartCoroutine(FadeCo(s, to, secs)); }
        IEnumerator FadeCo(AudioSource s, float to, float secs)
        {
            float from = s.volume;
            for (float t = 0; t < secs; t += Time.unscaledDeltaTime) { s.volume = Mathf.Lerp(from, to, t / secs); yield return null; }
            s.volume = to; if (to <= 0) s.Stop(); fades[s] = null;
        }
        // a hidden tab or a lost focus is silent (the 2D game paused on visibilitychange; the page runs in the background)
        void OnApplicationFocus(bool f) { AudioListener.pause = !f; }
        void OnApplicationPause(bool p) { AudioListener.pause = p; }
        public static string State => I ? I.mode + (I.song.isPlaying ? ":" + I.song.clip?.name : "") : "none";
    }
}
