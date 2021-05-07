using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SyrEssentials_Avarice
{
    [StaticConstructorOnStartup]
    public static class AvariceUtility
    {
        static AvariceUtility()
        {
            //Add pristine comp to valid items
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => !x.thingCategories.NullOrEmpty()
                    && (x.thingCategories.Contains(ThingCategoryDefOf.Apparel) || x.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel))
                    || x.thingCategories.Contains(ThingCategoryDefOf.Weapons) || x.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Weapons)))))
            {
                thingDef.comps.Add(new CompProperties(typeof(CompPristine)));
            }
            //Search for valid weapons
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => !x.thingCategories.NullOrEmpty() && (x.thingCategories.Contains(ThingCategoryDefOf.Weapons)
                    || x.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Weapons))) && !x.Verbs.Any(v => v.verbClass == typeof(Verb_ShootOneUse))))
            {
                validWeapons.Add(thingDef);
            }
            //Search for valid armor
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => !x.thingCategories.NullOrEmpty() && (x.thingCategories.Contains(AvariceDefOf.ApparelArmor)
                    || x.thingCategories.Any(tc => tc.Parents.Contains(AvariceDefOf.ApparelArmor)))))
            {
                validArmor.Add(thingDef);
            }
            //Search for valid turrets
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.designationCategory != null && x.designationCategory == DesignationCategoryDefOf.Security))
            {
                validTurrets.Add(thingDef);
            }
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(td => !td.thingCategories.NullOrEmpty() && td.thingCategories.Any(tc => tc.parent != null && tc.parent != ThingCategoryDefOf.Root)))
            {
                thingsWithParentCategories.Add(thingDef);
            }
        }

        public static HashSet<ThingDef> validWeapons = new HashSet<ThingDef>();
        public static HashSet<ThingDef> validArmor = new HashSet<ThingDef>();
        public static HashSet<ThingDef> validTurrets = new HashSet<ThingDef>();
        public static HashSet<ThingDef> thingsWithParentCategories = new HashSet<ThingDef>();
        public static float cachedValue;
        private static float lastCountTick = -99999f;
        private static List<Thing> tmpThings = new List<Thing>();
        public static readonly Material PristineMat = MaterialPool.MatFrom("Things/Avarice_Pristine", ShaderDatabase.MetaOverlay);
        public static WorldComponent_AvariceTradeData worldComp;

        public static float CalculateTotalAvarice(Map map)
        {
            float combatItemsWealth = CalculateCombatItems(map);
            return (map.wealthWatcher.WealthBuildings + map.wealthWatcher.WealthItems - combatItemsWealth) * 0.5f + map.wealthWatcher.WealthPawns 
                + combatItemsWealth * combatFactorCurve.Evaluate(map.wealthWatcher.WealthTotal);
        }

        //Checks at most every 5000 ticks - gets all items on the map and inside pawns/buildings, takes the best weapons/armors and all turrets and adds up their market value
        public static float CalculateCombatItems(Map map)
        {
            if (Find.TickManager.TicksGame - lastCountTick > 5000f)
            {
                tmpThings.Clear();
                ThingOwnerUtility.GetAllThingsRecursively<Thing>(map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), tmpThings, false, delegate (IThingHolder x)
                {
                    if (x is PassingShip || x is MapComponent || x is Building_AncientCryptosleepCasket || x is Building_CryptosleepCasket)
                    {
                        return false;
                    }
                    Pawn pawn = x as Pawn;
                    return (pawn == null || pawn.Faction == Faction.OfPlayer) && (pawn == null || !pawn.IsQuestLodger());
                }, true);
                List<Thing> weaponList = tmpThings.FindAll(t => validWeapons.Contains(t.def) && !t.PositionHeld.Fogged(map));
                List<Thing> armorList = tmpThings.FindAll(t => validArmor.Contains(t.def) && !t.PositionHeld.Fogged(map));
                List<Building> turretList = map.listerBuildings.allBuildingsColonist.FindAll(b => validTurrets.Contains(b.def));

                weaponList.SortByDescending(w => w.MarketValue);
                armorList.SortByDescending(a => a.MarketValue);

                int capablePawnsCount = map.mapPawns.FreeColonistsSpawned.FindAll(p => !p.WorkTagIsDisabled(WorkTags.Violent)
                    && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && !p.Downed && !p.Dead).Count;
                int weaponCount = Mathf.Min(capablePawnsCount, weaponList.Count);
                int armorCount = Mathf.Min(capablePawnsCount, armorList.Count);

                float num = 0f;
                for (int i = 0; i < weaponCount; i++)
                {
                    num += weaponList[i].MarketValue;
                }
                for (int i = 0; i < armorCount; i++)
                {
                    num += armorList[i].MarketValue;
                }
                foreach (Thing thing in turretList)
                {
                    num += thing.MarketValue;
                }

                lastCountTick = Find.TickManager.TicksGame;
                cachedValue = num;
                tmpThings.Clear();
                return num;
            }
            else
            {
                return cachedValue;
            }
            
        }

        public static SimpleCurve combatFactorCurve = new SimpleCurve
        {
            new CurvePoint(0f, 3f),
            new CurvePoint(400000f, 9f),
            new CurvePoint(1000000f, 12f)
        };

        public static TradeDataOrbital GenerateTraderMultipliers(Map map)
        {
            TradeDataOrbital traderMultipliers = new TradeDataOrbital
            {
                animalMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                foodMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                drugMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                medMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                rawMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                apparelMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                weaponMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                furnitureMultiplier = Rand.Chance(Avarice_Settings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f
            };
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout))
            {
                traderMultipliers.foodMultiplier = 1.5f;
            }
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.VolcanicWinter))
            {
                traderMultipliers.foodMultiplier = 1.5f;
            }
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ColdSnap))
            {
                traderMultipliers.foodMultiplier = 1.5f;
                traderMultipliers.apparelMultiplier = 1.5f;
            }
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.HeatWave))
            {
                traderMultipliers.apparelMultiplier = 1.5f;
            }
            return traderMultipliers;
        }
        public static float RandomTradeMultiplier()
        {
            float multiplier = GenMath.RoundTo(Rand.Range(Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue), 0.1f);
            if (multiplier < 1.2f && multiplier > 0.8f && Avarice_Settings.traderMultiplierChance != 1f)
            {
                multiplier = 1f;
            }
            return multiplier;
        }

        public static float IncreaseToDecrease(float increase)
        {
            return -(1 - (1 / (1 + increase)));
        }
        public static float CalculateFactor(int count, float price, TradeAction action)
        {
            float factor = Mathf.Abs(price * count) / (Avarice_Settings.silverThreshold * 100);
            if (action == TradeAction.PlayerBuys)
            {
                return factor;
            }
            else if (action == TradeAction.PlayerSells)
            {
                return IncreaseToDecrease(factor);
            }
            else
            {
                return 0f;
            }
        }

        public static void RegisterTrade(Thing thing, int count, float price, Faction faction, bool buying)
        {
            if (buying)
            {
                ChangePriceFactor(thing.def, CalculateFactor(count, price, TradeAction.PlayerBuys), faction);
            }
            else
            {
                ChangePriceFactor(thing.def, CalculateFactor(count, price, TradeAction.PlayerSells), faction);
            }
        }

        public static ItemData GetItemData(ThingDef thingDef, Faction faction)
        {
            if (worldComp == null)
            {
                worldComp = Current.Game.World.GetComponent<WorldComponent_AvariceTradeData>();
            }

            if (!(worldComp.tradeDataList.Find(td => td.faction == faction) is TradeData tradeData))
            {
                tradeData = new TradeData { faction = faction, lastTradeTick = Current.Game.tickManager.TicksGame, tile = null, itemDataList = new List<ItemData>() };
                worldComp.tradeDataList.Add(tradeData);
                ItemData itemData = new ItemData { thingDef = thingDef, priceFactor = 1f };
                tradeData.itemDataList.Add(itemData);
                return itemData;
            }
            else
            {
                if (!(tradeData.itemDataList.Find(id => id.thingDef == thingDef) is ItemData itemData))
                {
                    itemData = new ItemData { thingDef = thingDef, priceFactor = 1f };
                    tradeData.itemDataList.Add(itemData);
                    return itemData;
                }
                else
                {
                    return itemData;
                }
            }
        }

        public static void ChangePriceFactor(ThingDef thingDef, float factor, Faction faction)
        {
            ItemData itemData = GetItemData(thingDef, faction);
            //itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + GenMath.RoundRandom(factor * 100f) / 100f, Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue);
            itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + factor, Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue);
            if (!thingDef.thingCategories.NullOrEmpty())
            {
                if (thingDef.thingCategories.Find(tc => tc.parent != null && tc.parent != ThingCategoryDefOf.Root) is ThingCategoryDef categoryWithParent)
                {
                    foreach (ThingDef relatedDef in thingsWithParentCategories.Where(td => !td.thingCategories.NullOrEmpty() && td.thingCategories.Contains(categoryWithParent) && td != thingDef))
                    {
                        //Log.Message("The item " + relatedDef.label + " is in the same category (" + categoryWithParent.label + ") as " + thingDef.label);
                        itemData = GetItemData(relatedDef, faction);
                        itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + GenMath.RoundRandom(factor * 100f / 2f) / 100f, Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue);
                        foreach (Faction friendlyFaction in Find.FactionManager.AllFactions.Where(f => f.AllyOrNeutralTo(faction) && f != faction))
                        {
                            itemData = GetItemData(relatedDef, friendlyFaction);
                            itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + GenMath.RoundRandom(factor * 100f /4f) / 100f, Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue);
                        }
                    }
                }
                if (thingDef.thingCategories.Contains(ThingCategoryDefOf.Apparel))
                {
                    foreach (ThingDef relatedDef in DefDatabase<ThingDef>.AllDefs.Where(td => !td.thingCategories.NullOrEmpty() && td.thingCategories.Contains(ThingCategoryDefOf.Apparel) && td != thingDef))
                    {
                        //Log.Message("The item " + relatedDef.label + " is in the same category (" + ThingCategoryDefOf.Apparel.label + ") as " + thingDef.label);
                        itemData = GetItemData(relatedDef, faction);
                        itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + GenMath.RoundRandom(factor * 100f / 2f) / 100f, Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue);
                    }
                }
            }
            foreach (Faction friendlyFaction in Find.FactionManager.AllFactions.Where(f => f.AllyOrNeutralTo(faction) && f != faction))
            {
                itemData = GetItemData(thingDef, friendlyFaction);
                itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + GenMath.RoundRandom(factor * 100f / 2f) / 100f, Avarice_Settings.minTradeValue, Avarice_Settings.maxTradeValue);
            }
        }
    }
}
