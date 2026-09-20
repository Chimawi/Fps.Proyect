using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public interface IShootable
{
    void OnHitByProjectile(RaycastHit hit);
}

public static class ProceduralAudio
{
    public static float[] AllocateSamples(float duration, int sampleRate)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * sampleRate));
        return new float[sampleCount];
    }

    public static AudioClip CreateClip(string name, float[] samples, int sampleRate)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

#if ENABLE_INPUT_SYSTEM
public static class InputActionResolver
{
    public static InputAction Resolve(InputActionAsset explicitAsset, string actionMap, string actionName, Object context, string logTag)
    {
        InputActionAsset asset = explicitAsset != null ? explicitAsset : InputSystem.actions;
        if (asset == null)
        {
            Debug.LogError($"[{logTag}] No hay un InputActionAsset asignado ni acciones de proyecto configuradas.", context);
            return null;
        }

        InputActionMap map = asset.FindActionMap(actionMap, false);
        return map != null ? map.FindAction(actionName, false) : asset.FindAction(actionName, false);
    }
}
#endif
