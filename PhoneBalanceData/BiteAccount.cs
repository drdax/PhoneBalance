using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace DrDax.PhoneBalance.Data {
	public class BiteAccount : Account {
		public BiteAccount(string id) : base (Carrier.Bite, id) {}

		public static async Task RequestPassword(ProperHttpClient client, string number) {
			var response=await CallService<object>(client, 80, new BiteRequest {
				method="authMSISDN",
				@params=new Dictionary<string, string>(1) {
						{ "msisdn", "371"+number }
					}
			});
			return;
		}
		public static async Task<BiteAccount> Login(ProperHttpClient client, string number, string password) {
			var response=await CallService<BiteLogin>(client, 102, new BiteRequest {
				method="authPassword",
				@params=new Dictionary<string, string>(2) {
						{ "password", password },
						{ "msisdn", "371"+number }
					}
			});
			if (response == null) return null;
			var account=new BiteAccount(null);
			account.Token=response.result.securityKey;
			System.Diagnostics.Debug.WriteLine("Bite token "+account.Token);
			return account;
		}
		public override async Task SetBalance(ProperHttpClient client) {
			// Oriģināla programma padod masīvu ar vienu elementu uz androidb.json (cita adrese).
			// Laikam tā var prasīt atlikumu par vairākiem kontiem, bet mūsu arhitektūra to neļauj, tāpēc izmantojam adresi, kura sagaida vienīgo elementu.
			var response=await CallService<BiteBalance>(client, 113, new BiteRequest {
				method="getData",
				@params=new Dictionary<string, string>(1) {
						{ "securityKey", Token }
					}
			});
			if (response == null) return;
			var data=response.result;

			this.Balance=new AccountBalance(data.customerAmount,
				(data.customerType == BiteCustomer.PREPAID ? data.customerBalanceExp:data.customerRatePlanExp).Value);
			var balances=new List<IBalance>(1);
			if (data.customerDataUsage != 0)
				balances.Add(new DataBalance(data.customerDataUsage*DataBalance.MB, this.Balance.Expires, false));
			this.Balances=balances;
			this.RefreshDate=DateTime.Now;
			Settings.PutAccountBalances(this);
		}

		private static async Task<BiteResponse<TResult>> CallService<TResult>(ProperHttpClient client, int requestLength, object request) {
			var sb=new StringBuilder(requestLength);
			StringWriter sw=new StringWriter(sb);
			using (JsonTextWriter jsonWriter=new JsonTextWriter(sw))
				client.Serializer.Serialize(jsonWriter, request);
			string json=sw.ToString();
			System.Diagnostics.Debug.WriteLine(json);

			var content=new HttpStringContent(json);
			content.Headers.ContentType=new HttpMediaTypeHeaderValue("application/json");
			var httpRequest=new HttpRequestMessage(HttpMethod.Post, new Uri("https://213.226.139.54/prest/android.json"));
			httpRequest.Content=content;
			var response=await client.SendAsync<BiteResponse<TResult>>(httpRequest);
			if (response != null && response.error != null) {
				System.Diagnostics.Debug.WriteLine("Bite error message: "+response.error.message);
				throw new Exception(response.error.message);
			}
			return response;
		}
	}

	//{"id":3,"jsonrpc":"2.0","method":"getData","params":{"securityKey":"450dbb5ada64e3698a899d6bbb9f511ebe4b0111"}}
	internal class BiteRequest {
		/// <summary>Paziņojuma identifikators.</summary>
		/// <remarks>Oriģinālā paziņojuma identifikators palielinās ar katru izsaukumu.</remarks>
		public int id=1;
		/// <summary>Protokola versija.</summary>
		public string jsonrpc="2.0";
		/// <summary>Izsaucamā metode.</summary>
		public string method;
		/// <summary>Metodes parametri.</summary>
		public Dictionary<string, string> @params;
	}
	internal class BiteResponse<TResult> {
		/// <summary>Atbildes vērtības. <c>null</c> kļūdas gadījumā.</summary>
		public TResult result;
		/// <summary>Kļūdas detaļas. <c>null</c> veiksmes gadījumā.</summary>
		public BiteError error;
	}
	internal class BiteLogin {
		public string securityKey;
	}
	internal class BiteBalance {
		public BiteCustomer customerType;
		public decimal customerAmount;
		public int customerDataUsage; // Ir vēl customerRoamDataUsage, kuru jāizmanto ja isRoaming ir true, bet pagaidām to neatbalstām.

		[JsonConverter(typeof(BiteDateTimeConverter))]
		public DateTime? customerBalanceExp;
		[JsonConverter(typeof(BiteDateTimeConverter))]
		public DateTime? customerRatePlanExp;
	}
	internal class BiteDateTimeConverter : IsoDateTimeConverter {
		public BiteDateTimeConverter() {
			Culture=CultureInfo.InvariantCulture;
			DateTimeFormat="yyyy-MM-dd HH:mm:ss";
		}
	}
	internal enum BiteCustomer {
		PREPAID, POSTPAID
	}
	internal class BiteError {
		/// <summary>Kļūda apsraksts angļu valodā.</summary>
		public string message;
		// Ir vēl skaitliskais "code", bet nav pētīts, kā to atšifrēt.
	}
}