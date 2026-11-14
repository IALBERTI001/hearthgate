// Game feel (game-polish rules 15-18): small particle bursts in the element's colour, numbers that pop and drift up, a
// short camera shake only for big moments, a ring of light on combos. Particle counts are capped.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hearthgate.Game
{
    public class Fx : MonoBehaviour
    {
        public static Fx I;
        ParticleSystem sparks;
        RectTransform popLayer; Font font;
        readonly List<(Text txt, Vector3 at, float t, float life)> pops = new List<(Text, Vector3, float, float)>();
        float shake; public Vector3 camHome; Camera cam;   // the camera's resting place (the town walk moves it)

        public void Init(RectTransform layer, Font f)
        {
            I = this; popLayer = layer; font = f; cam = Camera.main; camHome = cam.transform.position;
            var go = new GameObject("Sparks"); go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            var main = sparks.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 400;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f); main.gravityModifier = 1.2f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = sparks.emission; em.rateOverTime = 0;
            var sh = sparks.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            var col = sparks.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Art.Set != null && Art.Set.particle != null ? new Material(Art.Set.particle) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        }

        public void Burst(Vector3 at, Color c, int n)
        {
            var ep = new ParticleSystem.EmitParams { position = at, startColor = c, applyShapeToPosition = true };
            sparks.Emit(ep, Mathf.Min(n, 30));
        }

        float bigUntil;
        public void Pop(Vector3 at, string text, Color c, float size = 30, float life = 0.9f)
        {
            if (pops.Count > 40) return;
            // one voice (game-polish rule 13): a big word (a combo) waits for the last one to clear, so two never print
            // over each other (Nov 13 2026: "FIRESTORM!!" and "SHATTER!!" stacked into one unreadable smear)
            if (size >= 36) { if (Time.time < bigUntil) return; bigUntil = Time.time + life + 0.15f; }   // not until the last has gone
            var go = new GameObject("pop", typeof(RectTransform)); go.transform.SetParent(popLayer, false);
            var t = go.AddComponent<Text>(); t.font = font; t.text = text; t.color = c; t.fontSize = (int)size; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.raycastTarget = false;
            var o = go.AddComponent<Outline>(); o.effectColor = new Color(0.1f, 0.05f, 0.08f, 0.9f); o.effectDistance = new Vector2(2, -2);
            if (size >= 36)   // a combo word: cream letters outlined in its element's colour, a plum drop shadow (review 2)
            {
                t.color = new Color(1f, 0.957f, 0.863f); o.effectColor = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 1f); o.effectDistance = new Vector2(3, -3);
                go.AddComponent<Shadow>().effectColor = new Color(0.16f, 0.08f, 0.19f, 0.9f);
            }
            pops.Add((t, at, 0, life));
        }

        // a shake is the fight's (the walls' view) or the town's own (a rebuild, a milestone). While the wizard walks the
        // town the camera is about 8 m from him instead of 55 m from the walls, so the same shake looked seven times bigger
        // (owner, Nov 14 2026: "screen shakes too much in town"): the fight's shakes stay at the walls, the town's are small
        float amp = 0.12f;
        public void Shake(float s, bool town = false)
        {
            bool walking = Wizard.I && Wizard.I.On;
            if (walking && !town) return;
            shake = Mathf.Max(shake, s); amp = walking ? 0.025f : 0.12f;
        }

        void LateUpdate()
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (txt, at, t, life) = pops[i]; t += Time.deltaTime;
                if (t >= life) { Destroy(txt.gameObject); pops.RemoveAt(i); continue; }
                pops[i] = (txt, at, t, life);
                var sp = cam.WorldToScreenPoint(at + Vector3.up * t * 1.2f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(popLayer, sp, null, out var lp);
                float half = txt.preferredWidth * 0.55f + 8f, edge = popLayer.rect.width * 0.5f;   // a word near the edge slides in, never cut off
                lp.x = Mathf.Clamp(lp.x, -edge + half, Mathf.Max(-edge + half, edge - half));
                txt.rectTransform.anchoredPosition = lp;
                float s = t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1, (t - 0.12f) / 0.1f));
                txt.rectTransform.localScale = Vector3.one * s;
                var c = txt.color; c.a = Mathf.Clamp01((life - t) / 0.3f); txt.color = c;
            }
            if (shake > 0) { shake -= Time.deltaTime; cam.transform.position = camHome + Random.insideUnitSphere * amp * Mathf.Clamp01(shake / 0.2f); }
            else cam.transform.position = camHome;
        }
    }
}
