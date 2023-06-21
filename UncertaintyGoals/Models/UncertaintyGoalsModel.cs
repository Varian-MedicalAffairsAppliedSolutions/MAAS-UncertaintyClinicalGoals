using System;
using System.IO;
using System.Linq;
using System.Windows;

using Serilog;

using TP = VMS.TPS.Common.Model.API;
using TPTypes = VMS.TPS.Common.Model.Types;

namespace UncertaintyGoals.Models
{
  internal class UncertaintyGoalsModel
  {
    private readonly TP.ScriptContext context;
    private readonly TP.PlanSetup plan;
    private readonly bool debugMode;

    private WriteToFileSettings writeToFileSettings;

    private string htmlFileFullPath = "";

    public UncertaintyGoalsModel(TP.ScriptContext context, bool debugMode = false)
    {
      this.context = context;
      this.plan = context.PlanSetup;
      this.debugMode = debugMode;
    }

    public bool ValidateContext(ref string errorMsg, ref string warningMsg)
    {
      errorMsg = "";
      warningMsg = "";

      if (this.plan == null)
      {
        errorMsg += "No plan open in the context.\n";
      }
      else
      {
        if (!this.plan.IsDoseValid)
          errorMsg += "Plan has no valid Dose.\n";

        var cGoals = this.plan.GetClinicalGoals();
        if (cGoals == null || cGoals.Count() == 0)
          errorMsg += "Plan contains no Clinical Goals.\n";

        var uncertainties = this.plan.PlanUncertainties;
        if (uncertainties == null || uncertainties.Count(x => x.Dose != null) == 0)
          errorMsg += "Plan contains no calculated Uncertainty Scenarios.\n";

        try
        {
          context.Patient.BeginModifications();
        }
        catch
        {
          warningMsg += "Patient data modifications not allowed.\n";
        }
      }

      return String.IsNullOrEmpty(errorMsg);
    }

    public void Execute(WriteToFileSettings writeToFileSettings)
    {
      this.writeToFileSettings = writeToFileSettings;

      string errorMsg = "";
      string warningMsg = "";
      if (!ValidateContext(ref errorMsg, ref warningMsg))
      {
        MessageBox.Show(errorMsg + "Uncertainty Clinical Goals were not calculated.", "Calculation Failed");
        return;
      }

      if (debugMode)
      { // See Serilog documentation for details.
        string logFileFullPath = Path.Combine(writeToFileSettings.SavePath, "UncertaintyClinicalGoals_log.txt");
        Log.Logger = new LoggerConfiguration()
          .MinimumLevel.Debug()
          .WriteTo.File(logFileFullPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 1)
          .CreateLogger();
        Log.Information($"Logging started");
      }

      #region Core part

      try
      {
        var uncertaintyGoals = EvaluateUncertaintyGoals(this.plan);
        htmlFileFullPath = WriteUncertaintyGoalsToFile(uncertaintyGoals);
      }
      catch (Exception e)
      {
        Log.Error(e.Message);
        throw;
      }
      finally
      {
        Log.Information($"\n\nFINISHED\n\n\n");
        Log.CloseAndFlush();
      }

      #endregion
    }

    public string GetHtmlfileFullPath()
    {
      return htmlFileFullPath;
    }

    #region Uncertainty Goals Methods

