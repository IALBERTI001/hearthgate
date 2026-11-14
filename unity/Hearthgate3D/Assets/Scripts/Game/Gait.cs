// Walking with legs (owner, Nov 14 2026: "Improve walking animations ... we are not lazy here. Legs move you know").
// The wizard and the townsfolk are built in Blender with jointed parts (hg_front.legs and Part): a thigh turning at each
// hip, a shin at each knee, an arm at each shoulder, and the wizard's hat tip. This drives them by the natural-movement
// rules (skill natural-movement-rules):
//  - the stance leg stays straight and its foot stays planted: the stride is set by the speed, so the foot moves back
//    exactly as fast as the body moves on (up to a cadence cap, where a scurry slides a little);
//  - the swing leg's knee bends to clear the ground and the leg reaches forward to strike with the heel;
//  - the hips drop over the stance leg as the legs spread (the body is lowest when the stride is widest);
//  - each arm swings against the leg on its side; the body turns a little against the hips; loose parts (the hat tip)
//    lag behind and flop on.
// At rest the legs come together and a slow breath goes on. Models face -Z, so a leg swings forward about +X.
using UnityEngine;

namespace Hearthgate.Game
{
    public class Gait : MonoBehaviour
    {
        Transform thighL, thighR, shinL, shinR, armL, armR, hat;
        Quaternion rThL, rThR, rShL, rShR, rArL, rArR, rHat;
        public float LegLength = 0.45f;     // hip to sole in world metres (set by the owner of the model)
        public float MaxCadence = 2.6f;     // strides (two steps) a second at most
        public float ArmSwing = 0.85f, StaffArm = 0.45f;   // the right arm holds the wizard's staff, so it swings less
        public float Drop { get; private set; }   // how far the hips sink this frame (negative), for the model's height
        public float Phase => phase;
        public System.Action Step;          // a foot strikes the ground
        float phase, amp, hatVel, hatAng, lastSpeed;
        bool ok;

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                switch (t.name)
                {
                    case "thigh_L": thighL = t; break; case "thigh_R": thighR = t; break;
                    case "shin_L": shinL = t; break; case "shin_R": shinR = t; break;
                    case "arm_L": armL = t; break; case "arm_R": armR = t; break;
                    case "hat_tip": hat = t; break;
                }
            ok = thighL && thighR && shinL && shinR;
            if (thighL) rThL = thighL.localRotation; if (thighR) rThR = thighR.localRotation;
            if (shinL) rShL = shinL.localRotation; if (shinR) rShR = shinR.localRotation;
            if (armL) rArL = armL.localRotation; if (armR) rArR = armR.localRotation;
            if (hat) rHat = hat.localRotation;
        }
        public bool HasLegs => ok;

        // speed in world metres a second along the ground; returns nothing, sets Drop and fires Step
        public void Tick(float dt, float speed)
        {
            if (!ok || dt <= 0) return;
            // the stride widens with speed (relative to the legs) up to about 34 degrees each way; the cadence follows so the
            // stance foot stays put
            float wantAmp = Mathf.Clamp(0.18f + speed / (LegLength * 9f), 0f, 0.6f) * Mathf.Clamp01(speed / 0.3f);
            amp = Mathf.MoveTowards(amp, wantAmp, dt * 2.5f);
            float stride = 4f * LegLength * Mathf.Sin(Mathf.Max(amp, 0.05f));   // metres per two steps
            float cadence = Mathf.Min(MaxCadence, speed / stride);
            float was = phase;
            if (speed > 0.05f) phase += dt * cadence * Mathf.PI * 2f;
            else phase = Mathf.MoveTowards(phase, Mathf.Round(phase / Mathf.PI) * Mathf.PI, dt * 6f);   // feet come together
            // a foot strikes when its thigh is furthest forward and starts back: sin(p) peaks (L at p = pi/2, R at 3pi/2)
            if (speed > 0.05f && Mathf.Floor((was - Mathf.PI / 2) / Mathf.PI) != Mathf.Floor((phase - Mathf.PI / 2) / Mathf.PI)) Step?.Invoke();

            float pL = phase, pR = phase + Mathf.PI;
            float thL = amp * Mathf.Sin(pL), thR = amp * Mathf.Sin(pR);
            // the swing leg (its thigh moving forward, cos > 0) bends its knee; the stance leg stays straight
            float kL = amp * 1.7f * Mathf.Max(0, Mathf.Cos(pL)), kR = amp * 1.7f * Mathf.Max(0, Mathf.Cos(pR));
            thighL.localRotation = rThL * Quaternion.Euler(thL * Mathf.Rad2Deg, 0, 0);
            thighR.localRotation = rThR * Quaternion.Euler(thR * Mathf.Rad2Deg, 0, 0);
            shinL.localRotation = rShL * Quaternion.Euler(-kL * Mathf.Rad2Deg, 0, 0);
            shinR.localRotation = rShR * Quaternion.Euler(-kR * Mathf.Rad2Deg, 0, 0);
            // the hips sit on the straight stance leg: lowest when the legs are widest apart (planted feet: rule 2)
            float stance = Mathf.Cos(pL) <= 0 ? thL : thR;
            Drop = LegLength * (Mathf.Cos(stance) - 1f) - (speed > 0.05f ? 0.01f : 0f);
            // arms swing against the leg on their own side
            if (armL) armL.localRotation = rArL * Quaternion.Euler(-thL * ArmSwing * Mathf.Rad2Deg, 0, -amp * 6f);
            if (armR) armR.localRotation = rArR * Quaternion.Euler(-thR * StaffArm * Mathf.Rad2Deg, 0, amp * 6f);
            // the hat tip: a spring that lags the body's bob and the change of speed, then flops on (rule 7)
            if (hat)
            {
                float accel = (speed - lastSpeed) / dt; lastSpeed = speed;
                float target = Mathf.Clamp(speed * 5f, 0, 18f) + Mathf.Clamp(accel * 1.5f, -12f, 12f) + Mathf.Cos(phase * 2f) * amp * 10f;   // + is back
                hatVel += ((target - hatAng) * 90f - hatVel * 9f) * dt; hatAng += hatVel * dt;
                hat.localRotation = rHat * Quaternion.Euler(hatAng, 0, Mathf.Sin(phase) * amp * 8f);
            }
        }
        // the body's turn against the hips, in degrees about up, for the owner to add to the model's yaw
        public float Twist => Mathf.Sin(phase) * amp * 7f;
    }
}
