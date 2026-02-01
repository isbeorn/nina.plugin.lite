#region "copyright"

/*
    Copyright © 2016 - 2024 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WhenPlugin.When {

    [JsonObject(MemberSerialization.OptIn)]
    public class WaitForTimeSpan : SequenceItem, IValidatable {

        [ImportingConstructor]
        public WaitForTimeSpan() {
            WaitExpr = new Expr(this, "");
        }

        private WaitForTimeSpan(WaitForTimeSpan cloneMe) : base(cloneMe) {
            WaitExpr = new Expr(this, cloneMe.WaitExpr.Expression);
            WaitExpr.Setter = ValidateTime;
            WaitExpr.Default = 1;
        }

        public override object Clone() {
            return new WaitForTimeSpan(this) {
            };
        }

        public void ValidateTime(Expr expr) {
            if (expr.Value < 0) {
                expr.Error = "Must be greater than or equal to zero";
            }
        }


        [JsonProperty]
        public int Time {
            get => 0;
            set {
                if (WaitExpr == null) {
                    WaitExpr = new Expr(this, "");
                }
                WaitExpr.Expression = value.ToString();
            }
        }
        private Expr _WaitExpr;
        [JsonProperty]
        public Expr WaitExpr {
            get => _WaitExpr;
            set {
                _WaitExpr = value;
                RaisePropertyChanged();
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return Task.CompletedTask;
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            Validate();
        }

        public IList<string> Issues { get; set; }

        public bool Validate() {
            return true;
        }

        public override TimeSpan GetEstimatedDuration() {
            return TimeSpan.FromSeconds(WaitExpr.Value);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(WaitForTimeSpan)}, Time: {WaitExpr.ValueString}s";
        }
    }
}