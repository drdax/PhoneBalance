using System;
using System.Linq;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using DrDax.PhoneBalance.Data;

namespace DrDax.PhoneBalance {
	public sealed partial class EditAccountPage : Page {
		public EditAccountPage() {
			this.InitializeComponent();
			intervalBox.ItemsSource=new Tuple<string, TimeSpan>[] {
				Tuple.Create(DisplayResources.Strings.GetString("Interval30Minutes"), TimeSpan.FromMinutes(30)),
				Tuple.Create(DisplayResources.Strings.GetString("Interval1Hour"), TimeSpan.FromHours(1)),
				Tuple.Create(DisplayResources.Strings.GetString("Interval4Hours"), TimeSpan.FromHours(4)),
				Tuple.Create(DisplayResources.Strings.GetString("Interval1Day"), TimeSpan.FromDays(1))
			};
			app=(App)App.Current;

			account=app.Account;
			captionBox.Text=account.Caption;
			intervalBox.SelectedValue=account.RefreshInterval;
			// TODO: pielikt aptuveno viena pieprasījuma pārsūtāmo datu apojumu atkarībā no operatora
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e) {
			// Ja nonāca šurp pēc pievienošanas, tad neļauj atgriezties ar atpakaļejošu navigāciju.
			var pageType=this.GetType();
			var pageStackEntry=Frame.BackStack.LastOrDefault(entry => entry.SourcePageType == pageType);
			if (pageStackEntry != null) Frame.BackStack.Remove(pageStackEntry);
		}
		private async void Save_Click(object sender, RoutedEventArgs e) {
			if (captionBox.Text.Length  == 0) { captionBox.Focus(FocusState.Programmatic); return; }

			account.RefreshInterval=(TimeSpan)intervalBox.SelectedValue;

			if (account.Caption != captionBox.Text) {
				account.Caption=captionBox.Text;
				var tile=(await SecondaryTile.FindAllAsync()).FirstOrDefault(a => a.TileId == account.Id);
				if (tile != null) {
					tile.DisplayName=account.Caption;
					await tile.UpdateAsync();
				}
			}

			Settings.PutAccount(account);
			GoAway();
		}
		private void Cancel_Click(object sender, RoutedEventArgs e) {
			GoAway();
		}

		private void GoAway() {
			if (Frame.CanGoBack) Frame.GoBack();
			else Frame.Navigate(typeof(AccountsPage));
		}

		private readonly Account account;
		private readonly App app;
	}
}