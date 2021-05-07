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
    public class TradeData : IExposable
    {
        public Faction faction;
        public Tile tile;
        public int lastTradeTick;
        public List<ItemData> itemDataList = new List<ItemData>();

        public void ExposeData()
        {
            Scribe_References.Look<Faction>(ref faction, "faction");
            Scribe_Values.Look<Tile>(ref tile, "tile", null, false);
            Scribe_Values.Look<int>(ref lastTradeTick, "lastTradeTick", -1, false);
            Scribe_Collections.Look<ItemData>(ref itemDataList, "itemDataList", LookMode.Deep);
            if (itemDataList == null)
            {
                itemDataList = new List<ItemData>();
            }
        }
    }
    public class ItemData : IExposable
    {
        public ThingDef thingDef;
        public float priceFactor;

        public void ExposeData()
        {
            Scribe_Defs.Look<ThingDef>(ref thingDef, "thingDef");
            Scribe_Values.Look<float>(ref priceFactor, "priceFactor", 1, false);
        }
    }
    
    public class WorldComponent_AvariceTradeData : WorldComponent
    {
        public List<TradeData> tradeDataList;
        public bool factionsInit = false;
        public WorldComponent_AvariceTradeData(World world) : base(world)
        {
            tradeDataList = new List<TradeData>();
            AvariceUtility.worldComp = this;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look<TradeData>(ref tradeDataList, "tradeDataList", LookMode.Deep);
        }
        public void InitFactions()
        {
            if (factionsInit)
            {
                return;
            }
            foreach (Faction faction in Find.FactionManager.AllFactionsVisible.Where(f => f.def.CanEverBeNonHostile))
            {
                tradeDataList.Add(new TradeData { faction = faction, lastTradeTick = 0, tile = null, itemDataList = new List<ItemData>() });
            }
            factionsInit = true;
        }
    }
}
