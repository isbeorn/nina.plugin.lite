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
    [ExportMetadata("Category", "Powerups")]
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

        private string ImagePatternAdded = String.Empty;

        private string ImagePatternExprDefinition = String.Empty;

        public bool Validate() {
            if (!UserSymbol.IsAttachedToRoot(this)) return true;

            IList<string> i = new List<string>();

            if (Identifier.Length == 0 || ExprExpression.Definition.Length == 0 || Description.Length == 0) {
                i.Add("A name, value, and description must be specified");
            } else if (!Regex.IsMatch(Identifier, VALID_SYMBOL)) {
                i.Add("The name of an image pattern token must be all uppercase alphabetic characters");
            } else {
                // Create it
                // Check if pattern changed...
                if (ImagePatternAdded.Length != 0) {
                    // Remove existing pattern
                    if (Identifier != ImagePatternAdded || ExprExpression.Definition != ImagePatternExprDefinition) {
                        var toRemove = ImagePatterns.Find(p => p.Pattern.Key == "$$" + ImagePatternAdded + "$$");
                        if (toRemove != null) {
                            ImagePatterns.Remove(toRemove);
                            OptionsVM.RemoveImagePattern(toRemove.Pattern.Key);
                        }
                        ImagePatternAdded = "";
                    }
                }
                if (ImagePatternAdded.Length == 0) {
                    string desc = PatternDescription;
                    
                    // Create the pattern with a placeholder value
                    var pattern = new ImagePattern("$$" + Identifier + "$$", desc, "Sequencer Powerups");
                    
                    // Add to our tracking list with reference to parent for dynamic evaluation
                    ImagePatterns.Add(new ImagePatternExpr(pattern, ExprExpression, this));
                    
                    // Get initial formatted value for display
                    var expandable = new ExpandableString(Expr);
                    expandable.SetSymbolBroker(SymbolBroker);
                    expandable.SetParent(Parent);
                    string initialValue = expandable.Expanded;
                    
                    // Add to OptionsVM with initial formatted value
                    pattern.Value = expandable.HasError ? "Error" : initialValue;
                    OptionsVM.RemoveImagePattern(pattern.Key);
                    OptionsVM.AddImagePattern(pattern);
                    
                    ImagePatternAdded = Identifier;
                    ImagePatternExprDefinition = ExprExpression.Definition;
                    Notification.ShowInformation($"Image pattern '{Identifier}' added with format support");
                }
            }

            Expression.ValidateExpressions(i, ExprExpression);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
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
