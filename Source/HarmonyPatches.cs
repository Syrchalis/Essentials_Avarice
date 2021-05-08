using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.AI;
using Verse.Sound;
using Verse.Grammar;
using System.Reflection.Emit;
using System.Xml;
using RimWorld.SketchGen;
using RimWorld.Planet;

namespace SyrEssentials_Avarice
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("Syrchalis.Rimworld.Essentials_Avarice");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    //Pristine Patches

    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class PostProcessProductPatch
    {
        [HarmonyPostfix]
        public static void PostProcessProduct_Postfix(Thing __result, Thing product, RecipeDef recipeDef, Pawn worker)
        {
            if (product.def.thingCategories.Contains(ThingCategoryDefOf.Apparel) || product.def.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel))
                    || product.def.thingCategories.Contains(ThingCategoryDefOf.Weapons) || product.def.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Weapons)))
            {
                CompPristine comp = product.TryGetComp<CompPristine>();
                if (comp != null)
                {
                    comp.pristine = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(GenLabel), "NewThingLabel", new Type[] { typeof(Thing), typeof(int), typeof(bool) })]
    public static class NewThingLabelPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NewThingLabel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo PristineString = AccessTools.Method(typeof(NewThingLabelPatch), nameof(NewThingLabelPatch.PristineString));
            MethodInfo ConcatString = AccessTools.Method(typeof(String), nameof(String.Concat), new Type[] { typeof(string), typeof(string) });
            foreach (CodeInstruction i in instructions)
            {
                if (i.opcode == OpCodes.Ldstr && (string)i.operand == ")")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, PristineString);
                    yield return new CodeInstruction(OpCodes.Call, ConcatString);
                }
                yield return i;
            }
        }
        public static string PristineString(Thing t)
        {
            CompPristine comp = t.TryGetComp<CompPristine>();
            if (comp != null && comp.pristine && Avarice_Settings.pristineModule)
            {
                return " P";
            }
            else
            {
                return "";
            }
        }
    }

    //Disables the price reduction of tainted apparel because we give apparel a sell price multiplier
    [HarmonyPatch(typeof(StatPart_WornByCorpse), nameof(StatPart_WornByCorpse.TransformValue))]
    public static class TransformValuePatch
    {
        [HarmonyPrefix]
        public static bool TransformValue_Prefix(StatRequest req, ref float val)
        {
            if (Avarice_Settings.pristineModule)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    //Wealth Patches

    [HarmonyPatch(typeof(Map), nameof(Map.PlayerWealthForStoryteller), MethodType.Getter)]
    public static class PlayerWealthForStorytellerPatch
    {
        [HarmonyPrefix]
        public static bool PlayerWealthForStoryteller_Prefix(ref float __result, Map __instance)
        {
            if (__instance.IsPlayerHome && Avarice_Settings.wealthModule)
            {
                __result = AvariceUtility.CalculateTotalAvarice(__instance);
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultThreatPointsNow))]
    public static class DefaultThreatPointsNowPatch
    {
        [HarmonyPrefix]
        public static bool DefaultThreatPointsNow_Prefix(ref float __result, IIncidentTarget target)
        {
            if (!Avarice_Settings.wealthModule)
            {
                return true;
            }
            float avarice = target.PlayerWealthForStoryteller;
            float avaricePoints = PointsPerWealthCurve.Evaluate(avarice);
            float pawnPoints = 0f;
            foreach (Pawn pawn in target.PlayerPawnsForStoryteller)
            {
                if (!pawn.IsQuestLodger())
                {
                    float pawnValue = 0f;
                    if (pawn.IsFreeColonist)
                    {
                        pawnValue = PointsPerPawnByWealthCurve.Evaluate(avarice);
                    }
                    else if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer && !pawn.Downed && pawn.training.CanAssignToTrain(TrainableDefOf.Release).Accepted)
                    {
                        pawnValue = 0.08f * pawn.kindDef.combatPower;
                        if (target is Caravan)
                        {
                            pawnValue *= 0.7f;
                        }
                    }
                    if (pawnValue > 0f)
                    {
                        if (pawn.ParentHolder != null && pawn.ParentHolder is Building_CryptosleepCasket)
                        {
                            pawnValue *= 0.1f;
                        }
                        if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                        {
                            pawnValue *= 0.5f;
                            if (pawn.skills.GetSkill(SkillDefOf.Medicine).Level < 5)
                            {
                                pawnValue *= 0.5f;
                            }
                        }
                        pawnValue *= pawn.health.summaryHealth.SummaryHealthPercent;
                        pawnPoints += pawnValue;
                    }
                }
            }
            float combinedPoints = (avaricePoints + pawnPoints) * target.IncidentPointsRandomFactorRange.RandomInRange;
            //adaptionEffectFactor is 1 for all difficulties as of 1.1
            float adaptionFactor = Mathf.Lerp(1f, Find.StoryWatcher.watcherAdaptation.TotalThreatPointsFactor, Find.Storyteller.difficulty.adaptationEffectFactor);
            __result = Mathf.Clamp(combinedPoints * adaptionFactor * Find.Storyteller.difficulty.threatScale
                                    * Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate(GenDate.DaysPassed), 35f, 10000f);
            if (Avarice_Settings.logPointCalc)
            {
                Log.Message("[Essentials: Avarice] (Points from Avarice: " + avaricePoints + " + Points from Pawns: " + pawnPoints
                            + ") * Adaption Factor: " + adaptionFactor + " * Difficulty Factor: " + Find.Storyteller.difficulty.threatScale
                            + " * Time Factor: " + Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate(GenDate.DaysPassed) + " = " + __result);
            }
            return false;
        }

        public static SimpleCurve PointsPerWealthCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(Avarice_Settings.startingWealth, 0f),
            new CurvePoint(400000f, 1400f),
            new CurvePoint(700000f, 2100f),
            new CurvePoint(1000000f, 2450f)
        };
        public static SimpleCurve PointsPerPawnByWealthCurve = new SimpleCurve
        {
            new CurvePoint(0f, 20f),
            new CurvePoint(Avarice_Settings.startingWealth, 20f),
            new CurvePoint(400000f, 240f),
            new CurvePoint(700000f, 300f),
            new CurvePoint(1000000f, 330f)
        };
    }

    //Trading Patches

    [HarmonyPatch(typeof(Dialog_Trade), MethodType.Constructor, new Type[] { typeof(Pawn), typeof(ITrader), typeof(bool) })]
    public static class Dialog_TradePatch
    {
        [HarmonyPrefix]
        public static void Dialog_Trade_Prefix(Pawn playerNegotiator, ITrader trader)
        {
            if (AvariceUtility.worldComp == null)
            {
                AvariceUtility.worldComp = Current.Game.World.GetComponent<WorldComponent_AvariceTradeData>();
            }
            TradeData tradeData = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == trader.Faction);
            if (tradeData == null)
            {
                AvariceUtility.worldComp.InitFactions();
                tradeData = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == trader.Faction);
            }
            if (AvariceUtility.worldComp.tradeDataList.Any(td => td.faction == trader.Faction && Find.TickManager.TicksGame < td.lastTradeTick + GenDate.TicksPerDay))
            {
                return;
            }
            else
            {
                Log.Message("Dialog_Trade_Prefix");
            }
            tradeData.lastTradeTick = Find.TickManager.TicksGame;
        }
    }

    [HarmonyPatch(typeof(Tradeable), "InitPriceDataIfNeeded")]
    public static class InitPriceDataIfNeededPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InitPriceDataIfNeeded_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo PriceMultiplier = AccessTools.Method(typeof(PriceTypeUtlity), nameof(PriceTypeUtlity.PriceMultiplier));
            MethodInfo ChangePriceData = AccessTools.Method(typeof(InitPriceDataIfNeededPatch), nameof(InitPriceDataIfNeededPatch.ChangePriceData));
            foreach (CodeInstruction i in instructions)
            {
                yield return i;

                if (i.opcode == OpCodes.Call && (MethodInfo)i.operand == PriceMultiplier)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, ChangePriceData);
                    yield return new CodeInstruction(OpCodes.Mul);
                }
            }
        }

        public static float ChangePriceData(Tradeable tradeable)
        {
            if (!Avarice_Settings.tradeModule)
            {
                return 1f;
            }
            
            ThingDef thingdef = tradeable.ThingDef;
            ITrader trader = TradeSession.trader;
            Map map = TradeSession.playerNegotiator.Map;
            GameComponent_AvariceTraderDict gameComp = Current.Game.GetComponent<GameComponent_AvariceTraderDict>();

            //Orbital Traders - their faction is null
            if (trader.Faction == null)
            {
                //Check if trader already exists in dictionary by reference
                if (!gameComp.traderDictionary.ContainsKey(trader))
                {
                    gameComp.traderDictionary.Add(trader, AvariceUtility.GenerateTraderMultipliers(map));
                }
                //AnimalMultiplier
                if (thingdef.race != null && thingdef.race.Animal)
                {
                    return gameComp.traderDictionary.TryGetValue(trader).animalMultiplier;
                }
                if (!thingdef.thingCategories.NullOrEmpty())
                {
                    //FoodMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.Foods) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Foods)))
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).foodMultiplier;
                    }
                    //DrugMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.Drugs) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Drugs)))
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).drugMultiplier;
                    }
                    //MedicineMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.Medicine) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Medicine))
                        || thingdef.thingCategories.Contains(ThingCategoryDefOf.BodyParts) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.BodyParts))
                        || thingdef == AvariceDefOf.Neutroamine)
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).medMultiplier;
                    }
                    //RawResourceMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.ResourcesRaw) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.ResourcesRaw))
                        || thingdef.thingCategories.Contains(AvariceDefOf.Textiles) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(AvariceDefOf.Textiles))
                        || thingdef.thingCategories.Contains(ThingCategoryDefOf.Manufactured))
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).rawMultiplier;
                    }
                    //ApparelMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.Apparel) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel)))
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).apparelMultiplier;
                    }
                    //WeaponMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.Weapons) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Weapons)))
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).weaponMultiplier;
                    }
                    //FurnitureMultiplier
                    if (thingdef.thingCategories.Contains(ThingCategoryDefOf.Buildings) || thingdef.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Buildings)))
                    {
                        return gameComp.traderDictionary.TryGetValue(trader).furnitureMultiplier;
                    }
                }
                return 1f;
            }
            else
            {
                TradeAction action = tradeable.ActionToDo;
                float currentPriceFactor = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == trader.Faction).itemDataList?.Find(id => id.thingDef == thingdef)?.priceFactor ?? 1f;
                float pricePlayerBuy = TradeUtility.GetPricePlayerBuy(tradeable.AnyThing, tradeable.PriceTypeFor(TradeAction.PlayerBuys).PriceMultiplier() * currentPriceFactor, TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true), TradeSession.trader.TradePriceImprovementOffsetForPlayer);
                float pricePlayerSell = TradeUtility.GetPricePlayerSell(tradeable.AnyThing, tradeable.PriceTypeFor(TradeAction.PlayerSells).PriceMultiplier() * currentPriceFactor, TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true), TradeSession.trader.TradePriceImprovementOffsetForPlayer);
                return (currentPriceFactor + currentPriceFactor + AvariceUtility.CalculateFactorOffset(tradeable.CountToTransfer, action == TradeAction.PlayerBuys ? pricePlayerBuy : pricePlayerSell, action)) / 2f;
            }
        }
    }

    //Colors prices based on offer
    [HarmonyPatch(typeof(TradeUI), "DrawPrice")]
    public static class DrawPricePatch
    {
        [HarmonyPrefix]
        public static bool DrawPrice_Prefix(Rect rect, Tradeable trad, TradeAction action)
        {
            if (trad.IsCurrency || !trad.TraderWillTrade)
            {
                return false;
            }
            rect = rect.Rounded();
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, new TipSignal(() => trad.GetPriceTooltip(action), trad.GetHashCode() * 297));
            }
            float playerNegotiatorFactor = TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true);
            float baseFactor = TradeSession.trader.TradePriceImprovementOffsetForPlayer;
            float normalBuyPrice = TradeUtility.GetPricePlayerBuy(trad.AnyThing, 1f, playerNegotiatorFactor, baseFactor);
            float normalSellPrice = TradeUtility.GetPricePlayerSell(trad.AnyThing, 1f, playerNegotiatorFactor, baseFactor, TradeSession.TradeCurrency);
            float price = trad.GetPriceFor(action);
            int expensive = 0;
            if (action == TradeAction.PlayerBuys)
            {
                float t = price / normalBuyPrice;
                if (price < normalBuyPrice)
                {
                    GUI.color = Color.Lerp(Color.green, Color.white, (t - Avarice_Settings.minTradeValue) * (1f / (1f - Avarice_Settings.minTradeValue)));
                    expensive = -1;
                }
                else if (price > normalBuyPrice && t != 1f)
                {
                    GUI.color = Color.Lerp(Color.white, Color.red, (Avarice_Settings.maxTradeValue - 1f) * (t - 1f));
                    expensive = 1;
                }
                else
                {
                    GUI.color = Color.white;
                }
            }
            else
            {
                float t = price / normalSellPrice;
                if (price < normalSellPrice)
                {
                    GUI.color = Color.Lerp(Color.red, Color.white, (t - Avarice_Settings.minTradeValue) * (1f / (1f - Avarice_Settings.minTradeValue)));
                    expensive = -1;
                }
                else if (price > normalSellPrice && t != 1f)
                {
                    GUI.color = Color.Lerp(Color.white, Color.green, (Avarice_Settings.maxTradeValue - 1f) * (t - 1f));
                    expensive = 1;
                }
                else
                {
                    GUI.color = Color.white;
                }
            }
            string label = (TradeSession.TradeCurrency == TradeCurrency.Silver) ? price.ToStringMoney("F2") : price.ToString();
            if (expensive > 0)
                label += " ▲";
            else if (expensive < 0)
                label += " ▼";
            Rect rect2 = new Rect(rect);
            rect2.xMax -= 5f;
            rect2.xMin += 5f;
            if (Text.Anchor == TextAnchor.MiddleLeft)
            {
                rect2.xMax += 300f;
            }
            if (Text.Anchor == TextAnchor.MiddleRight)
            {
                rect2.xMin -= 300f;
            }
            Widgets.Label(rect2, label);
            GUI.color = Color.white;
            return false;
        }
    }

    //Changing the translation string, nothing else
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceTooltip))]
    public static class GetPriceTooltipPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetPriceTooltip_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction i in instructions)
            {
                if (i.opcode == OpCodes.Ldstr && (string)i.operand == "TraderTypePrice")
                {
                    yield return new CodeInstruction(OpCodes.Ldstr, "Avarice_TraderTypePrice");
                    continue;
                }
                yield return i;
            }
        }
    }

    [HarmonyPatch]
    public static class TradeBuyPatches
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodInfo> TradeBuyMethods()
        {
            if (ModsConfig.IsActive("automatic.traderships"))
            {
                yield return AccessTools.Method(Type.GetType("TraderShips.LandedShip, TraderShips"), "GiveSoldThingToPlayer");
            }
            yield return AccessTools.Method(typeof(Pawn), nameof(Pawn.GiveSoldThingToPlayer));
            yield return AccessTools.Method(typeof(Caravan), nameof(Caravan.GiveSoldThingToPlayer));
            yield return AccessTools.Method(typeof(Settlement), nameof(Settlement.GiveSoldThingToPlayer));
            yield return AccessTools.Method(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer));
        }
        [HarmonyPrefix]
        public static void TradeBuyPrefix(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (toGive.def != ThingDefOf.Silver)
            {
                float currentPriceFactor = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == TradeSession.trader.Faction).itemDataList?.Find(id => id.thingDef == toGive.def)?.priceFactor ?? 1f;
                float normalBuyPrice = TradeUtility.GetPricePlayerBuy(toGive, TradeSession.trader.TraderKind.PriceTypeFor(toGive.def, TradeAction.PlayerBuys).PriceMultiplier() * currentPriceFactor, playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true), TradeSession.trader.TradePriceImprovementOffsetForPlayer) /* 0.714285f */;
                AvariceUtility.RegisterTrade(toGive, countToGive, normalBuyPrice, TradeSession.trader.Faction, true);
            }
        }
    }
    [HarmonyPatch]
    public static class TradeSellPatches
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodInfo> TradeSellMethods()
        {
            yield return AccessTools.Method(typeof(Pawn), nameof(Pawn.GiveSoldThingToTrader));
            yield return AccessTools.Method(typeof(Caravan), nameof(Caravan.GiveSoldThingToTrader));
            yield return AccessTools.Method(typeof(Settlement), nameof(Settlement.GiveSoldThingToTrader));
            yield return AccessTools.Method(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToTrader));
        }
        [HarmonyPrefix]
        public static void TradeSellPrefix(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (toGive.def != ThingDefOf.Silver)
            {
                float currentPriceFactor = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == TradeSession.trader.Faction).itemDataList?.Find(id => id.thingDef == toGive.def)?.priceFactor ?? 1f;
                float normalSellPrice = TradeUtility.GetPricePlayerSell(toGive, TradeSession.trader.TraderKind.PriceTypeFor(toGive.def, TradeAction.PlayerSells).PriceMultiplier() * currentPriceFactor, playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true), TradeSession.trader.TradePriceImprovementOffsetForPlayer) /* 1.66666666f */;
                AvariceUtility.RegisterTrade(toGive, countToGive, normalSellPrice, TradeSession.trader.Faction, false);
            }
        }
    }

    //Caching to reduce re-caching of InitPriceDataIfNeeded
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.CountToTransfer), MethodType.Setter)]
    public static class CountToTransferPatch
    {
        [HarmonyPostfix]
        public static void CountToTransfer_Postfix(ref int __0, ref float ___pricePlayerBuy, Tradeable __instance)
        {
            Thing thing = __instance.AnyThing;
            if (thingIDToTickCount.TryGetValue(thing.thingIDNumber, out var store) && store.First == GenTicks.TicksGame && store.Second == __0)
            {
                return;
            }
            thingIDToTickCount[thing.thingIDNumber] = new Pair<int, int>(GenTicks.TicksGame, __0);
            ___pricePlayerBuy = 0f;
        }
        public static Dictionary<int, Pair<int, int>> thingIDToTickCount = new Dictionary<int, Pair<int, int>>();
    }

    //Caching GetMarketValue to massively reduce impact of UI methods
    [HarmonyPatch]
    public static class GetPricePlayerBuyPatch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodInfo> GetMarketValueMethods()
        {
            yield return AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.GetPricePlayerBuy));
            yield return AccessTools.Method(typeof(TradeUtility), nameof(TradeUtility.GetPricePlayerSell));
        }
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MarketValue)), AccessTools.Method(typeof(GetPricePlayerBuyPatch), nameof(GetPricePlayerBuyPatch.GetMarketValue_Cached)));
        }
        public static Dictionary<int, Pair<int, float>> thingIDToTickPrice = new Dictionary<int, Pair<int, float>>();
        public static float GetMarketValue_Cached(Thing thing)
        {
            if (thingIDToTickPrice.TryGetValue(thing.thingIDNumber, out var store) && store.First == GenTicks.TicksGame)
            {
                return store.Second;
            }
            var value = thing.GetStatValue(StatDefOf.MarketValue);
            thingIDToTickPrice[thing.thingIDNumber] = new Pair<int, float>(GenTicks.TicksGame, value);
            return value;
        }
    }
}
