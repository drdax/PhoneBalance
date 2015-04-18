using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;

namespace DrDax.PhoneBalance.Data {
	public abstract class Account : INotifyPropertyChanged {
		public const string Arguments="Account";

		/// <summary>Serializētās konta informācijas identifikators.</summary>
		public readonly string Id;
		/// <summary>Serializētu konta atlikumu identifikators.</summary>
		public readonly string BalancesId;
		/// <summary>Operatora pakalpojuma sesijas identifikators, pastāvīgs vai mainīgs.</summary>
		public string Token;

		public readonly Carrier Carrier;
		public string Caption {
			get { return caption; }
			set {
				caption=value;
				NotifyChanged("Caption");
			}
		}
		public AccountBalance Balance {
			get { return balance; }
			set {
				balance=value;
				NotifyChanged("Balance");
			}
		}
		public List<IBalance> Balances {
			get { return balances; }
			set {
				balances=value;
				NotifyChanged("Balances");
			}
		}
		public DateTime? RefreshDate {
			get { return refreshDate; }
			set {
				refreshDate=value;
				NotifyChanged("RefreshString");
			}
		}
		public TimeSpan RefreshInterval {
			get { return refreshInterval; }
			set {
				refreshInterval=value;
				NotifyChanged("RefreshInterval");
			}
		}
		public string RefreshString {
			get {
				if (refreshDate == null) return DisplayResources.Strings.GetString("RefreshedNot");
				TimeSpan age=DateTime.Now-refreshDate.Value;
				if (age.TotalSeconds < 5) return DisplayResources.Strings.GetString("RefreshedNow");
				if (age.TotalMinutes < 1) return GetRefreshString(age.Seconds, "Second");
				if (age.TotalHours < 1) return GetRefreshString(age.Minutes, "Minute");
				if (age.TotalDays < 1) return GetRefreshString(age.Hours, "Hour");
				if (age.TotalDays < 2) return DisplayResources.Strings.GetString("RefreshedYesterday");
				if (age.TotalDays > 30) return DisplayResources.Strings.GetString("RefreshedLongAgo");
				return GetRefreshString(age.Days, "Day");
			}
		}
		private string GetRefreshString(int value, string unit) {
			int lastDigit=value%10;
			// pāri 100 netiek izskatīts. Ja 1 vai beidzas ar vieninieku un nav 11, tad vienskaitlis.
			if (value == 1 || (value > 20 && lastDigit == 1)) return string.Format(DisplayResources.Strings.GetString("Refreshed"+unit), value);
			// Krievu valodā līdz 5 ir īpašā forma, pārējiem daudzskaitlis.
			return string.Format(DisplayResources.Strings.GetString(
				string.Concat(DisplayResources.LanguageIsRussian && (value < 5 || value > 20 && lastDigit > 1 && lastDigit< 5) ? "Refreshed2":"Refreshed", unit, "s")), value);
		}

		protected Account(Carrier carrier, string id) {
			this.Carrier=carrier;
			if (string.IsNullOrEmpty(id))
				this.Id=Guid.NewGuid().ToString();
			else this.Id=id;
			this.BalancesId=this.Id+'B';
		}

		public abstract Task SetBalance(ProperHttpClient client);
		public void UpdateTile() {
			if (refreshDate == null || refreshDate.Value.Add(refreshInterval) < DateTime.Now) return;
			if (SecondaryTile.Exists(this.Id)) {
				XmlDocument tileXml=TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text01);
				var texts=tileXml.GetElementsByTagName("text");
				texts[0].InnerText=balance.TileString;
				if (balances != null)
					for (int n=0; n < Math.Min(texts.Count-1, balances.Count); n++)
						texts[n+1].InnerText=balances[n].TileString;

				var updater=TileUpdateManager.CreateTileUpdaterForSecondaryTile(this.Id);
				updater.Clear();
				updater.Update(new TileNotification(tileXml) {
					// Lai nerādītu, kad nolasīja datus, noņem informāciju pēc lietotāja noteiktā intervāla.
					ExpirationTime=DateTimeOffset.Now.Add(RefreshInterval)
				});
			}
		}

		private string caption;
		private AccountBalance balance;
		private List<IBalance> balances;
		private DateTime? refreshDate;
		private TimeSpan refreshInterval;

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyChanged(string propertyName) {
			if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
		public void UpdateRefreshString() {
			NotifyChanged("RefreshString");
		}
	}
	public enum Carrier : byte {
		ZZ, Bite
	}
}