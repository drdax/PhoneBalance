using System;
using DrDax.PhoneBalance.Data;
using Windows.ApplicationModel.Background;

namespace DrDax.PhoneBalance.Tasks {
	public sealed class UpdateBalanceTask : IBackgroundTask {
		public async void Run(IBackgroundTaskInstance taskInstance) {
			// Patur procesu, kamēr darbina asinhronu kodu.
			BackgroundTaskDeferral deferral=taskInstance.GetDeferral();

			var client=new ProperHttpClient();
			foreach (Account account in Settings.GetAccounts()) {
				// Neskatoties uz SystemConditionType.InternetAvailable, kuru pieprasīja reģistrācijas brīdī, var gadīties, ka uzdevumu palaiž bez interneta savienojuma.
				// Tāpēc to šeit pārbauda. Un dara pirms katra izsaukuma, jo tie var būt tik ilgi, ka pa starpai atslēdz savienojumu.
				if (Network.Disconnected) break;
#if !DEBUG
				if (account.RefreshDate == null || account.RefreshDate+account.RefreshInterval < DateTime.Now)
				try {
#endif
					await account.SetBalance(client);
#if !DEBUG
				} catch {} // Atļauj pamēģināt ar katru numuru.
#endif
			}

			deferral.Complete();
		}
	}
}