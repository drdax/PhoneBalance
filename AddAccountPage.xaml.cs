using System;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using DrDax.PhoneBalance.Data;

namespace DrDax.PhoneBalance {
	public sealed partial class AddAccountPage : Page {
		public AddAccountPage() {
			InitializeComponent();
			app=(App)App.Current;
			carrierBox.SelectedIndex=0;
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e) {
			// Neļauj atgriezties uz pievienošanas lapu, ja tā veiksmīgi nostrādāja.
			var pageType=this.GetType();
			var pageStackEntry=Frame.BackStack.LastOrDefault(entry => entry.SourcePageType == pageType);
			if (pageStackEntry != null) Frame.BackStack.Remove(pageStackEntry);
		}

		private async void Save_Click(object sender, RoutedEventArgs e) {
			if (numberBox.Text.Length < numberBox.MaxLength || !numberRx.IsMatch(numberBox.Text)) { numberBox.Focus(FocusState.Programmatic); return; }
			if (passwordBox.Password.Length == 0 || carrierBox.SelectedIndex == 1 && passwordBox.Password.Length != BitePasswordLength) {
				passwordBox.Focus(FocusState.Programmatic); return;
			}

			if (Network.Disconnected) {
				await new MessageDialog(DisplayResources.Strings.GetString("NetworkOffline")).ShowAsync();
				return;
			}

			SetEnabled(false);
			var statusBar=StatusBar.GetForCurrentView();
			statusBar.ProgressIndicator.Text=DisplayResources.Strings.GetString("LoggingIn");
			await statusBar.ProgressIndicator.ShowAsync();

			string number=numberBox.Text;
			Account account=null;
			string errorMessage=null, errorMessageId=null;
			try {
				switch (carrierBox.SelectedIndex) {
					case 0:
						errorMessageId="ZZLoginFailure";
						account=await ZZAccount.Login(client, numberBox.Text, passwordBox.Password);
						break;
					case 1:
						errorMessageId="BiteLoginFailure";
						account=await BiteAccount.Login(client, numberBox.Text, passwordBox.Password.ToUpper());
						break;
					case 2:
						errorMessageId="OLoginFailure";
						account=await OAccount.Login(client, numberBox.Text, passwordBox.Password);
						break;
				}
				if (account == null)
					errorMessage=DisplayResources.Strings.GetString(errorMessageId);
			} catch (Exception ex) {
				errorMessage=ex.Message+Environment.NewLine+DisplayResources.Strings.GetString(errorMessageId);
			}

			await statusBar.ProgressIndicator.HideAsync();
			if (errorMessage != null) {
				await new MessageDialog(errorMessage).ShowAsync();
				SetEnabled(true);
				return;
			}
	
			account.RefreshInterval=TimeSpan.FromMinutes(30);
			account.Caption=string.Concat(number.Substring(0, 2), " ", number.Substring(2, 3), " ", number.Substring(5, 3));
			app.Accounts.Add(account);
			Settings.SetAccounts(app.Accounts);
			app.HaveAccountsChanged=false;
			Settings.AddAccount(account);

			statusBar.ProgressIndicator.Text=DisplayResources.Strings.GetString("Refreshing");
			await statusBar.ProgressIndicator.ShowAsync();
			errorMessage=null;
			try {
				await account.SetBalance(client);
			} catch (Exception) {
				// Ja nespēja noskaidrot atlikumu, ļauj lietotājam to vēlāk pamēģināt.
			}
			await statusBar.ProgressIndicator.HideAsync();

			app.Account=account;
			Frame.Navigate(typeof(EditAccountPage));
		}
		private void Carrier_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			Carrier carrier;
			switch (carrierBox.SelectedIndex) {
				case 0:
					carrier=Carrier.ZZ;
					passwordBox.Header=DisplayResources.Strings.GetString("ZZPassword");
					break;
				case 1:
					carrier=Carrier.Bite;
					passwordBox.Header=DisplayResources.Strings.GetString("BitePassword");
					break;
				default://case 2:
					carrier=Carrier.OKarte;
					passwordBox.Header=DisplayResources.Strings.GetString("OPassword");
					break;
			}
			requestPasswordBtn.Visibility=carrier == Carrier.Bite ? Visibility.Visible:Visibility.Collapsed;
			passwordBox.MaxLength=carrier == Carrier.Bite ? BitePasswordLength:255;
			disclaimer.Visibility=carrier == Carrier.OKarte ? Visibility.Collapsed:Visibility.Visible;
			oDisclaimer.Visibility=carrier == Carrier.OKarte ? Visibility.Visible:Visibility.Collapsed;

		}
		private async void RequestPassword_Click(object sender, RoutedEventArgs e) {
			SetEnabled(false);
			var statusBar=StatusBar.GetForCurrentView();
			statusBar.ProgressIndicator.Text=DisplayResources.Strings.GetString("RequestingPassword");
			await statusBar.ProgressIndicator.ShowAsync();
			string errorMessage=null;
			try {
				await BiteAccount.RequestPassword(client, numberBox.Text);
			} catch (Exception ex) {
				errorMessage=ex.Message+Environment.NewLine+DisplayResources.Strings.GetString("RequestPasswordFailure");
			}

			await statusBar.ProgressIndicator.HideAsync();
			if (errorMessage != null)
				await new MessageDialog(errorMessage).ShowAsync();
			SetEnabled(true);
		}

		private void SetEnabled(bool enabled) {
			BottomAppBar.IsEnabled=enabled;
			carrierBox.IsEnabled=enabled;
			numberBox.IsEnabled=enabled;
			passwordBox.IsEnabled=enabled;
		}

		private readonly App app;
		private readonly ProperHttpClient client=new ProperHttpClient();
		private readonly Regex numberRx=new Regex("^2[0-9]{7}$"); // LMT tīklā var būt numuri ar 6 priekšā, bet tas neattiecas uz OKarti.
		private const int BitePasswordLength=6;
	}
}