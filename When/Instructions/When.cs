#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Sequencer.Validations;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using NINA.Core.Enum;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Trigger;
using Newtonsoft.Json;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Core.Model;
using System.Diagnostics;
using System.Threading.Tasks;
using NINA.Sequencer.SequenceItem.Expressions;
using NINA.Sequencer.Generators;
using NINA.Sequencer.Logic;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "When")]
    [ExportMetadata("Description", "Runs a customizable set of instructions when the specified Expression is true.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Powerups (Triggers)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    [UsesExpressions]

    public partial class WhenSwitch : When, IValidatable, ITrueFalse {

        [ImportingConstructor]
        public WhenSwitch(ISafetyMonitorMediator safetyMediator, ISequenceMediator sequenceMediator, IApplicationStatusMediator applicationStatusMediator, ISwitchMediator switchMediator,
                IWeatherDataMediator weatherMediator, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator)
            : base(safetyMediator, sequenceMediator, applicationStatusMediator, switchMediator, weatherMediator, cameraMediator, telescopeMediator) {
        }

        public ICameraConsumer cameraConsumer {  get; set; } 

        protected WhenSwitch(WhenSwitch cloneMe) : base(cloneMe.safetyMediator, cloneMe.sequenceMediator, cloneMe.applicationStatusMediator, cloneMe.switchMediator, cloneMe.weatherMediator, cloneMe.cameraMediator,
                cloneMe.telescopeMediator) {
            if (cloneMe != null) {
                CopyMetaData(cloneMe);
                Instructions = (IfContainer)cloneMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = cloneMe.Name;
                Instructions.Icon = cloneMe.Icon;
                OnceOnly = cloneMe.OnceOnly;
                Interrupt = cloneMe.Interrupt;
            }
        }

        public bool Disabled { get; set; } = false;

        private bool iOnceOnly = false;

        [JsonProperty]
        public bool OnceOnly {
            get => iOnceOnly;
            set {
                iOnceOnly = value;
                RaisePropertyChanged();
            }
        }

        [IsExpression]
        private string predicate;

        public string ValidateConstant(double temp) {
            if ((int)temp == 0) {
                return "False";
            } else if ((int)temp == 1) {
                return "True";
            }
            return string.Empty;
        }

        protected bool IsActive() {
            return ItemUtility.IsInRootContainer(Parent) && Parent.Status == SequenceEntityStatus.RUNNING && Status != SequenceEntityStatus.DISABLED;
        }

        public override bool Check() {
            if (Disabled) {
                Logger.Trace("Check = TRUE (Disabled)");
                return true;
            }

            PredicateExpression.Evaluate();

            if (!string.Equals(PredicateExpression.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (PredicateExpression.Error == null)) {
                Logger.Trace("Check = FALSE");
                return false;
            }
            Logger.Trace("Check = TRUE");
            return true;
        }

        public override string ToString() {
            return $"Trigger: {nameof(When)} Expression: {PredicateExpression.Definition} Value: {PredicateExpression.ValueString}";
        }

        public override bool AllowMultiplePerSet => true;

        public IList<string> Switches { get; set; } = null;
        public new bool Validate() {

            CommonValidate();

            var i = new List<string>();
            Expression.ValidateExpressions(i, PredicateExpression);

            Issues = i;
            return i.Count == 0;
        }
    }
}
