using System.Windows;

namespace WhenPlugin.When {
    /// <summary>
    /// Helper class providing attached properties for the CollapsibleInstructionsStyle.
    /// </summary>
    public static class CollapsibleInstructionsHelper {
        
        #region PlaceholderText Attached Property

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.RegisterAttached(
                "PlaceholderText",
                typeof(string),
                typeof(CollapsibleInstructionsHelper),
                new FrameworkPropertyMetadata("Drop instructions here"));

        public static string GetPlaceholderText(DependencyObject obj) {
            return (string)obj.GetValue(PlaceholderTextProperty);
        }

        public static void SetPlaceholderText(DependencyObject obj, string value) {
            obj.SetValue(PlaceholderTextProperty, value);
        }

        #endregion

        #region HeaderText Attached Property

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.RegisterAttached(
                "HeaderText",
                typeof(string),
                typeof(CollapsibleInstructionsHelper),
                new FrameworkPropertyMetadata("Header Text"));

        public static string GetHeaderText(DependencyObject obj) {
            return (string)obj.GetValue(HeaderTextProperty);
        }

        public static void SetHeaderText(DependencyObject obj, string value) {
            obj.SetValue(HeaderTextProperty, value);
        }

        #endregion
    }
}