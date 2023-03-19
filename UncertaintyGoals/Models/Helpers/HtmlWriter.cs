using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UncertaintyGoals.Models
{
  class HtmlWriter
  {
    StringBuilder StringBuilder;
    string FileFullPath;

    int NumberOfOpenTableBodies = 0;
    int NumberOfOpenTableHeaders = 0;
    int NumberOfOpenTables = 0;
    int NumberOfOpenRows = 0;

    public HtmlWriter(string fileFullPath)
    {
      this.StringBuilder = new StringBuilder();
      this.FileFullPath = fileFullPath;
      WriteHtmlHead();
    }

    public void CloseHtmlAndWriteToFile()
    {
      WriteHtmlTail();

      var fInfo = new FileInfo(FileFullPath);
      System.IO.Directory.CreateDirectory(fInfo.DirectoryName);
      using (var stream = new StreamWriter(FileFullPath, append: false))
      {
        stream.Write(this.StringBuilder.ToString());
      }
    }

    private void WriteHtmlHead()
    {
      string header =
@"<!DOCTYPE html>
<html>
<body>

<head>
  <style>
    table{border-spacing:10px; border-collapse:collapse;}
    tr {margin: 5pt;}
    td{padding:10px; text-align:center;}
    .tr-last {border-bottom: 1pt solid black;}
    .headerEntry {background-color:#00A9E0; color:white; padding:10px;}
    .passed {color:green;}
    .failed {color:red;}
    .withinvariationacceptable {color:orange;}
  </style>
</head>
";
      this.StringBuilder.AppendLine(header);
    }

    private void WriteHtmlTail()
    {
      string tail =
@"</body>
</html>
";
      this.StringBuilder.Append(tail);
    }

    public void WritePlanInfo(UncertaintyGoalListContainer uncertaintyGoalListContainer)
    {
      string formattedInfo =
@"<h1>Clinical Goals for Plan Uncertainty Doses</h1>
<h2>Patient: {0}</h2>
<h2>Plan (Course):   {1} ({2})</h2>
<h2>{3} {4}</h2>

";
      this.StringBuilder.AppendFormat(formattedInfo,
        uncertaintyGoalListContainer.patientId,
        uncertaintyGoalListContainer.planId,
        uncertaintyGoalListContainer.courseId,
        System.DateTime.Now.ToLongDateString(), System.DateTime.Now.ToLongTimeString());
    }

    public void WriteUncertaintyGoalLists(List<UncertaintyGoalList> uncertaintyGoalLists)
    {
      #region Write header

      OpenTableAndHeader();

      WriteTableHeaderItem("Priority");
      WriteTableHeaderItem("Structure");
      WriteTableHeaderItem("Objective");
      foreach (var uncertaintyGoal in uncertaintyGoalLists.FirstOrDefault().uncertaintyGoals)
        WriteTableHeaderItem(uncertaintyGoal.name);

      CloseTableHeader();

      #endregion

      #region Write contents

      OpenTableBody();

      foreach (var uncertaintyGoalList in uncertaintyGoalLists)
      {
        OpenRow(
          uncertaintyGoalList == uncertaintyGoalLists.Last()
          ? "tr-last"
          : "");
        WriteRowItem($"P{(int)uncertaintyGoalList.priority + 1}");
        WriteRowItem(uncertaintyGoalList.structureId);
        WriteRowItem(uncertaintyGoalList.objective);

        foreach (var uncertaintyGoal in uncertaintyGoalList.uncertaintyGoals)
          WriteRowItem($"{uncertaintyGoal.value:F2}", uncertaintyGoal.goalResult.ToLower());

        CloseRow();
      }

      CloseTableBody();
      CloseTable();

      #endregion
    }

    public void OpenTableAndHeader()
    {
      this.NumberOfOpenTables++;
      this.NumberOfOpenTableHeaders++;
      string tableHeader =
@"<table>
<thead>";
      this.StringBuilder.AppendLine(tableHeader);
      OpenRow("tr-last");
    }

    public void CloseTable()
    {
      this.NumberOfOpenTables--;
      this.StringBuilder.AppendLine("</table>\n");
    }

    public void CloseTableHeader()
    {
      CloseRow();
      this.NumberOfOpenTableHeaders--;
      this.StringBuilder.AppendLine(@"</thead>");
    }

    public void OpenRow(string className = "")
    {
      this.NumberOfOpenRows++;
      string rowHeader = "<tr" +
        (String.IsNullOrWhiteSpace(className) ? ">" : $" class=\"{className}\">");

      this.StringBuilder.AppendLine(rowHeader);
    }

    public void CloseRow()
    {
      this.NumberOfOpenRows--;
      this.StringBuilder.AppendLine(@"</tr>");
    }

    public void WriteTableHeaderItem(string item)
    {
      this.StringBuilder.AppendLine($"  <th class=\"headerEntry\">{item}</th>");
    }

    public void OpenTableBody()
    {
      this.NumberOfOpenTableBodies++;
      this.StringBuilder.AppendLine(@"<tbody>");
    }

    public void CloseTableBody()
    {
      this.NumberOfOpenTableBodies--;
      this.StringBuilder.AppendLine(@"</tbody>");
    }

    public void WriteRowItem(string item, string className = "")
    {
      string itemText = "  <td";
      itemText += (String.IsNullOrEmpty(className) ? "" : $" class=\"{className}\"");
      itemText += ">" + item + "</td>";

      this.StringBuilder.AppendLine(itemText);
    }


  }
}
