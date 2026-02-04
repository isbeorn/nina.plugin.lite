using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.ViewModel.Sequencer;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Expression = NINA.Sequencer.Logic.Expression;
using Settings = WhenPlugin.When.Properties.Settings;

namespace WhenPlugin.When {
    [Export(typeof(IPluginManifest))]
    public class WhenPluginManifest : PluginBase, INotifyPropertyChanged {
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
        public WhenPluginManifest(IProfileService profileService, IOptionsVM options, IImageSaveMediator imageSaveMediator,
            ISwitchMediator switchMediator, IWeatherDataMediator weatherDataMediator, ICameraMediator cameraMediator, IDomeMediator domeMediator,
                IFlatDeviceMediator flatMediator, IFilterWheelMediator filterWheelMediator, IRotatorMediator rotatorMediator, ISafetyMonitorMediator safetyMonitorMediator,
                IFocuserMediator focuserMediator, ITelescopeMediator telescopeMediator, IImagingMediator imagingMediator, ISequenceMediator sequenceMediator, IMessageBroker messageBroker,
                IGuiderMediator guiderMediator, ISymbolBroker symbolBroker) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            PluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            ProfileService = profileService;
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            SequenceMediator = sequenceMediator;
            FilterWheelMediator = filterWheelMediator;
            SymbolBrokerVM = symbolBroker;

            imageSaveMediator.BeforeFinalizeImageSaved += ImageSaveMediator_BeforeFinalizeImageSaved;

            OpenRoofFilePathDiagCommand = new RelayCommand(OpenRoofFilePathDiag);

            Plugin = this;

            SymbolProvider = symbolBroker.RegisterSymbolProvider("Powerups");
        }

        private Task ImageSaveMediator_BeforeFinalizeImageSaved(object sender, BeforeFinalizeImageSavedEventArgs e) {
            foreach (AddImagePattern.ImagePatternExpr pe in AddImagePattern.ImagePatterns) {
                ImagePattern p = pe.Pattern;
                Expression expr = pe.Expr;

                if (expr.Context == null || expr.Context.Parent == null) {
                    continue;
                }

                expr.Evaluate();
                string v = (expr.Error != null) ? "ERROR" : expr.ValueString;
                e.AddImagePattern(new ImagePattern(p.Key, p.Description, p.Category) { Value = v });
            }
            return Task.CompletedTask;
        }

        public static WhenPluginManifest Plugin { get; private set; }

        public static ISymbolProvider SymbolProvider { get; private set; }

        public override Task Teardown() {
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
            RaisePropertyChanged(nameof(ProfileSpecificNotificationMessage));
        }

        public static double GetLatitude() => ProfileService.ActiveProfile.AstrometrySettings.Latitude;

        public static double GetLongitude() => ProfileService.ActiveProfile.AstrometrySettings.Longitude;

        public ICommand OpenRoofFilePathDiagCommand { get; private set; }

        private void OpenRoofFilePathDiag(object obj) {
            var dialog = GetFilteredFileDialog(string.Empty, string.Empty, "Text File (*.txt)|*.txt");
            if (dialog.ShowDialog() == true) {
                RoofStatus = dialog.FileName;
            }
        }

        public static Microsoft.Win32.OpenFileDialog GetFilteredFileDialog(string path, string filename, string filter) {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();

            if (System.IO.File.Exists(path)) {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(path);
            }
            dialog.FileName = filename;
            dialog.Filter = filter;
            return dialog;
        }

        public static string DockableExpressions {
            get => PluginSettings.GetValueString(nameof(DockableExpressions), Settings.Default.DockableExprs);
            set => PluginSettings.SetValueString(nameof(DockableExpressions), value);
        }

        public string RoofStatus {
            get => Settings.Default.RoofStatus;
            set {
                Settings.Default.RoofStatus = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string RoofOpenString {
            get => Settings.Default.RoofOpenString;
            set {
                Settings.Default.RoofOpenString = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        public string ProfileSpecificNotificationMessage {
            get => PluginSettings.GetValueString(nameof(ProfileSpecificNotificationMessage), string.Empty);
            set {
                PluginSettings.SetValueString(nameof(ProfileSpecificNotificationMessage), value);
                RaisePropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
