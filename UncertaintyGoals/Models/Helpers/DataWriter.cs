using System;
using System.IO;

using Newtonsoft.Json;

namespace UncertaintyGoals.Models
{
  static class DataWriter
  {
    public static void WriteJsonFile<T>(T obj, string fileFullPath)
    {
      var fInfo = new FileInfo(fileFullPath);
      System.IO.Directory.CreateDirectory(fInfo.DirectoryName);

      var jsonString = JsonConvert.SerializeObject(obj);
      File.WriteAllText(fileFullPath, jsonString);
    }

    public static void WriteCsvFile(UncertaintyGoalListContainer uncertaintyGoalListContainer, string fileFullPath)
    {
      var fInfo = new FileInfo(fileFullPath);
      System.IO.Directory.CreateDirectory(fInfo.DirectoryName);

      using (var stream = new StreamWriter(fileFullPath, append: false))
      {
        string headerStr = "";
        foreach (var uncertaintyGoalList in uncertaintyGoalListContainer.uncertaintyGoalLists)
        {
          // Write header row based on the first goal
          if (String.IsNullOrEmpty(headerStr))
          {
            headerStr = "Priority, Structure, Objective";
            foreach (var uncertaintyGoal in uncertaintyGoalList.uncertaintyGoals)
            {
              headerStr += ", " + uncertaintyGoal.name;
            }

            stream.WriteLine(headerStr);
          }

          // Write the results for this goal
          var resultsStr = $"{uncertaintyGoalList.priority}, " +
            $"{uncertaintyGoalList.structureId}, {uncertaintyGoalList.objective}";
          foreach (var uncertaintyGoal in uncertaintyGoalList.uncertaintyGoals)
          {
            resultsStr += $", {uncertaintyGoal.value:F2} ({uncertaintyGoal.goalResult})";
          }

          stream.WriteLine(resultsStr);
        }
      }
    }

  }
}
