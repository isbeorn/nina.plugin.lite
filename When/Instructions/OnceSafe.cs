using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Once Safe")]
    [ExportMetadata("Description", "Waits for Safe condition, then executes the specified instructions.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Powerups (Safety)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]

    public class OnceSafe: SequentialContainer, IValidatable {

        private ISafetyMonitorMediator safetyMonitorMediator;

        [ImportingConstructor]
        public OnceSafe(ISafetyMonitorMediator safetyMonitorMediator) {
            this.safetyMonitorMediator = safetyMonitorMediator;
        }

        public OnceSafe(OnceSafe copyMe) : this(copyMe.safetyMonitorMediator) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        public override object Clone() {
            return new OnceSafe(this);
        }

        [JsonIgnore]
        public SequentialContainer Instructions { get; set; }

        [JsonProperty("Instructions")]
        private IfContainer ObsoleteInstructions {
            // get is intentionally omitted here
            set { Instructions = value; }
        }

        private bool isSafe;

        public bool IsSafe {
            get => isSafe;
            private set {
                isSafe = value;
                RaisePropertyChanged();
            }
        }

        public TimeSpan WaitInterval { get; set; } = TimeSpan.FromSeconds(5);

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            bool IsSafe = WhenUnsafe.CheckSafe(this, safetyMonitorMediator);
            while (!IsSafe && !(Parent == null)) {
                progress?.Report(new ApplicationStatus() { Status = Loc.Instance["Lbl_SequenceItem_SafetyMonitor_WaitUntilSafe_Waiting"] });
                await CoreUtil.Wait(WaitInterval, token, default);
                IsSafe = WhenUnsafe.CheckSafe(this, safetyMonitorMediator);
            }

            // Execute instructions now
            await base.Execute(progress, token);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(OnceSafe)}";
        }

        public new bool Validate() {
            base.Validate();
            var i = new List<string>();
            Issues = i;
            return true;
        }
    }
}
