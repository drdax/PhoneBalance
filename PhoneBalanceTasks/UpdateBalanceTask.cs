using System;
using DrDax.PhoneBalance.Data;
using Windows.ApplicationModel.Background;

namespace DrDax.PhoneBalance.Tasks {
	public sealed class UpdateBalanceTask : IBackgroundTask {
		public async void Run(IBackgroundTaskInstance taskInstance) {
			// Patur procesu, kamēr darbina asinhronu kodu.
			BackgroundTaskDeferral deferral=taskInstance.GetDeferral();

			// Uzdevumam nepieciešamo interneta savienojumu pieprasa reģistrācijas brīdī, tāpēc šeit papildus nepārbauda.
			var client=new ProperHttpClient();
			foreach (Account account in Settings.GetAccounts()) {
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