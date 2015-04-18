using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace DrDax.PhoneBalance.Data {
	public class ZZAccount : Account {
		private const string ClientId="AppZeltazivtinaLV",
			ClientSecret="%7BN%2368-mr'uA!t_6",
			ClientVersion="1.0.1";
		public ZZAccount(string id) : base (Carrier.ZZ, id) {}

		public static async Task<ZZAccount> Login(ProperHttpClient client, string number, string password) {
			var loginParams=new Dictionary<string, string>() {
					{ "grant_type", "password" },
					{ "scope", "userpriceplans%20accountinformation%20userbucketbalance" }, // Pietiekams atlikuma noskaidrošanai.
					// Pilns scope, kuru izmanto Android lietotne: temporarypassword%20balanceprolongation%20voucherrefill%20userpriceplans%20priceplanordering%20offeredpriceplans%20userservices%20serviceordering%20offerings%20registergcmtoken%20accountinformation%20subscription%20userbucketbalance%20billinglanguage%20whois
					{ "client_id", ClientId },
					{ "client_secret", ClientSecret },
					{ "username", number },
					{ "password", password },
					{ "auth_by_IP", "false" },
					{ "version_no", ClientVersion }
				};

			var oauth=await client.PostAsync<OAuthToken>("https://app.zeltazivtina.lv/thin/rest/Auth", new HttpFormUrlEncodedContent(loginParams));
			if (oauth == null) return null;
			System.Diagnostics.Debug.WriteLine("ZZ access token "+oauth.access_token);
			var account=new ZZAccount(null);
			account.SetTokens(oauth);
			return account;
		}

		public override async Task SetBalance(ProperHttpClient client) {
			if (accessToken == null || tokenExpires < DateTime.Now) {
				var refreshParams=new Dictionary<string, string>() {
					{ "grant_type", "refresh_token" },
					{ "client_id", ClientId },
					{ "client_secret", ClientSecret },
					{ "refresh_token", Token },
					{ "version_no", ClientVersion }
				};
				// Saņem jaunu sesijas kodu.
				var oauth=await client.PostAsync<OAuthToken>("https://app.zeltazivtina.lv/thin/rest/Auth", new HttpFormUrlEncodedContent(refreshParams));
				if (oauth == null) return;
				System.Diagnostics.Debug.WriteLine("ZZ access token "+oauth.access_token);
				SetTokens(oauth);
			}

			var request=new HttpRequestMessage(HttpMethod.Get, new Uri("https://app.zeltazivtina.lv/thin/rest/getsummaryinfo"));
			request.Headers.Authorization=new HttpCredentialsHeaderValue("bearer", accessToken);
			var info=await client.SendAsync<ZZBalance>(request);
			if (info == null) return;
			this.Balance=new AccountBalance(info.Balance, info.BalanceExpirationDate);
			var balances=new List<IBalance>(4);
			if (info.AddonSecondsBalance.HasValue)
				balances.Add(new VoiceBalance(TimeSpan.FromSeconds(info.AddonSecondsBalance.Value), info.AddonSecondsBalanceExpirationDate.Value));
			if (info.AddonSmsBalanceExpirationDate.HasValue)
				balances.Add(new SmsBalance(info.AddonSmsBalance.HasValue ? info.AddonSmsBalance.Value:int.MaxValue, info.AddonSmsBalanceExpirationDate.Value));
			if (info.DataUsedToday != 0)
				balances.Add(new DataBalance(info.DataUsedToday, DateTime.Today.AddSeconds(23*60*60+59*60+59), false));
			else if (info.AddonDataBalance.HasValue)
				balances.Add(new DataBalance(info.AddonDataBalance.Value, info.AddonDataBalanceExpirationDate.Value, true));
			if (info.PricePlanList[0].ID != 102 && info.PricePlanList[0].ValidUntil.HasValue)
				balances.Add(new PriceBalance(info.PricePlanList[0].Name, info.PricePlanList[0].ValidUntil.Value));
			this.Balances=balances;
			this.RefreshDate=DateTime.Now;
			Settings.PutAccountBalances(this);
			UpdateTile();
		}

		private void SetTokens(OAuthToken oauth) {
			accessToken=oauth.access_token;
			tokenExpires=DateTime.Now.AddSeconds(oauth.expires_in);
			Token=oauth.refresh_token;
		}

		private string accessToken;
		private DateTime tokenExpires=DateTime.Now.AddDays(-1);
	}

	internal class OAuthToken {
		public string scope, access_token, token_type, refresh_token;
		/// <summary>Sekundes kopš izsaukuma brīža, kamēr <see cref="access_token"/> ir izmantojams.</summary>
		public int expires_in;
	}
	internal class ZZBalance {
		public decimal Balance;
		public DateTime BalanceExpirationDate;

		public int? AddonSecondsBalance;
		public DateTime? AddonSecondsBalanceExpirationDate;

		public int? AddonSmsBalance;
		public DateTime? AddonSmsBalanceExpirationDate;

		public long? AddonDataBalance;
		public DateTime? AddonDataBalanceExpirationDate;

		public long DataUsedToday;

		public List<PricePlan> PricePlanList;
	}
	internal class PricePlan {
		public int ID;
		public string Name;
		public DateTime? ValidUntil;
	}
}