using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyrEssentials_Avarice
{
    public class CompLegitimate : ThingComp
    {
		public CompProperties_Legitimate Props
		{
			get
			{
				return (CompProperties_Legitimate)props;
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (legitimate && AvariceSettings.legitimateModule)
			{
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(parent.TrueCenter() + new Vector3(0.25f, 0f, 0.25f), Quaternion.identity, new Vector3(0.25f, 1f, 0.25f)), AvariceUtility.LegitimateMat, 0);
			}
		}

		public bool legitimate = false;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look<bool>(ref legitimate, "legitimate", false, false);
		}
	}

	public class CompProperties_Legitimate : CompProperties
	{
		public CompProperties_Legitimate()
		{
			compClass = typeof(CompLegitimate);
		}
	}
}
