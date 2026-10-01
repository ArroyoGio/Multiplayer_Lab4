using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// BARRA DE VIDA DEL JUGADOR (punto 2)
//
// Solo LEE la NetworkVariable de vida y la dibuja.
// Esta interfaz NO escribe la vida ni calcula el daño:
// el daño lo aplica el servidor y la vida llega replicada.
// =========================================================

public class PlayerHealthBar : MonoBehaviour
{
    private const float BarWidth = 130f;
    private const float BarHeight = 18f;
    private const float BarScale = 0.01f;
    private const float BarHeightOverPlayer = 2.35f;

    private static readonly Color BackgroundColor = new Color(0.05f, 0.05f, 0.07f, 0.75f);
    private static readonly Color FullHealthColor = new Color(0.20f, 0.85f, 0.30f, 1f);
    private static readonly Color DeadHealthColor = new Color(0.90f, 0.15f, 0.15f, 1f);

    private SimplePlayer player;
    private Image fill;
    private TextMeshProUGUI label;
    private int shownHealth = int.MinValue;


    private void Start()
    {
        player = GetComponent<SimplePlayer>();

        if (player == null) return;

        BuildBar();

        // La vida es una NetworkVariable: escuchamos sus cambios.
        player.health.OnValueChanged += OnHealthChanged;

        Refresh();
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.health.OnValueChanged -= OnHealthChanged;
        }
    }

    private void LateUpdate()
    {
        // El jugador que entra tarde recibe el valor inicial sin que se
        // dispare OnValueChanged, por eso comparamos y refrescamos.
        if (player != null && shownHealth != player.health.Value)
        {
            Refresh();
        }
    }

    private void OnHealthChanged(int previous, int current)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (player == null) return;

        int current = player.health.Value;

        shownHealth = current;

        // IsDead es DERIVADO: lo sacamos de la vida, no lo replicamos.
        bool dead = player.IsDead;

        float ratio = player.HealthRatio;

        if (fill != null)
        {
            fill.fillAmount = ratio;
            fill.color = dead ? DeadHealthColor : Color.Lerp(DeadHealthColor, FullHealthColor, ratio);
        }

        if (label != null)
        {
            label.text = dead ? "MUERTO" : current + " / " + player.maxHealth;
            label.color = dead ? DeadHealthColor : Color.white;
        }
    }


    // =========================================================
    // CONSTRUCCIÓN DE LA BARRA
    //
    // Se crea por código para que el prefab no necesite
    // referencias privadas que se puedan perder.
    // =========================================================

    private void BuildBar()
    {
        GameObject canvasObject = new GameObject(
            "HealthBarCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );

        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(BarWidth, BarHeight);
        canvasRect.localScale = Vector3.one * BarScale;
        canvasRect.localPosition = new Vector3(0f, BarHeightOverPlayer, 0f);
        canvasRect.localRotation = Quaternion.identity;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // Fondo de la barra.
        GameObject background = CreateUIObject("Background", canvasRect);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = BackgroundColor;

        // Relleno de la barra.
        GameObject fillObject = CreateUIObject("Fill", canvasRect);
        fill = fillObject.AddComponent<Image>();
        fill.color = FullHealthColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        // Texto con la vida.
        GameObject labelObject = CreateUIObject("Label", canvasRect);
        label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.color = Color.white;
        label.text = "100";
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject uiObject = new GameObject(name, typeof(RectTransform));

        RectTransform rect = uiObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        return uiObject;
    }
}
