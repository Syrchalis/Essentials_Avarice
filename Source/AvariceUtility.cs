using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
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
            //Add legitimate comp to valid items
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => !x.thingCategories.NullOrEmpty()
                    && (x.thingCategories.Contains(ThingCategoryDefOf.Apparel) || x.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel))
                    || x.thingCategories.Contains(ThingCategoryDefOf.Weapons) || x.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Weapons)))))
            {
                thingDef.comps.Add(new CompProperties(typeof(CompLegitimate)));
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
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(td => td.tradeability != Tradeability.None && td.GetStatValueAbstract(StatDefOf.MarketValue, null) > 0f && (td.category == ThingCategory.Item || td.category == ThingCategory.Building || td.category == ThingCategory.Pawn) && (td.Minifiable || td.category != ThingCategory.Building)))
            {
                tradeableThings.Add(thingDef);
            }
            foreach (ThingDef thingDef in tradeableThings.Where(td => !td.thingCategories.NullOrEmpty() && td.thingCategories.Any(tc => tc.parent != null && tc.parent != ThingCategoryDefOf.Root)))
            {
                thingsWithParentCategories.Add(thingDef);
            }
            Log.Message("Tradables: " + tradeableThings.Count + " |  In sub-category: " + thingsWithParentCategories.Count);
        }

        public static HashSet<ThingDef> validWeapons = new HashSet<ThingDef>();
        public static HashSet<ThingDef> validArmor = new HashSet<ThingDef>();
        public static HashSet<ThingDef> validTurrets = new HashSet<ThingDef>();
        public static HashSet<ThingDef> thingsWithParentCategories = new HashSet<ThingDef>();
        public static HashSet<ThingDef> tradeableThings = new HashSet<ThingDef>();
        public static float cachedValue;
        private static float lastCountTick = -99999f;
        public static Dictionary<ITrader, List<LockData>> tradeLockDict = new Dictionary<ITrader, List<LockData>>();
        private static List<Thing> tmpThings = new List<Thing>();
        public static readonly Material LegitimateMat = MaterialPool.MatFrom("Things/Avarice_Legitimate", ShaderDatabase.MetaOverlay);
        public static WorldComponent_AvariceTradeData worldComp;

        public static bool SatisfiesLegitimateConditions(ThingDef thingDef)
        {
            if (thingDef.thingCategories.Contains(ThingCategoryDefOf.Apparel) || thingDef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel))
                || thingDef.thingCategories.Contains(ThingCategoryDefOf.Weapons) || thingDef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Weapons)))
            {
                return true;
            }
            return false;
        }

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
                animalMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                foodMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                drugMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                medMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                rawMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                apparelMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                weaponMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f,
                furnitureMultiplier = Rand.Chance(AvariceSettings.traderMultiplierChance) ? RandomTradeMultiplier() : 1f
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
            float multiplier = GenMath.RoundTo(Rand.Range(AvariceSettings.minTradeValue, AvariceSettings.maxTradeValue), 0.1f);
            if (multiplier < 1.2f && multiplier > 0.8f && AvariceSettings.traderMultiplierChance != 1f)
            {
                multiplier = 1f;
            }
            return multiplier;
        }

        public static float IncreaseToDecrease(float increase)
        {
            return -(1 - (1 / (1 + increase)));
        }
        public static float CalculateFactorOffset(int count, float price, TradeAction action)
        {
            float factor = Mathf.Abs(price * count) / (AvariceSettings.silverThreshold * 100);
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
        public static void ChangePriceFactorDirect(ThingDef thingDef, Faction faction, float factorOffset)
        {
            float priceFactor = worldComp.tradeDataList?.Find(td => td.faction == faction)?.itemDataList?.Find(id => id.thingDef == thingDef)?.priceFactor ?? 1f;
            if (AvariceSettings.priceFactorRounding)
            {
                priceFactor = Mathf.Clamp(GenMath.RoundTo(priceFactor + GenMath.RoundRandom(factorOffset * 100f) / 100f, 0.01f), AvariceSettings.minTradeValue, AvariceSettings.maxTradeValue);
                if (priceFactor != 1f)
                {
                    ItemData itemData = GetItemData(thingDef, faction);
                    itemData.priceFactor = priceFactor;
                }
            }
            else
            {
                priceFactor = Mathf.Clamp(GenMath.RoundTo(priceFactor + factorOffset, 0.001f), AvariceSettings.minTradeValue, AvariceSettings.maxTradeValue);
                if (priceFactor != 1f)
                {
                    ItemData itemData = GetItemData(thingDef, faction);
                    itemData.priceFactor = priceFactor;
                }
            }
        }

        public static void RegisterTrade(Thing thing, int count, float price, Faction faction, bool buying)
        {
            if (buying)
            {
                ChangePriceFactor(thing.def, faction, CalculateFactorOffset(count, price, TradeAction.PlayerBuys));
            }
            else
            {
                ChangePriceFactor(thing.def, faction, CalculateFactorOffset(count, price, TradeAction.PlayerSells));
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
                tradeData = new TradeData { faction = faction, lastTradeTick = Current.Game.tickManager.TicksGame - 60000, tile = null, itemDataList = new List<ItemData>() };
                worldComp.tradeDataList.Add(tradeData);
                ItemData itemData = new ItemData { thingDef = thingDef, priceFactor = 1f };
                tradeData.itemDataList.Add(itemData);
                Log.Warning("Tried getting ItemData for Essentials: Avarice and couldn't find faction: " + faction);
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

        public static void ChangePriceFactor(ThingDef thingDef, Faction faction, float factorOffset)
        {
            //Change the faction's item price for the full offset
            ChangePriceFactorDirect(thingDef, faction, factorOffset);

            //Change the items in the same sub-category for half the offset and change these items for friendly factions by a quarter of the offset
            if (!thingDef.thingCategories.NullOrEmpty())
            {
                if (thingDef.thingCategories.Find(tc => tc.parent != null && tc.parent != ThingCategoryDefOf.Root) is ThingCategoryDef categoryWithParent)
                {
                    foreach (ThingDef influencedThingDef in thingsWithParentCategories.Where(td => !td.thingCategories.NullOrEmpty() && td.thingCategories.Contains(categoryWithParent) && td != thingDef))
                    {
                        ChangePriceFactorDirect(influencedThingDef, faction, factorOffset / 2f);
                        foreach (Faction influencedFaction in Find.FactionManager.AllFactionsVisible.Where(f => f.AllyOrNeutralTo(faction) && f != faction && f != Faction.OfPlayer))
                        {
                            ChangePriceFactorDirect(influencedThingDef, influencedFaction, factorOffset / 4f);
                        }
                    }
                }
                //Special case for apparel, because we want to influence the other apparel in the main category
                if (thingDef.thingCategories.Contains(ThingCategoryDefOf.Apparel))
                {
                    foreach (ThingDef influencedThingDef in DefDatabase<ThingDef>.AllDefs.Where(td => !td.thingCategories.NullOrEmpty() && td.thingCategories.Contains(ThingCategoryDefOf.Apparel) && td != thingDef))
                    {
                        ChangePriceFactorDirect(influencedThingDef, faction, factorOffset / 2f);
                    }
                }
            }
            //Change the item for friendly factions for half the offset
            foreach (Faction influencedFaction in Find.FactionManager.AllFactionsVisible.Where(f => f.AllyOrNeutralTo(faction) && f != faction && f != Faction.OfPlayer))
            {
                ChangePriceFactorDirect(thingDef, influencedFaction, factorOffset / 2f);
            }
        }

        public static void MarketSimulation(ITrader trader, int ticksPassed)
        {
            Faction faction = trader.Faction;
            TradeData tradeData = worldComp.tradeDataList.Find(td => td.faction == trader.Faction);
            float daysPassed = Mathf.Abs((float)ticksPassed / GenDate.TicksPerDay);
            int itemChanges = GenMath.RoundRandom(Rand.Range(daysPassed, daysPassed * 3));
            int categoryChanges = GenMath.RoundRandom(Rand.Range(daysPassed / 15, daysPassed / 5));

            foreach (ThingDef thingDef in tradeableThings)
            {
                if (tradeData.itemDataList?.Find(id => id.thingDef == thingDef) is ItemData itemData && itemData.priceFactor != 1f)
                {
                    if (itemData.priceFactor > 1f)
                    {
                        itemData.priceFactor = Mathf.Clamp(itemData.priceFactor - (AvariceSettings.normalisationPerDay * daysPassed * itemData.priceFactor), 1f, itemData.priceFactor);
                    }
                    if (itemData.priceFactor < 1f)
                    {
                        itemData.priceFactor = Mathf.Clamp(itemData.priceFactor + (AvariceSettings.normalisationPerDay * daysPassed / itemData.priceFactor), itemData.priceFactor, 1f);
                    }
                }
            }
            
            List<ThingDef> changedDefs = trader.Goods.Select(t => t.def).ToList();
            for (int i = 0; i < itemChanges; i++)
            {
                ThingDef currentDef = changedDefs.RandomElement();
                //ChangePriceFactor(currentDef, faction, Rand.Value - 0.5f);
                ChangePriceFactorDirect(currentDef, faction, Rand.Value - 0.5f);
                changedDefs.Remove(currentDef);
            }
        }

        public static void LockTradeAction(Thing thing, bool buying)
        {
            if (tradeLockDict.TryGetValue(TradeSession.trader, out var lockData))
            {
                if (thing.def.stackLimit == 1 && !lockData.Any(ld => ld.thingID == thing.thingIDNumber) || thing.def.stackLimit != 1 && !lockData.Any(ld => ld.thingDef == thing.def))
                {
                    lockData.Add(new LockData { thingDef = thing.def, thingID = thing.thingIDNumber, buyingLocked = !buying, sellingLocked = buying });
                }
            }
            else
            {
                tradeLockDict.Add(TradeSession.trader, new List<LockData> { new LockData { thingDef = thing.def, thingID = thing.thingIDNumber, buyingLocked = !buying, sellingLocked = buying } });
            }
        }

        public static LockData GetLockData(Thing thing)
        {
            if (tradeLockDict.TryGetValue(TradeSession.trader) == null)
            {
                return null;
            }
            if (thing.def.stackLimit == 1)
            {
                return tradeLockDict.TryGetValue(TradeSession.trader).Find(ld => ld.thingID == thing.thingIDNumber);
            }
            else
            {
                return tradeLockDict.TryGetValue(TradeSession.trader).Find(ld => ld.thingDef == thing.def);
            }
        }

        [DebugAction("Spawning", "Try place near stack of 500...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TryPlaceNearStacksOf75()
        {
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(DebugThingPlaceHelper.TryPlaceOptionsForStackCount(500, false)));
        }
    }
}
