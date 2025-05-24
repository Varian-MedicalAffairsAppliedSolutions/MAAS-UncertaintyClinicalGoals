using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using VMS.TPS.Common.Model.API;

namespace ViewModels
{
  public class MainViewModel : BindableBase
  {
    private string myHeader;
    public string MyHeader
    {
      get { return myHeader; }
      set { SetProperty(ref myHeader, value); }
    }

    private string postText;
    public string PostText
    {
      get { return postText; }
      set { SetProperty(ref postText, value); }
    }

    private bool isValidated;
    public bool IsValidated
    {
      get { return isValidated; }
      set 
      { 
        SetProperty(ref isValidated, value);
        RaisePropertyChanged(nameof(ValidationBannerVisibility));
        RaisePropertyChanged(nameof(WindowTitle));
      }
    }

    // Property to control visibility of validation banners
    public Visibility ValidationBannerVisibility
    {
      get { return IsValidated ? Visibility.Collapsed : Visibility.Visible; }
    }

    // Property for dynamic window title
    public string WindowTitle
    {
      get { return IsValidated ? "UncertaintyClinicalGoals" : "UncertaintyClinicalGoals - NOT VALIDATED FOR CLINICAL USE"; }
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
      System.Diagnostics.Process.Start(
          new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
       );
      e.Handled = true;
    }

    public MainViewModel(ScriptContext context, bool isValidated)
    {
      MyHeader = $"PLAN: {context.PlanSetup.Id}";
      IsValidated = isValidated;

      PostText = "";
      if (!IsValidated) { PostText += " *** Not Validated For Clinical Use ***"; }
    }
  }
}
