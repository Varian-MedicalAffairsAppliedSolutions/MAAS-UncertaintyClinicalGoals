using System;
using System.Collections.ObjectModel;

using VMS.TPS.Common.Model.API;

namespace UncertaintyGoals.Models
{
  public class UncertaintyScenarioItem
  {
    public string Name { get; set; }

    public UncertaintyScenarioItem(string name)
    {
      this.Name = name;
    }

    public static ObservableCollection<UncertaintyScenarioItem> CreateUncertaintyScenarioItems(PlanSetup plan)
    {
      var retVal = new ObservableCollection<UncertaintyScenarioItem>();
      if (plan == null)
        return retVal;

      foreach (var uncertainty in plan.PlanUncertainties)
      {
        if(uncertainty.Dose != null)
          retVal.Add(new UncertaintyScenarioItem(uncertainty.DisplayName));
      }

      return retVal;
    }
  }
}
