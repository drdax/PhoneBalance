using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace DrDax.PhoneBalance {
	public class Attached {
		public static readonly DependencyProperty RunsProperty=DependencyProperty.RegisterAttached("Runs",
			typeof(List<Run>), typeof(Attached), new PropertyMetadata(null, Runs_PropertyChanged));

		// Obligātās īpašības iestatīšanas/nolasīšanas metodes.
		public static List<Run> GetRuns(DependencyObject obj) {
			return (List<Run>)obj.GetValue(RunsProperty);
		}
		public static void SetRuns(DependencyObject obj, List<Run> value) {
			obj.SetValue(RunsProperty, value);
		}

		private static void Runs_PropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e) {
			TextBlock textBlock=obj as TextBlock;
			if (textBlock == null) return; // Ignorē ne teksta elementus.

			textBlock.Inlines.Clear();
			if (e.NewValue != null) {
				foreach (Run run in (List<Run>)e.NewValue)
					textBlock.Inlines.Add(run);
			}
		}
	}
}