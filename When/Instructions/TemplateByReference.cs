
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.ViewModel.Sequencer;
using System.Reflection;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Core.Utility;
using System.Linq;
using Accord.Math;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Serialization;
using System.Diagnostics;
using NINA.Core.MyMessageBox;
using System.Runtime.Serialization;
using System.Windows.Navigation;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Template by Reference")]
    [ExportMetadata("Description", "Incorporate a template by reference.  Please read the description on the plugin page.")]
    [ExportMetadata("Icon", "BoxClosedSVG")]
    [ExportMetadata("Category", "Powerups")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]

    public class TemplateByReference : SequentialContainer, IValidatable {

        static protected ISequenceMediator sequenceMediator;
        static protected ISequenceNavigationVM sequenceNavigationVM;
        static protected TemplateController ninaTemplateController;
        static protected TemplateControllerLite templateController;
        private static SequenceJsonConverter sequenceJsonConverter;
        private static IProfileService profileService;
        private static ISequencerFactory sequencerFactory;

        [ImportingConstructor]
        public TemplateByReference(ISequenceMediator seqMediator, IProfileService pService) {
            sequenceMediator = seqMediator;
            profileService = pService;
            Name = Name;
            
            // Get the various NINA components we need
            if (sequenceNavigationVM == null || templateController == null) {
                FieldInfo fi = sequenceMediator.GetType().GetField("sequenceNavigation", BindingFlags.Instance | BindingFlags.NonPublic); 
                if (fi != null) {
                    sequenceNavigationVM = (ISequenceNavigationVM)fi.GetValue(sequenceMediator);
                    ISequence2VM s2vm = sequenceNavigationVM.Sequence2VM;
                    if (s2vm != null) {
                        sequencerFactory = s2vm.SequencerFactory;
                        PropertyInfo pi = s2vm.GetType().GetRuntimeProperty("TemplateController");
                        ninaTemplateController = (TemplateController)pi.GetValue(s2vm);
                        fi = ninaTemplateController.GetType().GetField("sequenceJsonConverter", BindingFlags.Instance | BindingFlags.NonPublic);
                        sequenceJsonConverter = (SequenceJsonConverter)fi.GetValue(ninaTemplateController);
                        templateController = new TemplateControllerLite(sequenceJsonConverter, profileService);
                    }
                }
            }
        }

        public TemplateByReference(TemplateByReference copyMe) : this(sequenceMediator, profileService) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        [JsonIgnore]
        public SequentialContainer Instructions { get; set; }

        [JsonProperty("Instructions")]
        private IfContainer ObsoleteInstructions {
            // get is intentionally omitted here
            set { Instructions = value; }
        }

        private string iTemplateName = null;
        [JsonProperty]
        public string TemplateName { 
            get {
                return iTemplateName;
            }
            set {
                iTemplateName = value;
                RaisePropertyChanged("TemplateName");
            }
        }

        public bool TemplateNameIsTrue {
            get {
                return TemplateName == null;
            }
        }
        
        public IList<TemplatedSequenceContainer> Templates {
            get {
                lock (TemplateControllerLite.TemplateLock) {
                    return templateController.TBRTemplates;
                }
            }
        }

        private int TemplateCompare (TemplatedSequenceContainer a, TemplatedSequenceContainer b) {
            return String.Compare(a.Container.Name, b.Container.Name);

        }

        public TemplatedSequenceContainer[] SortedTemplates {
            get {
                lock (TemplateControllerLite.TemplateLock) {
                    IList<TemplatedSequenceContainer> l = Templates;
                    TemplatedSequenceContainer[] lCopy = Templates.ToArray();
                    lCopy.Sort(TemplateCompare);
                    return lCopy;
                }
            }
        }

        private TemplatedSequenceContainer selectedTemplate;
        public TemplatedSequenceContainer SelectedTemplate {
            get => selectedTemplate;
            set {
                if (value == null) {
                    //RaisePropertyChanged("SelectedTemplate");
                    value = FindTemplate(TemplateName);
                    if (value == null) {
                        return;
                    }
                }

                selectedTemplate = value;
                if (Items.Count > 0) {
                    Items.Clear();
                }
                TemplateName = selectedTemplate.Container.Name;
                Items.Add((ISequenceContainer)SelectedTemplate.Container.Clone());
                foreach (ISequenceItem item in Items) {
                    item.AttachNewParent(this);
                }
                RaisePropertyChanged("SelectedTemplate");
                RaisePropertyChanged("TemplateNameIsTrue");
                Validate();
            }
        }

        public override object Clone() {

            if (TemplateName != null && cycleStack.Contains(TemplateName)) {
                Notification.ShowError("The template '" + TemplateName + "' is recursive.  Please don't do that.");
                TemplateName = "{Error}";
                throw new Exception("Recursive Template");
            }
            if (TemplateName != null) {
                cycleStack.Push(TemplateName);
            }

            TemplateByReference clone = new TemplateByReference(this);
            clone.TemplateName = TemplateName;
            if (TemplateName != null && templateController != null) {
                TemplatedSequenceContainer tc = FindTemplate(TemplateName);
                if (tc != null) {
                    SelectedTemplate = tc;
                    TemplateName = tc.Container.Name;
                    RaisePropertyChanged("TemplateNameIsTrue");
                }
            }
            if (TemplateName != null) {
                cycleStack.Pop();
            }

            return clone;
        }

        private Stack<string> cycleStack = new Stack<string>();

        private TemplatedSequenceContainer FindTemplate(string name) {

            lock (TemplateControllerLite.TemplateLock) {
                for (int i = 0; i < 4; i++) {
                    try {
                        foreach (var tmp in Templates) {
                            if (tmp.Container.Name.Equals(name)) {
                                return tmp;
                            }
                        }
                    } catch (Exception) {
                        Thread.Sleep(100);
                    }
                }
                return null;
            }
        }

        public override void ResetProgress() {
            base.ResetProgress();
            Status = NINA.Core.Enum.SequenceEntityStatus.DISABLED;
            ShowMenu = false;
        }

        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            await Run(progress, token);
        }

        
        private void UpdateChangedTemplates(string name) {
            if (name == null) return;
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is SequenceRootContainer root) {
                    try {
                        UpdateChangedTemplate(root, name.Split('.')[0]);
                    } catch (Exception ex) {
                        Logger.Warning("Exception trying to parse template file name: " + name + " Error: " + ex.Message);
                    }
                    return;
                }
                p = p.Parent;
            }
            return;
        }

        private void UpdateChangedTemplate(ISequenceContainer p, string name) {
            foreach (ISequenceItem item in p.Items) {
                if (item is ISequenceContainer sc) {
                    UpdateChangedTemplate(sc, name);
                } else if (item is TemplateByReference tbr) {
                    if (tbr != null && tbr.TemplateName.Equals(name)) {
                        // Update instruction set if user wants
                        if (MyMessageBox.Show("An instruction set named '" + tbr.Parent.Name + "' includes a reference to template '" + name + "', which has just been changed.  Do you want this instruction set to be updated as well?", "Update instruction set?", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxResult.No) == System.Windows.MessageBoxResult.Yes) {
                            tbr.SelectedTemplate = FindTemplate(name);
                            Logger.Info("Updated due to '" + name + "' changed, " + tbr.SelectedTemplate.Container.Name);
                            Notification.ShowSuccess("The instruction set '" + tbr.Parent.Name + "' has been updated.");
                        }
                    }
                }
            }
        }
        
        public override bool Validate() {

            if (templateController == null) return true;

            //if (!Symbol.IsAttachedToRoot(this)) return true;

            var i = new List<string>();

            if (SelectedTemplate == null && TemplateName == null) {
                i.Add("A template must be selected!");
            } else if (TemplateName != null && SelectedTemplate == null) {
                TemplatedSequenceContainer tc = FindTemplate(TemplateName);
                if (tc != null) {
                    SelectedTemplate = tc;
                } else {
                    i.Add("The specified template '" + TemplateName + "' was not found.");
                }
            } else if (SelectedTemplate == null) {
                i.Add("The specified template '" + TemplateName + "' was not found.");
            }

            if (templateController.Updated) {
                SelectedTemplate = FindTemplate(TemplateName);
                _ = SortedTemplates;
                RaisePropertyChanged("SortedTemplates");
                UpdateChangedTemplates(templateController.UpdatedFile);
            }

            foreach (ISequenceItem item in Items) {
                if (item is IValidatable val) {
                    _ = val.Validate();
                }
            }

            Issues = i;
            return i.Count == 0;
        }
        
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(TemplateByReference)}, TemplateName: {TemplateName}";
        }
    }
}