    /// <summary>
    /// Evaluates the Clinical Goals for each Uncertainty Scenario of the plan.
    /// </summary>
    /// <param name="plan">Plan with Clinical Goals and Uncertainty Scenarios defined.</param>
    /// <returns>Container for the evaluation results. See <see cref="UncertaintyGoalListContainer"/>.</returns>
    private static UncertaintyGoalListContainer EvaluateUncertaintyGoals(TP.PlanSetup plan)
    {
      Log.Information($"Evaluating uncertainty goals.");
      var uncertaintyGoalListContainer = new UncertaintyGoalListContainer(plan);

      // Iterate the Clinical Goals.
      foreach (var cGoal in plan.GetClinicalGoals())
      {
        Log.Debug($"{cGoal.StructureId}  |>  {cGoal.ObjectiveAsString}");

        // Start a new Uncertainty Goal List for this Clinical Goal.
        var uncertaintyGoalList = new UncertaintyGoalList(cGoal.StructureId, cGoal.ObjectiveAsString, cGoal.Priority);

        // Add results for the nominal case.
        uncertaintyGoalList.uncertaintyGoals.Add(new UncertaintyGoal("Nominal", cGoal.ActualValue, cGoal.EvaluationResult));

        var tgtStructure = plan.StructureSet.Structures.FirstOrDefault(x => x.Id == cGoal.StructureId);
        if (tgtStructure == null)
          throw new ApplicationException($"Structureset '{plan.StructureSet.Id}' does not contain Clinica Goal structure '{cGoal.StructureId}'");

        // Iterate the Uncertainty Scenarios of the plan.
        foreach (var uncertainty in plan.PlanUncertainties)
        {
          if (uncertainty.Dose == null)
            continue;

          Log.Debug($"{uncertainty.DisplayName}");

          var tmpCGoal = EvaluateCGoalForUncertaintyDoses(cGoal, tgtStructure, uncertainty);
          uncertaintyGoalList.uncertaintyGoals.Add(new UncertaintyGoal(uncertainty.DisplayName, tmpCGoal.ActualValue, tmpCGoal.EvaluationResult));
        }

        uncertaintyGoalListContainer.uncertaintyGoalLists.Add(uncertaintyGoalList);
      }

      return uncertaintyGoalListContainer;
    }

    /// <summary>
    /// Evaluates a Clinical Goal for an Uncertainty Scenario.
    /// </summary>
    /// <param name="cGoal">Clinica Goal to evaluate.</param>
    /// <param name="structure">Structure matching the cGoal target structure.</param>
    /// <param name="planUncertainty">Uncertainty Scenario where the cGoal is evaluated.</param>
    /// <returns>Copy of the cGoal object, where the evaluation data is according to the Uncertainty Scenario.</returns>
    private static TPTypes.ClinicalGoal EvaluateCGoalForUncertaintyDoses(TPTypes.ClinicalGoal cGoal, TP.Structure structure, TP.PlanUncertainty planUncertainty)
    {
      bool needsInterpolation = true; // Does the value need to be interpolated from the DVH.
      bool isDoseVal = false; // Are we reading the dose or volume value from the DVH.

      #region Resolve relative / absolute units

      var dosePresentation =
        cGoal.Objective.LimitUnit == TPTypes.ObjectiveUnit.Relative
          ? TPTypes.DoseValuePresentation.Relative
          : TPTypes.DoseValuePresentation.Absolute;

      var volumePresentation =
        cGoal.Objective.LimitUnit == TPTypes.ObjectiveUnit.Relative
          ? TPTypes.VolumePresentation.Relative
          : TPTypes.VolumePresentation.AbsoluteCm3;

      if (cGoal.MeasureType == TPTypes.MeasureType.MeasureTypeDQP_DXXX)
      {
        volumePresentation = TPTypes.VolumePresentation.Relative;
      }
      else if (cGoal.MeasureType == TPTypes.MeasureType.MeasureTypeDQP_DXXXcc)
      {
        volumePresentation = TPTypes.VolumePresentation.AbsoluteCm3;
      }
      else if (cGoal.MeasureType == TPTypes.MeasureType.MeasureTypeDQP_VXXX)
      {
        dosePresentation = TPTypes.DoseValuePresentation.Relative;
        isDoseVal = true;
      }
      else if (cGoal.MeasureType == TPTypes.MeasureType.MeasureTypeDQP_VXXXGy)
      {
        dosePresentation = TPTypes.DoseValuePresentation.Absolute;
        isDoseVal = true;
      }
      else
      {
        needsInterpolation = false;
      }

      #endregion

      #region Resolve the actual value of the goal for this Uncertainty Scenario

      double actual = Double.NaN;
      TP.DVHData dvh = planUncertainty.GetDVHCumulativeData(
        structure, dosePresentation, volumePresentation, 0.01);

      if (needsInterpolation)
      {
        actual = Helpers.InterpolateDVH(dvh.CurveData, cGoal.Objective,
          isDoseVal, volumePresentation == TPTypes.VolumePresentation.AbsoluteCm3);
      }
      else
      {
        switch (cGoal.MeasureType)
        {
          case TPTypes.MeasureType.MeasureTypeDoseMax:
            actual = dvh.MaxDose.Dose;
            break;
          case TPTypes.MeasureType.MeasureTypeDoseMin:
            actual = dvh.MinDose.Dose;
            break;
          case TPTypes.MeasureType.MeasureTypeDoseMean:
            actual = dvh.MeanDose.Dose;
            break;
        }
      }

      #endregion

      // Compare the actual value against the Clinical Goal limits.
      TPTypes.GoalEvalResult evalRes = EvaluateOneGoal(cGoal, actual);

      // Build and return a new Clinical Goal object
      return new TPTypes.ClinicalGoal(
        cGoal.MeasureType, cGoal.StructureId, cGoal.Objective,
        cGoal.ObjectiveAsString, cGoal.Priority,
        cGoal.VariationAcceptable, cGoal.VariationAcceptableAsString,
        actual, String.Format("{0:F2}", actual), evalRes
      );
    }

