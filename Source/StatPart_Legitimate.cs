using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SyrEssentials_Avarice
{
    public class StatPart_Legitimate : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing)
            {
                CompLegitimate comp = req.Thing.TryGetComp<CompLegitimate>();
                if (comp != null && comp.legitimate)
                {
                    val = AvariceSettings.legitimateValue;
                }
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing)
            {
                CompLegitimate comp = req.Thing.TryGetComp<CompLegitimate>();
                if (comp != null && comp.legitimate)
                {
                    return "StatsReport_Legitimate".Translate(AvariceSettings.legitimateValue.ToStringPercent());
                }
            }
            return null;
        }
    }

    public class StatPart_ScaleTradePriceImprovement : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            return;
        }

        public override string ExplanationPart(StatRequest req)
        {
            return "Reduced by half because of Essentials: Avarice";
        }
    }
}
