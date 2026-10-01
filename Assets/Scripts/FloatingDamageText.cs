using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// DAÑO FLOTANTE (punto 5)
//
// Es un efecto visual NO FIABLE que se crea a partir de un RPC.
// Solo muestra el número que le pasaron: no lee, no escribe
// ni modifica la vida de nadie.
// =========================================================

public class FloatingDamageText : MonoBehaviour
{
    private const float Scale = 0.01f;
    private const float LifeTime = 0.9f;
    private const float RiseSpeed = 1.6f;

    private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);

    public static void Spawn(Vector3 worldPosition, int amount, Color color)
    {
        GameObject textObject = new GameObject("FloatingDamageText");

        textObject.transform.position = worldPosition;
        textObject.transform.SetParent(CombatVfx.Root, true);

        FloatingDamageText floatingText = textObject.AddComponent<FloatingDamageText>();
        floatingText.Build(amount, color);
        floatingText.StartCoroutine(floatingText.Animate());
    }

    private TextMeshProUGUI text;
    private Vector3 startPosition;


    private void Build(int amount, Color color)
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );

        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(120f, 40f);
        canvasRect.localScale = Vector3.one * Scale;
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(canvasRect, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;

        text = labelObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 40f;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.outlineColor = OutlineColor;
        text.outlineWidth = 0.2f;
        text.text = "-" + amount;

        startPosition = transform.position;
    }


    private IEnumerator Animate()
    {
        float elapsed = 0f;

        while (elapsed < LifeTime)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / LifeTime);

            // Sube hacia arriba.
            transform.position = startPosition + Vector3.up * (RiseSpeed * progress * LifeTime);

            if (text != null)
            {
                // Se desvanece al final.
                Color color = text.color;
                color.a = 1f - progress;
                text.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
