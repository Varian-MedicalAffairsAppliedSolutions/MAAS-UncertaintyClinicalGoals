using System;

namespace UncertaintyGoals.Models
{
  internal class SettingsClass
  {
    public bool Debug { get; set; }
    public bool Validated { get; set; }
    public bool EULAAgreed { get; set; }
  }

  internal class WriteToFileSettings
  {
    public bool WriteJson { get; set; }
    public bool WriteCsv { get; set; }
    public bool WriteAndOpenHtml { get; set; }
    public string SavePath { get; set; }
  }
}
