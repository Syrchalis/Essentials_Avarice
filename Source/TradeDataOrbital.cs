using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SyrEssentials_Avarice
{
    public class TradeDataOrbital : IExposable
    {
        public float animalMultiplier = 1f;
        public float foodMultiplier = 1f;
        public float drugMultiplier = 1f;
        public float medMultiplier = 1f;
        public float rawMultiplier = 1f;
        public float apparelMultiplier = 1f;
        public float weaponMultiplier = 1f;
        public float furnitureMultiplier = 1f;

        //Simple names for these values because encapsulating dictionary is named "Avarice_traderDictionary"
        public void ExposeData()
        {
            Scribe_Values.Look<float>(ref animalMultiplier, "animalMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref foodMultiplier, "foodMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref drugMultiplier, "drugMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref medMultiplier, "medMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref rawMultiplier, "rawMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref apparelMultiplier, "apparelMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref weaponMultiplier, "weaponMultiplier", 1f, false);
            Scribe_Values.Look<float>(ref furnitureMultiplier, "furnitureMultiplier", 1f, false);
        }
    }

    public class GameComponent_AvariceTraderDict : GameComponent
    {
        public GameComponent_AvariceTraderDict(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look<ITrader, TradeDataOrbital>(ref traderDictionary, "Avarice_traderDictionary", LookMode.Reference, LookMode.Deep, ref tmpTraders, ref tmpTraderMultipliers);
        }
        public Dictionary<ITrader, TradeDataOrbital> traderDictionary = new Dictionary<ITrader, TradeDataOrbital>();
        private List<ITrader> tmpTraders;
        private List<TradeDataOrbital> tmpTraderMultipliers;
    }
}
