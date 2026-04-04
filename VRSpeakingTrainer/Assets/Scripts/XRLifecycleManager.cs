using System.Collections;
using UnityEngine;
using UnityEngine.XR.Management;

/// <summary>
/// Manually starts and stops the Cardboard XR subsystem.
/// Attach to any persistent GameObject in the Session scene.
///
/// XR auto-init is disabled in XR Plugin Management so the MainMenu
/// renders as a plain 2D screen. This component re-enables XR exactly
/// when the Session scene is active and tears it down cleanly on exit.
/// </summary>
public class XRLifecycleManager : MonoBehaviour
{
    public bool IsXrRunning { get; private set; }
    public bool IsTransitioning { get; private set; }

    private Coroutine _startRoutine;

    private IEnumerator Start()
    {
        yield return StartXRCoroutine();
    }

    public void StartXR()
    {
        if (_startRoutine != null || IsXrRunning || IsTransitioning)
            return;

        _startRoutine = StartCoroutine(StartXRCoroutine());
    }

    public void StopXR()
    {
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }

        var manager = XRGeneralSettings.Instance?.Manager;
        if (manager == null)
        {
            IsTransitioning = false;
            return;
        }

        if (!manager.isInitializationComplete)
        {
            IsTransitioning = false;
            IsXrRunning = false;
            return;
        }

        manager.StopSubsystems();
        manager.DeinitializeLoader();

        IsTransitioning = false;
        IsXrRunning = false;
        Debug.Log("[XRLifecycleManager] Cardboard XR stopped.");
    }

    private void OnDestroy()
    {
        StopXR();
    }

    private IEnumerator StartXRCoroutine()
    {
        var manager = XRGeneralSettings.Instance?.Manager;
        if (manager == null)
        {
            Debug.LogError("[XRLifecycleManager] XRGeneralSettings not found.");
            _startRoutine = null;
            yield break;
        }

        IsTransitioning = true;

        yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.LogError("[XRLifecycleManager] XR loader failed to initialise. " +
                           "Check XR Plugin Management -> Android -> Cardboard is enabled.");
            IsTransitioning = false;
            _startRoutine = null;
            yield break;
        }

        manager.StartSubsystems();
        IsTransitioning = false;
        IsXrRunning = true;
        _startRoutine = null;
        Debug.Log("[XRLifecycleManager] Cardboard XR started.");
    }
}
