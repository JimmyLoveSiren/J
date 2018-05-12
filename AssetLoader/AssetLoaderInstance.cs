﻿namespace J
{
	using UnityEngine;

	public partial class AssetLoaderInstance : SingletonMonoBehaviour<AssetLoaderInstance>
	{
		static readonly char[] Delimiters = { '/', '\\' };

		[SerializeField] bool m_DontDestroyOnLoad = true;
		[SerializeField] bool m_SimulationMode = true;
		[SerializeField] bool m_AutoLoadManifest = true;

		[SerializeField] string STANDALONE_URI;
		[SerializeField] string ANDROID_URI;
		[SerializeField] string IOS_URI;

		public bool SimulationMode => Application.isEditor && m_SimulationMode && AssetGraphLoader.IsValid;
		public bool AutoLoadManifest => m_AutoLoadManifest;
		public string AutoLoadManifestUri =>
#if UNITY_EDITOR
			STANDALONE_URI
#elif UNITY_ANDROID
			ANDROID_URI
#elif UNITY_IOS
			IOS_URI
#else
			STANDALONE_URI
#endif
			;

		protected override void SingletonAwake()
		{
			base.SingletonAwake();
			if (m_DontDestroyOnLoad)
				DontDestroyOnLoad(gameObject);
			if (!SimulationMode && AutoLoadManifest && !string.IsNullOrWhiteSpace(AutoLoadManifestUri))
				LoadManifest(AutoLoadManifestUri);
		}

		protected override void SingletonOnDestroy()
		{
			base.SingletonOnDestroy();
			m_BundlePending.Dispose();
		}
	}
}
