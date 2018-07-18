﻿#if !UNITY_2018_1_OR_NEWER
using UnityWebRequestAssetBundle = UnityEngine.Networking.UnityWebRequest;
#endif

namespace J
{
	using System;
	using System.IO;
	using UniRx;
	using UnityEngine;
	using UnityEngine.Networking;

	partial class AssetLoaderInstance
	{
		public const string ManifestVersionKey = "AssetLoader.ManifestVersion";
		public const string ManifestETagKey = "AssetLoader.ManifestETag";

		public ManifestStatus ManifestStatus
		{
			get { return m_ManifestStatus.Value; }
			private set { m_ManifestStatus.Value = value; }
		}

		public AssetBundleManifest Manifest { get; private set; }
		public int ManifestVersion { get; private set; }
		public string RootUrl { get; set; }

		public IObservable<Unit> LoadManifest(string url = null, bool? setRootUrl = null) => Observable.Defer(() =>
		{
			// TODO
			ManifestStatus = ManifestStatus.Loading;
			if (string.IsNullOrEmpty(url))
			{
				url = PresetManifestUrl;
				if (string.IsNullOrEmpty(url)) url = "/";
			}
			//RequestInfo requestInfo = null;
			RequestInfo requestInfo = new RequestInfo(null, 0);
			AssetBundle manifestBundle = null;
			//return SendAssetBundleRequest(url, ManifestVersionKey, ManifestETagKey).Select(info =>
			//{
			//	requestInfo = info;
			//	return info.Request;
			//}).LoadAssetBundle().ContinueWith(bundle =>
			return UnityWebRequestAssetBundle.GetAssetBundle(url).SendAsObservable().LoadAssetBundle().ContinueWith(bundle =>
			{
				manifestBundle = bundle;
				return bundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest")
					.AsAsyncOperationObservable();
			}).Select(bundleRequest =>
			{
				var manifest = bundleRequest.asset as AssetBundleManifest;
				if (manifest == null) throw new InvalidDataException("AssetBundleManifest not found.");
				if (setRootUrl ?? true) RootUrl = url.Substring(0, url.LastIndexOfAny(Delimiters) + 1);
				SetManifest(manifest, requestInfo.Version);
				return Unit.Default;
			}).Finally(() =>
			{
				if (manifestBundle != null) manifestBundle.Unload(false);
				if (ManifestStatus == ManifestStatus.Loading)
					ManifestStatus = Manifest != null ? ManifestStatus.Loaded : ManifestStatus.NotLoaded;
			});
		});

		void SetManifest(AssetBundleManifest manifest, int version)
		{
			if (manifest == null) throw new ArgumentNullException(nameof(manifest));
			UnloadUnusedBundles(false);
			Manifest = manifest;
			ManifestVersion = version;
			CreateNormToActualNameDict();
			ManifestStatus = ManifestStatus.Loaded;
		}

		void CreateNormToActualNameDict()
		{
			m_NormToActual.Clear();
			var all = Manifest.GetAllAssetBundles();
			foreach (string actualName in all)
				m_NormToActual.Add(ActualToNormName(actualName), actualName);
		}

		string ActualToNormName(string actualName)
		{
			string hash = Manifest.GetAssetBundleHash(actualName).ToString();
			if (actualName.EndsWith(hash, StringComparison.OrdinalIgnoreCase))
				return actualName.Substring(0, actualName.Length - hash.Length - 1);
			return actualName;
		}

		public IObservable<Unit> WaitForManifestLoaded(bool? autoLoad = null) => Observable.Defer(() =>
		{
			if (ManifestStatus == ManifestStatus.Loaded)
				return Observable.ReturnUnit();
			if (ManifestStatus == ManifestStatus.NotLoaded && (autoLoad ?? AutoLoadManifest))
				LoadManifest().Subscribe();
			return m_ManifestStatus.FirstOrEmpty(status =>
			{
				if (status == ManifestStatus.NotLoaded)
					throw new InvalidOperationException("No AssetBundleManifest loading or loaded.");
				return status == ManifestStatus.Loaded;
			}).AsUnitObservable();
		});

		public bool ManifestContains(BundleEntry entry) => ManifestContains(entry.NormName);
		bool ManifestContains(string normBundleName)
		{
			ThrowIfManifestNotLoaded();
			return m_NormToActual.ContainsKey(normBundleName);
		}

		void ThrowIfManifestNotLoaded()
		{
			if (ManifestStatus != ManifestStatus.Loaded)
				throw new InvalidOperationException("AssetBundleManifest not loaded.");
		}
	}

	public enum ManifestStatus
	{
		NotLoaded,
		Loading,
		Loaded,
	}

	partial class AssetLoader
	{
		public static ManifestStatus ManifestStatus => Instance ? Instance.ManifestStatus : ManifestStatus.NotLoaded;

		public static AssetBundleManifest Manifest => Instance.Manifest;

		public static int ManifestVersion => Instance.ManifestVersion;

		public static string RootUrl { get { return Instance.RootUrl; } set { Instance.RootUrl = value; } }

		public static IObservable<Unit> LoadManifest(string url = null, bool? setRootUrl = null) =>
			Instance.LoadManifest(url, setRootUrl);

		public static IObservable<Unit> WaitForManifestLoaded(bool? autoLoad = null) =>
			Instance.WaitForManifestLoaded(autoLoad);

		public static bool ManifestContains(string bundleName) =>
			ManifestStatus == ManifestStatus.Loaded && Instance.ManifestContains(new BundleEntry(bundleName));
	}
}
