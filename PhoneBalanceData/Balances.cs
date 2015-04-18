using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace DrDax.PhoneBalance.Data {
	public interface IBalance {
		string Caption { get; }
		string ExpiresString { get; }
		string TileString { get; }
		DateTime Expires { get; }
		void Serialize(ApplicationDataCompositeValue serializedValue);
	}
	public abstract class Balance<TValue> : IBalance {
		public abstract char SerializationPrefix { get; }
		public abstract string Caption { get; }
		public abstract string TileString { get; }
		public List<Run> ValueRuns {
			get {
				valueRuns.Clear();
				// Iestata katru reizi, kad prasa, lai neraisītu kļūdu, kad pārkarto elementus sarakstā.
				SetValueRuns();
				return valueRuns;
			}
		}
		public string ExpiresString {
			get {
				return expiresString;
			}
		}

		public DateTime Expires {
			get {
				return expires;
			}
		}

		public Balance(TValue value, DateTime expires) {
			this.expiresString=expires.ToString(expires.Day < 10 ? " d.MMM":"d.MMM");
			this.value=value; this.expires=expires;
		}
		public virtual void Serialize(ApplicationDataCompositeValue serializedValue) {
			// Serializē uz string un long, jo daži tipi (decimal, DateTime) netiek atbalstīti.
			serializedValue[SerializationPrefix+"V"]=value.ToString();
			serializedValue[SerializationPrefix+"E"]=expires.ToBinary();
		}
		public abstract void SetValueRuns();

		protected void AddValue(string value, string units) {
			valueRuns.Add(new Run() { Text=value });
			valueRuns.Add(new Run() { Text=units, Foreground=DisplayResources.UnitsBrush });
		}

		private readonly string expiresString;
		protected readonly List<Run> valueRuns=new List<Run>(6);
		/// <summary>Atlikuma vērtība attēlošanai pirms pieejama svaigāka.</summary>
		protected readonly TValue value;
		protected readonly DateTime expires;
	}

	public class AccountBalance : Balance<decimal> {
		public override char SerializationPrefix { get { return 'A'; } }
		public override string Caption { get { return null; } }
		public override string TileString { get { return value.ToString("0.00 ")+Currency; } }

		public AccountBalance(decimal amount, DateTime expires) : base(amount, expires) {}
		public AccountBalance(ApplicationDataCompositeValue serializedValue)
			: base(decimal.Parse((string)serializedValue["AV"], CultureInfo.InvariantCulture), DateTime.FromBinary((long)serializedValue["AE"])) {}

		public override void SetValueRuns() {
			AddValue(value.ToString("0.00 "), Currency);
		}
		public override void Serialize(ApplicationDataCompositeValue serializedValue) {
			// Nodrošina uzticamu decimāldaļas atdalītāju.
			serializedValue["AV"]=value.ToString(CultureInfo.InvariantCulture);
			serializedValue["AE"]=expires.ToBinary();
		}

		private const string Currency="€";
	}
	public class DataBalance : Balance<long> {
		public const long GB=1073741824, MB=1048576, KB=1024;
		private readonly bool isLimit;
		public override char SerializationPrefix { get { return 'D'; } }
		public override string Caption { get { return DisplayResources.Strings.GetString(isLimit ? "DataBalanceLimit":"DataBalanceSpent"); } }
		public override string TileString {
			get {
				var formatted=GetFormattedValue();
				return formatted.Item1+formatted.Item2;
			}
		}

		public DataBalance(long bytes, DateTime expires, bool isLimit) : base(bytes, expires) {
			this.isLimit=isLimit;
		}
		public DataBalance(ApplicationDataCompositeValue serializedValue)
			: base(int.Parse((string)serializedValue["DV"]), DateTime.FromBinary((long)serializedValue["DE"])) {
				this.isLimit=(bool)serializedValue["DL"];
		}

		public override void Serialize(ApplicationDataCompositeValue serializedValue) {
			serializedValue["DV"]=value.ToString();
			serializedValue["DE"]=expires.ToBinary();
			serializedValue["DL"]=isLimit;
		}
		public override void SetValueRuns() {
			var formatted=GetFormattedValue();
			AddValue(formatted.Item1, formatted.Item2);
		}
		private Tuple<string, string> GetFormattedValue() {
			float powerBytes; string units;
			if (value >= GB) {
				powerBytes=value/(float)GB;
				units="DataUnitsGB";
			} else if (value >= MB) {
				powerBytes=value/(float)MB;
				units="DataUnitsMB";
			} else if (value >= KB) {
				powerBytes=value/(float)KB;
				units="DataUnitsKB";
			} else {
				powerBytes=value;
				units="DataUnitsB";
			}

			string format;
			if (powerBytes >= 100f) format="0 ";
			else if (powerBytes >= 10f) format="0.# ";
			else format="0.## ";
			return Tuple.Create(powerBytes.ToString(format), DisplayResources.Strings.GetString(units));
		}
	}
	public class SmsBalance : Balance<int> {
		public override char SerializationPrefix { get { return 'S'; } }
		public override string Caption { get { return DisplayResources.Strings.GetString("SmsBalance"); } }
		public override string TileString {
			get {
				if (value == int.MaxValue) return string.Empty;
				return value.ToString("0 ")+DisplayResources.Strings.GetString("SmsUnits");
			}
		}

		public SmsBalance(int messages, DateTime expires) : base(messages, expires) {}
		public SmsBalance(ApplicationDataCompositeValue serializedValue)
			: base(int.Parse((string)serializedValue["SV"]), DateTime.FromBinary((long)serializedValue["SE"])) {}

		public override void SetValueRuns() {
			AddValue(value == int.MaxValue ? "∞ ":value.ToString("0 "), DisplayResources.Strings.GetString("SmsUnits"));
		}
	}
	public class VoiceBalance : Balance<TimeSpan> {
		public override char SerializationPrefix { get { return 'V'; } }
		public override string Caption { get { return DisplayResources.Strings.GetString("VoiceBalance"); } }
		public override string TileString {
			get {
				var sb=new StringBuilder(11);
				if (value.Hours != 0) {
					sb.Append(value.Hours).Append("h");
					sb.Append(" ").Append(value.Minutes).Append("m");
					sb.Append(" ").Append(value.Seconds).Append("s");
				} else if (value.Minutes != 0) {
					sb.Append(value.Minutes).Append("m");
					sb.Append(" ").Append(value.Seconds).Append("s");
				} else sb.Append(value.Seconds).Append("s");
				return sb.ToString();
			}
		}

		public VoiceBalance(TimeSpan duration, DateTime expires) : base(duration, expires) {}
		public VoiceBalance(ApplicationDataCompositeValue serializedValue)
			: base(TimeSpan.Parse((string)serializedValue["VV"]), DateTime.FromBinary((long)serializedValue["VE"])) {}

		public override void SetValueRuns() {
			if (value.Hours != 0) {
				AddValue(value.Hours.ToString(), "h");
				AddValue(" "+value.Minutes, "m");
				AddValue(" "+value.Seconds, "s");
			} else if (value.Minutes != 0) {
				AddValue(value.Minutes.ToString(), "m");
				AddValue(" "+value.Seconds, "s");
			} else AddValue(value.Seconds.ToString(), "s");
		}
	}
	public class PriceBalance : Balance<string> {
		public override char SerializationPrefix { get { return 'P'; } }
		public override string Caption { get { return value; } }
		public override string TileString { get { return value; } }

		public PriceBalance(string caption, DateTime expires) : base(caption, expires) {}
		public PriceBalance(ApplicationDataCompositeValue serializedValue) 
			: base((string)serializedValue["PV"], DateTime.FromBinary((long)serializedValue["PE"])) {}

		public override void SetValueRuns() {}
	}

	// Resursi netiek padoti konstruktorā, jo to izsauc krietni pirms attēlošanas, un netiek izmantoti no App.Current.Resources, jo tie nesatur nepieciešamās tēmas vertības.
	public static class DisplayResources {
		/// <summary>Atlikuma mērvienību krāsa.</summary>
		public static Brush UnitsBrush;
		public static readonly ResourceLoader Strings=new ResourceLoader();
		public static readonly bool LanguageIsRussian=CultureInfo.CurrentUICulture.Name == "ru-RU";
	}
}