// The halls (owner, Nov 14 2026: "i want the perks, shop, and library quests to only be available when you walk into the
// building ... Make it so you open a menu by looking at something in the middle of the room"). The Watchtower's star
// table keeps the perks and stars, the Market's Starshard counter the Shop, the Library's great book the quests, Feats,
// Bestiary and Garden. Each is a Blender model (hg_city.centrepiece) the game stands in the middle of its room once that
// feature is unlocked; after an Ascension it stands in the ruin, having survived the fire. When the wizard walks up to it
// and looks at it, its card opens; he must look away or step back before it opens again. E opens it too.
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        GameObject[] centres;
        float lookT; int lookedAt = -1; bool lookArmed = true;

        // is this room's centrepiece standing (its feature unlocked)?
        public bool CentreOn(int i)
        {
            if (!Field.HasCentre(i)) return false;
            switch (HG.BUILDING_IDS[i])
            {
                case "watchtower": return P.ascensions > 0 || P.restored.Contains("watchtower");
                case "market": return P.shopOpen;
                case "library": return P.shopOpen || P.restored.Contains("library");
            }
            return false;
        }
        public static string CentreName(int i) => HG.BUILDING_IDS[i] == "watchtower" ? "star table" : HG.BUILDING_IDS[i] == "market" ? "Starshard counter" : "great book";

        void TickHalls(float dt)
        {
            if (centres == null) centres = new GameObject[HG.BUILDING_IDS.Length];
            for (int i = 0; i < centres.Length; i++)
            {
                bool on = CentreOn(i);
                if (on && !centres[i])
                {
                    var prefab = art == null ? null : art.Centre(HG.BUILDING_IDS[i]);
                    if (prefab == null) continue;   // no stand-in shapes
                    centres[i] = Instantiate(prefab, world); centres[i].name = "centre " + HG.BUILDING_IDS[i];
                    centres[i].transform.position = Field.Centre(i); centres[i].transform.rotation = Field.PlotRot(i);
                }
                else if (!on && centres[i]) { Destroy(centres[i]); centres[i] = null; }
            }
            // the look: a short look opens the card once; looking away (or stepping back) arms it again
            var wz = Wizard.I; int look = wz ? wz.Looking : -1;
            if (look != lookedAt) { lookedAt = look; lookT = 0; if (look < 0) lookArmed = true; }
            if (look < 0) return;
            lookT += dt;
            bool key = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
            if ((lookArmed && lookT > 0.3f) || key) { lookArmed = false; OpenHall(look); }
        }
        public void OpenHall(int i)
        {
            if (Hud.Paused) return;
            Wizard.I?.Stop(); Sound.Click();
            switch (HG.BUILDING_IDS[i])
            {
                case "watchtower": Hud.ShowPerks(); break;
                case "market": Hud.ShowShop("tower"); break;
                case "library": Hud.ShowQuests(); break;
            }
        }
        // the first time the player could use a hall, say once where it is
        void TellHalls()
        {
            // the gamble, said once: past the Ascension point, a fall loses the whole run (GameLoop.Fall)
            if (CanAscend && !told.Contains("lockin") && !Hud.Paused) { told.Add("lockin"); Hud.Banner("You can Ascend! It locks this run in. If the walls fall first, the run is lost"); }
            if (P.ascensions > 0 && P.perkPts > 0 && !told.Contains("hallPerks") && !(Wizard.I && Wizard.I.On))
            { told.Add("hallPerks"); Hud.Banner("Walk the town into the Watchtower: its star table keeps your perks"); }
        }
    }
}
