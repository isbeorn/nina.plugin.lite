using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using Serilog.Debugging;
using System;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Send via Ground Station")]
    [ExportMetadata("Description", "Send a message via Ground Station, including Powerups Expressions.")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Powerups")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]

    public class GSSend : SequentialContainer {

        [ImportingConstructor]
        public GSSend() {
            Condition = new IfContainer();
            Instructions = new IfContainer();
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }
        public GSSend(GSSend copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Condition = (IfContainer)copyMe.Condition.Clone();
                Instructions = (IfContainer)copyMe.Instructions.Clone();
            }
        }

        public override object Clone() {
            return new GSSend(this) {
            };
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
        public ISequenceItem? GSInstruction { get; set; }

        public ICommand DropIntoIfCommand { get; set; }

        private object lockObj = new object();

        public string ProcessedScript(string message) {
            string value = message;
            if (value != null) {
                while (true) {
                    string toReplace = Regex.Match(value, @"\{([^\}]+)\}").Groups[1].Value;
                    if (toReplace == null) {
                        Logger.Error("toReplace is null?");
                        break;
                    }
                    if (toReplace.Length == 0) break;
                    Expression ex = ExpressionHelper.Expr(toReplace, Parent, SymbolBroker, null);
                    if (ex.Error != null) {
                        Logger.Warning("Send via Ground Station, error processing script, " + ex.Error);
                        value = value.Replace("{" + toReplace + "}", ex.Error);
                    } else if (ex.StringValue != null) {
                        value = value.Replace("{" + toReplace + "}", ex.StringValue);
                    } else {
                        value = value.Replace("{" + toReplace + "}", ex.ValueString);
                    }
                    Logger.Info("Replacing " + toReplace + " with " + ex.ValueString);
                }
            }
            return value;
        }


        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceItem condition = Condition.Items[0];

            if (condition == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            // Execute the conditional
            condition.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;

            var messageProperty = condition.GetType().GetProperty("Message");
            if (messageProperty == null) {
                messageProperty = condition.GetType().GetProperty("Payload");
                if (messageProperty == null) {
                    throw new SequenceEntityFailedException("Not a Ground Station instruction?");
                }
            }
            string message = (string)messageProperty.GetValue(condition);
            if (message == null) {
                throw new SequenceEntityFailedException("Message is null?");
            }
            message = message.Replace('\t', ' ');
            var processedMessage = ProcessedScript(message);
            Logger.Info("Sending to Ground Station: " + processedMessage);
            if (processedMessage == null) {
                throw new SequenceEntityFailedException("Processed message is null?");
            }
            messageProperty.SetValue(condition, processedMessage, null);
            condition.AttachNewParent(Parent);
            await condition.Run(progress, token);
            messageProperty.SetValue(condition, message, null);
        }

        public void DropIntoCondition(DropIntoParameters parameters) {
            ISequenceItem item;
            var source = parameters.Source as ISequenceItem;
            if (source == null) return;

            if (source.Parent != null && !parameters.Duplicate) {
                item = source;
            } else {
                item = (ISequenceItem)source.Clone();
            }

            GSInstruction = item;
            item.AttachNewParent(this);
            RaisePropertyChanged("GSInstruction");
        }

        public override bool Validate() {
            Issues.Clear();
            if (GSInstruction == null) {
                Issues.Add("There must be a Ground Station instruction included in this instruction");
            } else {
                var messageProperty = GSInstruction.GetType().GetProperty("Message");
                if (messageProperty == null) {
                    messageProperty = GSInstruction.GetType().GetProperty("Payload");
                    if (messageProperty == null) {
                        Issues.Add("This instruction cannot be used with Send via Ground Station");
                    }
                }
             }
            RaisePropertyChanged("Issues");
            return Issues.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(GSSend)}";
        }
    }
}
