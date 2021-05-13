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

    //------------------------------------ Legitimate Patches ------------------------------------

    //Makes crafted items legitimate if they satisfy the conditions
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class PostProcessProductPatch
    {
        [HarmonyPostfix]
        public static void PostProcessProduct_Postfix(Thing __result, Thing product, RecipeDef recipeDef, Pawn worker)
        {
            if (AvariceUtility.SatisfiesLegitimateConditions(__result.def))
            {
                CompLegitimate comp = product.TryGetComp<CompLegitimate>();
                if (comp != null)
                {
                    comp.legitimate = true;
                }
            }
        }
    }

    //Adds "L" affix to legitimate things
    [HarmonyPatch(typeof(Thing), nameof(Thing.LabelNoCount), MethodType.Getter)]
    public static class ThingLabelPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NewThingLabel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo LegitimateString = AccessTools.Method(typeof(ThingLabelPatch), nameof(ThingLabelPatch.LegitimateString));
            foreach (CodeInstruction i in instructions)
            {
                if (i.opcode == OpCodes.Ret)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, LegitimateString);
                }
                yield return i;
            }
        }
        public static string LegitimateString(string s, Thing t)
        {
            CompLegitimate comp = t.TryGetComp<CompLegitimate>();
            if (comp != null && comp.legitimate && AvariceSettings.legitimateModule)
            {
                int index = s.LastIndexOf(')');
                if (index <= 0)
                {
                    index = s.Length;
                }
                return s.Insert(index, " " + "Avarice_LegitimateChar".Translate());
            }
            else
            {
                return s;
            }
        }
    }

    //Disables the price reduction of tainted apparel because apparel is given a sell price multiplier
    [HarmonyPatch(typeof(StatPart_WornByCorpse), nameof(StatPart_WornByCorpse.TransformValue))]
    public static class TransformValuePatch
    {
        [HarmonyPrefix]
        public static bool TransformValue_Prefix(StatRequest req, ref float val)
        {
            if (AvariceSettings.legitimateModule)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    //------------------------------------ Wealth Patches ------------------------------------

    //Swaps wealth calculation if on home map
    [HarmonyPatch(typeof(Map), nameof(Map.PlayerWealthForStoryteller), MethodType.Getter)]
    public static class PlayerWealthForStorytellerPatch
    {
        [HarmonyPrefix]
        public static bool PlayerWealthForStoryteller_Prefix(ref float __result, Map __instance)
        {
            if (__instance.IsPlayerHome && AvariceSettings.wealthModule)
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

    //New wealth calculation
    [HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultThreatPointsNow))]
    public static class DefaultThreatPointsNowPatch
    {
        [HarmonyPrefix]
        public static bool DefaultThreatPointsNow_Prefix(ref float __result, IIncidentTarget target)
        {
            if (!AvariceSettings.wealthModule)
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
            float adaptionFactor = Mathf.Lerp(1f, Find.StoryWatcher.watcherAdaptation.TotalThreatPointsFactor, Find.Storyteller.difficultyValues.adaptationEffectFactor);
            __result = Mathf.Clamp(combinedPoints * adaptionFactor * Find.Storyteller.difficultyValues.threatScale
                                    * Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate(GenDate.DaysPassed), 35f, 10000f);
            if (AvariceSettings.logPointCalc)
            {
                Log.Message("[Essentials: Avarice] (Points from Avarice: " + avaricePoints + " + Points from Pawns: " + pawnPoints
                            + ") * Adaption Factor: " + adaptionFactor + " * Difficulty Factor: " + Find.Storyteller.difficultyValues.threatScale
                            + " * Time Factor: " + Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate(GenDate.DaysPassed) + " = " + __result);
            }
            return false;
        }

        public static SimpleCurve PointsPerWealthCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(AvariceSettings.startingWealth, 0f),
            new CurvePoint(400000f, 1400f),
            new CurvePoint(700000f, 2100f),
            new CurvePoint(1000000f, 2450f)
        };
        public static SimpleCurve PointsPerPawnByWealthCurve = new SimpleCurve
        {
            new CurvePoint(0f, 20f),
            new CurvePoint(AvariceSettings.startingWealth, 20f),
            new CurvePoint(400000f, 240f),
            new CurvePoint(700000f, 300f),
            new CurvePoint(1000000f, 330f)
        };
    }

    //------------------------------------ Trading Patches ------------------------------------

    //Triggers changes to prices when a trade is started
    [HarmonyPatch(typeof(Dialog_Trade), MethodType.Constructor, new Type[] { typeof(Pawn), typeof(ITrader), typeof(bool) })]
    public static class Dialog_TradePatch
    {
        [HarmonyPrefix]
        public static void Dialog_Trade_Prefix(Pawn playerNegotiator, ITrader trader)
        {
            if (trader.Faction == null)
            {
                return;
            }
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
            int ticksSinceLastTrade = tradeData.lastTradeTick;
            if (Find.TickManager.TicksGame < ticksSinceLastTrade + GenDate.TicksPerDay)
            {
                return;
            }
            else
            {
                AvariceUtility.MarketSimulation(trader, ticksSinceLastTrade);
            }
            tradeData.lastTradeTick = Find.TickManager.TicksGame;
        }
    }

    //TradePriceType multiplier is used to change prices in general - also turns off that sell price is always <= buyprice if trading module is on
    [HarmonyPatch(typeof(Tradeable), "InitPriceDataIfNeeded")]
    public static class InitPriceDataIfNeededPatch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> InitPriceDataIfNeeded_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo PriceMultiplier = AccessTools.Method(typeof(PriceTypeUtlity), nameof(PriceTypeUtlity.PriceMultiplier));
            MethodInfo ChangePriceData = AccessTools.Method(typeof(InitPriceDataIfNeededPatch), nameof(InitPriceDataIfNeededPatch.ChangePriceData));

            var insts = instructions.ToList();
            for (int i = 0; i < insts.Count; i++)
            {
                if (MatchesSequence(insts, i))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(AvariceSettings), nameof(AvariceSettings.tradeModule)));
                    yield return new CodeInstruction(OpCodes.Brtrue, insts[i + 4].operand);
                }

                yield return insts[i];

                if (insts[i].opcode == OpCodes.Call && (MethodInfo)insts[i].operand == PriceMultiplier)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, ChangePriceData);
                    yield return new CodeInstruction(OpCodes.Mul);
                }
            }
        }
        public static bool MatchesSequence(List<CodeInstruction> insts, int idx)
        {
            return idx < insts.Count - 4 &&
                   insts[idx].opcode == OpCodes.Ldarg_0 &&
                   insts[idx + 1].opcode == OpCodes.Ldfld &&
                   insts[idx + 2].opcode == OpCodes.Ldarg_0 &&
                   insts[idx + 3].opcode == OpCodes.Ldfld &&
                   insts[idx + 4].opcode == OpCodes.Blt_Un_S;
        }

        public static float ChangePriceData(Tradeable tradeable)
        {
            if (!AvariceSettings.tradeModule)
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
                    GUI.color = Color.Lerp(Color.green, Color.white, (t - AvariceSettings.minTradeValue) * (1f / (1f - AvariceSettings.minTradeValue)));
                    expensive = -1;
                }
                else if (price > normalBuyPrice && t != 1f)
                {
                    GUI.color = Color.Lerp(Color.white, Color.red, (AvariceSettings.maxTradeValue - 1f) * (t - 1f));
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
                    GUI.color = Color.Lerp(Color.red, Color.white, (t - AvariceSettings.minTradeValue) * (1f / (1f - AvariceSettings.minTradeValue)));
                    expensive = -1;
                }
                else if (price > normalSellPrice && t != 1f)
                {
                    GUI.color = Color.Lerp(Color.white, Color.green, (AvariceSettings.maxTradeValue - 1f) * (t - 1f));
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
        public static IEnumerable<CodeInstruction> GetPriceTooltip_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
        {
            FieldInfo buyFactorField = AccessTools.Field(typeof(AvariceSettings), nameof(AvariceSettings.buyFactor));
            FieldInfo sellFactorField = AccessTools.Field(typeof(AvariceSettings), nameof(AvariceSettings.sellFactor));
            Label buySkipLabel = ilGen.DefineLabel();
            Label sellSkipLabel = ilGen.DefineLabel(); 

            var insts = instructions.ToList();
            for (int i = 0; i < insts.Count; i++)
            {
                //New translation string so it can be renamed
                if (insts[i].opcode == OpCodes.Ldstr && (string)insts[i].operand == "TraderTypePrice")
                {
                    yield return new CodeInstruction(OpCodes.Ldstr, "Avarice_TraderTypePrice");
                    continue;
                }
                //Replace fixed factor string with field
                if (insts[i].opcode == OpCodes.Ldc_R4 && (float)insts[i].operand == 1.4f)
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, buyFactorField);
                    continue;
                }
                //Same for selling
                if (insts[i].opcode == OpCodes.Ldc_R4 && (float)insts[i].operand == 0.6f)
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, sellFactorField);
                    continue;
                }
                //If it's 1 the text should be skipped, so a condition is added
                if (MatchesBuySequence(insts, i))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, buyFactorField);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                    yield return new CodeInstruction(OpCodes.Beq_S, buySkipLabel);
                }
                if (MatchesSellSequence(insts, i))
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, sellFactorField);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                    yield return new CodeInstruction(OpCodes.Beq_S, sellSkipLabel);
                }
                //Lastly the labels to which the above condition skips to is added
                if (MatchesBuySkip(insts, i))
                {
                    yield return insts[i].WithLabels(buySkipLabel);
                    continue;
                }
                if (MatchesSellSkip(insts, i))
                {
                    yield return insts[i].WithLabels(sellSkipLabel);
                    continue;
                }
                yield return insts[i];
            }
        }
        public static bool MatchesBuySequence(List<CodeInstruction> insts, int idx)
        {
            return idx < insts.Count - 4 &&
                   insts[idx].opcode == OpCodes.Ldloc_0 &&
                   insts[idx + 1].opcode == OpCodes.Ldstr &&
                   insts[idx + 2].opcode == OpCodes.Ldc_R4 && insts[idx + 2].operand as float? == 1.4f &&
                   insts[idx + 3].opcode == OpCodes.Stloc_2 &&
                   insts[idx + 4].opcode == OpCodes.Ldloca_S;
        }
        public static bool MatchesSellSequence(List<CodeInstruction> insts, int idx)
        {
            return idx < insts.Count - 4 &&
                   insts[idx].opcode == OpCodes.Ldloc_0 &&
                   insts[idx + 1].opcode == OpCodes.Ldstr &&
                   insts[idx + 2].opcode == OpCodes.Ldc_R4 && insts[idx + 2].operand as float? == 0.6f &&
                   insts[idx + 3].opcode == OpCodes.Stloc_2 &&
                   insts[idx + 4].opcode == OpCodes.Ldloca_S;
        }
        public static bool MatchesBuySkip(List<CodeInstruction> insts, int idx)
        {
            return idx < insts.Count - 3 &&
                   insts[idx].opcode == OpCodes.Ldarg_0 &&
                   insts[idx + 1].opcode == OpCodes.Ldfld && insts[idx +1].operand as FieldInfo == AccessTools.Field(typeof(Tradeable), "priceFactorBuy_TraderPriceType") &&
                   insts[idx + 2].opcode == OpCodes.Ldc_R4 && insts[idx + 2].operand as float? == 1f &&
                   insts[idx + 3].opcode == OpCodes.Beq_S;
        }
        public static bool MatchesSellSkip(List<CodeInstruction> insts, int idx)
        {
            return idx < insts.Count - 3 &&
                   insts[idx].opcode == OpCodes.Ldarg_0 &&
                   insts[idx + 1].opcode == OpCodes.Ldfld && insts[idx + 1].operand as FieldInfo == AccessTools.Field(typeof(Tradeable), "priceFactorSell_TraderPriceType") &&
                   insts[idx + 2].opcode == OpCodes.Ldc_R4 && insts[idx + 2].operand as float? == 1f &&
                   insts[idx + 3].opcode == OpCodes.Beq_S;
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
            if (toGive.def != ThingDefOf.Silver && TradeSession.trader.Faction != null)
            {
                float currentPriceFactor = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == TradeSession.trader.Faction).itemDataList?.Find(id => id.thingDef == toGive.def)?.priceFactor ?? 1f;
                float normalBuyPrice = TradeUtility.GetPricePlayerBuy(toGive, TradeSession.trader.TraderKind.PriceTypeFor(toGive.def, TradeAction.PlayerBuys).PriceMultiplier() * currentPriceFactor, playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true), TradeSession.trader.TradePriceImprovementOffsetForPlayer) /* 0.714285f */;
                
                AvariceUtility.RegisterTrade(toGive, countToGive, normalBuyPrice, TradeSession.trader.Faction, true);
            }
        }
        [HarmonyPostfix]
        public static void TradeBuyPostfix(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (AvariceSettings.legitimateModule && AvariceUtility.SatisfiesLegitimateConditions(toGive.def))
            {
                CompLegitimate comp = toGive.TryGetComp<CompLegitimate>();
                if (comp != null)
                {
                    comp.legitimate = true;
                }
            }
            if (AvariceSettings.tradeModule && countToGive != 0 && toGive.def != ThingDefOf.Silver)
            {
                AvariceUtility.LockTradeAction(toGive, true);
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
            if (toGive.def != ThingDefOf.Silver && TradeSession.trader.Faction != null)
            {
                float currentPriceFactor = AvariceUtility.worldComp.tradeDataList.Find(td => td.faction == TradeSession.trader.Faction).itemDataList?.Find(id => id.thingDef == toGive.def)?.priceFactor ?? 1f;
                float normalSellPrice = TradeUtility.GetPricePlayerSell(toGive, TradeSession.trader.TraderKind.PriceTypeFor(toGive.def, TradeAction.PlayerSells).PriceMultiplier() * currentPriceFactor, playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement, true), TradeSession.trader.TradePriceImprovementOffsetForPlayer) /* 1.66666666f */;
                
                AvariceUtility.RegisterTrade(toGive, countToGive, normalSellPrice, TradeSession.trader.Faction, false);
            }
        }
        [HarmonyPostfix]
        public static void TradeSellPostfix(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (AvariceSettings.legitimateModule && AvariceUtility.SatisfiesLegitimateConditions(toGive.def))
            {
                CompLegitimate comp = toGive.TryGetComp<CompLegitimate>();
                if (comp != null)
                {
                    comp.legitimate = true;
                }
            }
            if (AvariceSettings.tradeModule && countToGive != 0 && toGive.def != ThingDefOf.Silver)
            {
                AvariceUtility.LockTradeAction(toGive, false);
            }
        }
    }

    //Caching to reduce re-caching of InitPriceDataIfNeeded
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.CountToTransfer), MethodType.Setter)]
    public static class CountToTransferPatch
    {
        [HarmonyPrefix]
        public static void CountToTransfer_Postfix(ref int value, ref float ___pricePlayerBuy, Tradeable __instance)
        {
            int thingID = __instance.AnyThing.thingIDNumber;
            if (thingIDToTickCount.TryGetValue(thingID, out var store) && store.First == GenTicks.TicksGame && store.Second == value)
            {
                return;
            }
            if (AvariceUtility.GetLockData(__instance.AnyThing) is LockData lockData)
            {
                if (lockData.buyingLocked && value > 0)
                {
                    value = 0;
                }
                if (lockData.sellingLocked && value < 0)
                {
                    value = 0;
                }
            }
            thingIDToTickCount[thingID] = new Pair<int, int>(GenTicks.TicksGame, value);
            ___pricePlayerBuy = 0f;
        }
        public static Dictionary<int, Pair<int, int>> thingIDToTickCount = new Dictionary<int, Pair<int, int>>();
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetMinimumToTransfer))]
    public static class GetMinimumToTransferPatch
    {
        [HarmonyPostfix]
        public static void GetMinimumToTransfer_Postfix(Tradeable __instance, ref int __result)
        {
            if (AvariceUtility.GetLockData(__instance.AnyThing) is LockData lockData)
            {
                if (__instance.PositiveCountDirection == TransferablePositiveCountDirection.Destination && lockData.buyingLocked)
                {
                    __result = 0;
                }
                else if (lockData.sellingLocked)
                {
                    __result = 0;
                }
            }
        }
    }
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetMaximumToTransfer))]
    public static class GetMaximumToTransferPatch
    {
        [HarmonyPostfix]
        public static void GetMaximumToTransfer_Postfix(Tradeable __instance, ref int __result)
        {
            if (AvariceUtility.GetLockData(__instance.AnyThing) is LockData lockData)
            {
                if (__instance.PositiveCountDirection == TransferablePositiveCountDirection.Destination && lockData.sellingLocked)
                {
                    __result = 0;
                }
                else if (lockData.buyingLocked)
                {
                    __result = 0;
                }
            }
        }
    }


    //Caching GetMarketValue to massively reduce impact of UI methods and replace hardcoded sell/buy penalties
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
            instructions = instructions.MethodReplacer(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MarketValue)), AccessTools.Method(typeof(GetPricePlayerBuyPatch), nameof(GetPricePlayerBuyPatch.GetMarketValue_Cached)));
            foreach (CodeInstruction i in instructions)
            {
                if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 1.4f)
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(AvariceSettings), nameof(AvariceSettings.buyFactor)));
                    continue;
                }
                if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0.6f)
                {
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(AvariceSettings), nameof(AvariceSettings.sellFactor)));
                    continue;
                }
                yield return i;
            }
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
