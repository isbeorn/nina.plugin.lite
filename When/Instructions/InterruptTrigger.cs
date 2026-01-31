using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "Interrupt Trigger")]
    [ExportMetadata("Description", "This trigger will stop execution after the currently running instruction, allowing you to add whatever instructions you want before proceeding.")]
    [ExportMetadata("Icon", "SequenceSVG")]
    [ExportMetadata("Category", "Powerups (Triggers)")]
    [Export(typeof(ISequenceTrigger))]
    
    [JsonObject(MemberSerialization.OptIn)]
    public class InterruptTrigger : SequenceTrigger, IValidatable, IDSOTargetProxy {

        private GeometryGroup HourglassIcon = (GeometryGroup)Application.Current.Resources["HourglassSVG"];

        [JsonProperty]
        public IfContainer Instructions { get; set; }
        [JsonProperty]
        public IfContainer Runner { get; set; }

        [ImportingConstructor]
        public InterruptTrigger() {
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            AddItem(Instructions, new WaitIndefinitely() { Name="Wait Indefinitely", Icon = HourglassIcon }); ;
        }

        private void AddItem(IfContainer Instructions, ISequenceItem item) {
            Instructions.Items.Add(item);
            item.AttachNewParent(Instructions);
        }

        private InterruptTrigger(InterruptTrigger copyMe) {
            CopyMetaData(copyMe);
            Name = copyMe.Name;
            Icon = copyMe.Icon;
            Instructions = (IfContainer)copyMe.Instructions.Clone();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
        }

        public override object Clone() {
            return new InterruptTrigger(this);
        }

        public bool InFlight { get; set; }

        public IList<string> Issues => new List<string>();

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            try {
                Target = DSOTarget.FindTarget(Parent);
                if (Target != null) {
                    Logger.Info("Found Target: " + Target);
                    UpdateChildren(Instructions);
                }
                await Instructions.Run(progress, token);
            } finally {
                //InFlight = false;
            }
        }


        public override void AfterParentChanged() {
            foreach (ISequenceTrigger item in Instructions.Triggers) {
                if (item.Parent == null) item.AttachNewParent(Instructions);
            }
            foreach (ISequenceItem item in Instructions.Items) {
                if (item.Parent == null) item.AttachNewParent(Instructions);
            }
            Instructions.AttachNewParent(Parent);
        }

        public InputTarget DSOProxyTarget() {
            return Target;
        }
        
        public InputTarget Target = null;

        public InputTarget FindTarget(ISequenceContainer c) {
            while (c != null) {
                if (c is IDeepSkyObjectContainer dso) {
                    return dso.Target;
                } else {
                    c = c.Parent;
                }
            }
            return null;
        }

        private void UpdateChildren(ISequenceContainer c) {
            foreach (var item in c.Items) {
                item.AfterParentChanged();
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return !InFlight;
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(InterruptTrigger)}";
        }

        public bool Validate() {
            // Make sure a proper tree is maintained
            foreach (ISequenceItem item in Instructions.Items) {
                item.AttachNewParent(Instructions);
            }
            try {
                Target = DSOTarget.FindTarget(Parent);
                if (Target != null) {
                    //Logger.Info("Found Target: " + Target);
                    UpdateChildren(Instructions);
                }
            } finally {
                //InFlight = false;
            }

            Instructions.Validate();

            return true;
        }
    }
}