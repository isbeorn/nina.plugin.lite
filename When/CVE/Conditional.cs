using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhenPlugin.When {
    public abstract class Conditional : SequenceContainer, IValidatable {

        protected Conditional(IExecutionStrategy strategy) : base(strategy) {
        }

        [JsonProperty]
        public IfContainer Condition { get; protected set; }
        
        public IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public override void ResetProgress() {
            base.ResetProgress();
            if (Condition != null && !(Condition.Items == null || Condition.Items.Count == 0)) {
                Condition.Items[0].Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
            }
        }
        
        public override void AfterParentChanged() {
            base.AfterParentChanged();
            if (Condition != null) {
                foreach (ISequenceItem item in Condition.Items) {
                    item.AfterParentChanged();
                }
            }
        }
        protected void ValidateInstructions(IfContainer instructions) {
            try {
                if (instructions.PseudoParent == null) {
                    instructions.PseudoParent = this;
                }

                // Avoid infinite loop by checking first...
                if (instructions.Parent != Parent) {
                    instructions.AttachNewParent(Parent);
                }

                foreach (ISequenceItem item in instructions.Items) {
                    if (item is IValidatable val) {
                        _ = val.Validate();
                    }

                }

                if (Condition != null) {
                    if (Condition.Parent != Parent) {
                        Condition.AttachNewParent(Parent);
                    }
                    foreach (ISequenceItem item in Condition.Items) {
                        if (item is IValidatable val) {
                            _ = val.Validate();
                        }
                    }
                }
            } catch (Exception ex) {
                Logger.Info("Exception in ValidateInstructions: " + ex.Message);
            }
        }

        public override void Initialize() {
            base.Initialize();
        }

        public virtual bool Validate() {
            //CommonValidate();

            var i = new List<string>();
            if (Condition == null) { }
            else if (Condition.Items == null || Condition.Items.Count == 0) {
                i.Add("The instruction to check must not be empty!");
            } else if (Condition.Items[0] is IValidatable val) {
                _ = val.Validate();
            }
 
            Issues = i;
            return i.Count == 0;
        }
    }
}
