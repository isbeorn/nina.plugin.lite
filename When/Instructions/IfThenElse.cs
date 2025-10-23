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

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If/Then/Else")]
    [ExportMetadata("Description", "Executes an instruction set if the Expression is True (or 1)")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class IfThenElse : IfCommand, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public IfThenElse() {
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Instructions.Name = Name;
            Instructions.Icon = Icon;
            ElseInstructions = new IfContainer();
            ElseInstructions.AttachNewParent(Parent);
            ElseInstructions.PseudoParent = this;
            ElseInstructions.Name = Name;
            ElseInstructions.Icon = Icon;
        }

        public IfThenElse(IfThenElse copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = Name;
                Instructions.Icon = Icon;
                ElseInstructions = (IfContainer)copyMe.ElseInstructions.Clone();
                ElseInstructions.AttachNewParent(Parent);
                ElseInstructions.PseudoParent = this;
                ElseInstructions.Name = Name;
                ElseInstructions.Icon = Icon;
            }
        }

        [IsExpression]
        private string predicate;

        [JsonProperty]
        public IfContainer ElseInstructions { get; set; }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            Logger.Info("Execute, Predicate: " + PredicateExpression.Definition);
            if (string.IsNullOrEmpty(PredicateExpression.Definition)) {
                Status = SequenceEntityStatus.FAILED;
                return;
            }

            try {
                PredicateExpression.Evaluate();

                if (!string.Equals(PredicateExpression.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (PredicateExpression.Error == null)) {
                    Logger.Info("Predicate is true; running Then");
                    await Instructions.Run(progress, token);
                } else {
                    Logger.Info("Predicate is false; running Else");
                    await ElseInstructions.Run(progress, token);
                }
            } catch (ArgumentException ex) {
                Logger.Info("If error: " + ex.Message);
                Status = SequenceEntityStatus.FAILED;
            }
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfThenElse)}, Expr: {PredicateExpression}";
        }

        public override void ResetProgress() {
            base.ResetProgress();
            ElseInstructions.ResetAll();
            foreach (ISequenceItem item in ElseInstructions.Items) {
                item.ResetProgress();
            }
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            foreach (ISequenceItem item in ElseInstructions.Items) {
                item.AfterParentChanged();
            }
        }

        public override void ResetAll() {
            base.ResetAll();
            ElseInstructions.ResetAll();
        }

        public new bool Validate() {

            var i = new List<string>();

            ValidateInstructions(Instructions);
            ValidateInstructions(ElseInstructions);

            Expression.ValidateExpressions(i, PredicateExpression);

            Issues = i;
            return i.Count == 0;
        }

    }
}
