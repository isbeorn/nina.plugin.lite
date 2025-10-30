#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NINA.Equipment.Model;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Core.Locale;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Sequencer.Utility;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Sequencer.SequenceItem.FlatDevice;
using NINA.Sequencer.SequenceItem;
using NINA.Equipment.Equipment.MyFilterWheel;

namespace WhenPlugin.When {

    [ExportMetadata("Name", "Trained Dark Exposure +")]
    [ExportMetadata("Description", "Lbl_SequenceItem_FlatDevice_TrainedDarkFlatExposure_Description")]
    [ExportMetadata("Icon", "BrainBulbSVG")]
    [ExportMetadata("Category", "Powerups (Deprecated)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]
    public class TrainedDarkFlatExposure : SequentialContainer, IImmutableContainer {

        [OnDeserializing]
        public void OnDeserializing(StreamingContext context) {
            this.Items.Clear();
            this.Conditions.Clear();
            this.Triggers.Clear();
        }

        public IProfileService ProfileService;
        public IFilterWheelMediator FilterWheelMediator;
        private ICameraMediator cameraMediator;
        private bool keepPanelClosed;

        [ImportingConstructor]
        public TrainedDarkFlatExposure(IProfileService profileService, ICameraMediator cameraMediator, IImagingMediator imagingMediator, IImageSaveMediator imageSaveMediator, IImageHistoryVM imageHistoryVM, IFilterWheelMediator filterWheelMediator, IFlatDeviceMediator flatDeviceMediator) :
            this(
                null,
                profileService,
                cameraMediator,
                new CloseCover(flatDeviceMediator),
                new ToggleLight(flatDeviceMediator) { OnOff = false },
                new SwitchFilter(profileService, filterWheelMediator),
                new SetBrightness(flatDeviceMediator),
                new TakeExposure(profileService, cameraMediator, imagingMediator, imageSaveMediator, imageHistoryVM) { ImageType = CaptureSequence.ImageTypes.DARK},
                new LoopCondition() { Iterations = 1 },
                new OpenCover(flatDeviceMediator),
                filterWheelMediator
            ) {
            IterExpr = new Expr(this, "", "Integer");
            FExpr = new Expr(this, "", "Integer");
        }

        private TrainedDarkFlatExposure(
            TrainedDarkFlatExposure cloneMe,
            IProfileService profileService,
            ICameraMediator cameraMediator,
            CloseCover closeCover,
            ToggleLight toggleLightOff,
            SwitchFilter switchFilter,
            SetBrightness setBrightness,
            TakeExposure takeExposure,
            LoopCondition loopCondition,
            OpenCover openCover,
            IFilterWheelMediator filterWheelMediator
            ) {
            ProfileService = profileService;
            FilterWheelMediator = filterWheelMediator;

            this.Add(closeCover);
            this.Add(toggleLightOff);
            this.Add(switchFilter);
            this.Add(setBrightness);

            var container = new SequentialContainer();
            container.Add(loopCondition);
            container.Add(takeExposure);
            this.Add(container);
            this.Add(openCover);

            IsExpanded = false;

            if (cloneMe != null) {
                CopyMetaData(cloneMe);
                IterExpr = new Expr(this, cloneMe.IterExpr.Expression, "Integer");
                IterExpr.Setter = SetIterationCount;
                IterExpr.Default = 1;
                FExpr = new Expr(this, cloneMe.FExpr.Expression, "Integer");
                FilterExpr = cloneMe.FilterExpr;
            }
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            Validate();
        }

        [JsonProperty]
        public Expr IterExpr { get; set; }
        [JsonProperty]
        public Expr FExpr { get; set; }

        private InstructionErrorBehavior errorBehavior = InstructionErrorBehavior.ContinueOnError;

        [JsonProperty]
        public override InstructionErrorBehavior ErrorBehavior {
            get => errorBehavior;
            set {
                errorBehavior = value;
                foreach (var item in Items) {
                    item.ErrorBehavior = errorBehavior;
                }
                RaisePropertyChanged();
            }
        }

        private int attempts = 1;

        [JsonProperty]
        public override int Attempts {
            get => attempts;
            set {
                if (value > 0) {
                    attempts = value;
                    foreach (var item in Items) {
                        item.Attempts = attempts;
                    }
                    RaisePropertyChanged();
                }
            }
        }

        [JsonProperty]
        public bool KeepPanelClosed {
            get => keepPanelClosed;
            set {
                keepPanelClosed = value;

                RaisePropertyChanged();
            }
        }

        public CloseCover GetCloseCoverItem() {
            return (Items[0] as CloseCover);
        }

        public ToggleLight GetToggleLightOffItem() {
            return (Items[1] as ToggleLight);
        }

        public SwitchFilter GetSwitchFilterItem() {
            return (Items[2] as SwitchFilter);
        }

        public SetBrightness GetSetBrightnessItem() {
            return (Items[3] as SetBrightness);
        }

        public SequentialContainer GetImagingContainer() {
            return (Items[4] as SequentialContainer);
        }

        public TakeExposure GetExposureItem() {
            return ((Items[4] as SequentialContainer).Items[0] as TakeExposure);
        }

        public LoopCondition GetIterations() {
            return ((Items[4] as IConditionable).Conditions[0] as LoopCondition);
        }

        public OpenCover GetOpenCoverItem() {
            return (Items[5] as OpenCover);
        }

        public override object Clone() {
            var clone = new TrainedDarkFlatExposure(
                this,
                ProfileService,
                cameraMediator,
                (CloseCover)this.GetCloseCoverItem().Clone(),
                (ToggleLight)this.GetToggleLightOffItem().Clone(),
                (SwitchFilter)this.GetSwitchFilterItem().Clone(),
                (SetBrightness)this.GetSetBrightnessItem().Clone(),
                (TakeExposure)this.GetExposureItem().Clone(),
                (LoopCondition)this.GetIterations().Clone(),
                (OpenCover)this.GetOpenCoverItem().Clone(),
                FilterWheelMediator
            ) {
                KeepPanelClosed = KeepPanelClosed,
            };
            return clone;
        }

        [JsonProperty]
        public string IterationsExpr {
            get => null;
            set {
                IterExpr.Expression = value;
            }
        }
        [JsonProperty]
        public int IterationCount {
            get => GetIterations().Iterations;
            set {
                //
                if (Items.Count == 0) return;
                LoopCondition loop = GetIterations();
                if (loop != null) {
                    loop.Iterations = value;
                }
                RaisePropertyChanged("IterationCount");
            }
        }

        public string SelectedFilter { get; set; }
        public bool CVFilter { get; set; } = false;


        private void SetFInfo() {
            SwitchFilter sw = Items.Count == 0 ? null : GetSwitchFilterItem();
            if (sw != null) {
                FilterWheelInfo filterWheelInfo = FilterWheelMediator.GetInfo();
                var fwi = ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                if (Filter == -1) {
                    if (filterWheelInfo.Connected && filterWheelInfo.SelectedFilter != null) {
                        Filter = filterWheelInfo.SelectedFilter.Position;
                    }
                    sw.FInfo = filterWheelInfo.SelectedFilter;
                } else if (Filter < fwi.Count) {
                    sw.FInfo = fwi[Filter];
                }
            }
        }

        private string iFilterExpr = "";
        [JsonProperty]
        public string FilterExpr {
            get => iFilterExpr;
            set {
                value ??= "(Current)";
                if (value.Length == 0) {
                    value = "(Current)";
                }
                iFilterExpr = value;
                FExpr.Expression = value;
                FExpr.IsExpression = true;

                // Find in FilterWheelInfo
                var fwi = ProfileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;
                Filter = -1;
                CVFilter = false;

                foreach (var fw in fwi) {
                    if (fw.Name.Equals(value)) {
                        Filter = fw.Position;
                        FExpr.Value = Filter;
                        FExpr.Error = null;
                        break;
                    }
                }

                if (Filter == -1 && !value.Equals("(Current)")) {
                    CVFilter = true;
                    if (FExpr.Error == null && FExpr.Value < fwi.Count) {
                        Filter = (int)FExpr.Value;
                    }
                }

                SetFInfo();
                RaisePropertyChanged(nameof(CVFilter));
                RaisePropertyChanged();
            }
        }

        private int iFilter = -1;
        public int Filter {
            get => iFilter;
            set {
                iFilter = value;
                RaisePropertyChanged();
            }
        }

       public void SetIterationCount(Expr expr) {
            IterationCount = (int)expr.Value;
        }

        private List<string> iFilterNames = new List<string>();
        public List<string> FilterNames {
            get => iFilterNames;
            set {
                iFilterNames = value;
            }
        }

        private CameraInfo cameraInfo;
        public CameraInfo CameraInfo {
            get => cameraInfo;
            private set {
                cameraInfo = value;
                RaisePropertyChanged();
            }
        }
        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return base.Execute(progress, token);
        }

        public override bool Validate() {
            return true;
        }

        /// <summary>
        /// When an inner instruction interrupts this set, it should reroute the interrupt to the real parent set
        /// </summary>
        /// <returns></returns>
        public override Task Interrupt() {
            return this.Parent?.Interrupt();
        }
    }
}