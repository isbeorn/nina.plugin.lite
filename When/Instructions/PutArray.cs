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
using System.Reflection;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Put into Array")]
    [ExportMetadata("Description", "Puts a value into an Array at the specified index")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Powerups Lite")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class PutArray : SequenceItem, IValidatable {

        [ImportingConstructor]
        public PutArray() : base() {
            Name = Name;
            Icon = Icon;
        }

        public PutArray(PutArray copyMe) : base(copyMe) {
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
            return $"Put Array: {NameExprExpression.StringValue} at {IExprExpression.Value}, value {VExprExpression.Value}";
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
                } else if (IExprExpression.Definition != null && IExprExpression.Definition.Length == 0) {
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
            
            VExprExpression.Evaluate();

            object val = VExprExpression.Value == double.NegativeInfinity ? "'" + VExprExpression.ValueString + "'" : VExprExpression.Value;
            try {
                arr[IExprExpression.ValueString] = val;
            } catch (Exception ex) {
                throw new SequenceEntityFailedException(ex.Message);
            }
            return Task.CompletedTask;
        }
    }
}
