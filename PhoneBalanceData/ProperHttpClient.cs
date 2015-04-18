using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Security.Cryptography.Certificates;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace DrDax.PhoneBalance.Data {
	public class ProperHttpClient {
		public readonly JsonSerializer Serializer=new JsonSerializer();
		// Windows.Web nevis System.Net, jo ir nepieciešams atļaut neuzticamus SSL sertifikātus.
		private readonly HttpClient client;

		public ProperHttpClient() {
			var filter=new HttpBaseProtocolFilter();
			// Nodrošina, ka HTTP client katru reizi veiks GET pieprasījumu (kuri atšķiras ar galvenēm, bet ne ar adresi).
			filter.CacheControl.WriteBehavior=HttpCacheWriteBehavior.NoCache;
			// Atļauj Bites pašparakstīto sertifikātu, kurš izdots uz "test.testas" domēnu.
			filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
			filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);
			// Gadījumā, ja kādreiz serveris sadomās prasīt paroli, neļauj to attēlot grafiskā saskarnē.
			filter.AllowUI=false;
			client=new HttpClient(filter);
			client.DefaultRequestHeaders.Add("User-Agent", "Dalvik/1.6.0 (Linux; U; Android 4.0.3; GT-I9100 Build/IML74K)");
		}

		public async Task<TResponse> PostAsync<TResponse>(string url, HttpFormUrlEncodedContent content) {
			Debug.WriteLine("HTTP post "+url);
			return await GetResponse<TResponse>(await client.PostAsync(new Uri(url), content));
		}
		public async Task<TResponse> SendAsync<TResponse>(HttpRequestMessage request) {
			Debug.WriteLine("HTTP {0} {1}", request.Method, request.RequestUri);
			return await GetResponse<TResponse>(await client.SendRequestAsync(request));
		}

		private async Task<TResponse> GetResponse<TResponse>(HttpResponseMessage response) {
			Debug.WriteLine("HTTP status code "+response.StatusCode);
			if (response.StatusCode == HttpStatusCode.Ok)
				return await Deserialize<TResponse>(response);
			return default(TResponse);
		}
		private async Task<TResponse> Deserialize<TResponse>(HttpResponseMessage response) {
			TResponse responseObject;

			// AsStreamForRead ir System.IO metode, kura pārveido plūsmu no WinRT uz .NET, jo Newtonsoft Json prot strādāt tikai ar tādu:
			// http://stackoverflow.com/questions/7669311/is-there-a-way-to-convert-a-system-io-stream-to-a-windows-storage-streams-irando
			using (var reader=new StreamReader((await response.Content.ReadAsInputStreamAsync()).AsStreamForRead()))
			using (JsonReader jsonReader=new JsonTextReader(reader))
				responseObject=(TResponse)Serializer.Deserialize(jsonReader, typeof(TResponse));

			return responseObject;
		}
	}
}