using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using DrDax.PhoneBalance.Data;

namespace DrDax.PhoneBalance {
	/// <summary>
	/// Provides application-specific behavior to supplement the default Application class.
	/// </summary>
	public sealed partial class App : Application {
		private TransitionCollection transitions;
		/// <summary>Vai kontu sarakstā ir nesaglabātas izmaiņas.</summary>
		public bool HaveAccountsChanged;
		public ObservableCollection<Account> Accounts;
		public Account Account;

		/// <summary>
		/// Initializes the singleton application object.  This is the first line of authored code
		/// executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App() {
			InitializeComponent();
			Suspending += OnSuspending;
			HardwareButtons.BackPressed += HardwareButtons_BackPressed;
		}

		/// <summary>
		/// Invoked when the application is launched normally by the end user.  Other entry points
		/// will be used when the application is launched to open a specific file, to display
		/// search results, and so forth.
		/// </summary>
		/// <param name="e">Details about the launch request and process.</param>
		protected override void OnLaunched(LaunchActivatedEventArgs e) {
			Frame rootFrame = Window.Current.Content as Frame;

			// Do not repeat app initialization when the Window already has content,
			// just ensure that the window is active
			if (rootFrame == null) {
				// Create a Frame to act as the navigation context and navigate to the first page
				rootFrame = new Frame();

				rootFrame.CacheSize = 1;

				/*if (e.PreviousExecutionState == ApplicationExecutionState.Terminated) {
				}*/

				// Place the frame in the current Window
				Window.Current.Content = rootFrame;
			}

			if (rootFrame.Content == null) {
				Accounts=new ObservableCollection<Account>(Settings.GetAccounts());
				Accounts.CollectionChanged+=Accounts_CollectionChanged;

				// Removes the turnstile navigation for startup.
				if (rootFrame.ContentTransitions != null) {
					this.transitions = new TransitionCollection();
					foreach (var c in rootFrame.ContentTransitions) {
						this.transitions.Add(c);
					}
				}

				rootFrame.ContentTransitions = null;
				rootFrame.Navigated += this.RootFrame_FirstNavigated;
				// Ja vēl nav neviena numura, atver pievienošanas dialogu.
				if (!rootFrame.Navigate(Accounts.Count == 0 ? typeof(AddAccountPage):typeof(AccountsPage), e.Arguments))
					throw new Exception("Failed to create initial page");
			}

			// Ensure the current window is active
			Window.Current.Activate();
		}

		private void Accounts_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
			HaveAccountsChanged=true;
		}

		/// <summary>
		/// Restores the content transitions after the app has launched.
		/// </summary>
		/// <param name="sender">The object where the handler is attached.</param>
		/// <param name="e">Details about the navigation event.</param>
		private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e) {
			var rootFrame = sender as Frame;
			rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
			rootFrame.Navigated -= this.RootFrame_FirstNavigated;
		}

		/// <summary>
		/// Invoked when application execution is being suspended.  Application state is saved
		/// without knowing whether the application will be terminated or resumed with the contents
		/// of memory still intact.
		/// </summary>
		/// <param name="sender">The source of the suspend request.</param>
		/// <param name="e">Details about the suspend request.</param>
		private void OnSuspending(object sender, SuspendingEventArgs e) {
			var deferral = e.SuspendingOperation.GetDeferral();

			// Ja vajadzēs darbināt datu izgūšanu fona uzdevumā, kamēr programma atvērta, izsaukt SetAccounts pēc dzēšanas/pievienošanas.
			if (HaveAccountsChanged) {
				Settings.SetAccounts(Accounts);
				HaveAccountsChanged=false;
			}
			deferral.Complete();
		}
		private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e) {
			Frame frame = Window.Current.Content as Frame;
			if (frame == null) {
				return;
			}

			if (frame.CanGoBack) {
				frame.GoBack();
				e.Handled = true;
			}
		}
	}
}