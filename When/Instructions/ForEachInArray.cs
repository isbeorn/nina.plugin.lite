using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Conditions;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "For Each in Array")]
    [ExportMetadata("Description", "Iterates over the elements of an Array, executing the embedded instructions for each")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Powerups (Arrays)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class ForEachInArray : ForEachList, IValidatable {

        [ImportingConstructor]
        public ForEachInArray() : base() {
        }

        public ForEachInArray(ForEachInArray copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                ValueVariable = copyMe.ValueVariable;
                IndexVariable = copyMe.IndexVariable;
            }
        }

        private string indexVariable = "";

        [JsonProperty]
        public string IndexVariable {
            get => indexVariable;
            set {
                indexVariable = value;
                RaisePropertyChanged();
            }
        }

        private string valueVariable = "";

        [JsonProperty]
        public string ValueVariable {
            get => valueVariable;
            set {
                valueVariable = value;
                RaisePropertyChanged();
            }
        }


        [IsExpression]
        public partial string NameExpr { get; set; }

        [JsonProperty]
        public string Array {
            get { return null; }
            set {
                NameExprExpression.Definition = value;
            }
        }

        partial void AfterClone(ForEachInArray clone) {
            clone.Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem));
            foreach (var item in clone.Items) {
                item.AttachNewParent(clone);
            }
            AttachNewParent(Parent);
            if (clone.Conditions.Count == 0) {
                clone.Add(new LoopCondition());
            }
        }

        public static readonly String VALID_SYMBOL = "^[a-zA-Z][a-zA-Z0-9-+_]*$";

        public new string ValidateArguments () {

            if (IndexVariable == null || IndexVariable.Length == 0) {
                return "There must be an index variable specified";
            }
            if (ValueVariable == null || ValueVariable.Length == 0) {
                return "There must be a value variable specified";
            }

            if (NameExprExpression.StringValue != null) {
                if (NameExprExpression.StringValue.Length == 0) {
                    return "A name for the Array must be specified";
                } else if (!Regex.IsMatch(NameExprExpression.StringValue, VALID_SYMBOL)) {
                    return "The name of an Array must be alphanumeric";
                 }
            }

            return null;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ForEachInArray)}, IndexVariable: {IndexVariable}, ValueVariable: {ValueVariable}, Array: {NameExprExpression.Definition}";
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Array a = new Array();

            if (NameExprExpression.StringValue != null) {
                if (NameExprExpression.StringValue.Length == 0) {
                    throw new SequenceEntityFailedException("An Array must be specified and must have been initialized");
                }

                if (!WhenPlugin.When.Array.Arrays.ContainsKey(NameExprExpression.StringValue)) {
                    throw new SequenceEntityFailedException("The Array specified does not exist");
                }

                if (!WhenPlugin.When.Array.Arrays.TryGetValue(NameExprExpression.StringValue, out a)) {
                    throw new SequenceEntityFailedException("Huh?  Key exists but not Array??");
                }
            }

            ETokens = new string[a.Count];
            int i = 0;
            foreach (var kvp in a) {
                double d;
                object v = kvp.Value;
                if (!Double.TryParse((string)v, out d)) {
                    v = "'" + v + "'";
                }
                ETokens[i++] = kvp.Key + "," + v;
            }

            if (Conditions.Count > 0) {
                LoopCondition lp = Conditions[0] as LoopCondition;
                if (lp != null) {
                    lp.Iterations = ETokens.Length;
                }
            }

            Variable = IndexVariable + "," + ValueVariable;
            StringBuilder sb = new StringBuilder();
            foreach (string e in ETokens) {
                sb.Append(e);
                sb.Append(";");
            }
            ListExpression = sb.ToString();

            return base.Execute(progress, token);
        }

        public new bool Validate() {

            var i = new List<string>();
            if (!IsAttachedToRoot()) return true;

            string e = ValidateArguments();
            if (e != null) {
                i.Add(e);
            }

            Expression.ValidateExpressions(i, NameExprExpression);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }
    }
}
