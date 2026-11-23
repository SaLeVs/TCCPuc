using TMPro;
using UnityEngine;

namespace Ui.EndScreen
{
    /// <summary>
    /// A player's name floating over one of the end-scene models, in the same font as the in-game
    /// tag. Turns to the camera and keeps about the same size on screen at any distance or zoom.
    /// </summary>
    public class EndScreenNameTag : MonoBehaviour
    {
        // Text height as a fraction of the screen height, like PlayerNameTag.
        private const float ScreenFraction = 0.032f;

        private Transform _anchor;
        private Vector3 _offset;
        private TextMeshPro _text;
        private float _alpha = 1f;
        private float _targetAlpha = 1f;

        public static EndScreenNameTag Create(Transform anchor, Vector3 offset, string playerName, TMP_FontAsset font, Material fontMaterial)
        {
            GameObject go = new GameObject($"NameTag ({playerName})");
            EndScreenNameTag tag = go.AddComponent<EndScreenNameTag>();
            tag._anchor = anchor;
            tag._offset = offset;

            tag._text = go.AddComponent<TextMeshPro>();
            if (font != null) tag._text.font = font;
            if (fontMaterial != null) tag._text.fontSharedMaterial = fontMaterial;
            tag._text.text = playerName;
            tag._text.fontSize = 10f;
            tag._text.fontStyle = FontStyles.Bold;
            tag._text.alignment = TextAlignmentOptions.Center;
            tag._text.enableWordWrapping = false;
            tag._text.rectTransform.sizeDelta = new Vector2(20f, 2f);

            tag.LateUpdate();
            return tag;
        }

        public void SetVisible(bool visible) => _targetAlpha = visible ? 1f : 0f;

        private void LateUpdate()
        {
            if (_anchor == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = _anchor.position + _offset;

            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);

                // World height that covers the wanted fraction of the screen at this distance and FOV.
                float distance = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
                float worldHeight = 2f * Mathf.Max(distance, 0.1f) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float wanted = worldHeight * ScreenFraction;
                // fontSize 10 in a TextMeshPro gives letters about 1 unit tall.
                transform.localScale = Vector3.one * wanted;
            }

            _alpha = Mathf.MoveTowards(_alpha, _targetAlpha, Time.deltaTime * 3f);
            if (_text != null) _text.alpha = _alpha;
        }
    }
}
