using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Views;
using System.Net.NetworkInformation;
using System.IO;
using JR.Utils.GUI.Forms;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using ViewModels;
using UncertaintyGoals.Models;
using Newtonsoft.Json;
using System.Globalization;
using System.Windows.Media.Imaging;
using MAAS.Common.EulaVerification;
using UncertaintyGoals.Services;

// TODO: Uncomment the following line if the script requires write access.
//15.x or later:
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        // Define the project information for EULA verification
        private const string PROJECT_NAME = "UncertaintyClinicalGoals";
        private const string PROJECT_VERSION = "1.0.0";
        private const string LICENSE_URL = "https://varian-medicalaffairsappliedsolutions.github.io/MAAS-UncertaintyClinicalGoals/";
        private const string GITHUB_URL = "https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-UncertaintyClinicalGoals";

        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context)
        {
            try
            {
                // First, initialize the AppConfig - THIS IS CRUCIAL
                // based on the AppConfig implementation you shared
                string scriptPath = Assembly.GetExecutingAssembly().Location;
                try
                {
                    // Initialize the AppConfig with the executing assembly path
                    AppConfig.GetAppConfig(scriptPath);
                    //MessageBox.Show("AppConfig initialized successfully", "Configuration", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception configEx)
                {
                    MessageBox.Show($"Failed to initialize AppConfig: {configEx.Message}\n\nPath: {scriptPath}\n\nContinuing without configuration...",
                                   "Configuration Warning",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);

                    // Create an empty dictionary as fallback if the config file can't be loaded
                    AppConfig.m_appSettings = new Dictionary<string, string>();
                }

                // Set up the EulaConfig directory
                string scriptDirectory = Path.GetDirectoryName(scriptPath);
                EulaConfig.ConfigDirectory = scriptDirectory;

                // EULA verification
                var eulaVerifier = new EulaVerifier(PROJECT_NAME, PROJECT_VERSION, LICENSE_URL);
                var eulaConfig = EulaConfig.Load(PROJECT_NAME);
                if (eulaConfig.Settings == null)
                {
                    eulaConfig.Settings = new ApplicationSettings();
                }

                if (!eulaVerifier.IsEulaAccepted())
                {
                    MessageBox.Show(
                        $"This version of {PROJECT_NAME} (v{PROJECT_VERSION}) requires license acceptance before first use.\n\n" +
                        "You will be prompted to provide an access code. Please follow the instructions to obtain your code.",
                        "License Acceptance Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    BitmapImage qrCode = null;
                    try
                    {
                        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                        qrCode = new BitmapImage(new Uri($"pack://application:,,,/{assemblyName};component/Resources/qrcode.bmp"));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading QR code: {ex.Message}");
                    }

                    if (!eulaVerifier.ShowEulaDialog(qrCode))
                    {
                        MessageBox.Show(
                            "License acceptance is required to use this application.\n\n" +
                            "The application will now close.",
                            "License Not Accepted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }

                // Check if patient/plan is selected
                if (context.Patient == null || context.PlanSetup == null)
                {
                    MessageBox.Show("No active patient/plan selected - exiting",
                                    "MAAS-UncertaintyClinicalGoals",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Exclamation);
                    return;
                }

                // Continue with original expiration check
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var noexp_path = Path.Combine(path, "NOEXPIRE");
                bool foundNoExpire = File.Exists(noexp_path);

                var provider = new CultureInfo("en-US");
                var asmCa = typeof(Script).Assembly.CustomAttributes.FirstOrDefault(ca => ca.AttributeType == typeof(AssemblyExpirationDate));

                // Check if we have a valid expiration date and if the app is expired
                if (asmCa != null && asmCa.ConstructorArguments.Count > 0)
                {
                    DateTime endDate;
                    if (DateTime.TryParse(asmCa.ConstructorArguments.FirstOrDefault().Value as string, provider, DateTimeStyles.None, out endDate)
                        && (DateTime.Now <= endDate || foundNoExpire))
                    {
                        // Display opening msg based on validation status
                        string msg;

                        if (!eulaConfig.Settings.Validated)
                        {
                            // First-time message
                            msg = $"The current MAAS-UncertaintyClinicalGoals application is provided AS IS as a non-clinical, research only tool in evaluation only. The current " +
                            $"application will only be available until {endDate.Date} after which the application will be unavailable. " +
                            $"By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                            $"Newer builds with future expiration dates can be found here: {GITHUB_URL}\n\n" +
                            "See the FAQ for more information on how to remove this pop-up and expiration";
                        }
                        else
                        {
                            // Returning user message
                            msg = $"Application will only be available until {endDate.Date} after which the application will be unavailable. " +
                            "By Clicking 'Yes' you agree that this application will be evaluated and not utilized in providing planning decision support\n\n" +
                            $"Newer builds with future expiration dates can be found here: {GITHUB_URL} \n\n" +
                            "See the FAQ for more information on how to remove this pop-up and expiration";
                        }

                        if (!foundNoExpire)
                        {
                            bool userAgree = MessageBox.Show(msg,
                                                            "MAAS-UncertaintyClinicalGoals",
                                                            MessageBoxButton.YesNo,
                                                            MessageBoxImage.Question) == MessageBoxResult.Yes;
                            if (!userAgree)
                            {
                                return;
                            }
                        }

                        // Launch the main window
                        var mainWindow = new MainWindow(context, new MainViewModel(context, eulaConfig.Settings.Validated));
                        mainWindow.ShowDialog();
                    }
                    else
                    {
                        MessageBox.Show($"Application has expired. Newer builds with future expiration dates can be found here: {GITHUB_URL}");
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("No expiration date found in assembly.");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}\n\n{ex.StackTrace}",
                               "Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }
    }
}
