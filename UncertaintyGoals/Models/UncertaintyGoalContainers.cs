using System;
using System.Collections.Generic;
using System.Linq;

using TP = VMS.TPS.Common.Model.API;
using TPTypes = VMS.TPS.Common.Model.Types;

namespace UncertaintyGoals.Models
{
  /// <summary>
  /// Container for all Clinical Goals and their Uncertainty Scenario results.
  /// </summary>
  public class UncertaintyGoalListContainer
  {
    public string patientId { get; set; }
    public string courseId { get; set; }
    public string planId { get; set; }

    public List<UncertaintyGoalList> uncertaintyGoalLists { get; set; }

    public UncertaintyGoalListContainer(TP.PlanSetup plan)
    {
      this.planId = plan.Id;
      this.courseId = plan.Course.Id;
      this.patientId = plan.Course.Patient.Id;

      this.uncertaintyGoalLists = new List<UncertaintyGoalList>();
    }

    /// <summary>
    /// Generic constructor for the "write to JSON" functionality.
    /// </summary>
    public UncertaintyGoalListContainer()
    {
      this.patientId = "";
      this.courseId = "";
      this.planId = "";
    }
  }

  /// <summary>
  /// Container for all Uncertainty Scenario results for a singe Clinical Goal.
  /// </summary>
  public class UncertaintyGoalList
  {
    /// <summary>
    /// ID of this Clinical Goal's target structure.
    /// </summary>
    public string structureId { get; set; }

    /// <summary>
    /// Clinical Goal's objective as a string.
    /// </summary>
    public string objective { get; set; }

    /// <summary>
    /// Clinical Goal's priority.
    /// </summary>
    public TPTypes.GoalPriority priority { get; set; }

    /// <summary>
    /// List of this Clinical Goal's evaluation results for different Uncertainty Scenarios.
    /// </summary>
    public List<UncertaintyGoal> uncertaintyGoals { get; set; }

    public UncertaintyGoalList(string structureId, string objective, TPTypes.GoalPriority priority)
    {
      this.structureId = structureId;
      this.objective = objective;
      this.priority = priority;

      this.uncertaintyGoals = new List<UncertaintyGoal>();
    }

    /// <summary>
    /// Generic constructor for the "write to JSON" functionality.
    /// </summary>
    public UncertaintyGoalList()
    {
      this.structureId = "";
      this.objective = "";

      this.uncertaintyGoals = new List<UncertaintyGoal>();
    }

    public override string ToString()
    {
      var str = this.structureId + " (" + this.objective + ") | " + this.uncertaintyGoals.Count().ToString();
      return str;
    }
  }

  /// <summary>
  /// Container for a single Clinical Goal - Uncertainty Scenario pair.
  /// Helps with writing results to a file.
  /// </summary>
  public class UncertaintyGoal
  {
    /// <summary>
    /// Name of the Uncertainty Scenario.
    /// </summary>
    public string name { get; set; }

    /// <summary>
    /// Value of the Clinical Goal for this Uncertainty Scenario
    /// </summary>
    public double value { get; set; }

    /// <summary>
    /// Evaluation result for this Clinical Goal - Uncertainty Scenario.
    /// </summary>
    public string goalResult { get; set; }

    public UncertaintyGoal(string name, double value, TPTypes.GoalEvalResult evaluationResult)
    {
      this.name = name;
      this.value = value;
      this.goalResult = evaluationResult.ToString();
    }
  }
}