    /// <summary>
    /// Evaluate a value against one Clinical Goal.
    /// </summary>
    /// <param name="cGoal">Clinical Goal to evaluate teh value against.</param>
    /// <param name="actual">The value to evaluate.</param>
    /// <returns>Evaluation result of the cGoal with the actual value. See <see cref="TPTypes.GoalEvalResult"/>.</returns>
    private static TPTypes.GoalEvalResult EvaluateOneGoal(TPTypes.ClinicalGoal cGoal, double actual)
    {
      double limit = cGoal.Objective.Limit;
      // If limit is Volume in cc, it has to be divided by 1000.
      if ((cGoal.MeasureType == TPTypes.MeasureType.MeasureTypeDQP_VXXXGy
           || cGoal.MeasureType == TPTypes.MeasureType.MeasureTypeDQP_VXXX)
          && cGoal.Objective.LimitUnit == TPTypes.ObjectiveUnit.Absolute)
      {
        limit /= 1000;
      }

      // Is variation acceptable
      var variation = double.IsNaN(cGoal.VariationAcceptable)
        ? 0.0
        : cGoal.VariationAcceptable;

      // Compare the limit and actual values according to the operator and acceptable variation of the Clinical Goal.
      switch (cGoal.Objective.Operator)
      {
        case TPTypes.ObjectiveOperator.GreaterThan:
          if (actual > limit)
            return TPTypes.GoalEvalResult.Passed;
          else if (!double.IsNaN(cGoal.VariationAcceptable) && actual > cGoal.VariationAcceptable)
            return TPTypes.GoalEvalResult.WithinVariationAcceptable;
          return TPTypes.GoalEvalResult.Failed;

        case TPTypes.ObjectiveOperator.GreaterThanOrEqual:
          if (actual >= limit)
            return TPTypes.GoalEvalResult.Passed;
          else if (!double.IsNaN(cGoal.VariationAcceptable) && actual >= cGoal.VariationAcceptable)
            return TPTypes.GoalEvalResult.WithinVariationAcceptable;
          return TPTypes.GoalEvalResult.Failed;

        case TPTypes.ObjectiveOperator.LessThan:
          if (actual < limit)
            return TPTypes.GoalEvalResult.Passed;
          else if (!double.IsNaN(cGoal.VariationAcceptable) && actual < cGoal.VariationAcceptable)
            return TPTypes.GoalEvalResult.WithinVariationAcceptable;
          return TPTypes.GoalEvalResult.Failed;

        case TPTypes.ObjectiveOperator.LessThanOrEqual:
          if (actual <= limit)
            return TPTypes.GoalEvalResult.Passed;
          else if (!double.IsNaN(cGoal.VariationAcceptable) && actual <= cGoal.VariationAcceptable)
            return TPTypes.GoalEvalResult.WithinVariationAcceptable;
          return TPTypes.GoalEvalResult.Failed;

        // Equal operator is not supported!
        //case ObjectiveOperator.Equals:
        //  if (Math.Abs(actual - limit) < 0.001)
        //    return GoalEvalResult.Passed;
        //  else if (!double.IsNaN(cg.VariationAcceptable) && Math.Abs(actual - limit) < (variation))
        //    return GoalEvalResult.WithinVariationAcceptable;
        //  return GoalEvalResult.Failed;

        // If none of the Objective Operator above match, return "Not Available" result.
        default:
          return TPTypes.GoalEvalResult.NA;
      }
    }

