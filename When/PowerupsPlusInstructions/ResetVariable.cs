using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using WhenPlugin.When;

namespace WhenPlugin.When {
    [JsonObject(MemberSerialization.OptIn)]
    public class ResetVariable : SequenceItem, IValidatable {
        [ImportingConstructor]


        public ResetVariable() {
            Icon = Icon;
            Expr = new Expr(this);
        }

        public ResetVariable(ResetVariable copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        public override object Clone() {
            ResetVariable clone = new ResetVariable(this) { };
            clone.Expr = new Expr(clone, this.Expr.Expression);
            clone.Expr.Type = "Any";
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

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ResetVariable)}, Variable: {variable}, Expr: {Expr}";
        }

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
