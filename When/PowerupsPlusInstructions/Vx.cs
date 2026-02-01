using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using NINA.Sequencer.Container;
using NINA.Core.Utility;
using System.Data;
using System.Runtime.Serialization;

namespace WhenPlugin.When {
    [JsonObject(MemberSerialization.OptIn)]

    public class SetVariable : Symbol {

        [ImportingConstructor]
        public SetVariable() : base() {
            Name = Name;
            Icon = Icon;
            OriginalExpr = new Expr(this);
        }
        public SetVariable(SetVariable copyMe) : base(copyMe) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Name = copyMe.Name;
                Icon = copyMe.Icon;
            }
        }

        [OnDeserialized]
        public void OnDeserialized(StreamingContext c) {
            if (Definition != null) {
                Definition = "";
            }
        }

        public SetVariable(string id, string def, ISequenceContainer parent) {
            SetVariable sv = new SetVariable();
            sv.AttachNewParent(parent);
            sv.Identifier = id;
            sv.Definition = def;
            sv.Executed = true;
        }

        public static void SetVariableReference(string id, string def, ISequenceContainer parent) {
        }

        public override object Clone() {
            SetVariable clone = new SetVariable(this);
 
            clone.Identifier = Identifier;
            clone.Definition = Definition;
            clone.OriginalExpr = new Expr(OriginalExpr);
            return clone;
        }

        private bool iExecuted = false;
        public bool Executed {
            get => iExecuted;
            set {
                iExecuted = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string OriginalDefinition {
            get => OriginalExpr?.Expression;
            set {
                OriginalExpr.Expression = value;
                RaisePropertyChanged("OriginalExpr");
            }
        }

        private Expr _originalExpr = null;
        public Expr OriginalExpr {
            get => _originalExpr;
            set {
                _originalExpr = value;
                RaisePropertyChanged();
            }
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            OriginalExpr = new Expr(OriginalDefinition, this);
            OriginalExpr.Type = "Any";
            if (!Executed && Parent != null && Expr != null) {
                Expr.IsExpression = true;
                if (Expr.Expression.Length > 0) {
                    Expr.Error = "Not evaluated";
                }
            }
        }


        public override string ToString() {
            if (Expr != null) {
                return $"Variable: {Identifier}, Definition: {Definition}, Parent {Parent?.Name}, Expr: {Expr}";

            } else {
                return $"Variable: {Identifier}, Definition: {Definition}, Parent {Parent?.Name} Expr: null";
            }
        }

        public override bool Validate() {
            if (!IsAttachedToRoot()) return true;
            IList<string> i = new List<string>();

            if (Identifier.Length == 0 || OriginalDefinition?.Length == 0) {
                i.Add("A name and an initial value must be specified");
            } else if (!Regex.IsMatch(Identifier, VALID_SYMBOL)) {
                i.Add("The name of a Constant must be alphanumeric");
            }

            if (!Executed) {
                OriginalExpr.Validate();
                if (OriginalExpr.Error != null) {
                    Expr.AddExprIssues(i, OriginalExpr);
                }
            }
            if (Expr.Error != null) {
                Expr.AddExprIssues(i, Expr, OriginalExpr);
            }



            if (Definition != Expr.Expression) {
                Definition = Expr.Expression;
                Logger.Info("Validate: Definition diverges from Expr; fixing");
            }

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        public override void ResetProgress() {
            base.ResetProgress();
            Executed = false;
            Definition = "";
            if (Expr != null) {
                Expr.IsExpression = true;
                Expr.Evaluate();
            }
            SymbolDirty(this);
        }


        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return Task.CompletedTask;
        }

        // Legacy

        [JsonProperty]
        public string Variable {
            get => null;
            set {
                if (value != null) {
                    Identifier = value;
                }
            }
        }
        
        [JsonProperty]
        public string OValueExpr {
            get => null;
            set {
                if (value != null) {
                    OriginalDefinition = value;
                }
            }
        }

        public string CValue { get; set; }

        public string CValueExpr { get; set; }
    }
}
