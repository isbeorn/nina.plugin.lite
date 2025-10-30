using NCalc;
using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Sequencer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static WhenPlugin.When.Symbol;
using Expression = NCalc.Expression;

namespace WhenPlugin.When {
    [JsonObject(MemberSerialization.OptIn)]
    public class Expr : BaseINPC {

        public Expr() {

        }

        public Expr(string exp, Symbol sym) {
            Symbol = sym;
            SequenceEntity = sym;
            Expression = exp;
        }

        public Expr(ISequenceEntity item, string expression) {
            SequenceEntity = item;
            Expression = expression;
        }
        public Expr(ISequenceEntity item) {
            SequenceEntity = item;
        }

        public Expr(ISequenceEntity item, string expression, string type) {
            SequenceEntity = item;
            // TYPE MUST BE BEFORE EXPRESSION!!
            Type = type;
            Expression = expression;
        }

        public Expr(ISequenceEntity item, string expression, string type, Action<Expr> setter) {
            SequenceEntity = item;
            // SETTER MUST BE BEFORE EXPRESSION!!
            Setter = setter;
            Expression = expression;
            Type = type;
        }

        public Expr(ISequenceEntity item, string expression, string type, Action<Expr> setter, double def) {
            SequenceEntity = item;
            // SETTER and DEFAULT MUST BE BEFORE EXPRESSION!!
            Setter = setter;
            Default = def;
            Expression = expression;
            Type = type;
        }

        public Expr(Expr cloneMe) : this(cloneMe.SequenceEntity, cloneMe.Expression, cloneMe.Type) {
            Setter = cloneMe.Setter;
            Symbol = cloneMe.Symbol;
        }

        public static bool JustWarnings (string error) {
            string[] errors = error.Split(";");
            bool red = false;
            bool orange = false;
            foreach (string e in errors) {
                if (e.Contains("Not evaluated") || e.Contains("External")) {
                    orange = true; ;
                } else {
                    red = true;
                }
            }
            if (orange && !red) return true;
            return false;
        }

        private string _expression = "";

        private object LOCK = new object();

