using NINA.Sequencer.Container;
using NINA.Sequencer.Logic;

namespace WhenPlugin.When
{
    public class ExpressionHelper {

        public static Expression Expr (string definition, ISequenceContainer parent, ISymbolBroker broker, int? def) {
            Expression e = new Expression(definition, parent);
            if (def != null) {
                e.Default = (int)def;
            }
            e.SymbolBroker = broker;
            e.Evaluate();
            return e;
        }
    }
}
