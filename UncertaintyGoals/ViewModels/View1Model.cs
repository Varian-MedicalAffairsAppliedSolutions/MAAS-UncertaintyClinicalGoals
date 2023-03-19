using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Xml;
using System.Collections.ObjectModel;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Threading;
using System.Net;
using System.IO;

using System.Runtime.CompilerServices;
using System.Windows.Media;
using Prism.Mvvm;
using UncertaintyGoals.Models;
using Prism.Commands;
using System.Numerics;
using JR.Utils.GUI.Forms;
using System.Windows.Controls;
using System.Resources;
using UncertaintyGoals.CustomWidgets;
using System.Windows.Controls.Primitives;
using Views;

namespace ViewModels
{

  public class View1Model : BindableBase
  {
    private ScriptContext context;

    private Window SubWindow;

    public string AboutURI { get; set; }

    private string savePath_ { get; set; }
    public string SavePath
    {
      get
      {
        return savePath_;
      }
      set
      {
        if(value != savePath_)
        {
          savePath_ = value;
          RaisePropertyChanged();
        }
      }
    }

    public DelegateCommand AboutCmd { get; set; }
    public DelegateCommand CalculateCmd { get; set; }
    public DelegateCommand SelectSavePathCmd { get; set; }

    public bool SaveCsv { get; set; }
    public bool SaveJson { get; set; }
    public bool SaveAndShowHtml { get; set; }

    public string ErrorMsg { get; set; }
    public bool SomethingWrongWithPlan { get; set; }

    public ObservableCollection<ClinicalGoalItem> ClinicalGoalList { get; set; }
    public ObservableCollection<UncertaintyScenarioItem> UncertaintyScenarioList { get; set; }

    internal UncertaintyGoalsModel UncertaintyGoalsModel { get; }

    public DelegateCommand ShowWindowCmd { get; set; }


    public View1Model(ScriptContext currentContext)
    {
      this.context = currentContext;
      this.UncertaintyGoalsModel = new UncertaintyGoalsModel(context);

      string tmpErrorMsg = "";
      this.SomethingWrongWithPlan = this.UncertaintyGoalsModel.ValidateContext(ref tmpErrorMsg);
      this.ErrorMsg = tmpErrorMsg;

      this.ClinicalGoalList = ClinicalGoalItem.CreateClinicalGoalItems(this.context.PlanSetup);
      this.UncertaintyScenarioList = UncertaintyScenarioItem.CreateUncertaintyScenarioItems(this.context.PlanSetup);

      this.SavePath = @"C:\temp\UncertaintyGoals\";
      this.SaveCsv = false;
      this.SaveJson = false;
      this.SaveAndShowHtml = true;

      CalculateCmd = new DelegateCommand(OnCalculate);
      AboutCmd = new DelegateCommand(OnAbout);
      SelectSavePathCmd = new DelegateCommand(OnSelectSavePath);

      SubWindow = new Window();
      SubWindow.Height = 500;
      SubWindow.Width = 500;
      SubWindow.Title = "About";
      SubWindow.Content = new BindableRichTextBox()
      {
        IsReadOnly = true,
        Source = new Uri(@"pack://application:,,,/UncertaintyClinicalGoals.esapi;component/Resources/About.rtf"),
      };

      SubWindow.Closing += OnClosing;

    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
      SubWindow.Hide();
      e.Cancel = true;
    }

    private void OnAbout()
    {
      SubWindow.Show();
    }

    private void OnCalculate()
    {
      this.UncertaintyGoalsModel.Execute(
        new WriteToFileSettings()
        {
          WriteJson = this.SaveJson,
          WriteCsv = this.SaveCsv,
          WriteAndOpenHtml = this.SaveAndShowHtml,
          SavePath = this.SavePath
        });

      System.Diagnostics.Process.Start(this.UncertaintyGoalsModel.GetHtmlfileFullPath());
    }

    private void OnSelectSavePath()
    {
      using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
      {
        var result = dialog.ShowDialog();

        if (result == System.Windows.Forms.DialogResult.OK)
          this.SavePath = dialog.SelectedPath;
      }
    }

  }
}
