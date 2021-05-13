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
            Scribe_Collections.Look<ItemData>(ref itemDataList, "itemData", LookMode.Deep);
            if (itemDataList == null)
            {
                itemDataList = new List<ItemData>();
            }
        }
    }

    public class ItemData : IExposable
    {
        public ThingDef thingDef;
        public float priceFactor = 1f;

        public void ExposeData()
        {
            Scribe_Defs.Look<ThingDef>(ref thingDef, "thingDef");
            Scribe_Values.Look<float>(ref priceFactor, "priceFactor", 1f, false);
        }
    }

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

    public class LockData
    {
        public int thingID;
        public ThingDef thingDef;
        public bool buyingLocked;
        public bool sellingLocked;
    }
}
