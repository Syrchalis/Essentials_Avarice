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
