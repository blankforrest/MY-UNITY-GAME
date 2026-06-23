using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedurally generates and updates the Survival HUD (Health, Hunger, Air bubbles) entirely from code.
/// This prevents any IP/copyright issues from using external assets.
/// </summary>
public class SurvivalHUD : MonoBehaviour
{
    public static SurvivalHUD Instance { get; private set; }

    private GameObject hudRoot;
    private GameObject heartsContainer;
    private GameObject hungerContainer;
    private GameObject airContainer;

    private List<Image> heartImages = new List<Image>();
    private List<Image> hungerImages = new List<Image>();
    private List<Image> airImages = new List<Image>();

    private Sprite fullHeart;
    private Sprite halfHeart;
    private Sprite emptyHeart;

    private Sprite fullShank;
    private Sprite halfShank;
    private Sprite emptyShank;

    private Sprite fullBubble;
    private Sprite emptyBubble;

    private PlayerController player;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        GenerateProceduralSprites();
        BuildHUD();
    }

    void Update()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
        }

        // Hide HUD in creative mode or when UI is open/dead/paused
        bool shouldShow = !player.isCreativeMode && 
                          !InventoryUI.IsInventoryOpen && 
                          !ConfirmationWindow.IsOpen && 
                          !DevToolsUI.IsCursorUnlocked && 
                          !player.IsDead && 
                          !PauseMenu.IsPaused;

        if (hudRoot != null)
        {
            hudRoot.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        UpdateHearts();
        UpdateHunger();
        UpdateAir();
    }

    void UpdateHearts()
    {
        float health = player.currentHealth;
        for (int i = 0; i < 10; i++)
        {
            float threshold = i * 2 + 1;
            if (health >= threshold + 1)
            {
                heartImages[i].sprite = fullHeart;
            }
            else if (health >= threshold)
            {
                heartImages[i].sprite = halfHeart;
            }
            else
            {
                heartImages[i].sprite = emptyHeart;
            }
        }
    }

    void UpdateHunger()
    {
        float hunger = player.currentHunger;
        for (int i = 0; i < 10; i++)
        {
            float threshold = i * 2 + 1;
            if (hunger >= threshold + 1)
            {
                hungerImages[i].sprite = fullShank;
            }
            else if (hunger >= threshold)
            {
                hungerImages[i].sprite = halfShank;
            }
            else
            {
                hungerImages[i].sprite = emptyShank;
            }
        }
    }

    void UpdateAir()
    {
        bool headInWater = player.currentAir < player.maxAir;
        airContainer.SetActive(headInWater);

        if (headInWater)
        {
            int bubbleCount = Mathf.CeilToInt(player.currentAir);
            for (int i = 0; i < 10; i++)
            {
                if (i < bubbleCount)
                {
                    airImages[i].enabled = true;
                    airImages[i].sprite = fullBubble;
                }
                else
                {
                    airImages[i].enabled = false;
                }
            }
        }
    }

    void GenerateProceduralSprites()
    {
        fullHeart = MakeHeartSprite(1f);
        halfHeart = MakeHeartSprite(0.5f);
        emptyHeart = MakeHeartSprite(0f);

        fullShank = MakeShankSprite(1f);
        halfShank = MakeShankSprite(0.5f);
        emptyShank = MakeShankSprite(0f);

        fullBubble = MakeBubbleSprite(true);
        emptyBubble = MakeBubbleSprite(false);
    }

    private Sprite MakeHeartSprite(float fillAmount)
    {
        const int SZ = 16;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color border = Color.black;
        Color red = new Color(0.85f, 0.12f, 0.12f, 1.0f);
        Color white = Color.white;
        Color empty = new Color(0.15f, 0.15f, 0.15f, 0.6f);

        int[,] heartGrid = new int[16, 16] {
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 
            {0,0,1,1,1,0,0,0,0,0,1,1,1,0,0,0}, 
            {0,1,2,3,2,1,0,0,0,1,2,2,2,1,0,0}, 
            {0,1,3,2,2,2,1,0,1,2,2,2,2,1,0,0}, 
            {1,2,2,2,2,2,2,1,2,2,2,2,2,2,1,0}, 
            {1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,0}, 
            {1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,0}, 
            {1,2,2,2,2,2,2,2,2,2,2,2,2,2,1,0}, 
            {0,1,2,2,2,2,2,2,2,2,2,2,2,1,0,0}, 
            {0,0,1,2,2,2,2,2,2,2,2,2,1,0,0,0}, 
            {0,0,0,1,2,2,2,2,2,2,2,1,0,0,0,0}, 
            {0,0,0,0,1,2,2,2,2,2,1,0,0,0,0,0}, 
            {0,0,0,0,0,1,2,2,2,1,0,0,0,0,0,0}, 
            {0,0,0,0,0,0,1,2,1,0,0,0,0,0,0,0}, 
            {0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0}, 
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}
        };

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                int val = heartGrid[15 - y, x];
                if (val == 1)
                {
                    Set(x, y, border);
                }
                else if (val == 2 || val == 3)
                {
                    float ratio = (float)x / SZ;
                    if (ratio <= fillAmount)
                    {
                        Set(x, y, val == 3 ? white : red);
                    }
                    else
                    {
                        Set(x, y, empty);
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite MakeShankSprite(float fillAmount)
    {
        const int SZ = 16;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color border = Color.black;
        Color meat = new Color(0.59f, 0.34f, 0.15f, 1f); 
        Color bone = new Color(0.92f, 0.92f, 0.92f, 1f); 
        Color empty = new Color(0.15f, 0.15f, 0.15f, 0.6f);

        int[,] shankGrid = new int[16, 16] {
            {0,0,0,0,0,0,0,0,0,1,1,1,1,0,0,0}, 
            {0,0,0,0,0,0,0,0,1,2,2,2,2,1,0,0}, 
            {0,0,0,0,0,0,0,1,2,2,2,2,2,1,0,0}, 
            {0,0,0,0,0,0,1,2,2,2,2,2,2,1,0,0}, 
            {0,0,0,0,0,1,2,2,2,2,2,2,2,1,0,0}, 
            {0,0,0,0,1,2,2,2,2,2,2,2,1,0,0,0}, 
            {0,0,0,1,2,2,2,2,2,2,2,1,0,0,0,0}, 
            {0,0,1,2,2,2,2,2,2,2,1,0,0,0,0,0}, 
            {0,1,2,2,2,2,2,2,2,1,0,0,0,0,0,0}, 
            {0,1,2,2,2,2,2,2,1,0,0,0,0,0,0,0}, 
            {0,0,1,2,2,2,2,1,0,0,0,0,0,0,0,0}, 
            {0,0,0,1,2,2,1,0,0,0,0,0,0,0,0,0}, 
            {0,0,0,0,1,2,1,0,0,0,0,0,0,0,0,0}, 
            {0,0,0,1,2,1,1,0,0,0,0,0,0,0,0,0}, 
            {0,0,1,2,1,0,0,0,0,0,0,0,0,0,0,0}, 
            {0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0}
        };

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                int val = shankGrid[15 - y, x];
                if (val == 1)
                {
                    Set(x, y, border);
                }
                else if (val == 2)
                {
                    float ratio = (float)x / SZ;
                    if (ratio <= fillAmount)
                    {
                        if (x + y < 14)
                        {
                            Set(x, y, bone);
                        }
                        else
                        {
                            Set(x, y, meat);
                        }
                    }
                    else
                    {
                        Set(x, y, empty);
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite MakeBubbleSprite(bool filled)
    {
        const int SZ = 16;
        Color[] px = new Color[SZ * SZ];
        for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

        void Set(int x, int y, Color c)
        { if (x >= 0 && x < SZ && y >= 0 && y < SZ) px[y * SZ + x] = c; }

        Color border = Color.black;
        Color fill = new Color(0.3f, 0.7f, 0.9f, 0.85f);
        Color highlight = Color.white;

        int[,] bubbleGrid = new int[16, 16] {
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 
            {0,0,0,0,0,1,1,1,1,1,0,0,0,0,0,0}, 
            {0,0,0,1,1,2,2,2,2,2,1,1,0,0,0,0}, 
            {0,0,1,2,2,2,2,2,2,2,2,2,1,0,0,0}, 
            {0,0,1,2,3,3,2,2,2,2,2,2,1,0,0,0}, 
            {0,1,2,3,2,2,2,2,2,2,2,2,2,1,0,0}, 
            {0,1,2,2,2,2,2,2,2,2,2,2,2,1,0,0}, 
            {0,1,2,2,2,2,2,2,2,2,2,2,2,1,0,0}, 
            {0,1,2,2,2,2,2,2,2,2,2,2,2,1,0,0}, 
            {0,1,2,2,2,2,2,2,2,2,2,2,2,1,0,0}, 
            {0,0,1,2,2,2,2,2,2,2,2,2,1,0,0,0}, 
            {0,0,1,1,2,2,2,2,2,2,1,1,0,0,0,0}, 
            {0,0,0,0,1,1,1,1,1,1,0,0,0,0,0,0}, 
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}
        };

        for (int y = 0; y < SZ; y++)
        {
            for (int x = 0; x < SZ; x++)
            {
                int val = bubbleGrid[15 - y, x];
                if (val == 1)
                {
                    Set(x, y, border);
                }
                else if (val == 2)
                {
                    if (filled)
                    {
                        Set(x, y, fill);
                    }
                }
                else if (val == 3)
                {
                    if (filled)
                    {
                        Set(x, y, highlight);
                    }
                }
            }
        }

        Texture2D tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
    }

    void BuildHUD()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject cgo = new GameObject("Canvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<CanvasScaler>();
            cgo.AddComponent<GraphicRaycaster>();
        }

        hudRoot = new GameObject("SurvivalHUD", typeof(RectTransform));
        hudRoot.transform.SetParent(canvas.transform, false);
        RectTransform rootRT = hudRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.sizeDelta = Vector2.zero;
        rootRT.anchoredPosition = Vector2.zero;

        // Hearts container
        heartsContainer = new GameObject("HeartsContainer", typeof(RectTransform));
        heartsContainer.transform.SetParent(hudRoot.transform, false);
        RectTransform heartsRT = heartsContainer.GetComponent<RectTransform>();
        heartsRT.anchorMin = new Vector2(0.5f, 0f);
        heartsRT.anchorMax = new Vector2(0.5f, 0f);
        heartsRT.pivot = new Vector2(1f, 0f);
        heartsRT.anchoredPosition = new Vector2(-10f, 72f);
        heartsRT.sizeDelta = new Vector2(200f, 20f);

        // Hunger container
        hungerContainer = new GameObject("HungerContainer", typeof(RectTransform));
        hungerContainer.transform.SetParent(hudRoot.transform, false);
        RectTransform hungerRT = hungerContainer.GetComponent<RectTransform>();
        hungerRT.anchorMin = new Vector2(0.5f, 0f);
        hungerRT.anchorMax = new Vector2(0.5f, 0f);
        hungerRT.pivot = new Vector2(0f, 0f);
        hungerRT.anchoredPosition = new Vector2(10f, 72f);
        hungerRT.sizeDelta = new Vector2(200f, 20f);

        // Air container
        airContainer = new GameObject("AirContainer", typeof(RectTransform));
        airContainer.transform.SetParent(hudRoot.transform, false);
        RectTransform airRT = airContainer.GetComponent<RectTransform>();
        airRT.anchorMin = new Vector2(0.5f, 0f);
        airRT.anchorMax = new Vector2(0.5f, 0f);
        airRT.pivot = new Vector2(0f, 0f);
        airRT.anchoredPosition = new Vector2(10f, 88f);
        airRT.sizeDelta = new Vector2(200f, 20f);

        // Hearts
        for (int i = 0; i < 10; i++)
        {
            GameObject heartGO = new GameObject("Heart_" + i, typeof(RectTransform), typeof(Image));
            heartGO.transform.SetParent(heartsContainer.transform, false);
            RectTransform hRT = heartGO.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0f, 0.5f);
            hRT.anchorMax = new Vector2(0f, 0.5f);
            hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(18f, 18f);
            hRT.anchoredPosition = new Vector2(i * 19f + 9f, 0f);

            Image img = heartGO.GetComponent<Image>();
            img.sprite = emptyHeart;
            heartImages.Add(img);
        }

        // Hunger
        for (int i = 0; i < 10; i++)
        {
            GameObject shankGO = new GameObject("Shank_" + i, typeof(RectTransform), typeof(Image));
            shankGO.transform.SetParent(hungerContainer.transform, false);
            RectTransform sRT = shankGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0.5f);
            sRT.anchorMax = new Vector2(0f, 0.5f);
            sRT.pivot = new Vector2(0.5f, 0.5f);
            sRT.sizeDelta = new Vector2(18f, 18f);
            sRT.anchoredPosition = new Vector2(i * 19f + 9f, 0f);

            Image img = shankGO.GetComponent<Image>();
            img.sprite = emptyShank;
            hungerImages.Add(img);
        }

        // Air Bubbles
        for (int i = 0; i < 10; i++)
        {
            GameObject bubbleGO = new GameObject("Bubble_" + i, typeof(RectTransform), typeof(Image));
            bubbleGO.transform.SetParent(airContainer.transform, false);
            RectTransform bRT = bubbleGO.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0f, 0.5f);
            bRT.anchorMax = new Vector2(0f, 0.5f);
            bRT.pivot = new Vector2(0.5f, 0.5f);
            bRT.sizeDelta = new Vector2(16f, 16f);
            bRT.anchoredPosition = new Vector2(i * 19f + 9f, 0f);

            Image img = bubbleGO.GetComponent<Image>();
            img.sprite = emptyBubble;
            airImages.Add(img);
        }

        airContainer.SetActive(false);
    }
}
