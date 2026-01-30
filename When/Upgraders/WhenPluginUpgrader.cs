using Newtonsoft.Json.Linq;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.SequenceItem.Expressions;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Serialization;
using NINA.Sequencer.Trigger;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "WhenPlugin Upgrader")]
    [Export(typeof(ISequenceEntityUpgrader))]

    public class WhenPluginUpgrader : ISequenceEntityUpgrader {

        public string Name { get; set; }

        public string AssemblyName { get; set; }

        public static ISequencerFactory Factory { get; set; }

        public bool CanUpgrade(SequenceUpgradeContext context, SequenceUpgradeStage stage) {
            return true;
        }

        public object? Upgrade(SequenceUpgradeContext context, SequenceUpgradeStage stage, object? current) {
            var typeString = context.OriginalTypeString ?? context.RequestedType.FullName;
            
            switch (stage) {
                case SequenceUpgradeStage.AfterCreate: {
                        PreUpgradeInstruction(typeString, context.Json);
                        break;
                    }
                case SequenceUpgradeStage.AfterPopulate: {
                        UpgradeInstruction(current, context);
                        break;
                    }
            }

            return current;
        }

        public static void PreUpgradeInstruction(string originalType, JObject jObject) {
            switch (originalType) {
                case "WhenPlugin.When.GetArray, WhenPlugin":
                case "WhenPlugin.When.PutArray, WhenPlugin":
                    if (jObject.ContainsKey("NameExpr")) {
                        jObject.Add("iNameExpr", jObject["NameExpr"]);
                        jObject.Remove("NameExpr");
                        jObject.Add("iIExpr", jObject["IExpr"]);
                        jObject.Remove("IExpr");
                        jObject.Add("iVExpr", jObject["VExpr"]);
                        jObject.Remove("VExpr");
                    }
                    break;
                case "WhenPlugin.When.InitializeArray, WhenPlugin":
                case "WhenPlugin.When.ForEachInArray, WhenPlugin":
                    if (jObject.ContainsKey("NameExpr")) {
                        jObject.Add("iNameExpr", jObject["NameExpr"]);
                        jObject.Remove("NameExpr");
                    }
                    break;
                case "WhenPlugin.When.AddImagePattern, WhenPlugin":
                    if (jObject.ContainsKey("Expr")) {
                        jObject.Add("iExpr", jObject["Expr"]);
                        jObject.Remove("Expr");
                    }
                    break;
                case "WhenPlugin.When.RepeatUntilAllSucceed, WhenPlugin":
                    if (jObject.ContainsKey("WaitExpr")) {
                        jObject.Add("iWaitExpr", jObject["WaitExpr"]);
                        jObject.Remove("WaitExpr");
                    }
                    break;
                case "WhenPlugin.When.ConditionalTrigger, WhenPlugin":
                    if (jObject.ContainsKey("IfExpr")) {
                        jObject.Add("iIfExpr", jObject["IfExpr"]);
                        jObject.Remove("IfExpr");
                    }
                    break;
            }
        }

        private static T CreateNewItem<T>(ISequenceItem item) {
            var method = Factory.GetType().GetMethod(nameof(Factory.GetItem)).MakeGenericMethod(new Type[] { typeof(T) });
            T newObj = (T)method.Invoke(Factory, null);
            ISequenceItem newItem = (ISequenceItem)newObj;
            newItem.Name += " [" + item.Name + " =>NINA";
            newItem.Attempts = item.Attempts;
            newItem.ErrorBehavior = item.ErrorBehavior;
            return newObj;
        }
        private static T CreateNewContainer<T>(string oldName) {
            var method = Factory.GetType().GetMethod(nameof(Factory.GetContainer)).MakeGenericMethod(new Type[] { typeof(T) });
            T newObj = (T)method.Invoke(Factory, null);
            ((ISequenceContainer)newObj).Name += " [" + oldName + " =>NINA";
            return newObj;
        }

        private static T CreateNewCondition<T>(string oldName) {
            var method = Factory.GetType().GetMethod(nameof(Factory.GetCondition)).MakeGenericMethod(new Type[] { typeof(T) });
            T newObj = (T)method.Invoke(Factory, null);
            ((ISequenceCondition)newObj).Name += " [" + oldName + " =>NINA";
            return newObj;
        }

        private static T CreateNewTrigger<T>(string oldName) {
            var method = Factory.GetType().GetMethod(nameof(Factory.GetTrigger)).MakeGenericMethod(new Type[] { typeof(T) });
            T newObj = (T)method.Invoke(Factory, null);
            ((ISequenceTrigger)newObj).Name += " [" + oldName + " =>NINA";
            return newObj;
        }

         private static string GetExpr(Type t, ISequenceEntity item, string propertyName) {
            PropertyInfo pi = t.GetProperty(propertyName);
            object expr = pi.GetValue(item);
            pi = expr.GetType().GetProperty("Expression");
            return UpdateSymbols(pi.GetValue(expr) as string);
        }

        private static void PutExpr(Type t, ISequenceEntity item, string propertyName, string value) {
            PropertyInfo pi = t.GetProperty(propertyName);
            Expression expr = pi.GetValue(item) as Expression;
            expr.Definition = value;
        }
        private static readonly IDictionary<string, object> EmptyReferences = new Dictionary<string, object>();

        private static readonly IReadOnlyDictionary<string, string> SymbolUpgradeMap = new Dictionary<string, string> {
            {"TIME", "ApplicationUptime"},
            {"RightAscension", "RightAscensionJ2000" },
            {"Declination", "DeclinationJ2000" },
            {"FocuserPosition", "Focuser_Position"},
            {"FocuserTemperature", "Focuser_Temperature" },
            {"SensorTemp", "Camera_Temperature" },
            {"camera__PixelSize", "Camera_PixelSize" },
            {"camera__XSize", "Camera_XSize" },
            {"camera__YSize", "Camera_YSize" },
            {"camera__CoolerPower", "Camera_CoolerPower" },
            {"camera__CoolerOn", "Camera_CoolerOn" },
            {"RotatorPosition", "Rotator_Position" }


        };
        private static string GetUpgradedSymbol(string oldSymbol) {
            return SymbolUpgradeMap.TryGetValue(oldSymbol, out var newSymbol) ? newSymbol : null;
        }

        private static string UpdateSymbols(string exprDef) {
            if (string.IsNullOrEmpty(exprDef)) {
                return exprDef;
            }
            NCalc.Expression e = new NCalc.Expression(exprDef, NCalc.ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
            e.Parameters = EmptyReferences;
            try {
                e.Evaluate();
            } catch (NCalc.Exceptions.NCalcParserException) {
                return exprDef;
            } catch (Exception) {
                // That's ok, because we just want to find the symbol references
            }
            foreach (var p in e.GetParameterNames()) {
                // See if the symbol needs to be updated
                string newSymbol = GetUpgradedSymbol(p);
                // Update in the exprDef
                if (newSymbol != null) {
                    exprDef = exprDef.Replace(p, newSymbol);
                    Logger.Info($"Updated symbol '{p}' to '{newSymbol}' in expression '{exprDef}'");
                }
            }
            return exprDef;
        }

        private static void UpdateIfContainer(ISequenceItem item) {
            ISequenceContainer instructions = item.GetType().GetProperty("Instructions").GetValue(item, null) as ISequenceContainer;
            if (instructions != null && instructions.Items.Count > 0) {
                ISequenceContainer updated = item as ISequenceContainer;
                updated.Items.Clear();
                for (int i = 0; i < instructions.Items.Count; i++) {
                    ISequenceItem oldItem = instructions.Items[i];
                    ISequenceItem newItem = oldItem.Clone() as ISequenceItem;
                    updated.Add(newItem); // Temporarily add clone to expand collection
                    newItem.AttachNewParent(updated);
                }
                instructions.Items.Clear();
            }

            PropertyInfo cp = item.GetType().GetProperty("Condition");
            if (cp != null) {
                ISequenceContainer condition = cp.GetValue(item, null) as ISequenceContainer;
                if (condition != null && condition.Items.Count > 0) {
                    ISequenceItem oldItem = condition.Items[0];
                    ISequenceItem newItem = oldItem.Clone() as ISequenceItem;
                    // CheckInstruction
                    PropertyInfo cip = item.GetType().GetProperty("CheckInstruction");
                    if (cip != null) {
                        cip.SetValue(item, newItem);
                    }
                    newItem.AttachNewParent((ISequenceContainer)item);
                    condition.Items.Clear();
                }
            }
        }

        private static void UpdateIfThenElse(ISequenceItem item) {
            ISequenceContainer instructions = item.GetType().GetProperty("Instructions").GetValue(item, null) as ISequenceContainer;
            if (instructions != null && instructions.Items.Count > 0) {
                ISequenceContainer thenBranch = item.GetType().GetProperty("ThenBranch").GetValue(item, null) as ISequenceContainer;
                thenBranch.Items.Clear();
                for (int i = 0; i < instructions.Items.Count; i++) {
                    ISequenceItem oldItem = instructions.Items[i];
                    ISequenceItem newItem = oldItem.Clone() as ISequenceItem;
                    thenBranch.Add(newItem); // Temporarily add clone to expand collection
                    newItem.AttachNewParent(thenBranch);
                }
                instructions.Items.Clear();
            }

            ISequenceContainer elseInstructions = item.GetType().GetProperty("ElseInstructions").GetValue(item, null) as ISequenceContainer;
            if (elseInstructions != null && elseInstructions.Items.Count > 0) {
                ISequenceContainer elseBranch = item.GetType().GetProperty("ElseBranch").GetValue(item, null) as ISequenceContainer;
                elseBranch.Items.Clear();
                for (int i = 0; i < elseInstructions.Items.Count; i++) {
                    ISequenceItem oldItem = elseInstructions.Items[i];
                    ISequenceItem newItem = oldItem.Clone() as ISequenceItem;
                    elseBranch.Add(newItem); // Temporarily add clone to expand collection
                    newItem.AttachNewParent(elseBranch);
                }
                elseInstructions.Items.Clear();
            }
        }

        public static object UpgradeInstruction(object obj, SequenceUpgradeContext context) {
            JObject jObject = context.Json;
            Factory = context.Factory;
            try {
                ISequenceItem item = obj as ISequenceItem;
                ISequenceCondition condition = obj as ISequenceCondition;
                ISequenceTrigger trigger = obj as ISequenceTrigger;
                if (item == null && condition == null && trigger == null) {
                    return obj;
                }
                Type t;
                if (condition != null) {
                    t = condition.GetType();
                } else if (trigger != null) {
                    t = trigger.GetType();
                } else {
                    t = item.GetType();
                }

                //Logger.Info("Powerups Upgrade: " + t);
                switch (t.Name) {
                    // The following are updates from Powerups + instructions to NINA instructions
                    case "ExternalScript": {
                            NINA.Sequencer.SequenceItem.Utility.ExternalScript newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Utility.ExternalScript>(item);
                            PropertyInfo pi = t.GetProperty("Script");
                            string script = pi.GetValue(item) as string;

                            // Extract and update each expression in braces
                            if (!string.IsNullOrEmpty(script)) {
                                script = System.Text.RegularExpressions.Regex.Replace(
                                    script,
                                    @"\{([^}]+)\}",
                                    match => {
                                        string expr = match.Groups[1].Value;
                                        string updated = UpdateSymbols(expr);
                                        return "{" + updated + "}";
                                    }
                                );
                            }

                            newObj.Script = script;
                            return newObj;
                        }

                    case "DitherAfterExposures": {
                            NINA.Sequencer.Trigger.Guider.DitherAfterExposures newObj = CreateNewTrigger<NINA.Sequencer.Trigger.Guider.DitherAfterExposures>(trigger.Name);
                            newObj.AfterExposuresExpression.Definition = GetExpr(t, trigger, "AfterExpr");
                            newObj.AttachNewParent(trigger.Parent);
                            return newObj;
                        }
                    case "CoolCamera": {
                            NINA.Sequencer.SequenceItem.Camera.CoolCamera newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Camera.CoolCamera>(item);
                            newObj.TemperatureExpression.Definition = GetExpr(t, item, "TempExpr");
                            newObj.DurationExpression.Definition = GetExpr(t, item, "DurExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "MoveRotatorMechanical": {
                            NINA.Sequencer.SequenceItem.Rotator.MoveRotatorMechanical newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Rotator.MoveRotatorMechanical>(item);
                            newObj.MechanicalPositionExpression.Definition = GetExpr(t, item, "RExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SetSwitchValue": {
                            NINA.Sequencer.SequenceItem.Switch.SetSwitchValue newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Switch.SetSwitchValue>(item);
                            newObj.ValueExpression.Definition = GetExpr(t, item, "ValueExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SlewToAltAz": {
                            NINA.Sequencer.SequenceItem.Telescope.SlewScopeToAltAz newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Telescope.SlewScopeToAltAz>(item);
                            newObj.AzExpression.Definition = GetExpr(t, item, "AzExpr");
                            newObj.AltExpression.Definition = GetExpr(t, item, "AltExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SlewToRADec": {
                            NINA.Sequencer.SequenceItem.Telescope.SlewScopeToRaDec newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Telescope.SlewScopeToRaDec>(item);
                            newObj.RaExpression.Definition = GetExpr(t, item, "RAExpr");
                            newObj.DecExpression.Definition = GetExpr(t, item, "DecExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "Center": {
                            NINA.Sequencer.SequenceItem.Platesolving.Center newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Platesolving.Center>(item);
                            newObj.RaExpression.Definition = GetExpr(t, item, "RAExpr");
                            newObj.DecExpression.Definition = GetExpr(t, item, "DecExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "MoveFocuserAbsolute": {
                            NINA.Sequencer.SequenceItem.Focuser.MoveFocuserAbsolute newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Focuser.MoveFocuserAbsolute>(item);
                            newObj.PositionExpression.Definition = GetExpr(t, item, "PExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "MoveFocuserRelative": {
                            NINA.Sequencer.SequenceItem.Focuser.MoveFocuserRelative newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Focuser.MoveFocuserRelative>(item);
                            newObj.RelativePositionExpression.Definition = GetExpr(t, item, "PExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SlewDomeAzimuth": {
                            NINA.Sequencer.SequenceItem.Dome.SlewDomeAzimuth newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Dome.SlewDomeAzimuth>(item);
                            newObj.AzimuthDegreesExpression.Definition = GetExpr(t, item, "AzExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "WaitForTimeSpan": {
                            NINA.Sequencer.SequenceItem.Utility.WaitForTimeSpan newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Utility.WaitForTimeSpan>(item);
                            newObj.TimeExpression.Definition = GetExpr(t, item, "WaitExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "LoopWhile": {
                            NINA.Sequencer.Conditions.LoopWhile newObj = CreateNewCondition<NINA.Sequencer.Conditions.LoopWhile>(condition.Name);
                            newObj.PredicateExpression.Definition = GetExpr(t, condition, "PredicateExpr");
                            newObj.AttachNewParent(condition.Parent);
                            return newObj;
                        }
                    case "WaitUntil": {
                            NINA.Sequencer.SequenceItem.Utility.WaitUntil newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Utility.WaitUntil>(item);
                            newObj.PredicateExpression.Definition = GetExpr(t, item, "PredicateExpr");
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SwitchFilter": {
                            NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter newObj = CreateNewItem<NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter>(item);
                            PropertyInfo pi = t.GetProperty("FilterExpr");
                            newObj.ComboBoxText = UpdateSymbols(pi.GetValue(item) as string);
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SmartExposure": {
                            NINA.Sequencer.SequenceItem.Imaging.SmartExposure newObj = CreateNewContainer<NINA.Sequencer.SequenceItem.Imaging.SmartExposure>(item.Name);
                            ((LoopCondition)newObj.Conditions[0]).IterationsExpression.Definition = GetExpr(t, item, "IterExpr");
                            ISequenceContainer smart = item as ISequenceContainer;
                            NINA.Sequencer.SequenceItem.Imaging.TakeExposure oldTe = (NINA.Sequencer.SequenceItem.Imaging.TakeExposure)smart.Items[1];
                            NINA.Sequencer.SequenceItem.Imaging.TakeExposure newTe = (NINA.Sequencer.SequenceItem.Imaging.TakeExposure)newObj.Items[1];
                            newTe.ExposureTimeExpression.Definition = UpdateSymbols(oldTe?.ExposureTimeExpression.Definition);
                            newTe.GainExpression.Definition = UpdateSymbols(oldTe?.GainExpression.Definition);
                            newTe.OffsetExpression.Definition = UpdateSymbols(oldTe?.OffsetExpression.Definition);
                            newTe.Binning = oldTe?.Binning;
                            newTe.ImageType = oldTe?.ImageType;
                            NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter oldSf = (NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter)smart.Items[0];
                            NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter newSf = (NINA.Sequencer.SequenceItem.FilterWheel.SwitchFilter)newObj.Items[0];
                            newSf.ComboBoxText = oldSf.ComboBoxText;
                            // Dither?
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "TakeManyExposures": {
                            NINA.Sequencer.SequenceItem.Imaging.TakeManyExposures newObj = CreateNewContainer<NINA.Sequencer.SequenceItem.Imaging.TakeManyExposures>(item.Name);
                            ((LoopCondition)newObj.Conditions[0]).IterationsExpression.Definition = GetExpr(t, item, "IterExpr");
                            ISequenceContainer smart = item as ISequenceContainer;
                            NINA.Sequencer.SequenceItem.Imaging.TakeExposure oldTe = (NINA.Sequencer.SequenceItem.Imaging.TakeExposure)smart.Items[0];
                            NINA.Sequencer.SequenceItem.Imaging.TakeExposure newTe = (NINA.Sequencer.SequenceItem.Imaging.TakeExposure)newObj.Items[0];
                            newTe.ExposureTimeExpression.Definition = UpdateSymbols(oldTe?.ExposureTimeExpression.Definition);
                            newTe.GainExpression.Definition = UpdateSymbols(oldTe?.GainExpression.Definition);
                            newTe.OffsetExpression.Definition = UpdateSymbols(oldTe?.OffsetExpression.Definition);
                            newTe.Binning = oldTe?.Binning;
                            newTe.ImageType = oldTe?.ImageType;
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "TakeExposure": {
                            NINA.Sequencer.SequenceItem.Imaging.TakeExposure newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Imaging.TakeExposure>(item);
                            newObj.ExposureTimeExpression.Definition = GetExpr(t, item, "EExpr");
                            newObj.GainExpression.Definition = GetExpr(t, item, "GExpr");
                            newObj.OffsetExpression.Definition = GetExpr(t, item, "OExpr");

                            PropertyInfo pi = t.GetProperty("Binning");
                            newObj.Binning = (BinningMode)pi.GetValue(item);
                            pi = t.GetProperty("ImageType");
                            newObj.ImageType = (string)pi.GetValue(item);
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SetConstant": {
                            // Local Constants become Scoped Variables
                            Variable newObj = CreateNewItem<Variable>(item);
                            PropertyInfo pi = t.GetProperty("Definition");
                            newObj.OriginalExpr.Definition = UpdateSymbols(pi.GetValue(item) as string);
                            pi = t.GetProperty("Identifier");
                            newObj.Identifier = (string)pi.GetValue(item);
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SetGlobalVariable": {
                            GlobalVariable newObj = CreateNewItem<GlobalVariable>(item);
                            PropertyInfo pi = t.GetProperty("OriginalDefinition");
                            newObj.OriginalExpr.Definition = UpdateSymbols(pi.GetValue(item) as string);
                            pi = t.GetProperty("Identifier");
                            newObj.Identifier = (string)pi.GetValue(item);
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "SetVariable": {
                            Variable newObj = CreateNewItem<Variable>(item);
                            PropertyInfo pi = t.GetProperty("OriginalDefinition");
                            newObj.OriginalExpr.Definition = UpdateSymbols(pi.GetValue(item) as string);
                            pi = t.GetProperty("Identifier");
                            newObj.Identifier = (string)pi.GetValue(item);
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "ResetVariable": {
                            NINA.Sequencer.SequenceItem.Expressions.ResetVariable newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Expressions.ResetVariable>(item);
                            PropertyInfo pi = t.GetProperty("Variable");
                            newObj.Variable = (string)pi.GetValue(item);
                            pi = t.GetProperty("Expr");
                            object expr = pi.GetValue(item);
                            pi = expr.GetType().GetProperty("Expression");
                            string exp = UpdateSymbols(pi.GetValue(expr) as string);
                            if (exp != null) {
                                newObj.Expr.Definition = exp;
                            }
                            newObj.AttachNewParent(item.Parent);
                            return newObj;
                        }
                    case "ResetVariableToDate": {
                            NINA.Sequencer.SequenceItem.Expressions.ResetVariableToDate newObj = CreateNewItem<NINA.Sequencer.SequenceItem.Expressions.ResetVariableToDate>(item);
                            PropertyInfo pi = t.GetProperty("Variable");
                            newObj.Variable = (string)pi.GetValue(item);
                            newObj.AttachNewParent(item.Parent);
                            pi = t.GetProperty("Hours");
                            newObj.Hours = (int)(pi.GetValue(item) as Int32?);
                            pi = t.GetProperty("Minutes");
                            newObj.Minutes = (int)(pi.GetValue(item) as Int32?);
                            pi = t.GetProperty("Seconds");
                            newObj.Seconds = (int)(pi.GetValue(item) as Int32?);
                            return newObj;
                        }

                    // The following are updates from Powerups 3.2 to Powerups 4
                    // Primarily this is changing from Powerups Expr class to NINA Expression class
                    case "AddImagePattern": {
                            if (jObject.ContainsKey("iExpr")) {
                                PutExpr(t, item, "ExprExpression", GetExpr(t, item, "iExpr"));
                                item.Name += " [Upgraded";
                            }
                            return obj;
                        }

                    case "RepeatUntilAllSucceed": {
                            if (jObject.ContainsKey("iWaitExpr")) {
                                PutExpr(t, item, "WaitExpression", GetExpr(t, item, "iWaitExpr"));
                                item.Name += " [Upgraded";
                            }
                            UpdateIfContainer(item);
                            return obj;
                        }

                    case "OnceSafe":
                        UpdateIfContainer(item);
                        return obj;

                    case "InitializeArray":
                    case "ForEachInArray":
                    case "GetArray":
                    case "PutArray":
                        if (jObject.ContainsKey("iNameExpr")) {
                            PutExpr(t, item, "NameExprExpression", GetExpr(t, item, "iNameExpr"));
                            if (t.Name == "GetArray" || t.Name == "PutArray") {
                                PutExpr(t, item, "IExprExpression", GetExpr(t, item, "iIExpr"));
                                PutExpr(t, item, "VExprExpression", GetExpr(t, item, "iVExpr"));
                            }
                            item.Name += " [Upgraded";
                        }
                        return obj;

                    case "ConditionalTrigger":
                        if (jObject.ContainsKey("iIfExpr")) {
                            PutExpr(t, trigger, "PredicateExpression", GetExpr(t, trigger, "iIfExpr"));
                            trigger.Name += " [Upgraded";
                        }
                        return obj;

                    case "IfConstant":
                        Expression e = (Expression)item.GetType().GetProperty("PredicateExpression").GetValue(item, null);
                        if (jObject["IfExpr"] != null) {
                            e.Definition = UpdateSymbols(jObject["IfExpr"]["Expression"].ToString());
                            item.Name += " [Upgraded";
                        }
                        UpdateIfContainer(item);
                        break;

                    case "IfThenElse":
                        Expression e1 = (Expression)item.GetType().GetProperty("PredicateExpression").GetValue(item, null);
                        if (jObject["IfExpr"] != null) {
                            e1.Definition = UpdateSymbols(jObject["IfExpr"]["Expression"].ToString());
                            item.Name += " [Upgraded";
                        }
                        UpdateIfThenElse(item);
                        break;

                    case "WhenSwitch":
                        Expression e2 = (Expression)trigger.GetType().GetProperty("PredicateExpression").GetValue(trigger, null);
                        if (jObject["IfExpr"] != null) {
                            e2.Definition = UpdateSymbols(jObject["IfExpr"]["Expression"].ToString());
                            trigger.Name += " [Upgraded";  // Also fixed: should be trigger.Name, not item.Name
                        }
                        break;

                    case "GSSend":
                        ISequenceContainer c = item.GetType().GetProperty("Condition").GetValue(item, null) as ISequenceContainer;
                        ISequenceContainer gss = item as ISequenceContainer;
                        if (c != null) {
                            gss.Items.Clear();
                            for (int j = 0; j < c.Items.Count; j++) {
                                ISequenceItem oldItem = c.Items[j];
                                ISequenceItem newItem = oldItem.Clone() as ISequenceItem;
                                gss.Add(newItem);
                            }
                            c.Items.Clear();
                        }
                        break;

                    case "IfFailed":
                    case "IfTimeout":
                        UpdateIfContainer(item);
                        break;

                    case "DIYTrigger":
                        ISequenceContainer triggerRunner = trigger.GetType().GetProperty("TriggerRunner").GetValue(trigger, null) as ISequenceContainer;
                        ISequenceContainer triggerInstructions = trigger.GetType().GetProperty("Instructions").GetValue(trigger, null) as ISequenceContainer;
                        if (triggerRunner != null && triggerInstructions != null && triggerRunner.Items.Count > 0) {
                            triggerInstructions.Items.Clear();
                            ISequenceContainer instructions = triggerRunner.Items as ISequenceContainer;
                            for (int i = 0; i < triggerRunner.Items.Count; i++) {
                                ISequenceItem oldItem = triggerRunner.Items[i];
                                ISequenceItem newItem = oldItem.Clone() as ISequenceItem;
                                triggerInstructions.Add(newItem); // Temporarily add clone to expand collection
                                newItem.AttachNewParent(triggerInstructions);
                            }
                            triggerRunner.Items.Clear();
                        }
                        break;

                    // Unchanged (no Expressions)
                    case "IfContainer":
                    case "FlipRotator":
                    case "TemplateContainer":
                    case "DoFlip":
                    case "DIYMeridianFlipTrigger":
                    case "PassMeridian":
                    case "RotateImage":
                    case "WaitIndefinitely":
                    case "Breakpoint":
                    case "EndSequence":
                    case "EndInstructionSet":
                    case "WhenUnsafe":
                    case "InterruptTrigger":
                    case "AutofocusTrigger":
                    case "LogThis":
                    case "TemplateByReference":
                    case "SequentialContainer":   // For upgrading CVContainer
                    case "ForEachList":
                        break;

                    case "SafeTrigger":
                        trigger?.Name += " *USE CONDITIONAL TRIGGER WITH IsSafe";
                        break;

                    default: {
                            item?.Name += " *NOT AVAILABLE IN POWERUPS 4";
                            condition?.Name += " *NOT AVAILABLE IN POWERUPS 4";
                            trigger?.Name += " *NOT AVAILABLE IN POWERUPS 4";
                            break;
                        }
                }
                return obj;
            } catch (Exception ex) {
                Logger.Error(ex);
                return obj;
            }
        }
    }
}
