using System;
using System.Collections;
using System.Reflection;
using JSAM;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

public class SpatialAudioPlaybackTests
{
    private static readonly Type ManagerType = Type.GetType("SpatialAudioManager, Assembly-CSharp", true);
    private static readonly Type SoundType = Type.GetType("DefaultAudioSounds, Assembly-CSharp", true);
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject root;
    private GameObject audioRoot;
    private Component manager;
    private Camera camera;
    private object previousInstance;
    private object sound;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        previousInstance = ManagerType.GetProperty("Instance").GetValue(null);
        root = new GameObject("Spatial audio playback tests");
        camera = root.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.aspect = 16f / 9f;
        root.AddComponent<AudioListener>();
        manager = new GameObject("Spatial audio manager").AddComponent(ManagerType);
        manager.transform.SetParent(root.transform);
        audioRoot = new GameObject("Proximity test JSAM");
        audioRoot.SetActive(false);
        var audioManager = audioRoot.AddComponent<AudioManager>();
        typeof(AudioManager).GetField("preloadedLibraries", PrivateInstance).SetValue(audioManager,
            new[] { AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Audio/DefaultAudio.asset") });
        audioRoot.SetActive(true);
        sound = Enum.Parse(SoundType, "Play_Props_Potion_ZhiLiao");
        yield return null;
#else
        Assert.Ignore("These tests load the authored sound library through the Editor.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null) Object.Destroy(root);
        yield return null;
        if (audioRoot != null) Object.Destroy(audioRoot);
        ManagerType.GetProperty("Instance").SetValue(null, previousInstance);
        yield return null;
    }

    private void Play(Vector3 position)
    {
        foreach (MethodInfo method in ManagerType.GetMethods())
        {
            if (method.Name != "PlayLocalSound" || method.GetParameters()[1].ParameterType != typeof(Vector3)) continue;
            method.MakeGenericMethod(SoundType).Invoke(manager, new[] { sound, (object)position });
            return;
        }
        Assert.Fail("Positional playback entry point not found.");
    }

    private void UpdateVolumes()
    {
        ManagerType.GetMethod("LateUpdate", PrivateInstance).Invoke(manager, null);
    }

    [Test]
    public void OffscreenRequestsDoNotStartAChannel()
    {
        int count = 0;
        Action<SoundChannelHelper, SoundFileObject> onPlayed = (helper, file) => count++;
        AudioManager.OnSoundPlayed += onPlayed;
        try
        {
            Play(camera.ViewportToWorldPoint(new Vector3(1.1f, 0.5f, 25f)));
            Assert.That(count, Is.Zero);
        }
        finally
        {
            AudioManager.OnSoundPlayed -= onPlayed;
        }
    }

    [Test]
    public void TwoDimensionalItemClipUsesProximityAndDoesNotCompoundItsVolume()
    {
        SoundChannelHelper played = null;
        Action<SoundChannelHelper, SoundFileObject> onPlayed = (helper, file) => played = helper;
        AudioManager.OnSoundPlayed += onPlayed;
        try
        {
            Play(camera.ViewportToWorldPoint(new Vector3(0.95f, 0.5f, 25f)));
            Assert.That(played, Is.Not.Null);
            Assert.That(played.AudioFile.spatialize, Is.False);
            for (int frame = 0; frame < 5; frame++) UpdateVolumes();
            Assert.That(played.AudioSource.volume, Is.EqualTo(played.Volume * 0.5f).Within(0.0001f));
            camera.transform.position = Vector3.right * 100f;
            UpdateVolumes();
            Assert.That(played.AudioSource.volume, Is.Zero);
            camera.transform.position = Vector3.zero;
            UpdateVolumes();
            Assert.That(played.AudioSource.volume, Is.EqualTo(played.Volume * 0.5f).Within(0.0001f));
        }
        finally
        {
            AudioManager.OnSoundPlayed -= onPlayed;
        }
    }

    [Test]
    public void ReusedChannelDoesNotKeepThePreviousSoundsProximity()
    {
        SoundChannelHelper played = null;
        Action<SoundChannelHelper, SoundFileObject> onPlayed = (helper, file) => played = helper;
        AudioManager.OnSoundPlayed += onPlayed;
        try
        {
            Play(camera.ViewportToWorldPoint(new Vector3(0.95f, 0.5f, 25f)));
            Assert.That(played, Is.Not.Null);
            SoundChannelHelper reused = played;
            SoundFileObject file = reused.AudioFile;
            reused.Stop();
            AudioManager.PlaySound(file, Vector3.zero, reused);
            camera.transform.position = Vector3.right * 100f;
            UpdateVolumes();
            Assert.That(reused.AudioSource.volume, Is.EqualTo(reused.Volume).Within(0.0001f));
        }
        finally
        {
            AudioManager.OnSoundPlayed -= onPlayed;
        }
    }

    [Test]
    public void DisablingTheManagerStopsItsSoundsInsteadOfMakingThemGlobal()
    {
        SoundChannelHelper played = null;
        Action<SoundChannelHelper, SoundFileObject> onPlayed = (helper, file) => played = helper;
        AudioManager.OnSoundPlayed += onPlayed;
        try
        {
            Play(camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 25f)));
            Assert.That(played, Is.Not.Null);
            ((Behaviour)manager).enabled = false;
            Assert.That(played.enabled, Is.False);
            Assert.That(played.AudioSource.isPlaying, Is.False);
        }
        finally
        {
            AudioManager.OnSoundPlayed -= onPlayed;
        }
    }

    [Test]
    public void TransformSourcesAreTrackedEvenForNonSpatializedClips()
    {
        var source = new GameObject("Moving proximity sound");
        source.transform.SetParent(root.transform);
        source.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 25f));
        SoundChannelHelper played = null;
        Action<SoundChannelHelper, SoundFileObject> onPlayed = (helper, file) => played = helper;
        AudioManager.OnSoundPlayed += onPlayed;
        try
        {
            foreach (MethodInfo method in ManagerType.GetMethods())
            {
                if (method.Name != "PlayLocalSound" || method.GetParameters()[1].ParameterType != typeof(Transform)) continue;
                method.MakeGenericMethod(SoundType).Invoke(manager, new[] { sound, (object)source.transform });
                break;
            }
            Assert.That(played, Is.Not.Null);
            Assert.That(played.AudioSource.volume, Is.EqualTo(played.Volume).Within(0.0001f));
            source.transform.position = camera.ViewportToWorldPoint(new Vector3(-0.1f, 0.5f, 25f));
            UpdateVolumes();
            Assert.That(played.AudioSource.volume, Is.Zero);
        }
        finally
        {
            AudioManager.OnSoundPlayed -= onPlayed;
        }
    }
}
