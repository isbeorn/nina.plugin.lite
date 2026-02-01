using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.Container;
using System.Diagnostics;
using NINA.Core.Enum;
using NINA.Sequencer;
using NINA.Core.Utility;
using NCalc.Domain;
using System.Text.RegularExpressions;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Profile;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Sequencer.Utility.DateTimeProvider;
using System.Linq;
using WhenPlugin.When;

namespace WhenPlugin.When {
    [JsonObject(MemberSerialization.OptIn)]
    public class ResetVariableToDate : SequenceItem, IValidatable {

        private IList<IDateTimeProvider> dateTimeProviders;
        private IDateTimeProvider selectedProvider;
        private int hours;
        private int minutes;
        private int minutesOffset;
        private int seconds;

        [ImportingConstructor]
        public ResetVariableToDate(IList<IDateTimeProvider> dateTimeProviders) {
            Icon = Icon;
            Expr = new Expr(this);
            DateTime = new SystemDateTime();
            this.DateTimeProviders = dateTimeProviders;
            this.SelectedProvider = DateTimeProviders?.FirstOrDefault();
        }

        public ResetVariableToDate(IList<IDateTimeProvider> dateTimeProviders, IDateTimeProvider selectedProvider) {
            DateTime = new SystemDateTime();
            this.DateTimeProviders = dateTimeProviders;
            this.SelectedProvider = selectedProvider;
        }

        public ResetVariableToDate(ResetVariableToDate copyMe) : this(copyMe.DateTimeProviders, copyMe.SelectedProvider) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        public ResetVariableToDate() { }

        public override object Clone() {
            ResetVariableToDate clone = new ResetVariableToDate(this) { };
            clone.Expr = new Expr(clone, this.Expr.Expression);
            clone.Variable = this.Variable;
            return clone;
        }

        private Expr _Expr = null;

        [JsonProperty]
        public Expr Expr {
            get => _Expr;
            set {
                _Expr = value;
                RaisePropertyChanged();
            }
        }

        private string variable;

        [JsonProperty]
        public string Variable {
            get => variable;
            set {
                if (value == variable) {
                    return;
                }
                variable = value;
                RaisePropertyChanged();
            }
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }
  
        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
             return Task.CompletedTask;
        }

        private bool IsAttachedToRoot() {
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is SequenceRootContainer) {
                    return true;
                }
                p = p.Parent;
            }
            return false;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ResetVariable)}, Variable: {variable}, Expr: {Expr}";
        }

        public IList<IDateTimeProvider> DateTimeProviders {
            get => dateTimeProviders;
            set {
                dateTimeProviders = value;
                RaisePropertyChanged();
            }
        }

        public bool HasFixedTimeProvider => selectedProvider != null && !(selectedProvider is NINA.Sequencer.Utility.DateTimeProvider.TimeProvider);

        [JsonProperty]
        public IDateTimeProvider SelectedProvider {
            get => selectedProvider;
            set {
                selectedProvider = value;
                if (selectedProvider != null) {
                    UpdateTime();
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(HasFixedTimeProvider));
                }
            }
        }

        public string TimeString { get; set; } = "Not Set";

        private bool timeDeterminedSuccessfully;
        private DateTime lastReferenceDate;
        private void UpdateTime() {
            try {
                lastReferenceDate = NighttimeCalculator.GetReferenceDate(DateTime.Now);
             } catch (Exception) {
                timeDeterminedSuccessfully = false;
                Validate();
            }
        }

        [JsonProperty]
        public int Hours {
            get => hours;
            set {
                hours = value;
                RaisePropertyChanged();
                Validate();
            }
        }

        [JsonProperty]
        public int Minutes {
            get => minutes;
            set {
                minutes = value;
                RaisePropertyChanged();
                Validate();
            }
        }

        [JsonProperty]
        public int MinutesOffset {
            get => minutesOffset;
            set {
                minutesOffset = value;
                UpdateTime();
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public int Seconds {
            get => seconds;
            set {
                seconds = value;
                RaisePropertyChanged();
                Validate();
            }
        }

        public ICustomDateTime DateTime { get; set; }

        public bool Validate() {
            return true;
        }

        // Legacy

        [JsonProperty]
        public string CValueExpr {
            get => null;
            set {
                Expr.Expression = value;
                RaisePropertyChanged("Expr.Expression");
            }
        }
    }
}
