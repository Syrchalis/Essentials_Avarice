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
        public static Avarice_Settings settings;
        public AvariceCore(ModContentPack content) : base(content)
        {
            settings = GetSettings<Avarice_Settings>();
        }
        public override string SettingsCategory() => "Avarice_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            checked
            {
                Listing_Standard listing_Standard = new Listing_Standard();
                listing_Standard.Begin(inRect);
                string startingWealthLabel = Avarice_Settings.startingWealth.ToString();

                listing_Standard.CheckboxLabeled("Avarice_enableWealthModule".Translate(), ref Avarice_Settings.wealthModule, "Avarice_enableWealthModuleTooltip".Translate());
                listing_Standard.CheckboxLabeled("Avarice_enablePristineModule".Translate(), ref Avarice_Settings.pristineModule, "Avarice_enablePristineModuleTooltip".Translate());
                listing_Standard.CheckboxLabeled("Avarice_enableTradeModule".Translate(), ref Avarice_Settings.tradeModule, "Avarice_enableTradeModuleTooltip".Translate());
                listing_Standard.GapLine();

                listing_Standard.Label("Avarice_TraderMultiplierChance".Translate() + ": " + Avarice_Settings.traderMultiplierChance.ToStringPercent(), tooltip: "Avarice_TraderMultiplierChanceTooltip".Translate());
                listing_Standard.Gap(6);
                Avarice_Settings.traderMultiplierChance = listing_Standard.Slider(GenMath.RoundTo(Avarice_Settings.traderMultiplierChance, 0.05f), 0f, 1f);
                listing_Standard.Gap(12);

                listing_Standard.Label("Avarice_PristineValue".Translate() + ": " + Avarice_Settings.pristineValue.ToStringByStyle(ToStringStyle.FloatOne), tooltip: "Avarice_PristineValueTooltip".Translate());
                listing_Standard.Gap(6);
                Avarice_Settings.pristineValue = listing_Standard.Slider(GenMath.RoundTo(Avarice_Settings.pristineValue, 0.1f), 0f, 5f);
                listing_Standard.Gap(12);

                listing_Standard.Label("Avarice_StartingWealth".Translate(), tooltip: "Avarice_StartingWealthTooltip".Translate());
                listing_Standard.TextFieldNumeric(ref Avarice_Settings.startingWealth, ref startingWealthLabel, 0, 100000);
                listing_Standard.Gap(12);

                listing_Standard.CheckboxLabeled("Avarice_LogPointCalc".Translate(), ref Avarice_Settings.logPointCalc, "Avarice_LogPointCalcTooltip".Translate());

                listing_Standard.End();
                settings.Write();
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
        }
    }
    public class Avarice_Settings : ModSettings
    {
        public static bool wealthModule = true;
        public static bool pristineModule = true;
        public static bool tradeModule = true;

        public static bool logPointCalc = false;
        public static int startingWealth = 14000;
        public static float traderMultiplierChance = 0.25f;
        public static float pristineValue = 0.25f;

        public static int silverThreshold = 100;

        public const float minTradeValue = 0.5f;
        public const float maxTradeValue = 2.0f;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref wealthModule, "Avarice_logPointCalc", true, false);
            Scribe_Values.Look<bool>(ref pristineModule, "Avarice_logPointCalc", true, false);
            Scribe_Values.Look<bool>(ref tradeModule, "Avarice_logPointCalc", true, false);

            Scribe_Values.Look<bool>(ref logPointCalc, "Avarice_logPointCalc", false, false);
            Scribe_Values.Look<int>(ref startingWealth, "Avarice_startingWealth", 14000, false);
            Scribe_Values.Look<float>(ref traderMultiplierChance, "Avarice_traderMultiplierChance", 0.25f, false);
            Scribe_Values.Look<float>(ref pristineValue, "Avarice_pristineValue", 1.0f, false);
        }
    }
}
