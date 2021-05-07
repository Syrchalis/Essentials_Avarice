using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SyrEssentials_Avarice
{
    public class StatPart_Pristine : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing)
            {
                CompPristine comp = req.Thing.TryGetComp<CompPristine>();
                if (comp != null && comp.pristine)
                {
                    val = Avarice_Settings.pristineValue;
                }
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (req.HasThing)
            {
                CompPristine comp = req.Thing.TryGetComp<CompPristine>();
                if (comp != null && comp.pristine)
                {
                    return "StatsReport_Pristine".Translate(Avarice_Settings.pristineValue.ToStringPercent());
                }
            }
            return null;
        }
    }
}
