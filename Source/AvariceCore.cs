using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;

namespace SyrEssentials_Avarice
{
    public class AvariceCore : Mod
    {
        public static AvariceSettings settings;
        public AvariceCore(ModContentPack content) : base(content)
        {
            settings = GetSettings<AvariceSettings>();
        }
        public override string SettingsCategory() => "Avarice_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            checked
            {
                Listing_Standard listing_Standard = new Listing_Standard();
                listing_Standard.Begin(inRect);
                string startingWealthLabel = AvariceSettings.startingWealth.ToString();

                listing_Standard.CheckboxLabeled("Avarice_enableWealthModule".Translate(), ref AvariceSettings.wealthModule, "Avarice_enableWealthModuleTooltip".Translate());
                listing_Standard.CheckboxLabeled("Avarice_enableLegitimateModule".Translate(), ref AvariceSettings.legitimateModule, "Avarice_enableLegitimateModuleTooltip".Translate());
                listing_Standard.CheckboxLabeled("Avarice_enableTradeModule".Translate(), ref AvariceSettings.tradeModule, "Avarice_enableTradeModuleTooltip".Translate());
                listing_Standard.GapLine();

                listing_Standard.Label("Avarice_TraderMultiplierChance".Translate() + ": " + AvariceSettings.traderMultiplierChance.ToStringPercent(), tooltip: "Avarice_TraderMultiplierChanceTooltip".Translate());
                listing_Standard.Gap(6);
                AvariceSettings.traderMultiplierChance = listing_Standard.Slider(GenMath.RoundTo(AvariceSettings.traderMultiplierChance, 0.05f), 0f, 1f);
                listing_Standard.Gap(12);

                listing_Standard.Label("Avarice_LegitimateValue".Translate() + ": " + AvariceSettings.legitimateValue.ToStringByStyle(ToStringStyle.FloatOne), tooltip: "Avarice_LegitimateValueTooltip".Translate());
                listing_Standard.Gap(6);
                AvariceSettings.legitimateValue = listing_Standard.Slider(GenMath.RoundTo(AvariceSettings.legitimateValue, 0.1f), 0f, 5f);
                listing_Standard.Gap(12);

                listing_Standard.Label("Avarice_StartingWealth".Translate(), tooltip: "Avarice_StartingWealthTooltip".Translate());
                listing_Standard.TextFieldNumeric(ref AvariceSettings.startingWealth, ref startingWealthLabel, 0, 100000);
                listing_Standard.Gap(12);

                listing_Standard.CheckboxLabeled("Avarice_LogPointCalc".Translate(), ref AvariceSettings.logPointCalc, "Avarice_LogPointCalcTooltip".Translate());

                listing_Standard.End();
                settings.Write();
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (!AvariceSettings.legitimateModule)
            {
                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(td => !td.thingCategories.NullOrEmpty() && (td.thingCategories.Contains(ThingCategoryDefOf.Apparel) 
                || td.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel))) && td.statBases.Any(sm => sm.stat == StatDefOf.SellPriceFactor && sm.value == 0.2f)))
                {
                    thingDef.statBases.Find(sm => sm.stat == StatDefOf.SellPriceFactor && sm.value == 0.2f).value = 1f;
                }
            }
            else
            {
                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(td => !td.thingCategories.NullOrEmpty() && (td.thingCategories.Contains(ThingCategoryDefOf.Apparel)
                || td.thingCategories.Any(tc => tc.Parents.Contains(ThingCategoryDefOf.Apparel))) && td.statBases.Any(sm => sm.stat == StatDefOf.SellPriceFactor && sm.value == 1f)))
                {
                    thingDef.statBases.Find(sm => sm.stat == StatDefOf.SellPriceFactor && sm.value == 1f).value = 0.2f;
                }
            }
        }
    }
    public class AvariceSettings : ModSettings
    {
        public static bool wealthModule = true;
        public static bool legitimateModule = true;
        public static bool tradeModule = true;

        public static bool logPointCalc = false;
        public static int startingWealth = 14000;
        public static float traderMultiplierChance = 0.25f;
        public static float legitimateValue = 1.0f;

        //Not yet implemented in settings, only in code
        public static float normalisationPerDay = 0.01f;
        public static int silverThreshold = 100;
        public static bool priceFactorRounding = false;
        public static float sellFactor = 1.0f;
        public static float buyFactor = 1.0f;

        public const float minTradeValue = 0.5f;
        public const float maxTradeValue = 2.0f;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref wealthModule, "Avarice_wealthModule", true, false);
            Scribe_Values.Look<bool>(ref legitimateModule, "Avarice_legitimateModule", true, false);
            Scribe_Values.Look<bool>(ref tradeModule, "Avarice_tradeModule", true, false);

            Scribe_Values.Look<bool>(ref logPointCalc, "Avarice_logPointCalc", false, false);
            Scribe_Values.Look<int>(ref startingWealth, "Avarice_startingWealth", 14000, false);
            Scribe_Values.Look<float>(ref traderMultiplierChance, "Avarice_traderMultiplierChance", 0.25f, false);
            Scribe_Values.Look<float>(ref legitimateValue, "Avarice_legitimateValue", 1.0f, false);
        }
    }
}
