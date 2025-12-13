using NINA.Equipment.Equipment.MyFilterWheel;
using NINA.Profile;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhenPlugin.When {
    public class DockableExpression : Expression {

        public DockableExpression (string definition, ISymbolBroker symbolBroker) : base(definition, (Variable)new Variable().Clone()) {
            Context.AttachNewParent(PseudoRoot);
            SymbolBroker = symbolBroker;
        }

        public static SequenceRootContainer PseudoRoot = new SequenceRootContainer();

        public override string Definition {
            get {
                return base.Definition;
            }
            set {
                base.Definition = value;
                // Note it's changed; save the list, remove empty items...
                if (value != null && value.Length == 0) {
                    // Remove it...
                    WhenPluginDockable.RemoveExpr(this);
                }
                WhenPluginDockable.SaveDockableExpressions();
                RaisePropertyChanged("IsEditable");
            }
        }

        public override double Value {
            get {
                return base.Value;
            }
            set {
                base.Value = value;
                RaisePropertyChanged("IsEditable");
            }
        }

        public override string Error {
            get {
                return base.Error;
            }
            set {
                base.Error = value;
                RaisePropertyChanged("IsEditable");
            }
        }

        public bool IsEditable {
            get {

                ISequenceEntity runningItem = WhenPlugin.GetRunningItem();
                if (runningItem != null) {
                    Context = runningItem;
                }

                return false;

                //Symbol s = Symbol.FindSymbol(Definition, Context.Parent);
                //return (s != null);
            }
            set { }
        }

        private bool isOpen = false;
        public bool IsOpen {
            get { return isOpen; }
            set {
                isOpen = value;
                RaisePropertyChanged(nameof(IsOpen));
            }
        }

        private string displayType = "Numeric";
        public string DisplayType {
            get => displayType;
            set {
                displayType = value;
                RaisePropertyChanged("DockableValue");
                WhenPluginDockable.SaveDockableExpressions();
            }
        }

        private string conversionType = "None";
        public string ConversionType {
            get => conversionType;
            set {
                conversionType = value;
                RaisePropertyChanged("DockableValue");
                WhenPluginDockable.SaveDockableExpressions();
            }
        }


        private const long ONE_YEAR = 60 * 60 * 24 * 365;
        public static string ExprValueString(double value) {
            long start = DateTimeOffset.Now.ToUnixTimeSeconds() - ONE_YEAR;
            long end = start + (2 * ONE_YEAR);
            if (value > start && value < end) {
                DateTime dt = Expr.ConvertFromUnixTimestamp(value).ToLocalTime();
                if (dt.Day == DateTime.Now.Day + 1) {
                    return dt.ToShortTimeString() + " tomorrow";
                } else if (dt.Day == DateTime.Now.Day - 1) {
                    return dt.ToShortTimeString() + " yesterday";
                } else
                    return dt.ToShortTimeString();
            } else {
                return value.ToString();
            }
        }

        public string DockableValue {
            get {
                Evaluate();
                if (Error != null) {
                    return Error;
                }
                if (DisplayType.Equals("Numeric")) {
                    if (Value == Double.NegativeInfinity) {
                        return StringValue;
                    } else if (ConversionType.Equals("C to F")) {
                        return Math.Round(32 + (Value * 9 / 5), 2).ToString() + "° F";
                    } else if (ConversionType.Equals("m/s to mph")) {
                        return Math.Round(Value * 2.237, 2).ToString() + " mph";
                    } else if (ConversionType.Equals("kph to mph")) {
                        return Math.Round(Value * .621, 2).ToString() + " mph";
                    } else if (ConversionType.Equals("hPa to inhg")) {
                        return Math.Round(Value * .0295, 2).ToString() + "\" hg";
                    }

                    return ExprValueString(Math.Round(Value, 2)); ;
                } else if (DisplayType.Equals("Boolean")) {
                    return (Value == 0) ? "False" : "True";
                } else {
                    FilterWheelInfo fwi = WhenPlugin.FilterWheelMediator.GetInfo();
                    if (fwi == null || fwi.Connected == false) {
                        return "Not connected";
                    }
                    var filters = WhenPlugin.ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                    if (Value < filters.Count) {
                        return filters[(int)Value].Name;
                    }
                    return "No filter";
                }
            }
        }



    }
}
