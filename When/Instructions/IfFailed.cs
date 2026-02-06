using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.DragDrop;
using System.Windows.Input;
using NINA.Sequencer.Container;
using NINA.Core.Utility;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If Fails")]
    [ExportMetadata("Description", "Executes an instruction set if the predicate instruction failed.")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups (Conditionals)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public class IfFailed : SequentialContainer {

        [ImportingConstructor]
        public IfFailed() {
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }
        public IfFailed(IfFailed copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
             }
        }

        [JsonIgnore]
        public IfContainer Condition { get; set; }

        [JsonProperty("Condition")]
        private IfContainer ObsoleteCondition {
            // get is intentionally omitted here
            set { Condition = value; }
        }

        [JsonIgnore]
        public SequentialContainer Instructions { get; set; }

        [JsonProperty("Instructions")]
        private IfContainer ObsoleteInstructions {
            // get is intentionally omitted here
            set { Instructions = value; }
        }

        [JsonProperty]
        public ISequenceItem? CheckInstruction { get; set; }

        public override object Clone() {
            return new IfFailed(this);
        }

        public ICommand DropIntoIfCommand { get; set; }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            if (CheckInstruction == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            while (true) {
                // Execute the conditional
                CheckInstruction.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
                await CheckInstruction.Run(progress, token);

                if (CheckInstruction.Status != NINA.Core.Enum.SequenceEntityStatus.FAILED) {
                    return;
                }
                Logger.Info("IfFailed - Triggered by: " + CheckInstruction.Name);
                
                // Items won't run unless we reset this to CREATED
                await base.Execute(progress, token);
                return;
            }
        }

        // Allow only ONE instruction to be added to Condition
        public void DropIntoCondition(DropIntoParameters parameters) {
            ISequenceItem item;
            var source = parameters.Source as ISequenceItem;
            if (source == null) return;

            if (source.Parent != null && !parameters.Duplicate) {
                item = source;
            } else {
                item = (ISequenceItem)source.Clone();
            }

            CheckInstruction = item;
            item.AttachNewParent(this);
            RaisePropertyChanged("CheckInstruction");
        }

        public override void ResetAll() {
            base.ResetAll();
            if (CheckInstruction != null) {
                CheckInstruction.ResetProgress();
            }
        }
        public override bool Validate() {
            //CommonValidate();
            return true;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfFailed)}";
        }

        public override bool Remove(ISequenceItem item) {
            if (item == CheckInstruction) {
                CheckInstruction = null;
                RaisePropertyChanged(nameof(CheckInstruction));
                return true;
            }
            return base.Remove(item);
        }
    }
}
