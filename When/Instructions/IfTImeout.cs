using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.Generators;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If Timed Out")]
    [ExportMetadata("Description", "Executes an instruction set if the predicate instruction failed.")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups (Conditionals)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class IfTimeout : SequentialContainer, IValidatable {

        public IfTimeout() {
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }
        public IfTimeout(IfTimeout copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        partial void AfterClone(IfTimeout original, IfTimeout clone) {
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

        [IsExpression]
        public partial int Time { get; set; }

        public ICommand DropIntoIfCommand { get; set; }

        private ConditionWatchdog Watchdog { get; set; }

        public DateTime StartTime {  get; set; }

        public volatile bool TimedOut = false;

        private CancellationTokenSource cts;
        private CancellationTokenSource linkedCts;

        private IProgress<ApplicationStatus> progress;


        private Task CheckTimer() {
            TimeSpan elapsed = DateTime.Now - StartTime;
            if (TimedOut) return Task.CompletedTask;
            if (elapsed > TimeSpan.FromSeconds(Time)) {
                TimedOut = true;
                Logger.Info("Timeout period over; interrupting...");
                cts.Cancel();
                linkedCts.Cancel();
                Notification.ShowWarning("Timed out!");
            }
            if (progress != null) {
                string progressStatus;

                TimeSpan t = TimeSpan.FromSeconds(Time);
                string status = "Timeout in ~";

                if (t.Hours > 0) {
                    var remaining = t - elapsed;
                    progressStatus = $"{status} {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                } else if (t.Minutes > 0) {
                    var remaining = t - elapsed;
                    progressStatus = $"{status} {remaining.Minutes:D2}:{remaining.Seconds:D2}";
                } else {
                    var remaining = t - elapsed;
                    progressStatus = $"{status} {remaining.Seconds} s";
                }

                progress?.Report(
                    new ApplicationStatus {
                        MaxProgress = 1,
                        Progress = -1,
                        Status = progressStatus,
                        ProgressType = ApplicationStatus.StatusProgressType.Percent
                    }
                ) ;  ;
            }
            return Task.CompletedTask;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (CheckInstruction == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            this.progress = new Progress<ApplicationStatus>((p) => {
                p.Source = Name;
                progress?.Report(p);
            });

            ConditionWatchdog watch = new ConditionWatchdog(CheckTimer, TimeSpan.FromSeconds(5));
            _ = watch.Start();

            cts = new CancellationTokenSource();
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);

            try {
                StartTime = DateTime.Now;
                TimedOut = false;
                // Execute the conditional
                CheckInstruction.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
                await CheckInstruction.Run(progress, linkedCts.Token);
            } catch (Exception ex) {
                watch.Cancel();
                if (TimedOut) {
                    Logger.Info("Timed out; executing instructions...");
                    Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
                    await Run(progress, token);
                } else {
                    Logger.Info("Exception: " + ex.Message);
                }
            } finally {
                this.progress.Report(new ApplicationStatus() { ProgressType = ApplicationStatus.StatusProgressType.ValueOfMaxValue, Status = "" });
                Logger.Info("Execution terminated");
                watch.Cancel();
                cts.Dispose();
                linkedCts.Dispose();
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

        public new bool Remove(ISequenceItem item) {
            if (item == CheckInstruction) {
                CheckInstruction = null;
                RaisePropertyChanged(nameof(CheckInstruction));
                return true;
            }
            return base.Remove(item);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfTimeout)}";
        }
    }
}
