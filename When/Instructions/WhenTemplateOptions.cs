using System.Windows;

namespace WhenPlugin.When {
    public static class WhenTemplateOptions {
        public static readonly DependencyProperty ShowPredicateProperty =
            DependencyProperty.RegisterAttached(
                "ShowPredicate",
                typeof(bool),
                typeof(WhenTemplateOptions),
                new FrameworkPropertyMetadata(true));

        public static void SetShowPredicate(DependencyObject element, bool value) =>
            element.SetValue(ShowPredicateProperty, value);

        public static bool GetShowPredicate(DependencyObject element) =>
            (bool)element.GetValue(ShowPredicateProperty);
    }
}