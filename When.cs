using NINA.Core.Utility;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Settings = WhenPlugin.When.Properties.Settings;
using NINA.Sequencer.Container;
using System.Reflection;
using System.Windows.Media;
using System.Windows;
using NINA.Equipment.Interfaces.Mediator;
using System.Windows.Input;
using System.IO;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.ViewModel.Sequencer;
using NINA.Sequencer.Logic;

namespace WhenPlugin.When {
    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    /// 
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "When_Options" where When corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class WhenPlugin : PluginBase, INotifyPropertyChanged {
        private static IPluginOptionsAccessor PluginSettings;
        public static IProfileService ProfileService;
        private static ISequenceMediator SequenceMediator;
        public static IFilterWheelMediator FilterWheelMediator;
        static protected ISequenceNavigationVM sequenceNavigationVM;
        private static protected ISequence2VM s2vm;
        private static ISymbolBroker SymbolBrokerVM;

        // Implementing a file pattern
        private GeometryGroup ConstantsIcon = (GeometryGroup)Application.Current.Resources["Pen_NoFill_SVG"];

        [ImportingConstructor]
        public WhenPlugin(IProfileService profileService, IOptionsVM options, IImageSaveMediator imageSaveMediator,
            ISwitchMediator switchMediator, IWeatherDataMediator weatherDataMediator, ICameraMediator cameraMediator, IDomeMediator domeMediator,
                IFlatDeviceMediator flatMediator, IFilterWheelMediator filterWheelMediator, IRotatorMediator rotatorMediator, ISafetyMonitorMediator safetyMonitorMediator,
                IFocuserMediator focuserMediator, ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, ISequenceMediator sequenceMediator, IMessageBroker messageBroker,
                IGuiderMediator guiderMediator, ISymbolBroker symbolBroker) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            // This helper class can be used to store plugin settings that are dependent on the current profile
            PluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            ProfileService = profileService;
            // React on a changed profile
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            SequenceMediator = sequenceMediator;
            FilterWheelMediator = filterWheelMediator;
            SymbolBrokerVM = symbolBroker;

            OpenRoofFilePathDiagCommand = new RelayCommand(OpenRoofFilePathDiag);

            Plugin = this;
            
            SymbolProvider = symbolBroker.RegisterSymbolProvider("Powerups");
            //sp.AddOrUpdateSymbol("MoonAltitude", 10);
            //sp.AddOrUpdateSymbol("Bar", 20);

        }

        public static WhenPlugin Plugin { get; private set; }

        public static ISymbolProvider SymbolProvider { get; private set; }

        public override Task Teardown() {
            // Make sure to unregister an event when the object is no longer in use. Otherwise garbage collection will be prevented.
            ProfileService.ProfileChanged -= ProfileService_ProfileChanged;
            return base.Teardown();
        }
        public static ISequenceItem GetRunningItem() {
            if (sequenceNavigationVM == null) {
                FieldInfo fi = SequenceMediator.GetType().GetField("sequenceNavigation", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null) {
                    sequenceNavigationVM = (ISequenceNavigationVM)fi.GetValue(SequenceMediator);
                    s2vm = sequenceNavigationVM.Sequence2VM;
                }
            } else if (s2vm == null) {
                s2vm = sequenceNavigationVM.Sequence2VM;
            }

            try {
                if (SequenceMediator.Initialized && SequenceMediator.IsAdvancedSequenceRunning()) {
                    ISequenceRootContainer root = s2vm.Sequencer.MainContainer;
                    Type type = typeof(SequenceRootContainer);
                    FieldInfo f = type.GetField("runningItems", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) {
                        try {
                            List<ISequenceItem> runningItems = (List<ISequenceItem>)f.GetValue(root);
                            if (runningItems.Count > 0) {
                                return runningItems[0];
                            }
                        } catch (Exception) {
                            Logger.Error("Can't get running items!");
                        }
                    }
                }
            } catch (Exception ex) {
                Logger.Warning("Can't get running items: " + ex.Message);
            }
            return null;
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e) {
            // Rase the event that this profile specific value has been changed due to the profile switch
            RaisePropertyChanged(nameof(ProfileSpecificNotificationMessage));
        }
        public static double GetLatitude() {
            return ProfileService.ActiveProfile.AstrometrySettings.Latitude;
        }

        public static double GetLongitude() {
            return ProfileService.ActiveProfile.AstrometrySettings.Longitude;
        }

        public ICommand OpenRoofFilePathDiagCommand { get; private set; }

        private void OpenRoofFilePathDiag(object obj) {
            var dialog = GetFilteredFileDialog(string.Empty, string.Empty, "Text File (*.txt)|*.txt");
            if (dialog.ShowDialog() == true) {
                RoofStatus = dialog.FileName;
            }
        }

        public static Microsoft.Win32.OpenFileDialog GetFilteredFileDialog(string path, string filename, string filter) {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            if (File.Exists(path)) {
                dialog.InitialDirectory = Path.GetDirectoryName(path);
            }
            dialog.FileName = filename;
            dialog.Filter = filter;
            return dialog;
        }

        public static string DockableExpressions {
            get {
                return PluginSettings.GetValueString(nameof(DockableExpressions), Settings.Default.DockableExprs);
            }
            set {
                PluginSettings.SetValueString(nameof(DockableExpressions), value);
            }
        }

        public string RoofStatus {
            get {
                return Settings.Default.RoofStatus;
            }
            set {
                Settings.Default.RoofStatus = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }
        public string RoofOpenString {
            get {
                return Settings.Default.RoofOpenString;
            }
            set {
                Settings.Default.RoofOpenString = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string ProfileSpecificNotificationMessage {
            get {
                return PluginSettings.GetValueString(nameof(ProfileSpecificNotificationMessage), string.Empty);
            }
            set {
                PluginSettings.SetValueString(nameof(ProfileSpecificNotificationMessage), value);
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
