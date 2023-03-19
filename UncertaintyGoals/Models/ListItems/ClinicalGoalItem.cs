using System;
using System.Collections.ObjectModel;

using VMS.TPS.Common.Model.API;

namespace UncertaintyGoals.Models
{
  public class ClinicalGoalItem
  {
    public string Priority { get; set; }
    public string StructureId { get; set; }
    public string Objective { get; set; }

    public ClinicalGoalItem(string priority, string structureId, string objective)
    {
      this.Priority = priority;
      this.StructureId = structureId;
      this.Objective = objective;
    }

    public static ObservableCollection<ClinicalGoalItem> CreateClinicalGoalItems(PlanSetup plan)
    {
      var retVal = new ObservableCollection<ClinicalGoalItem>();
      if (plan == null)
        return retVal;

      var clinicalGoals = plan.GetClinicalGoals();
      if (clinicalGoals == null)
        return retVal;

      foreach (var clinicalGoal in plan.GetClinicalGoals())
      {
        retVal.Add(new ClinicalGoalItem($"P{(int)clinicalGoal.Priority + 1}", 
          clinicalGoal.StructureId, clinicalGoal.ObjectiveAsString));
      }

      return retVal;
    }
  }
}
