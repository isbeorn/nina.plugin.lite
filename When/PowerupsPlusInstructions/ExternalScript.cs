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
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Sequencer.SequenceItem;
using System.Text.RegularExpressions;
using NINA.Core.Utility;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Logic;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "External Script +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Utility_ExternalScript_Description")]
    [ExportMetadata("Icon", "ScriptSVG")]
    [ExportMetadata("Category", "Powerups (Deprecated)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ExternalScript : SequenceItem, IValidatable {

        public ExternalScript() {
        }

        private ExternalScript(ExternalScript cloneMe) : this() {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new ExternalScript(this) {
                Script = Script
            };
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private string script;

        [JsonProperty]
        public string Script {
            get => script;
            set {
                script = value;
            }
        }

        public string ProcessedScriptAnnotated {
            get { return "As processed: " + iProcessedScript; }
            set { }
        }


        private string iProcessedScript;
        public string ProcessedScript {
            get {
                string value = Script;
                return value;
            }
        }

        public string ProcessedScriptError {  get; set; } = null;

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
        }

        public bool Validate() {
            var i = new List<string>();
            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ExternalScript)}, Script: {Script} ProcessedScript: {ProcessedScript}";
        }
    }
}
