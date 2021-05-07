using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SyrEssentials_Avarice
{
    public class HistoryAutoRecorderWorker_WealthCombat : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
			float num = 0f;
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					num += AvariceUtility.CalculateCombatItems(map);
				}
			}
			return num;
		}
    }

	public class HistoryAutoRecorderWorker_AvariceTotal : HistoryAutoRecorderWorker
	{
		public override float PullRecord()
		{
			float num = 0f;
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					num += AvariceUtility.CalculateTotalAvarice(map);
				}
			}
			return num;
		}
	}
	public class HistoryAutoRecorderWorker_AvariceItems : HistoryAutoRecorderWorker
	{
		public override float PullRecord()
		{
			float num = 0f;
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					num += map.wealthWatcher.WealthItems * 0.5f;
				}
			}
			return num;
		}
	}
	public class HistoryAutoRecorderWorker_AvariceBuildings : HistoryAutoRecorderWorker
	{
		public override float PullRecord()
		{
			float num = 0f;
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					num += map.wealthWatcher.WealthBuildings * 0.5f;
				}
			}
			return num;
		}
	}
	public class HistoryAutoRecorderWorker_AvaricePawns : HistoryAutoRecorderWorker
	{
		public override float PullRecord()
		{
			float num = 0f;
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					num += map.wealthWatcher.WealthPawns;
				}
			}
			return num;
		}
	}
	public class HistoryAutoRecorderWorker_AvariceCombat : HistoryAutoRecorderWorker
	{
		public override float PullRecord()
		{
			float num = 0f;
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					num += AvariceUtility.CalculateCombatItems(map) * AvariceUtility.combatFactorCurve.Evaluate(map.wealthWatcher.WealthTotal);
				}
			}
			return num;
		}
	}
}
