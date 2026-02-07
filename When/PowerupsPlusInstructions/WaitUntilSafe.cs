#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.ViewModel.Sequencer;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace WhenPlugin.When {

    public class WaitUntilSafe : SequenceItem, IValidatable {
        private ISafetyMonitorMediator safetyMonitorMediator;
        protected ISequenceMediator sequenceMediator;
        private IProfileService profileService;

        [ImportingConstructor]
        public WaitUntilSafe(ISafetyMonitorMediator safetyMonitorMediator, ISequenceMediator seqMediator, IProfileService pService) {
            this.safetyMonitorMediator = safetyMonitorMediator;
            this.sequenceMediator = seqMediator;
            this.profileService = pService;
        }

        private WaitUntilSafe(WaitUntilSafe cloneMe) : this(cloneMe.safetyMonitorMediator, cloneMe.sequenceMediator, cloneMe.profileService) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new WaitUntilSafe(this);
        }

        public WaitUntilSafe() { }

        private bool isSafe;

        public bool IsSafe {
            get => isSafe;
            private set {
                isSafe = value;
                RaisePropertyChanged();
            }
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public TimeSpan WaitInterval { get; set; } = TimeSpan.FromSeconds(5);

        public bool Validate() {
            return true;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(WaitUntilSafe)}";
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
        }
    }
}