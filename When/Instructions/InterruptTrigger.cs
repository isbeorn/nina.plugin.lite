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

        [ImportingConstructor]
        public InterruptTrigger() {
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            AddItem(Instructions, new WaitIndefinitely() { Name="Wait Indefinitely", Icon = HourglassIcon }); ;
        }

        private void AddItem(IfContainer instructions, ISequenceItem item) {
            instructions.Items.Add(item);
            //item.AttachNewParent(instructions);
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
                // Is this necessary?
                Validate();
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
        
        public InputTarget? Target {
            get => DSOTarget.FindTarget(Parent);
            set { }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return !InFlight;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(InterruptTrigger)}";
        }

        private InputTarget? LastTarget = null;
        
        public bool Validate() {
            // Make sure a proper tree is maintained
            foreach (ISequenceItem item in Instructions.Items) {
                if (item.Parent != Instructions) {
                    item.AttachNewParent(Instructions);
                }
            }
            
            // Check our target...
            Target = DSOTarget.FindTarget(Parent);
            if (Target != null && Target != LastTarget) {
                Instructions.AfterParentChanged();
                LastTarget = Target;
            }

            Instructions.Validate();

            return true;
        }
    }
}