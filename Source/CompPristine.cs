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
    public class CompPristine : ThingComp
    {
		public CompProperties_Pristine Props
		{
			get
			{
				return (CompProperties_Pristine)props;
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (pristine && Avarice_Settings.pristineModule)
			{
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(parent.TrueCenter() + new Vector3(0.25f, 0f, 0.25f), Quaternion.identity, new Vector3(0.25f, 1f, 0.25f)), AvariceUtility.PristineMat, 0);
			}
		}

		public bool pristine = false;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look<bool>(ref pristine, "pristine", false, false);
		}
	}

	public class CompProperties_Pristine : CompProperties
	{
		public CompProperties_Pristine()
		{
			compClass = typeof(CompPristine);
		}
	}
}
