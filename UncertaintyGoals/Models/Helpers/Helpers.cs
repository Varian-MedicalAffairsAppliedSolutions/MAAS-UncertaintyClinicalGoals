using System;
using System.Linq;
using VMS.TPS.Common.Model.Types;

namespace UncertaintyGoals.Models
{
  public static class Helpers
  {
    // Interpolation without extrapolation.
    // isDoseVal -> x data should be dose and output volume.
    // isVolumeAbsolute -> Volume in cc, so the objective has to be divided by 1000 (is in mm^3 by default)
    public static double InterpolateDVH(DVHPoint[] data, Objective obj, bool isDoseVal = false, bool isVolumeAbsolute = false)
    {
      var objectiveVal = obj.Value;

      var x = data.Select(d => d.Volume);
      var y = data.Select(d => d.DoseValue.Dose);
      if (isDoseVal)
      {
        var x_tmp = x;
        x = y.Reverse();
        y = x_tmp.Reverse();
      }
      else if (isVolumeAbsolute)
      {
        objectiveVal /= 1000.0;
      }

      if (x.ElementAt(0) < objectiveVal)
      {
        return Double.NaN;
      }

      for (int i = 1; i < data.Length; i++)
      {
        if (x.ElementAt(i) < objectiveVal)
        {
          return y.ElementAt(i - 1) +
                 (y.ElementAt(i) - y.ElementAt(i - 1)) * ((objectiveVal - x.ElementAt(i)) / (x.ElementAt(i) - x.ElementAt(i - 1)));
        }
      }

      return Double.NaN;
    }

  }
}

