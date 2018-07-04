﻿namespace J
{
	using J.Internal;
	using System;
	using System.Collections.Generic;
	using UniRx;
	using Object = UnityEngine.Object;

	partial class AssetLoaderInstance
	{
		static readonly HashSet<Object> Collector = new HashSet<Object>(); // FIXME

		IObservable<Object> LoadCore(AssetEntry entry)
		{
			return GetAssetBundleWithDependencies(entry.BundleEntry).ContinueWith(bundle =>
			{
				switch (entry.LoadMethod)
				{
					case LoadMethod.Single:
						return bundle.LoadAssetAsync(entry.AssetName, entry.AssetType)
							.AsAsyncOperationObservable().Select(req => req.asset)
							.Do(obj => Collector.Add(obj));
					case LoadMethod.Multi:
						return bundle.LoadAssetWithSubAssetsAsync(entry.AssetName, entry.AssetType)
							.AsAsyncOperationObservable().SelectMany(req => req.allAssets)
							.Do(obj => Collector.Add(obj));
					default: throw new ArgumentException("Unknown LoadMethod. " + entry.LoadMethod);
				}
			});
		}

		public LoadAssetDelegate Load { get; private set; }

		void UpdateLoadMethod()
		{
			if (IsSimulationEnabled)
			{
				switch (m_Simulation)
				{
					case AssetSimulation.AssetDatabase:
						Load = AssetDatabaseLoader.Load; return;
					case AssetSimulation.AssetGraph:
						Load = AssetGraphLoader.Load; return;
				}
			}
			Load = LoadCore;
		}

		public bool IsSimulationEnabled
		{
			get
			{
				switch (m_Simulation)
				{
					case AssetSimulation.AssetDatabase: return AssetDatabaseLoader.IsAvailable;
					case AssetSimulation.AssetGraph: return AssetGraphLoader.IsAvailable;
					default: return false;
				}
			}
		}
	}

	public enum AssetSimulation
	{
		Disable = 0,
		AssetDatabase = 1,
		AssetGraph = 2,
	}

	partial class AssetLoader
	{
		public static bool IsSimulationEnabled => Instance.IsSimulationEnabled;

		public static IObservable<Object> Load(string bundleName, string assetName = null, Type assetType = null) =>
			Instance.Load(new AssetEntry(bundleName, assetName, assetType, LoadMethod.Single));
		public static IObservable<Object> Load(string bundleName, Type assetType) =>
			Instance.Load(new AssetEntry(bundleName, null, assetType, LoadMethod.Single));
		public static IObservable<T> Load<T>(string bundleName, string assetName = null) where T : Object =>
			Instance.Load(new AssetEntry(bundleName, assetName, typeof(T), LoadMethod.Single)).Select(obj => (T)obj);

		public static IObservable<Object> LoadMulti(string bundleName, string assetName = null, Type assetType = null) =>
			Instance.Load(new AssetEntry(bundleName, assetName, assetType, LoadMethod.Multi));
		public static IObservable<Object> LoadMulti(string bundleName, Type assetType) =>
			Instance.Load(new AssetEntry(bundleName, null, assetType, LoadMethod.Multi));
		public static IObservable<T> LoadMulti<T>(string bundleName, string assetName = null) where T : Object =>
			Instance.Load(new AssetEntry(bundleName, assetName, typeof(T), LoadMethod.Multi)).Select(obj => (T)obj);
	}
}
