using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace DrDax.PhoneBalance.Data {
	public static class Settings {
		public static void AddAccount(Account account) {
			var value=new ApplicationDataCompositeValue();
			value["CR"]=(byte)account.Carrier;
			value["RT"]=account.Token;
			ApplicationData.Current.LocalSettings.Values[account.Id]=value;
			PutAccount(account);
		}
		public static void PutAccount(Account account) {
			var value=(ApplicationDataCompositeValue)ApplicationData.Current.LocalSettings.Values[account.Id];
			value["CN"]=account.Caption;
			value["RI"]=account.RefreshInterval.TotalMinutes;
			ApplicationData.Current.LocalSettings.Values[account.Id]=value;
		}
		public static void PutAccountBalances(Account account) {
			var value=new ApplicationDataCompositeValue();
			value["RD"]=account.RefreshDate == null ? (long?)null:account.RefreshDate.Value.ToBinary();
			value["RT"]=account.Token;
			if (account.Balance != null)
				account.Balance.Serialize(value);
			if (account.Balances != null)
				foreach (IBalance balance in account.Balances)
					balance.Serialize(value);
			ApplicationData.Current.LocalSettings.Values[account.BalancesId]=value;
		}
		public static Account GetAccount(string id) {
			var values=ApplicationData.Current.LocalSettings.Values;
			var value=(ApplicationDataCompositeValue)values[id];
			if (value == null) return null;

			Account account;
			switch ((Carrier)(byte)value["CR"]) {
				case Carrier.ZZ:
					account=new ZZAccount(id);
					break;
				case Carrier.Bite:
					account=new BiteAccount(id);
					break;
				default: return null;
			}

			account.Caption=(string)value["CN"];
			account.RefreshInterval=TimeSpan.FromMinutes((double)value["RI"]);

			LoadBalances(account);
			return account;
		}
		public static void LoadBalances(Account account) {
			var value=(ApplicationDataCompositeValue)ApplicationData.Current.LocalSettings.Values[account.Id];
			// Sesijas identifikators varēja mainīties pieprasot atlikumu, tāpēc to ielādē šeit.
			account.Token=(string)value["RT"];

			value=(ApplicationDataCompositeValue)ApplicationData.Current.LocalSettings.Values[account.BalancesId];
			if (value == null) return;

			long? binaryDate=(long?)value["RD"];
			account.RefreshDate=binaryDate == null ? null:(DateTime?)DateTime.FromBinary(binaryDate.Value);

			if (value.ContainsKey("AV"))
				account.Balance=new AccountBalance(value);
			var balances=new List<IBalance>(3);
			if (value.ContainsKey("VV"))
				balances.Add(new VoiceBalance(value));
			if (value.ContainsKey("SV"))
				balances.Add(new SmsBalance(value));
			if (value.ContainsKey("DV"))
				balances.Add(new DataBalance(value));
			if (value.ContainsKey("PV"))
				balances.Add(new PriceBalance(value));
			if (balances.Count != 0) account.Balances=balances;
			else account.Balances=null;
		}
		public static void RemoveAccount(ICollection<Account> accounts, Account account) {
			accounts.Remove(account);
			var values=ApplicationData.Current.LocalSettings.Values;
			values.Remove(account.Id); values.Remove(account.BalancesId);
		}
		public static IEnumerable<Account> GetAccounts() {
			var value=(string)ApplicationData.Current.LocalSettings.Values["Accounts"];
			if (string.IsNullOrEmpty(value)) yield break;
			var ids=value.Split(',');
			foreach (string id in ids)
				yield return GetAccount(id);
		}
		public static void SetAccounts(IEnumerable<Account> accounts) {
			ApplicationData.Current.LocalSettings.Values["Accounts"]=string.Join(",", accounts.Select(a => a.Id));
		}
	}
}