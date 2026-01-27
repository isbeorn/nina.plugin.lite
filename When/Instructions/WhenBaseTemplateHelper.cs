using System.Windows;

namespace WhenPlugin.When {
    public static class WhenBaseTemplateHelper {
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.RegisterAttached(
                "PlaceholderText",
                typeof(string),
                typeof(WhenBaseTemplateHelper),
                new PropertyMetadata("Instructions"));

        public static string GetPlaceholderText(DependencyObject obj) {
            return (string)obj.GetValue(PlaceholderTextProperty);
        }

        public static void SetPlaceholderText(DependencyObject obj, string value) {
            obj.SetValue(PlaceholderTextProperty, value);
        }
    }
}