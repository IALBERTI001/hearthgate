// The one object in the scene. It sets the camera, then makes the HUD and the game, in that order (the game reports to
// the HUD from its first frame). The art set is assigned by the editor setup (HearthgateSetup.BuildScene).
using UnityEngine;

namespace Hearthgate.Game
{
    public class Boot : MonoBehaviour
    {
        public ArtSet art;
        Camera cam;
        void Update() { if (cam) cam.fieldOfView = Field.FovFor(cam.aspect); }   // the window can change shape

        void Awake()
        {
            Art.Set = art;
            var cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>(); cam.tag = "MainCamera"; }
            // a three-quarter view from above the city, down the road: the gate, the towers and the whole road in view,
            // monsters seen from the moment they appear (owner's rule, Nov 13 2026: "one steady art and working area")
            cam.fieldOfView = Field.FovFor(cam.aspect); cam.nearClipPlane = 0.5f; cam.farClipPlane = 200f;
            cam.transform.position = Field.CAM_POS;
            cam.transform.LookAt(Field.CAM_LOOK);
            this.cam = cam;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
            new GameObject("HUD").AddComponent<Hud>().Init();
            new GameObject("Game").AddComponent<GameLoop>();
        }
    }
}