        [JsonProperty]
        public virtual string Expression {
            get => _expression;
            set {
                if (value == null) return;
                value = value.Trim();
                if (value.Length == 0) {
                    IsExpression = false;
                    if (!double.IsNaN(Default)) {
                        Value = Default;
                    } else {
                        Value = Double.NaN;
                    }
                    _expression = value;
                    Parameters.Clear();
                    Resolved.Clear();
                    References.Clear();
                    Error = null;
                    return;
                }
                Double result;

                if (value.StartsWith('%') && value.EndsWith('%') && value.Length > 2) {
                    value = "__ENV_" + value.Substring(1, value.Length - 2);
                }

                if (value != _expression && IsExpression) {
                    // The value has changed.  Clear what we had...cle
                    foreach (var symKvp in Resolved) {
                        Symbol s = symKvp.Value;
                        if (s != null) {
                            //symKvp.Value.RemoveConsumer(this);
                        }
                    }
                    Resolved.Clear();
                    Parameters.Clear();
                }

                _expression = value;
                if (Double.TryParse(value, out result)) {
                    Error = null;
                    IsExpression = false;
                    Value = result;
                    // Notify consumers
                    if (Symbol != null) {
                        SymbolDirty(Symbol);
                    } else {
                        // We always want to show the result if not a Symbol
                        //IsExpression = true;
                    }
                } else if (Regex.IsMatch(value, "{(\\d+)}")) { // Should be /^\d*\.?\d*$/
                    IsExpression = false;
                } else {
                    IsExpression = true;

                    // Evaluate just so that we can parse the expression
                    Expression e = new Expression(value, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
                    e.Parameters = EmptyDictionary;
                    IsSyntaxError = false;
                    try {
                        e.Evaluate();
                    } catch (NCalc.Exceptions.NCalcParserException) {
                        // We should expect this, since we're just trying to find the parameters used
                        Error = "Syntax Error";
                        return;
                    } catch (Exception) {
                        // That's ok
                    }

                    // Find the parameters used
                    References.Clear();
                    foreach (var p in e.GetParameterNames()) {
                        References.Add(p);
                    }

                    // References now holds all of the CV's used in the expression
                    Parameters.Clear();
                    Resolved.Clear();
                    Evaluate();
                    if (Symbol != null) SymbolDirty(Symbol);
                }
                RaisePropertyChanged("Expression");
                RaisePropertyChanged("IsAnnotated");
            }
        }

        private Double iDefault = Double.NaN;
        public Double Default {
            get => iDefault;
            set {
                iDefault = value;
                RaisePropertyChanged("Default");
                RaisePropertyChanged("Value");
                RaisePropertyChanged("ValueString");
                RaisePropertyChanged("StringValue");
            }
        }

        public string ExprErrors {
            get {
                if (Error == null) {
                    return "No errors in Expression";
                } else if (JustWarnings(Error)) {
                    return "Warning(s): " + Error;
                } else {
                    return "Error(s): " + Error;
                }
            }
            set { }
        }

        public Symbol Symbol { get; set; } = null;
        public ISequenceEntity SequenceEntity { get; set; } = null;

        [JsonProperty]
        public string Type { get; set; } = "Any";


        public Action<Expr> Setter { get; set; }

        public List<string> GenericData {
            get {
                return new List<string>();
            }
            set { }
        }

        private static Dictionary<string, object> EmptyDictionary = new Dictionary<string, object>();

        private double _value = Double.NaN;
        public virtual double Value {
            get {
                if (double.IsNaN(_value) && !double.IsNaN(Default)) {
                    return Default;
                }
                return _value;
            }
            set {
                if (value != _value) {
                    if ("Integer".Equals(Type)) {
                        if (StringValue != null) {
                            Error = "Value must be an Integer";
                        }
                        value = Double.Floor(value);
                    }
                    _value = value;
                    if (Setter != null) {
                        Setter(this);
                    }
                    RaisePropertyChanged("StringValue");
                    RaisePropertyChanged("ValueString");
                    RaisePropertyChanged("IsExpression");
                    RaisePropertyChanged("DockableValue");
                }
            }
        }

        private string _error;
        public virtual string Error {
            get => _error;
            set {
                if (value != _error) {
                    _error = value;
                    RaisePropertyChanged("ValueString");
                    RaisePropertyChanged("IsExpression");
                    RaisePropertyChanged("IsAnnotated");
                    RaisePropertyChanged("Error");
                    RaisePropertyChanged("StringValue");
                    RaisePropertyChanged("InfoButtonColor");
                    RaisePropertyChanged("InfoButtonChar");
                    RaisePropertyChanged("InfoButtonSize");
                    RaisePropertyChanged("InfoButtonMargin");
                }
            }
        }

        private const long ONE_YEAR = 60 * 60 * 24 * 365;

        public string StringValue { get; set; }

        public static string ExprValueString (long value) {
            long start = DateTimeOffset.Now.ToUnixTimeSeconds() - ONE_YEAR;
            long end = start + (2 * ONE_YEAR);
            if (value > start && value < end) {
                DateTime dt = ConvertFromUnixTimestamp(value).ToLocalTime();
                if (dt.Day == DateTime.Now.Day + 1) {
                    return dt.ToShortTimeString() + " tomorrow";
                } else if (dt.Day == DateTime.Now.Day - 1) {
                    return dt.ToShortTimeString() + " yesterday";
                } else
                    return dt.ToShortTimeString();
            } else {
                return value.ToString();
            }
        }

        public string ValueString {
            get {
                if (Error != null) return Error;
                if (Value is double.NegativeInfinity) {
                    return StringValue;
                }
                long start = DateTimeOffset.Now.ToUnixTimeSeconds() - ONE_YEAR;
                long end = start + (2 * ONE_YEAR);
                if (Value > start && Value < end) {
                    DateTime dt = ConvertFromUnixTimestamp(Value).ToLocalTime();
                    if (dt.Day == DateTime.Now.Day + 1) {
                        return dt.ToShortTimeString() + " tomorrow";
                    } else if (dt.Day == DateTime.Now.Day - 1) {
                        return dt.ToShortTimeString() + " yesterday";
                    } else
                        return dt.ToShortTimeString();
                } else {
                    return Value.ToString();
                }
            }
            set { }
        }

        public bool IsExpression { get; set; } = false;

        public bool IsSyntaxError { get; set; } = false;

        public bool IsAnnotated {
            get => IsExpression || Error != null;
            set { }
        }

        // References are the parsed tokens used in the Expr
        public HashSet<string> References { get; set; } = new HashSet<string>();

        // Resolved are the Symbol's that have been found (from the References)
        public Dictionary<string, Symbol> Resolved = new Dictionary<string, Symbol>();

        // Parameters are NCalc Parameters used in the call to NCalc.Evaluate()
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();

        public static DateTime ConvertFromUnixTimestamp(double timestamp) {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return origin.AddSeconds(timestamp);
        }
        public long UnixTimeNow() {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            return (long)timeSpan.TotalSeconds;
        }

        public static Random RNG = new Random();

        public bool Dirty { get; set; } = false;

        public bool Volatile { get; set; } = false;
        public bool ImageVolatile { get; set; } = false;
        public bool GlobalVolatile { get; set; } = false;

        public void DebugWrite() {
            Debug.WriteLine("* Expression " + Expression + " evaluated to " + ((Error != null) ? Error : Value) + " (in " + (Symbol != null ? Symbol : SequenceEntity) + ")");
        }

        public void ReferenceRemoved(Symbol sym) {
            // A definition we use was removed
            string identifier = sym.Identifier;
            Parameters.Remove(identifier);
            Resolved.Remove(identifier);
            Evaluate();
        }

        public static string NOT_DEFINED = "Parameter was not defined (Parameter";

        private void AddParameter(string reference, object value) {
            Parameters.Add(reference, value);
        }


        private void Resolve(string reference, Symbol sym) {
            Parameters.Remove(reference);
            Resolved.Remove(reference);
            if (sym.Expr.Error == null) {
                Resolved.Add(reference, sym);
                if (sym.Expr.Value == double.NegativeInfinity) {
                    AddParameter(reference, sym.Expr.StringValue);
                } else if (!Double.IsNaN(sym.Expr.Value)) {
                    AddParameter(reference, sym.Expr.Value);
                }
            }
        }

       public void Refresh() {
            Parameters.Clear();
            Resolved.Clear();
            Evaluate();
        }

        private void AddError (string s) {
            if (Error == null) {
                Error = s;
            } else {
                Error = Error + "; " + s;
            }
        }

        public void Evaluate() {
            Evaluate(false);
        }

        public void Evaluate(bool validateOnly) {
        }

        //private bool LOCK_ERROR = false;

        public void Validate(IList<string> issues) {
        }

        public void Validate() {
            Validate(null);
        }

        public void NotNegative(Expr expr) {
            if (expr.Value < 0) {
                expr.Error = "Must not be negative";
            }
        }

        public static void AddExprIssues (IList<string> issues, params Expr[] exprs) {
        }

        public override string ToString() {
            string id = Symbol != null ? Symbol.Identifier : SequenceEntity.Name;
            if (Error != null) {
                return $"'{Expression}' in {id}, References: {References.Count}, Error: {Error}";
            } else if (Expression.Length == 0) {
                return "None";
            }
            return $"Expression: {Expression} in {id}, References: {References.Count}, Value: {Value}";
        }
    }
}

