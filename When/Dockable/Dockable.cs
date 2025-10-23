using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using Nito.Mvvm;
using Serilog.Debugging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace WhenPlugin.When {
    /// <summary>
    /// This Class shows the basic principle on how to add a new panel to N.I.N.A. Imaging tab via the plugin interface
    /// In this example an altitude chart is added to the imaging tab that shows the altitude chart based on the position of the telescope    
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class WhenPluginDockable : DockableVM {

        [ImportingConstructor]
        public WhenPluginDockable(IProfileService profileService, ISymbolBroker symbolBroker) : base(profileService) {
            Title = "Powerups Panel";

            SymbolBroker = symbolBroker;

            ExpressionString = WhenPlugin.DockableExprs;
            BuildExprList();

            ConditionWatchdog = new ConditionWatchdog(UpdateData, TimeSpan.FromSeconds(5));
            ConditionWatchdog.Start();
        }

        private static ISymbolBroker SymbolBroker;
        
        private static ConditionWatchdog ConditionWatchdog;

        private static bool InhibitSave { get; set; } = false;

        public DockableExpr Exp {  get; set; }

        private void BuildExprList() {
            InhibitSave = true;
            if (Exp == null) {
                Exp = new DockableExpr("foo", SymbolBroker);
                Exp.Evaluate();
            }
            try {
                string[] l = ExpressionString.Split(EXPR_DIVIDER);
                foreach (string s in l) {
                    if (s.Length > 0) {
                        string[] parts = s.Split(EXPR_INTERNAL_DIVIDER);
                        DockableExpr expr = new DockableExpr(parts[0], SymbolBroker);

                        // Update radio buttons!
                        if (parts.Length > 1) {
                            expr.DisplayType = parts[1];
                        }
                        if (parts.Length > 2) {
                            expr.ConversionType = parts[2];
                        }
                        ExpressionList.Add(expr);
                    }
                }
            } finally {
                InhibitSave = false;
            }
        }

        private static HashSet<string> LoggedOnce = new HashSet<string>();
        public static void LogOnce(string message) {
            if (LoggedOnce.Contains(message)) return;
            Logger.Warning(message);
            LoggedOnce.Add(message);
        }

        private static Symbol[] RoofConstants = new Symbol[] { new Symbol("RoofNotOpen", 0), new Symbol("RoofOpen", 1), new Symbol("RoofCannotOpenOrRead", 2) };

        private static int lastValidRoofStatus = -1;
        private static int invalidRoofStatusCount = 0;

        public static Task UpdateData() {

            // Handle RoofStatus here
            string roofStatus = WhenPlugin.Plugin.RoofStatus;
            string roofOpenString = WhenPlugin.Plugin.RoofOpenString;


            if (roofStatus?.Length > 0 && roofOpenString?.Length > 0) {
                // It's actually a file name..
                int status = 0;
                try {
                    var lastLine = File.ReadLines(roofStatus).Last();
                    if (lastLine.ToLower().Contains(roofOpenString.ToLower())) {
                        status = 1;
                    }
                    lastValidRoofStatus = status;
                    invalidRoofStatusCount = 0;
                } catch (Exception e) {
                    Logger.Warning("Roof status, error: " + e.Message);
                    if (++invalidRoofStatusCount > 4) {
                        Logger.Warning("Four consecutive roof status errors, reporting status 2");
                        status = 2;
                    } else {
                        Logger.Warning("Roof status error #" + invalidRoofStatusCount + ", reporting status " + lastValidRoofStatus + " for now");
                        status = lastValidRoofStatus;
                    }
                }
                WhenPlugin.SymbolProvider.AddOrUpdateSymbol("RoofStatus", status, RoofConstants);
                Logger.Trace("RoofStatus: " + status);
            } else {
                Logger.Info("RoofStatus UNKNOWN");
            }

            ISequenceItem? runningItem = null;

            if (ExpressionList.Count > 0) {
                runningItem = WhenPlugin.GetRunningItem();
            }
            foreach (DockableExpr e in ExpressionList) {
                ISequenceEntity se = e.Context;
                if (runningItem != null) {
                    e.Context = runningItem;
                }

                if (e.Context is ISequenceContainer c && !ItemUtility.IsInRootContainer(c)) {
                    continue;
                }

                e.Refresh();
                if (runningItem != null) {
                    e.Context = se;
                }
            }
            return Task.CompletedTask;
        }

        private const char EXPR_DIVIDER = (char)0x0;
        private const char EXPR_INTERNAL_DIVIDER = (char)0x1;

        public static void SaveDockableExprs() {
            if (InhibitSave) return;
            int count = 0;
            StringBuilder sb = new StringBuilder();
            foreach (DockableExpr e in ExpressionList) {
                sb.Append(e.Definition);
                sb.Append(EXPR_INTERNAL_DIVIDER);
                sb.Append(e.DisplayType);
                sb.Append(EXPR_INTERNAL_DIVIDER);
                sb.Append(e.ConversionType);
                sb.Append(EXPR_DIVIDER);
                count++;
            }
            Logger.Info("SaveDockableExprs saving " + count + " Exprs");
            WhenPlugin.DockableExprs = sb.ToString();
        }

        public string ExpressionString { get; private set; }

        public static ObservableCollection<DockableExpr> ExpressionList { get; private set; } = new ObservableCollection<DockableExpr>();
        
        public static void RemoveExpr (DockableExpr e) {
            ExpressionList.Remove(e);
            SaveDockableExprs();
        }

        private GalaSoft.MvvmLight.Command.RelayCommand addInstruction;

        public ICommand AddInstruction => addInstruction ??= new GalaSoft.MvvmLight.Command.RelayCommand(PerformAddInstruction);

        private void PerformAddInstruction() {
            ExpressionList.Add(new DockableExpr("New", SymbolBroker));
            SaveDockableExprs();
        }
    }
}
