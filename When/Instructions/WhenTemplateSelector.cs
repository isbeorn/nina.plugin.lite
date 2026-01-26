using NINA.View.Sequencer;
using System.Windows;
using System.Windows.Controls;

namespace WhenPlugin.When {

    public class WhenTemplateSelector : DataTemplateSelector {

        public DataTemplateSelector? FallbackSelector { get; set; }

        public override DataTemplate? SelectTemplate(object? item, DependencyObject container) {
            if (container is FrameworkElement element && item != null) {
                var template = element.TryFindResource(new DataTemplateKey(item.GetType())) as DataTemplate;
                if (template != null) {
                    return template;
                }
            }

            return FallbackSelector?.SelectTemplate(item, container);
        }
    }
}