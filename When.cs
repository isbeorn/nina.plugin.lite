using NCalc.Handlers;
using NINA.Astrometry.Body;
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
using System.Text;
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
            // Add Array related functions
            var fn = new SymbolFunction(
                 key: "array_length",
                 category: "Powerups",
                 description: "Returns the length of an Array, or -1 if the Array does not exist",
                 usageExample: "array_length('MyArray')",
                 implementation: ArrayLengthImpl,
                 minArgs: 1,
                 maxArgs: 1,
                 isVolatile: false);

            SymbolProvider.RegisterFunction(fn);

            fn = new SymbolFunction(
                 key: "array_sum",
                 category: "Powerups",
                 description: "Returns the sum of the values in an Array, or -1 if the Array does not exist",
                 usageExample: "array_sum('MyArray')",
                 implementation: ArraySumImpl,
                 minArgs: 1,
                 maxArgs: 1,
                 isVolatile: false);

            SymbolProvider.RegisterFunction(fn);

            fn = new SymbolFunction(
                  key: "array_average",
                  category: "Powerups",
                  description: "Returns the average of the values in an Array, or -1 if the Array does not exist",
                  usageExample: "array_average('MyArray')",
                  implementation: ArrayAverageImpl,
                  minArgs: 1,
                  maxArgs: 1,
                  isVolatile: false);

            SymbolProvider.RegisterFunction(fn);
            
            fn = new SymbolFunction(
                  key: "string_concat",
                  category: "Powerups",
                  description: "Returns the concatenation of any number of Strings",
                  usageExample: "string_concat('MyArray', ' has a name of ', arrayName)",
                  implementation: StringConcatImpl,
                  minArgs: 2,
                  maxArgs: 10,
                  isVolatile: false);

            SymbolProvider.RegisterFunction(fn);
        }

        private static (string, Array)? GetArrayFromFunctionArgs(FunctionArgs args) {
            object?[] p = args.EvaluateParameters();
            
            if (p.Length != 1) {
                throw new ArgumentException("Requires one argument");
            }
            string? arrayName = p[0] as string;
            if (arrayName == null) return null;
            Array a;
            if (Array.Arrays.TryGetValue(arrayName, out a)) {
                return (arrayName, a);
            }
            return null;
        }

        private static object ArrayLengthImpl(FunctionArgs args) {
            var result = GetArrayFromFunctionArgs(args);
            return result?.Item2.Count ?? -1;
        }

        private static object ArraySumImpl(FunctionArgs args) {
            var result = GetArrayFromFunctionArgs(args);
            if (result == null) return -1;
            double sum = 0;
            foreach (var kvp in result.Value.Item2) {
                sum += Convert.ToDouble(kvp.Value);
            }
            return sum;
        }  

        private static object ArrayAverageImpl(FunctionArgs args) {
            var result = GetArrayFromFunctionArgs(args);
            if (result == null) return -1;
            double sum = 0;
            int count = 0;
            foreach (var kvp in result.Value.Item2) {
                sum += Convert.ToDouble(kvp.Value);
                count++;
            }
            return count > 0 ? sum / count : 0;
        }

        private static object StringConcatImpl(FunctionArgs args) {
            object?[] p = args.EvaluateParameters();
            if (p.Length < 2) {
                throw new ArgumentException("Requires at least two arguments");
            }
            StringBuilder sb = new StringBuilder();
            foreach (object? obj in p) {
                if (obj != null) {
                    sb.Append(obj.ToString());
                }
            }
            return sb.ToString();
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