    /// <summary>
    /// Write the data from Uncertainty Goal List Container to JSON and CSV files.
    /// </summary>
    /// <param name="uncertaintyGoalListContainer"></param>
    /// <param name="plan"></param>
    /// <returns>The full path to the html file, if one was written.</returns>
    private string WriteUncertaintyGoalsToFile(UncertaintyGoalListContainer uncertaintyGoalListContainer)
    {
      Log.Information($"Writing uncertainty goals to file.");
      string fileFullPathWithoutExtension = System.IO.Path.Combine(writeToFileSettings.SavePath,
          $"{uncertaintyGoalListContainer.patientId}_planUncertaintyGoals");

      if (writeToFileSettings.WriteJson)
      {
        var filePath_json = fileFullPathWithoutExtension + ".json";
        DataWriter.WriteJsonFile<UncertaintyGoalListContainer>(uncertaintyGoalListContainer, filePath_json);
      }

      if (writeToFileSettings.WriteCsv)
      {
        var filePath_csv = fileFullPathWithoutExtension + ".csv";
        DataWriter.WriteCsvFile(uncertaintyGoalListContainer, filePath_csv);
      }

      string filePath_html = "";
      if (writeToFileSettings.WriteAndOpenHtml)
      {
        filePath_html = fileFullPathWithoutExtension + ".html";
        var htmlWriter = new HtmlWriter(filePath_html);
        htmlWriter.WritePlanInfo(uncertaintyGoalListContainer);
        htmlWriter.WriteUncertaintyGoalLists(uncertaintyGoalListContainer.uncertaintyGoalLists);
        htmlWriter.CloseHtmlAndWriteToFile();
      }

      return filePath_html;
    }

    #endregion

    #region Min/Max Robust Dose Plan Creation

