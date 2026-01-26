using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Expressions;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If")]
    [ExportMetadata("Description", "Executes an instruction set if the Expression is True (or 1)")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups (Conditionals)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class IfConstant : SequentialContainer, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public IfConstant() {
        }

        public IfConstant(IfConstant copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        partial void AfterClone(IfConstant original, IfConstant clone) {
            clone.Icon = original.Icon;
            clone.Name = original.Name;
            clone.Category = original.Category;
            clone.Description = original.Description;
            clone.Items = new ObservableCollection<ISequenceItem>(original.Items.Select((ISequenceItem i) => i.Clone() as ISequenceItem));
            clone.Triggers = new ObservableCollection<ISequenceTrigger>(original.Triggers.Select((ISequenceTrigger t) => t.Clone() as ISequenceTrigger));
            clone.Conditions = new ObservableCollection<ISequenceCondition>(original.Conditions.Select((ISequenceCondition t) => t.Clone() as ISequenceCondition));
            foreach (ISequenceItem item in clone.Items) {
                item.AttachNewParent(clone);
            }
            foreach (ISequenceCondition condition in clone.Conditions) {
                condition.AttachNewParent(clone);
            }
            foreach (ISequenceTrigger trigger in clone.Triggers) {
                trigger.AttachNewParent(clone);
            }
        }

        [JsonProperty]
        public IfContainer Instructions { get; set; }

        [IsExpression]
        public partial string Predicate { get; set; }

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

            //CommonValidate();

            var i = new List<string>();

            Expression.ValidateExpressions(i, PredicateExpression);
 
            Issues = i;
            return i.Count == 0;
        }
    }
}
