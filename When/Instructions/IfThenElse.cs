using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Expressions;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If-Then-Else")]
    [ExportMetadata("Description", "Executes an instruction set if the Expression is True (or 1)")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups (Conditionals)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class IfThenElse : SequentialContainer, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public IfThenElse() {
            // Don't initialize branches here - will be done after deserialization or when first used
        }

        public IfThenElse(IfThenElse copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        private void InitializeBranches() {
            if (Items.Count == 0) {
                var thenBranch = new SequentialContainer { Name = "Then" };
                var elseBranch = new SequentialContainer { Name = "Else" };

                thenBranch.AttachNewParent(this);
                elseBranch.AttachNewParent(this);

                Items.Add(thenBranch);
                Items.Add(elseBranch);
            }
        }

        // Called after JSON deserialization completes
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context) {
            // If Items is empty, initialize the branches
            // This handles the case where an empty IfThenElse is deserialized
            InitializeBranches();
        }

        // These are just accessors to Items[0] and Items[1] - not serialized
        public SequentialContainer ThenBranch {
            get {
                // Ensure branches are initialized when accessed
                InitializeBranches();
                return Items.Count > 0 ? Items[0] as SequentialContainer : null;
            }
        }

        public SequentialContainer ElseBranch {
            get {
                // Ensure branches are initialized when accessed
                InitializeBranches();
                return Items.Count > 1 ? Items[1] as SequentialContainer : null;
            }
        }

        partial void AfterClone(IfThenElse original, IfThenElse clone) {
            clone.Icon = original.Icon;
            clone.Name = original.Name;
            clone.Category = original.Category;
            clone.Description = original.Description;
            clone.Items = new ObservableCollection<ISequenceItem>(original.Items.Select((ISequenceItem i) => i.Clone() as ISequenceItem));
            foreach (ISequenceItem item in clone.Items) {
                item.AttachNewParent(clone);
            }
        }

        // Old properties for backward compatibility - can be read during deserialization but not written
        [JsonProperty]
        public SequentialContainer Instructions { get; set; }

        [JsonProperty]
        public SequentialContainer ElseInstructions { get; set; }

        // Prevent these old properties from being serialized
        public bool ShouldSerializeInstructions() => false;
        public bool ShouldSerializeElseInstructions() => false;

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
                    Logger.Info("Predicate is true, executing Then branch");
                    if (ThenBranch != null) {
                        await ThenBranch.Run(progress, token);
                    }
                } else {
                    Logger.Info("Predicate is false, executing Else branch");
                    if (ElseBranch != null) {
                        await ElseBranch.Run(progress, token);
                    }
                }
            } catch (ArgumentException ex) {
                Logger.Info("If error: " + ex.Message);
                Status = SequenceEntityStatus.FAILED;
            }
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfThenElse)}, Expr: {PredicateExpression}";
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            PredicateExpression.Evaluate();
        }

        public new bool Validate() {
            Issues.Clear();

            var valid = base.Validate();

            var exprIssues = new List<string>();
            Expression.ValidateExpressions(exprIssues, PredicateExpression);

            foreach (var issue in exprIssues) {
                if (!string.IsNullOrWhiteSpace(issue) && !Issues.Contains(issue)) {
                    Issues.Add(issue);
                }
            }

            return Issues.Count == 0 && valid;
        }
    }
}