    public void CreateRobustMinMaxDosePlans()
    {
      Log.Information("Creating Robust Min/Max Dose Plans.");

      string errorMsg = "";
      string warningMsg = "";
      if (!ValidateContext(ref errorMsg, ref warningMsg) || !String.IsNullOrEmpty(warningMsg))
      {
        MessageBox.Show(errorMsg + warningMsg + "Robust Min/Max doses were not created.", "Calculation Failed");
        return;
      }

      try
      {
        var nominalDoseMatrixSizeXYZ = new Tuple<int, int, int>(plan.Dose.XSize, plan.Dose.YSize, plan.Dose.ZSize);

        var minDosePlan = plan.Course.AddExternalPlanSetup(plan.StructureSet);
        var maxDosePlan = plan.Course.AddExternalPlanSetup(plan.StructureSet);

        var minDose = minDosePlan.CopyEvaluationDose(plan.Dose);
        var maxDose = maxDosePlan.CopyEvaluationDose(plan.Dose);

        Log.Information("Reading uncertainty doses");

        int minDoseVoxelsReplaced = 0;
        int maxDoseVoxelsReplaced = 0;
        foreach (var planUncertainty in plan.PlanUncertainties)
        {
          if (planUncertainty.Dose != null)
          {
            Log.Information("Analyzing dose: " + planUncertainty.DisplayName);

            // Check that the dose matrices have the same size
            if (planUncertainty.Dose.XSize != nominalDoseMatrixSizeXYZ.Item1
              || planUncertainty.Dose.YSize != nominalDoseMatrixSizeXYZ.Item2
              || planUncertainty.Dose.ZSize != nominalDoseMatrixSizeXYZ.Item3)
            {
              errorMsg = "Nominal and Uncertainty dose matrices have different sizes. Exiting\n" +
                String.Format("Nominal: {0}, {1}, {2}\n", nominalDoseMatrixSizeXYZ.Item1, nominalDoseMatrixSizeXYZ.Item2, nominalDoseMatrixSizeXYZ.Item3) +
                String.Format("Uncertainty: {0}, {1}, {2}\n", planUncertainty.Dose.XSize, planUncertainty.Dose.YSize, planUncertainty.Dose.ZSize);

              Log.Error(errorMsg);
              throw new ApplicationException(errorMsg);
            }

            // Update min and max dose voxels
            for (int planeIdx = 0; planeIdx < nominalDoseMatrixSizeXYZ.Item3; planeIdx++)
            {
              var voxels = new int[nominalDoseMatrixSizeXYZ.Item1, nominalDoseMatrixSizeXYZ.Item2];
              planUncertainty.Dose.GetVoxels(planeIdx, voxels);

              var minVoxels = new int[nominalDoseMatrixSizeXYZ.Item1, nominalDoseMatrixSizeXYZ.Item2];
              minDose.GetVoxels(planeIdx, minVoxels);

              var maxVoxels = new int[nominalDoseMatrixSizeXYZ.Item1, nominalDoseMatrixSizeXYZ.Item2];
              maxDose.GetVoxels(planeIdx, maxVoxels);

              for (int i = 0; i < nominalDoseMatrixSizeXYZ.Item1; i++)
              {
                for (int j = 0; j < nominalDoseMatrixSizeXYZ.Item2; j++)
                {
                  if (voxels[i, j] < minVoxels[i, j])
                  {
                    minVoxels[i, j] = voxels[i, j];
                    minDoseVoxelsReplaced++;
                  }

                  if (voxels[i, j] > maxVoxels[i, j])
                  {
                    maxVoxels[i, j] = voxels[i, j];
                    maxDoseVoxelsReplaced++;
                  }
                }
              }

              minDose.SetVoxels(planeIdx, minVoxels);
              maxDose.SetVoxels(planeIdx, maxVoxels);
            }
          }
        }

        Log.Information("Setting prescription and normalization of voxel dose plans.");

        minDosePlan.SetPrescription(plan.NumberOfFractions ?? 1, plan.DosePerFraction, plan.TreatmentPercentage);
        maxDosePlan.SetPrescription(plan.NumberOfFractions ?? 1, plan.DosePerFraction, plan.TreatmentPercentage);

        minDosePlan.PlanNormalizationValue = plan.PlanNormalizationValue;
        maxDosePlan.PlanNormalizationValue = plan.PlanNormalizationValue;

        string msg = "Created Min and Max voxel dose plans:\n" + "  Min: " + minDosePlan.Id + "\n  Max: " + maxDosePlan.Id;
        Log.Information(msg);
        MessageBox.Show(msg);
      }
      catch(Exception e)
      {
        Log.Error("Error: " + e.ToString());
        MessageBox.Show(e.ToString() + "\n\nRobust Min/Max doses were not created.", "Calculation Failed");
      }
    }

    #endregion

  }
}
