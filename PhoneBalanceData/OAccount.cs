using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace DrDax.PhoneBalance.Data {
	public class OAccount : Account {
		public OAccount(string id) : base(Carrier.OKarte, id) {}
		public static async Task<OAccount> Login(ProperHttpClient client, string number, string password) {
			// Ar pieteikšanos pietiktu numura un paroles pārbaudei, bet vajag atsijāt pastāvīgā pieslēguma īpašniekus, jo tiem atlikums izskatās savādāk un pašlaik netiek atbalstīts.
			var balance=await GetBalance(client, number, password);
			if (balance == null) return null;
			var account=new OAccount(null) {
				Token=number+password,
				Balance=balance,
				balanceSetInLogin=true
			};
			return account;
		}
		public override async Task SetBalance(ProperHttpClient client) {
			if (!balanceSetInLogin) {
				var balance=await GetBalance(client, Token.Substring(0, 8), Token.Substring(8));
				if (balance == null) return;
				this.Balance=balance;
			}
			balanceSetInLogin=false;
			this.RefreshDate=DateTime.Now;
			Settings.PutAccountBalances(this);
			UpdateTile();
		}
		private static async Task<AccountBalance> GetBalance(ProperHttpClient clientWrapper, string number, string password) {
			var cookiesUri=new Uri("https://mans.lmt.lv/");
			// Dzēš iepriekšējās sesijas kūkas. ToList novēŗš dzēšanas konfliktu.
			foreach (var cookie in clientWrapper.CookieManager.GetCookies(cookiesUri).ToList())
				clientWrapper.CookieManager.DeleteCookie(cookie);

			var client=clientWrapper.Client;
			// Ielādē sākumlappusi. Sāk sesiju un noskaidro pieteikšanās žetonu.
			var response=await client.SendRequestAsync(new HttpRequestMessage(HttpMethod.Head, new Uri("https://mans.lmt.lv/lv/auth")));
			Debug.WriteLine("LMT home "+response.StatusCode);
			if (response.StatusCode != HttpStatusCode.Ok) return null;

			string csrf=clientWrapper.CookieManager.GetCookies(cookiesUri).First(c => c.Name == "lmt_csrf_cookie_name").Value;
			var jsonHeader=new HttpMediaTypeWithQualityHeaderValue("application/json");
			var xhrHeader=new KeyValuePair<string, string>("X-Requested-With", "XMLHttpRequest");

			// Piesakās ar tālruņa numuru un paroli. Nodotās galvenes ir obligātas.
			var request=new HttpRequestMessage(HttpMethod.Post, new Uri("https://mans.lmt.lv/lv/auth/login"));
			request.Headers.Accept.Add(jsonHeader);
			request.Headers.Add(xhrHeader);
			request.Content=new HttpFormUrlEncodedContent(new Dictionary<string, string>(4) {
				{ "lmt_csrf_name", csrf },
				{ "login-name", number },
				{ "login-pass", password }
				//{ "login-code", string.Empty }
			});
			response=await client.SendRequestAsync(request);
			Debug.WriteLine("LMT login "+response.StatusCode);
			// {"success":true,"step":"\/lv\/auth\/access_info"}
			var json=await response.Content.ReadAsStringAsync();
			Debug.WriteLine(json);
			if (response.StatusCode != HttpStatusCode.Ok || !json.StartsWith("{\"success\":true")) return null;

			// Lai mazinātu varbūtību, ka nākamais izsaukums atgriezīs "wait", gaida 2 sekundes tāpat kā LMT skripts.
			await Task.Delay(2000);
			// Apstiprina pieteikšanos. Vienkārši obligāts solis.
			request=new HttpRequestMessage(HttpMethod.Get, new Uri("https://mans.lmt.lv/lv/auth/access_info"));
			request.Headers.Accept.Add(jsonHeader);
			request.Headers.Add(xhrHeader);
			response=await client.SendRequestAsync(request);
			Debug.WriteLine("LMT authenticate "+response.StatusCode);
			// {"redirect":"\/lv\/index.php"} vai {"wait":true}
			json=await response.Content.ReadAsStringAsync();
			Debug.WriteLine(json);
			if (response.StatusCode != HttpStatusCode.Ok || !json.StartsWith("{\"redirect\":")) return null;

			// Iziet caur pāradresācijām, pieprasot lappusi ar atlikuma datiem.
			response=await client.GetAsync(new Uri("https://mans.lmt.lv/lv/index.php"));
			Debug.WriteLine("LMT balance redirect 1 {0} {1}", response.StatusCode, response.Headers.Location);
			if (response.StatusCode != HttpStatusCode.Found) return null;
			response=await client.GetAsync(new Uri("https://mans.lmt.lv/lv/")); // https://mans.lmt.lv/lv/
			Debug.WriteLine("LMT balance redirect 2 {0} {1}", response.StatusCode, response.Headers.Location);
			if (response.StatusCode != HttpStatusCode.Found) return null;
			response=await client.GetAsync(response.Headers.Location); // https://mans.lmt.lv/lv/icenter/info.php
			Debug.WriteLine("LMT balance redirect 3 {0} {1}", response.StatusCode, response.Headers.Location);
			if (response.StatusCode != HttpStatusCode.Found) return null;
			response=await client.GetAsync(response.Headers.Location); // https://mans.lmt.lv/lv/icenter/
			Debug.WriteLine("LMT balance "+response.StatusCode);
			if (response.StatusCode != HttpStatusCode.Ok) return null;
	
			string html=await response.Content.ReadAsStringAsync();
			// HTMLā ir dažādas rindu pārneses, bet šajā blokā CR-LF. Lauka nosaukums un datuma formāts krievu valodā atšķiras, bet šeit visi pieprasījumi attiecas uz latviešu valodu.
			var match=Regex.Match(html, @"<td class=""text okavanss""><span class=""ls"">Ls</span><span class=""eiro"">€</span> *([0-9]+\.[0-9][0-9])</td>.+Avansa derīguma termiņš</a></th>\r\n\s+<td class=""text okavanss"">([0-3][0-9]\.[01][0-9]\.[0-9]{4})\. plkst\. ([012][0-9]:[0-5][0-9])</td>", RegexOptions.Singleline);
			decimal amount=decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			DateTime expires=DateTime.ParseExact(match.Groups[2].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture).Add(TimeSpan.ParseExact(match.Groups[3].Value, "hh\\:mm", CultureInfo.InvariantCulture));
			Debug.WriteLine("LMT amount "+amount+" expires "+expires.ToString("F"));
			// Aizver sesiju. Sesijas ilgums kopš pieteikšanās ir viena stunda.
			response=await client.GetAsync(new Uri("https://mans.lmt.lv/lv/?logout=true"));
			Debug.WriteLine("LMT logout "+response.StatusCode);
			// Ignorē pāradresācijas, jo galvenais izsaukums jau veikts.
			if (response.StatusCode != HttpStatusCode.Found) return null;
			return new AccountBalance(amount, expires);
		}

		private bool balanceSetInLogin=false;
	}
}