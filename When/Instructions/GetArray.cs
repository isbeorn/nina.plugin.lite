using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using NINA.Sequencer.Validations;
using System.Collections.Generic;
using NINA.Core.Utility;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem.Expressions;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Get from Array")]
    [ExportMetadata("Description", "Gets a value from an Array at the specified index into a Variable")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Powerups (Arrays)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class GetArray : SequenceItem, IValidatable {

        [ImportingConstructor]
        public GetArray() : base() {
            Name = Name;
            Icon = Icon;
        }

        public GetArray(GetArray copyMe) : base(copyMe) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Name = copyMe.Name;
                Icon = copyMe.Icon;
            }
        }

        [IsExpression]
        private string nameExpr;

        [IsExpression]
        private string iExpr;

        [IsExpression]
        private string vExpr;

        public static readonly String VALID_SYMBOL = "^[a-zA-Z][a-zA-Z0-9-+_]*$";

        [JsonProperty]
        public string Identifier {
            get { return null; }
            set {
                NameExprExpression.Definition = value;
            }
        }
        public override string ToString() {
            return $"Get Array: {NameExprExpression.StringValue} at {IExprExpression.Value}, into Variable {VExprExpression.Definition}";
        }

        private IList<string> issues = new List<string>();
        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public bool Validate() {
            IList<string> i = new List<string>();

            if (NameExprExpression.StringValue != null) {
                if (NameExprExpression.StringValue.Length == 0) {
                    i.Add("A name for the Array must be specified");
                } else if (!Regex.IsMatch(NameExprExpression.StringValue, VALID_SYMBOL)) {
                    i.Add("The name of an Array must be alphanumeric");
                    //} else if (!Symbol.Arrays.ContainsKey(Identifier)) {
                    //    i.Add("The Array named '" + Identifier + "' has not been initialized");
                } else if (IExprExpression != null && IExprExpression.Definition != null && IExprExpression.Definition.Length == 0) {
                    i.Add("The Array index must be specified");
                }
            }

            Expression.ValidateExpressions(i, IExprExpression, VExprExpression, NameExprExpression);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Array arr;
            if (!Array.Arrays.TryGetValue(NameExprExpression.StringValue, out arr)) {
                throw new SequenceEntityFailedException("The Array named '" + Identifier + " has not been initialized");
            }
            object value;
            if (!arr.TryGetValue(IExprExpression.ValueString, out value)) {
                Logger.Warning("There is no value at index " + (int)IExprExpression.Value + " in Array " + Identifier + "; returning -1");
                value = -1;
                //throw new SequenceEntityFailedException("There was no value for index " + IExpr.Value + " in Array " + Identifier);
            }

            string resultName = VExprExpression.Definition;
            if (resultName == null || resultName.Length == 0) {
                throw new SequenceEntityFailedException("There must be a result Variable specified in order to use the Get from Array instruction");
            }
            UserSymbol sym = UserSymbol.FindSymbol(resultName, Parent);
            if (sym != null && sym is Variable sv) {
                if (value is string vs) {
                    value = "'" + vs + "'";
                }
                sv.Expr.Definition = value.ToString();
                Logger.Info("Setting Variable " + sv + " to " + value);
            } else {
                throw new SequenceEntityFailedException("Result Variable is not defined: " + VExprExpression.Definition);
            }

            return Task.CompletedTask;
        }
    }
}
