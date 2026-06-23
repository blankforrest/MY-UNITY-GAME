using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a polished, premium FPS counter and frame-time monitor in the top-left corner of the screen.
/// </summary>
public class FPSDisplay : MonoBehaviour
{
    private TextMeshProUGUI _fpsText;
    private Image _background;
    private float _deltaTime = 0.0f;
    private float _updateInterval = 0.25f; // Update 4 times a second
    private float _accumulatedFps = 0f;
    private int _frameCount = 0;
    private float _timeCounter = 0f;

    private void Awake()
    {
        // 1. Create a beautiful background panel
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
            rectTransform = gameObject.AddComponent<RectTransform>();

        rectTransform.anchorMin = new Vector2(0f, 1f); // top-left
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(10f, -10f); // 10px padding from top-left
        rectTransform.sizeDelta = new Vector2(110f, 26f); // neat size

        // Add a clean glassmorphic dark background
        _background = gameObject.AddComponent<Image>();
        _background.color = new Color(0.02f, 0.02f, 0.02f, 0.65f); // 65% opacity dark grey

        // 2. Create the child text object for the FPS text
        GameObject textGO = new GameObject("FPSText", typeof(RectTransform));
        textGO.transform.SetParent(transform, false);

        _fpsText = textGO.AddComponent<TextMeshProUGUI>();
        RectTransform textRT = _fpsText.rectTransform;
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.pivot = new Vector2(0.5f, 0.5f);
        textRT.sizeDelta = Vector2.zero; // fit parent exactly
        textRT.anchoredPosition = Vector2.zero;

        _fpsText.fontSize = 11f;
        _fpsText.fontStyle = FontStyles.Bold;
        _fpsText.alignment = TextAlignmentOptions.Center;
        _fpsText.color = Color.white;
        _fpsText.text = "FPS: --";
    }

    private void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        _timeCounter += Time.unscaledDeltaTime;
        _accumulatedFps += 1.0f / Time.unscaledDeltaTime;
        _frameCount++;

        if (_timeCounter >= _updateInterval)
        {
            float fps = _accumulatedFps / _frameCount;
            float ms = _deltaTime * 1000.0f;

            // Reset counters
            _timeCounter = 0f;
            _accumulatedFps = 0f;
            _frameCount = 0;

            // Update text and color dynamically based on performance
            string fpsColorHex;
            if (fps >= 55f)
                fpsColorHex = "#00FF66"; // Neon Green
            else if (fps >= 30f)
                fpsColorHex = "#FFCC00"; // Warm Yellow
            else
                fpsColorHex = "#FF3333"; // Vibrant Red

            _fpsText.text = $"<color={fpsColorHex}>{Mathf.RoundToInt(fps)}</color> <size=9>FPS</size>  <color=#CCCCCC><size=8>{ms:F1}ms</size></color>";
        }
    }
}
