using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using NINA.Sequencer.Validations;
using System.Collections.Generic;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.Logic;
using NINA.Sequencer.Generators;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Add Image Pattern")]
    [ExportMetadata("Description", "Add an image pattern for file naming")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Powerups (Misc)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class AddImagePattern : SequenceItem, IValidatable {

        public static IOptionsVM OptionsVM;

        [ImportingConstructor]
        public AddImagePattern(IOptionsVM options) : base() {
            Name = Name;
            Icon = Icon;
            OptionsVM = options;
        }

        [IsExpression]
        public partial string Expr { get; set; }

        public AddImagePattern(AddImagePattern copyMe) : this(OptionsVM) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Identifier = copyMe.Identifier;
            }
        }

        [JsonProperty]
        public string Identifier { get; set; } = string.Empty;

        public override string ToString() {
            return $"AddImagePattern: {Identifier}, Expr: {Expr}";
        }

        public IList<String> Issues { get; set; }

        public static readonly String VALID_SYMBOL = "^[A-Z]+$";

        [JsonProperty]
        public string PatternDescription { get; set; } = String.Empty;

        public class ImagePatternExpr {

            public ImagePatternExpr(ImagePattern p, Expression e, AddImagePattern parent) {
                Pattern = p;
                Expr = e;
                Parent = parent;
            }

            public ImagePattern Pattern;
            public Expression Expr;
            public AddImagePattern Parent;
            
            // Called during image capture to get current formatted value
            public string GetFormattedValue() {
                // Use ExpandableString to leverage the new format support
                var expandable = new ExpandableString(Parent.Expr);
                expandable.SetSymbolBroker(Parent.SymbolBroker);
                expandable.SetParent(Parent.Parent);
                
                string result = expandable.Expanded;
                
                // Return the expanded/formatted value, or empty if error
                return expandable.HasError ? string.Empty : result;
            }
        }

        public static List<ImagePatternExpr> ImagePatterns = new List<ImagePatternExpr>();

        private string _lastIdentifier = string.Empty;
        private string _lastExprDefinition = string.Empty;
        private string _lastPatternDescription = string.Empty;

        public bool Validate() {
            if (!UserSymbol.IsAttachedToRoot(this)) return true;

            IList<string> i = new List<string>();

            if (Identifier.Length == 0 || ExprExpression.Definition.Length == 0 || Description.Length == 0) {
                i.Add("A name, value, and description must be specified");
            } else if (!Regex.IsMatch(Identifier, VALID_SYMBOL)) {
                i.Add("The name of an image pattern token must be all uppercase alphabetic characters");
            } else {
                // Check if pattern changed and update accordingly
                UpdateImagePattern();
            }

            Expression.ValidateExpressions(i, ExprExpression);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        private void UpdateImagePattern() {
            bool hasChanged = Identifier != _lastIdentifier || ExprExpression.Definition != _lastExprDefinition || PatternDescription != _lastPatternDescription;
            bool isInitial = string.IsNullOrEmpty(_lastIdentifier);

            if (hasChanged) {
                // Remove old pattern if it exists
                if (!isInitial) {
                    RemoveOldPattern();
                }

                // Add new pattern
                AddNewPattern();

                // Update tracking
                _lastIdentifier = Identifier;
                _lastExprDefinition = ExprExpression.Definition;
                _lastPatternDescription = PatternDescription;

                Notification.ShowInformation($"Image pattern '{Identifier}' {(isInitial ? "added" : "updated")}");
            }
        }

        private void RemoveOldPattern() {
            string oldKey = "$$" + _lastIdentifier + "$$";

            // Remove from static list
            ImagePatterns.RemoveAll(p => p.Pattern.Key == oldKey);

            // Remove from OptionsVM
            OptionsVM.RemoveImagePattern(oldKey);
        }

        private void AddNewPattern() {
            string key = "$$" + Identifier + "$$";

            // Create the pattern
            var pattern = new ImagePattern(key, PatternDescription, "Sequencer Powerups");

            // Get initial formatted value
            var expandable = new ExpandableString(Expr);
            expandable.SetSymbolBroker(SymbolBroker);
            expandable.SetParent(Parent);
            pattern.Value = expandable.HasError ? "Error" : expandable.Expanded;

            // Add to static list
            ImagePatterns.Add(new ImagePatternExpr(pattern, ExprExpression, this));

            // Add to OptionsVM
            OptionsVM.AddImagePattern(pattern);
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            // Doesn't Execute
            return Task.CompletedTask;
        }

        // 3.3 Upgrade
        [JsonProperty]
        public Expr iExpr { get; set; }
        public bool ShouldSerializeiExpr() {
            return false;
        }
    }
}
