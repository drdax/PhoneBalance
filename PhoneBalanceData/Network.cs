using Windows.Networking.Connectivity;

namespace DrDax.PhoneBalance.Data {
	public static class Network {
		public static bool Disconnected {
			get {
				ConnectionProfile connections = NetworkInformation.GetInternetConnectionProfile();
				return connections == null || connections.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.InternetAccess;
			}
		}
	}
}