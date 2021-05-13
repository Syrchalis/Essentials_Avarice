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
    public class WorldComponent_AvariceTradeData : WorldComponent
    {
        public List<TradeData> tradeDataList;
        public WorldComponent_AvariceTradeData(World world) : base(world)
        {
            tradeDataList = new List<TradeData>();
            AvariceUtility.worldComp = this;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look<TradeData>(ref tradeDataList, "tradeData", LookMode.Deep);
        }
        public void InitFactions()
        {
            foreach (Faction faction in Find.FactionManager.AllFactionsVisible.Where(f => f.def.CanEverBeNonHostile && f != Faction.OfPlayer))
            {
                if (!tradeDataList.Any(td => td.faction == faction))
                {
                    tradeDataList.Add(new TradeData { faction = faction, lastTradeTick = -60000, tile = null, itemDataList = new List<ItemData>() });
                }
            }
        }
    }
}
