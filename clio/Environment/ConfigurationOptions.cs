using Clio.UserEnvironment;
using Clio.Utilities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Clio
{

	public class EnvironmentSettings
	{
		public string Uri
		{
			get; set;
		}

		public string Login
		{
			get; set;
		}

		public string Password
		{
			get; set;
		}

		public string Maintainer
		{
			get; set;
		}

		public bool IsNetCore
		{
			get; set;
		}

		public string ClientId
		{
			get; set;
		}

		public string ClientSecret
		{
			get; set;
		}

		private string _authAppUri;
		public string AuthAppUri
		{
			get
			{
				return string.IsNullOrEmpty(_authAppUri) ?
					Uri?.ToLower().Replace(".creatio.com", "-is.creatio.com/connect/token") : _authAppUri;
			}
			set
			{
				_authAppUri = value;
			}
		}

		public string SimpleloginUri
		{
			get
			{
				return string.IsNullOrEmpty(_authAppUri) ?
					Uri?.ToLower().Replace(".creatio.com", "-is.creatio.com/connect/token") : _authAppUri;
			}
			set
			{
				_authAppUri = value;
			}
		}

		internal void Merge(EnvironmentSettings environment)
		{
			if (!string.IsNullOrEmpty(environment.Login))
			{
				Login = environment.Login;
			}
			if (!string.IsNullOrEmpty(environment.Uri))
			{
				Uri = environment.Uri;
			}
			if (!string.IsNullOrEmpty(environment.Password))
			{
				Password = environment.Password;
			}
			if (!string.IsNullOrEmpty(environment.Maintainer))
			{
				Maintainer = environment.Maintainer;
			}
			if (environment.Safe.HasValue)
			{
				Safe = environment.Safe;
			}
			if (environment.DeveloperModeEnabled.HasValue)
			{
				DeveloperModeEnabled = environment.DeveloperModeEnabled;
			}
			IsNetCore = environment.IsNetCore;
			ClientId = environment.ClientId;
			ClientSecret = environment.ClientSecret;
			AuthAppUri = environment.AuthAppUri;
		}

		public bool? Safe
		{
			get; set;
		}


		public bool? DeveloperModeEnabled
		{
			get; set;
		}

		[JsonIgnore]
		public bool IsDevMode
		{
			get => DeveloperModeEnabled ?? false;
		}

	}

	public class Settings
	{
		public Settings()
		{
			Environments = new Dictionary<string, EnvironmentSettings>();
		}

		[JsonProperty("$schema")]
		public string Schema => "./schema.json";

		public string ActiveEnvironmentKey
		{
			get; set;
		}

		public EnvironmentSettings GetActiveEnviroment()
		{
			if (String.IsNullOrEmpty(ActiveEnvironmentKey)
				|| !Environments.ContainsKey(ActiveEnvironmentKey))
			{
				ActiveEnvironmentKey = Environments.First().Key;
				return Environments.First().Value;
			}
			else
			{
				return Environments[ActiveEnvironmentKey];
			}
		}

		public bool Autoupdate
		{
			get; set;
		}

		public Dictionary<string, EnvironmentSettings> Environments
		{
			get; set;
		}
	}

	public class SettingsRepository : ISettingsRepository
	{
		private const string FileName = "appsettings.json";
		private const string SchemaFileName = "schema.json";

		private Settings _settings;

		private string AppSettingsFolderPath
		{
			get
			{
				var userPath = Environment.GetEnvironmentVariable(
					RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
						"LOCALAPPDATA" : "HOME");
				var assy = Assembly.GetEntryAssembly();
				var companyName = assy.GetCustomAttributes<AssemblyCompanyAttribute>()
					.FirstOrDefault();
				var product = assy.GetCustomAttributes<AssemblyProductAttribute>()
					.FirstOrDefault();
				if (userPath == null)
				{
					userPath = "";
				}
				return Path.Combine(userPath, companyName?.Company, product?.Product);
			}
		}

		private string AppSettingsFilePath => Path.Combine(AppSettingsFolderPath, FileName);
		private string SchemaFilePath => Path.Combine(AppSettingsFolderPath, SchemaFileName);

		public SettingsRepository()
		{
			InitializeSettingsFile();
			InitSettings();
		}

		private void InitSettings()
		{
			try
			{
				var builder = new ConfigurationBuilder()
					.SetBasePath(Environment.CurrentDirectory)
					.AddJsonFile(AppSettingsFilePath, optional: false, reloadOnChange: true)
					.AddEnvironmentVariables();
				IConfigurationRoot configuration = builder.Build();
				_settings = new Settings();
				configuration.Bind(_settings);
			}
			catch (FormatException ex)
			{
				Console.WriteLine($"{ex.Message} Correct or delete settings file before use clio. File path: {AppSettingsFilePath}");
				throw;
			}
		}

		private void InitializeSettingsFile()
		{
			if (File.Exists(AppSettingsFilePath))
			{
				return;
			}
			if (!Directory.Exists(AppSettingsFolderPath))
			{
				Directory.CreateDirectory(AppSettingsFolderPath);
			}
			InitDefaultSettings();
			Save();
		}

		private void InitDefaultSettings()
		{
			_settings = new Settings();
			_settings.Environments.Add("dev", new EnvironmentSettings()
			{
				Login = "Supervisor",
				Password = "Supervisor",
				Uri = "http://localhost"
			});
			_settings.ActiveEnvironmentKey = "dev";
			SaveSchema();
		}

		/// <summary>
		/// Creates json schema file.
		/// This file is used by intelisence in vs code and other json editors.
		/// </summary>
		private void SaveSchema()
		{
			var baseDir = AppDomain.CurrentDomain.BaseDirectory;
			var tplPath = Path.Combine(baseDir, "tpl", "jsonschema", "schema.json.tpl");
			var tplContect = File.ReadAllText(tplPath);
			File.WriteAllText(SchemaFilePath, tplContect);
		}

		private void Save()
		{
			using (StreamWriter fileWriter = File.CreateText(AppSettingsFilePath))
			{
				JsonSerializer serializer = new JsonSerializer()
				{
					Formatting = Formatting.Indented,
					NullValueHandling = NullValueHandling.Ignore
				};

				//_settings.Schema = 
				serializer.Serialize(fileWriter, _settings);
			}

			if (!File.Exists(SchemaFilePath))
			{
				SaveSchema();
			}

		}

		public void ShowSettingsTo(TextWriter streamWriter, string environment = null)
		{
			JsonSerializer serializer = new JsonSerializer()
			{
				Formatting = Formatting.Indented,
				NullValueHandling = NullValueHandling.Ignore
			};
			if (String.IsNullOrEmpty(environment))
			{
				streamWriter.WriteLine($"\"appsetting file path: {AppSettingsFilePath}\"");
				serializer.Serialize(streamWriter, _settings);
			}
			else
			{
				serializer.Serialize(streamWriter, _settings.Environments[environment]);
			}
		}

		public EnvironmentSettings GetEnvironment(string name = null)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				var activeEnvironment = _settings.ActiveEnvironmentKey;
				return _settings.Environments[activeEnvironment];
			}
			if (!_settings.Environments.TryGetValue(name, out EnvironmentSettings environment))
			{
				environment = new EnvironmentSettings();
				_settings.Environments[name] = environment;
			}
			return environment;
		}

		private EnvironmentSettings FindEnvironment(string name = null)
		{
			EnvironmentSettings environment;
			try
			{
				environment = GetEnvironment(name);
			}
			catch
			{
				environment = new EnvironmentSettings();
			}
			return environment;
		}

		public EnvironmentSettings GetEnvironment(EnvironmentOptions options)
		{
			var result = new EnvironmentSettings();
			var settingsRepository = new SettingsRepository();
			var _settings = settingsRepository.FindEnvironment(options.Environment);
			result.Uri = string.IsNullOrEmpty(options.Uri) ? _settings.Uri : options.Uri;
			result.IsNetCore = options.IsNetCore ?? _settings.IsNetCore;
			result.DeveloperModeEnabled = options.DeveloperModeEnabled ?? _settings.DeveloperModeEnabled;
			result.Login = string.IsNullOrEmpty(options.Login) ? _settings.Login : options.Login;
			result.Password = string.IsNullOrEmpty(options.Password) ? _settings.Password : options.Password;
			result.ClientId = string.IsNullOrEmpty(options.ClientId) ? _settings.ClientId : options.ClientId;
			result.ClientSecret = string.IsNullOrEmpty(options.ClientSecret) ? _settings.ClientSecret : options.ClientSecret;
			result.AuthAppUri = string.IsNullOrEmpty(options.AuthAppUri) ? _settings.AuthAppUri : options.AuthAppUri;
			result.Maintainer =
				string.IsNullOrEmpty(options.Maintainer) ? _settings.Maintainer : options.Maintainer;
			if (_settings.Safe.HasValue && _settings.Safe.Value)
			{
				Console.WriteLine($"You try to apply the action on the production site {_settings.Uri}");
				Console.Write($"Do you want to continue? [Y/N]:");
				var answer = Console.ReadKey();
				Console.WriteLine();
				if (answer.KeyChar != 'y' && answer.KeyChar != 'Y')
				{
					Console.WriteLine("Operation was canceled by user");
					System.Environment.Exit(1);
				}
			}
			return result;
		}

		public bool IsEnvironmentExists(string name)
		{
			return _settings.Environments.ContainsKey(name);
		}

		public string FindEnvironmentNameByUri(string uri)
		{
			string safeUri = uri.TrimEnd('/');
			return _settings.Environments.FirstOrDefault(pair => pair.Value.Uri == safeUri).Key;
		}

		internal bool GetAutoupdate()
		{
			return _settings.Autoupdate;
		}

		public void ConfigureEnvironment(string name, EnvironmentSettings environment)
		{
			if (string.IsNullOrEmpty(name))
			{
				_settings.GetActiveEnviroment().Merge(environment);
			}
			else if (_settings.Environments.ContainsKey(name))
			{
				_settings.Environments[name].Merge(environment);
			}
			else
			{
				_settings.Environments.Add(name, environment);
			}
			Save();
		}

		public void SetActiveEnvironment(string activeEnvironment)
		{
			_settings.ActiveEnvironmentKey = activeEnvironment;
			Save();
		}

		public void RemoveEnvironment(string environment)
		{
			if (_settings.Environments.ContainsKey(environment))
			{
				_settings.Environments.Remove(environment);
				Save();
			}
			else
			{
				throw new KeyNotFoundException($"Application \"{environment}\" not found");
			}
		}

		public void OpenSettingsFile()
		{
			FileManager.OpenFile(AppSettingsFilePath);
		}
	}

}
