using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using DrDax.PhoneBalance.Data;
using Windows.ApplicationModel.Background;
using Windows.UI.Input;
using Windows.UI.Popups;
using Windows.UI.StartScreen;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace DrDax.PhoneBalance {
	public sealed partial class AccountsPage : Page {
		public ListViewReorderMode Ordering {
			get { return isOrdering ? ListViewReorderMode.Enabled:ListViewReorderMode.Disabled; }
			set {
				isOrdering=value == ListViewReorderMode.Enabled;
				refreshBtn.Visibility=isOrdering ? Visibility.Collapsed:Visibility.Visible;
				doneBtn.Visibility=isOrdering ? Visibility.Visible:Visibility.Collapsed;
				foreach (AppBarButton button in ((CommandBar)BottomAppBar).SecondaryCommands)
					button.IsEnabled=!isOrdering;
			}
		}

		public AccountsPage() {
			this.InitializeComponent();
			app=(App)App.Current;
			this.NavigationCacheMode=NavigationCacheMode.Required;
			list.SetBinding(ListView.ReorderModeProperty, new Binding {
				Source=this, Path=new PropertyPath("Ordering"),
				Mode=BindingMode.TwoWay // Abos virzienos, jo saraksts apstrādā Back nospiešanu, lai izietu no kārtošanas režīma
			});
		}

		protected override void OnNavigatedTo(NavigationEventArgs e) {
			// TODO: Nodrošināt, lai pēc atgriešanās programmā, ja UpdateTask nolasīja jaunākos datus, tie parādītos uz ekrāna.
			if (timer == null) {
				DisplayResources.UnitsBrush=(Brush)Resources["PhoneMidBrush"];
				list.ItemsSource=app.Accounts;
				timer=new DispatcherTimer();
				timer.Interval=TimeSpan.FromSeconds(5);
				timer.Tick+=timer_Tick;
				RegisterBackgroundTask();
			}
			timer.Start();
			timer_Tick(timer, EventArgs.Empty);
			//((App)App.Current).Accounts[0].Balances.Add(new SmsBalance(43, DateTime.Today.AddDays(-5), Resources));
			/*var account=new Account {
				Caption="26 139 620", Balance=new AccountBalance((decimal)11.24, DateTime.Today.AddDays(30), Resources)
			};
			account.Balances.Add(new VoiceBalance(TimeSpan.FromSeconds(2345), DateTime.Today.AddDays(3), Resources));
			account.Balances.Add(new SmsBalance(146, DateTime.Today.AddDays(7), Resources));
			account.Balances.Add(new DataBalance(1234567, DateTime.Today.AddDays(7), Resources));
			list.ItemsSource=new List<Account> {
				account,
				new Account { Caption="D8 Galaxy", Balance=new AccountBalance((decimal)0.43, DateTime.Today, Resources) },
				 new Account { Caption="D8 Experia", Balance=new AccountBalance((decimal)0, DateTime.Today.AddDays(-3), Resources) }
			};*/
		}

		protected override void OnNavigatedFrom(NavigationEventArgs e) {
			timer.Stop();
			if (app.Accounts.Count == 0) {
				// Ja dzēsa pēdējo kontu, neļauj atgriezties uz sarakstu
				var pageStackEntry=Frame.BackStack.LastOrDefault(entry => entry.SourcePageType == this.GetType());
				if (pageStackEntry != null) Frame.BackStack.Remove(pageStackEntry);
			}
		}

		private async void Refresh_Click(object sender, RoutedEventArgs e) {
			if (Network.Disconnected) {
				await new MessageDialog(DisplayResources.Strings.GetString("NetworkOffline")).ShowAsync();
				return;
			}
			timer.Stop();
			BottomAppBar.IsEnabled=false;
			var statusBar=StatusBar.GetForCurrentView();
			statusBar.ProgressIndicator.Text=DisplayResources.Strings.GetString("Refreshing");
			await statusBar.ProgressIndicator.ShowAsync();

			for (int n=0; n < app.Accounts.Count; n++)
				list.ContainerFromIndex(n).SetValue(ListViewItem.IsEnabledProperty, false);
			var client=new ProperHttpClient();
			for (int n=0; n < app.Accounts.Count; n++) {
				await app.Accounts[n].SetBalance(client);
				list.ContainerFromIndex(n).SetValue(ListViewItem.IsEnabledProperty, true);
			}
			BottomAppBar.IsEnabled=true;
			await statusBar.ProgressIndicator.HideAsync();
			timer.Start();
		}
		private void Add_Click(object sender, RoutedEventArgs e) {
			app.Account=null;
			Frame.Navigate(typeof(AddAccountPage));
		}

		private void Item_Holding(object sender, HoldingRoutedEventArgs e) {
			// Reaģē uz pirmo izsaukumu, lai parādītu izvēlni.
			if (e.HoldingState != HoldingState.Started) return;
			if (!BottomAppBar.IsEnabled) return;
			var itemElement=(FrameworkElement)sender;
			app.Account=(Account)itemElement.DataContext;
			pinBtn.Visibility=SecondaryTile.Exists(app.Account.Id) ? Visibility.Collapsed:Visibility.Visible;
			FlyoutBase.GetAttachedFlyout(list).ShowAt(itemElement);
		}
		private void Edit_Click(object sender, RoutedEventArgs e) {
			Frame.Navigate(typeof(EditAccountPage));
		}
		private async void Remove_Click(object sender, RoutedEventArgs e) {
			Settings.RemoveAccount(app.Accounts, app.Account);
			Settings.SetAccounts(app.Accounts);
			app.HaveAccountsChanged=false;

			string tileId=app.Account.Id;
			app.Account=null;
			var tile = (await SecondaryTile.FindAllAsync()).FirstOrDefault(t => t.TileId == tileId);
			if (tile != null)
				await tile.RequestDeleteAsync();

			if (app.Accounts.Count == 0)
				Frame.Navigate(typeof(AddAccountPage));
		}
		private async void Pin_Click(object sender, RoutedEventArgs e) {
			SecondaryTile tile=new SecondaryTile(app.Account.Id) {
				Arguments=Account.Arguments, // Pazīme, ka palaida programmu no šīs flīzes.
				DisplayName=app.Account.Caption,
				RoamingEnabled=false // Aizliedz flīzes sinhronizēšanu starp ierīcēm
			};
			tile.VisualElements.Square30x30Logo=new Uri("ms-appx:///Assets/SmallLogo.png");
			tile.VisualElements.Square150x150Logo=new Uri("ms-appx:///Assets/Logo.png");
			tile.VisualElements.ShowNameOnSquare150x150Logo=true;
	
			bool userCreated=await tile.RequestCreateAsync();
			if (userCreated)
				app.Account.UpdateTile();

			// Saņemt visus piespraustos
			// IReadOnlyList<SecondaryTile> pinnedSecondaryTiles=await SecondaryTile.FindAllAsync();
		}

		private void Reorder_Click(object sender, RoutedEventArgs e) {
			list.ReorderMode=ListViewReorderMode.Enabled;
		}
		private void Done_Click(object sender, RoutedEventArgs e) {
			list.ReorderMode=ListViewReorderMode.Disabled;
		}

		private void timer_Tick(object sender, object e) {
			foreach (Account account in app.Accounts)
				account.UpdateRefreshString();
		}
		private async void RegisterBackgroundTask() {
			// Noskaidro atļauju veidot fona uzdevumu.
			var access=await BackgroundExecutionManager.RequestAccessAsync();
			if (access == BackgroundAccessStatus.Denied || access == BackgroundAccessStatus.Unspecified) return;

			// Izmanto esošo uzdevumu, ja tāds ir.
			foreach (var existingTask in BackgroundTaskRegistration.AllTasks)
				if (existingTask.Value.Name == UpdateTaskName) {
					//existingTask.Value.Unregister(true);
					existingTask.Value.Completed+=task_Completed;
					return;
				}

			BackgroundTaskBuilder builder=new BackgroundTaskBuilder();
			builder.Name=UpdateTaskName;
			// Klase obligāti arī jānorāda manifestā un šajā projektā ir jābūt atsaucei uz uzdevuma projektu.
			builder.TaskEntryPoint="DrDax.PhoneBalance.Tasks."+UpdateTaskName;
			// Darbinās reizi pusstundā, bet pārbaudīs tikai iztecējušos atlikumus.
			builder.SetTrigger(new TimeTrigger(30, false));
			builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable)); 
			var task=builder.Register();
			task.Completed+=task_Completed;
		}

		private void task_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args) {
			this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
				foreach (Account account in app.Accounts)
					Settings.LoadBalances(account);
			});
		}

		private readonly App app;
		private bool isOrdering=false;
		private DispatcherTimer timer;
		private const string UpdateTaskName="UpdateBalanceTask";
	}
}