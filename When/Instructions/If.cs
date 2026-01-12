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
using NINA.Sequencer.Container.ExecutionStrategy;
using System.Windows.Markup;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If")]
    [ExportMetadata("Description", "Executes an instruction set if the Expression is True (or 1)")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups (Conditionals)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class IfConstant : SequentialContainer, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public IfConstant() : base() {
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
                    await Run(progress, token);
                } else {
                    Logger.Info("Predicate is false, " + PredicateExpression);
                    return;
                }
            } catch (ArgumentException ex) {
                Logger.Info("If error: " + ex.Message);
                Status = SequenceEntityStatus.FAILED;
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
            var i = new List<string>();
            Expression.ValidateExpressions(i, PredicateExpression);
            Issues = i;
            return i.Count == 0;
        }
    }
}
