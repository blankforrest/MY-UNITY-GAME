using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Cycle Settings")]
    [Tooltip("Duration of a full day/night cycle in seconds")]
    public float cycleDuration = 120f; 
    public float timeOfDay = 30f; // Start at high noon

    private Light sunLight;

    public static bool IsNight => Instance != null && (Instance.timeOfDay / Instance.cycleDuration >= 0.5f);
    public static bool IsMorning => !IsNight;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        // Explicitly restore standard URP skybox ambient mode
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        // Find directional light
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional)
            {
                sunLight = l;
                break;
            }
        }

        if (sunLight == null)
        {
            GameObject sunGO = new GameObject("SunLight", typeof(Light));
            sunLight = sunGO.GetComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    void Update()
    {
        // Temporarily disabled to isolate the black and white issue
    }

    void OnDisable()
    {
        // Restore editor defaults when play mode stops
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        if (sunLight != null)
        {
            sunLight.intensity = 1f;
            sunLight.color = Color.white;
            sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    private void UpdateAtmosphere(float percent)
    {
        // Day/Morning (percent 0.0 to 0.5)
        // Night/Evening (percent 0.5 to 1.0)
        float intensity = 0f;
        float ambientIntensity = 0.1f;
        Color sunColor = Color.white;

        if (percent >= 0.15f && percent < 0.45f)
        {
            // Day
            intensity = 1f;
            ambientIntensity = 1f;
            sunColor = new Color(1f, 0.98f, 0.9f);
        }
        else if (percent >= 0.45f && percent < 0.5f)
        {
            // Sunset transition
            float t = (percent - 0.45f) / 0.05f;
            intensity = Mathf.Lerp(1f, 0f, t);
            ambientIntensity = Mathf.Lerp(1f, 0.1f, t);
            sunColor = Color.Lerp(new Color(1f, 0.98f, 0.9f), new Color(1f, 0.3f, 0f), t);
        }
        else if (percent >= 0.5f && percent < 0.9f)
        {
            // Night
            intensity = 0f;
            ambientIntensity = 0.1f;
            sunColor = Color.black;
        }
        else if (percent >= 0.9f && percent < 0.95f)
        {
            // Sunrise transition
            float t = (percent - 0.9f) / 0.05f;
            intensity = Mathf.Lerp(0f, 0.5f, t);
            ambientIntensity = Mathf.Lerp(0.1f, 0.5f, t);
            sunColor = Color.Lerp(Color.black, new Color(0.9f, 0.4f, 0.2f), t);
        }
        else
        {
            // Morning twilight transition to Day
            float t = (percent - 0.95f) / 0.2f; // wraps up to 0.15
            intensity = Mathf.Lerp(0.5f, 1f, t);
            ambientIntensity = Mathf.Lerp(0.5f, 1f, t);
            sunColor = Color.Lerp(new Color(0.9f, 0.4f, 0.2f), new Color(1f, 0.98f, 0.9f), t);
        }

        if (sunLight != null)
        {
            sunLight.intensity = intensity;
            sunLight.color = sunColor;
        }
        RenderSettings.ambientIntensity = ambientIntensity;
    }
}
