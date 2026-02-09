using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "DIY Trigger")]
    [ExportMetadata("Description", "This trigger will run the specified instructions when the underlying trigger is activated.")]
    [ExportMetadata("Icon", "WandSVG")]
    [ExportMetadata("Category", "Powerups (Triggers)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DIYTrigger : SequenceTrigger, IValidatable, IDSOTargetProxy {
        
        [JsonProperty]
        public IfContainer Instructions { get; set; } = new IfContainer();

        public IfContainer Runner { get; set; } = new IfContainer();

        [ImportingConstructor]
        public DIYTrigger() {
            DropIntoDIYTriggersCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropInSequenceTrigger);
        }

        private DIYTrigger(DIYTrigger copyMe) : this() {
            CopyMetaData(copyMe);
            Instructions = (IfContainer)copyMe.Instructions.Clone();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Runner = (IfContainer)copyMe.Runner.Clone();
            Runner.PseudoParent = this;
        }
        public override object Clone() {
            return new DIYTrigger(this);
        }

        public override bool AllowMultiplePerSet => true;

        public ICommand DropIntoDIYTriggersCommand { get; set; }


        private static object lockObj = new object();
        public bool InFlight { get; set; }

        private void DropInSequenceTrigger(DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceTrigger item;
                var source = parameters.Source as ISequenceTrigger;

                if (source.Parent != null && !parameters.Duplicate) {
                    item = source;
                } else {
                    item = (ISequenceTrigger)source.Clone();
                }

                if (item.Parent != Runner) {
                    item.Parent?.Remove(item);
                    item.AttachNewParent(Runner);
                }

                Runner.Triggers.Clear();
                Runner.Triggers.Add(item);
            }
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            Instructions.AttachNewParent(context);
            Instructions.ResetAll();
            try {
                Logger.Info("DIY Trigger executing...");
                SetTarget();
                await Instructions.Run(progress, token);
            } finally {
                InFlight = false;
                Instructions.Parent?.Remove(Instructions);
                Instructions.AttachNewParent(Parent);
                Instructions.ResetAll();
            }
        }
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight) return false;
            if (Runner.Triggers.FirstOrDefault() == null) return false;
            var trigger = Runner.Triggers.FirstOrDefault();
            var result = trigger.ShouldTrigger(previousItem, nextItem);
            if (result) {
                Logger.Info("DIY Trigger " + trigger.Name + " ShouldTrigger returning true");
            }
            return result;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight) return false;
            if (Runner.Triggers.FirstOrDefault() == null) return false;
            var trigger = Runner.Triggers.FirstOrDefault();
            var result = trigger.ShouldTriggerAfter(previousItem, nextItem);
            if (result) {
                Logger.Info("DIY Trigger " + trigger.Name + " ShouldTriggerAfter returning true");
            }
            return result;
        }

        // Per Nick Holland
        public override void SequenceBlockInitialize() {
            if (!InFlight && Runner.Triggers.FirstOrDefault() != null) {
                Runner.Triggers.FirstOrDefault().SequenceBlockInitialize();
            }
            // Also initialize the Instructions container
            Instructions.ResetAll();
        }

        public override void AfterParentChanged() {
            // Handle TriggerRunner
            foreach (ISequenceTrigger item in Runner.Triggers) {
                if (item.Parent == null) item.AttachNewParent(Runner);
            }
            Runner.AttachNewParent(Parent);
            if (Runner.Triggers.Count > 0) {
                Runner.Triggers[0].AfterParentChanged();
            }
            
            // Handle Instructions
            foreach (ISequenceItem item in Instructions.Items) {
                if (item.Parent == null) item.AttachNewParent(Instructions);
            }
            Instructions.AttachNewParent(Parent);
        }
        public virtual bool Validate() {
            // Validate the Items (this will update their status)
            if (Runner == null) return true;
            foreach (ISequenceTrigger item in Runner.Triggers) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
            foreach (ISequenceItem item in Instructions.Items) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }

            SetTarget();

            return true;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(DIYTrigger)}";
        }
        private void SetTarget() {
            Target = DSOTarget.FindTarget(Parent);
            if (Target != null && Target != LastTarget) {
                Instructions.AfterParentChanged();
                Instructions.Validate();
                LastTarget = Target;
            }
        }
        public InputTarget? Target { get; set; }
        public InputTarget? LastTarget { get; set; }
    }
}
