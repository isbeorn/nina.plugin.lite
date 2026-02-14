using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using Serilog.Debugging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Send via Ground Station")]
    [ExportMetadata("Description", "Send a message via Ground Station, including Expressions.")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Powerups (Misc)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public class GSSend : SequentialContainer {

        [ImportingConstructor]
        public GSSend() : base() {
            Condition = new IfContainer();
            Instructions = new IfContainer();
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }

        public GSSend(GSSend copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Condition = (IfContainer)copyMe.Condition.Clone();
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Items = new ObservableCollection<ISequenceItem>(copyMe.Items.Select((ISequenceItem i) => i.Clone() as ISequenceItem));
            }
        }

        public override object Clone() {
            return new GSSend(this) {
            };
        }

        [JsonProperty]
        public SequentialContainer Condition { get; set; }

        [JsonIgnore]
        public SequentialContainer Instructions { get; set; }

        [JsonProperty("Instructions")]
        private IfContainer ObsoleteInstructions {
            // get is intentionally omitted here
            set { Instructions = value; }
        }

        // Prevent Condition from being serialized (old property for backward compat only)
        public bool ShouldSerializeCondition() => false;

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
                        Logger.Warning("Error processing script, " + ex.Error);
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
            if (Items.Count == 0) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            ISequenceItem instruction = Items[0];

            if (instruction == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            // Execute the conditional
            instruction.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;

            var messageProperty = instruction.GetType().GetProperty("Message");
            if (messageProperty == null) {
                messageProperty = instruction.GetType().GetProperty("Payload");
                if (messageProperty == null) {
                    throw new SequenceEntityFailedException("Not a supported Ground Station instruction?");
                }
            }
            string message = (string)messageProperty.GetValue(instruction);
            if (message == null) {
                throw new SequenceEntityFailedException("Message is null?");
            }
            message = message.Replace('\t', ' ');
            var processedMessage = ProcessedScript(message);
            Logger.Info("Sending to GNS: " + processedMessage);
            if (processedMessage == null) {
                throw new SequenceEntityFailedException("Processed message is null?");
            }
            messageProperty.SetValue(instruction, processedMessage, null);
            instruction.AttachNewParent(Parent);
            await instruction.Run(progress, token);
            messageProperty.SetValue(instruction, message, null);
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

            Items.Clear();
            Add(item);
        }

        public new IList<string> Issues { get; } = new List<string>();

        public override bool Validate() {
            Issues.Clear();
            if (Items.Count == 0) {
                Issues.Add("There must be a GNS instruction included in this instruction");
            } else {
                string itemName = Items[0].GetType().AssemblyQualifiedName;
                string[] parts = itemName.Split(',');
                string assemblyName = parts.Length > 1 ? parts[1].Trim() : null;
                if (assemblyName == "DaleGhent.NINA.GroundStation") {
                    var messageProperty = Items[0].GetType().GetProperty("Message");
                    if (messageProperty == null) {
                        messageProperty = Items[0].GetType().GetProperty("Payload");
                        if (messageProperty == null) {
                            Issues.Add("This Ground Station instruction cannot be used with Send with Ground Station");
                        }
                    }
                } else {
                    Issues.Add("The instruction specified isn't from the Ground Station plugin");
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