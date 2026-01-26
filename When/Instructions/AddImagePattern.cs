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

        private string ImagePatternAdded = String.Empty;

        [JsonProperty]
        public string PatternDescription { get; set; } = String.Empty;

        public class ImagePatternExpr {

            public ImagePatternExpr(ImagePattern p, Expression e) {
                Pattern = p;
                Expr = e;
            }

            public ImagePattern Pattern;
            public Expression Expr;
        }

        public static IList<ImagePatternExpr> ImagePatterns = new List<ImagePatternExpr>();

        public bool Validate() {
            if (!UserSymbol.IsAttachedToRoot(this)) return true;

            IList<string> i = new List<string>();

            if (Identifier.Length == 0 || ExprExpression.Definition.Length == 0 || Description.Length == 0) {
                i.Add("A name, value, and description must be specified");
            } else if (!Regex.IsMatch(Identifier, VALID_SYMBOL)) {
                i.Add("The name of an image pattern token must be all uppercase alphabetic characters");
            } else {
                // Create it
                if (ImagePatternAdded.Length == 0) {
                    string desc = PatternDescription;
                    ImagePatterns.Add(new ImagePatternExpr(new ImagePattern("$$" + Identifier + "$$", desc, "Sequencer Powerups"), ExprExpression));
                    OptionsVM.AddImagePattern(new ImagePattern("$$" + Identifier + "$$", desc, "Sequencer Powerups") { Value = "6.66" });
                    ImagePatternAdded = Identifier;
                    Notification.ShowInformation("Image pattern '" + Identifier + "' added");
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
