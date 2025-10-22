using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Enum;
using NINA.Core.Utility;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem.Expressions;
using NINA.Sequencer.Container;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If")]
    [ExportMetadata("Description", "Executes an instruction set if the Expression is True (or 1)")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups Lite")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class IfConstant : IfCommand, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public IfConstant() {
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Instructions.Name = Name;
            Instructions.Icon = Icon;
        }

        public IfConstant(IfConstant copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = copyMe.Name;
                Instructions.Icon = copyMe.Icon;
            }
        }

        [IsExpression]
        private string predicate;

        private void CheckItems (ISequenceContainer c) {

        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            Logger.Info("Predicate: " + PredicateExpression.Definition);
            if (string.IsNullOrEmpty(PredicateExpression.Definition)) {
                Status = SequenceEntityStatus.FAILED;
                return;
            }

            try {
                PredicateExpression.Evaluate();

                if (!string.Equals(PredicateExpression.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (PredicateExpression.Error == null)) {
                    Logger.Info("Predicate is true, " + PredicateExpression);
                    await Instructions.Run(progress, token);
                } else {
                    Logger.Info("Predicate is false, " + PredicateExpression);
                    return;
                }
            } catch (ArgumentException ex) {
                Logger.Info("If error: " + ex.Message);
                Status = SequenceEntityStatus.FAILED;
            }
        }


        public override void ResetProgress() {
            base.ResetProgress();
            foreach (ISequenceItem item in Instructions.Items) {
                item.ResetProgress();
            }
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfConstant)}, Expr: {PredicateExpression}";
        }

        public IList<string> Switches { get; set; } = null;

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            PredicateExpression.Evaluate();
        }

        public new bool Validate() {

            CommonValidate();

            var i = new List<string>();

            Expression.ValidateExpressions(i, PredicateExpression);
 
            Issues = i;
            return i.Count == 0;
        }
    }
}
