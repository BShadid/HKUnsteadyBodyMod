using GlobalEnums;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Smash Bros-style off-screen indicator. When the Knight leaves the screen,
    /// a bubble sits at the nearest edge showing them live (a small camera films
    /// only the Knight's layer into a texture), with an arrow pointing to where
    /// they are. The farther away they get, the smaller the bubble.
    /// </summary>
    internal class TrackerBubble : MonoBehaviour
    {
        // Portrait
        private const int PortraitPixels = 256;
        private const float PortraitHalfHeight = 1.3f;  // world units shown above and below their centre
        private const float PortraitFill = 1.6f;        // portrait width as a multiple of the bubble radius

        // Bubble size and placement, as shares of screen height
        private const float BubbleDiameter = 0.11f;
        private const float EdgeGap = 0.02f;

        // Shrinking with distance: smallest size once they're this many screens past the edge
        private const float SmallestScale = 0.55f;
        private const float ShrinkDistance = 1.5f;

        // Arrow, as multiples of the bubble radius
        private const float ArrowLength = 0.45f;
        private const float ArrowWidth = 0.75f;

        private static readonly Color BubbleFill = new Color(0.07f, 0.08f, 0.15f, 0.85f);
        private static readonly Color Outline = Color.white;

        private Camera _portraitCamera;
        private RenderTexture _portrait;
        private Texture2D _disc;
        private Texture2D _ring;
        private Texture2D _arrow;

        private bool _visible;
        private Vector2 _centre;     // screen position, GUI coordinates (y down)
        private Vector2 _direction;  // from the bubble towards the Knight, GUI coordinates
        private float _radius;

        private void Awake()
        {
            _portrait = new RenderTexture(PortraitPixels, PortraitPixels, 16, RenderTextureFormat.ARGB32);

            var cameraObject = new GameObject("Unsteady Body Portrait Camera");
            cameraObject.transform.SetParent(transform, false);
            _portraitCamera = cameraObject.AddComponent<Camera>();
            _portraitCamera.orthographic = true;
            _portraitCamera.orthographicSize = PortraitHalfHeight;
            _portraitCamera.clearFlags = CameraClearFlags.SolidColor;
            _portraitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _portraitCamera.nearClipPlane = 0.1f;
            _portraitCamera.farClipPlane = 30f;
            _portraitCamera.targetTexture = _portrait;
            _portraitCamera.enabled = false;

            _disc = MakeRing(128, 0f, BubbleFill);
            _ring = MakeRing(128, 0.88f, Outline);
            _arrow = MakeArrow(64, Outline);
        }

        private void OnDestroy()
        {
            if (_portraitCamera != null) _portraitCamera.targetTexture = null;
            if (_portrait != null) _portrait.Release();
            Destroy(_portrait);
            Destroy(_disc);
            Destroy(_ring);
            Destroy(_arrow);
        }

        private void LateUpdate()
        {
            _visible = false;

            HeroController hero = HeroController.instance;
            Camera view = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (view == null) view = Camera.main;

            if (UnsteadyBodyMod.Settings.TrackerBubble
                && hero != null && view != null
                && Launch.Active
                && GameManager.instance != null
                && GameManager.instance.gameState == GameState.PLAYING
                && !hero.cState.transitioning)
            {
                Vector3 knight = hero.transform.position;
                Vector3 vp = view.WorldToViewportPoint(knight);

                // How far past the edge of the screen they are (0 = on the edge).
                float beyond = Mathf.Max(Mathf.Abs(vp.x - 0.5f), Mathf.Abs(vp.y - 0.5f)) - 0.5f;

                if (vp.z > 0f && beyond > 0f)
                {
                    float scale = Mathf.Lerp(1f, SmallestScale, Mathf.Clamp01(beyond / ShrinkDistance));
                    _radius = Screen.height * BubbleDiameter * 0.5f * scale;

                    // Keep the bubble and its arrow fully on screen.
                    float margin = Screen.height * EdgeGap + _radius * (1f + ArrowLength);
                    Vector2 target = new Vector2(vp.x * Screen.width, (1f - vp.y) * Screen.height);
                    _centre = new Vector2(
                        Mathf.Clamp(target.x, margin, Screen.width - margin),
                        Mathf.Clamp(target.y, margin, Screen.height - margin));

                    Vector2 toKnight = target - _centre;
                    _direction = toKnight.sqrMagnitude > 0.01f ? toKnight.normalized : new Vector2(0f, -1f);

                    // Film just the Knight's layer, centred on them.
                    _portraitCamera.cullingMask = 1 << hero.gameObject.layer;
                    _portraitCamera.transform.position = new Vector3(knight.x, knight.y, knight.z - 15f);

                    _visible = true;
                }
            }

            _portraitCamera.enabled = _visible;
        }

        private void OnGUI()
        {
            if (!_visible || Event.current.type != EventType.Repaint) return;

            float r = _radius;

            // Arrow first, so the bubble covers its base.
            float arrowLength = r * ArrowLength;
            float arrowWidth = r * ArrowWidth;
            Vector2 arrowCentre = _centre + _direction * (r + arrowLength * 0.5f - r * 0.05f);
            Rect arrowRect = new Rect(arrowCentre.x - arrowWidth * 0.5f, arrowCentre.y - arrowLength * 0.5f, arrowWidth, arrowLength);

            Matrix4x4 saved = GUI.matrix;
            float angle = Mathf.Atan2(_direction.x, -_direction.y) * Mathf.Rad2Deg; // 0 = pointing up
            GUIUtility.RotateAroundPivot(angle, arrowCentre);
            GUI.DrawTexture(arrowRect, _arrow);
            GUI.matrix = saved;

            Rect bubble = new Rect(_centre.x - r, _centre.y - r, r * 2f, r * 2f);
            GUI.DrawTexture(bubble, _disc);

            float p = r * PortraitFill;
            GUI.DrawTexture(new Rect(_centre.x - p * 0.5f, _centre.y - p * 0.5f, p, p), _portrait);

            GUI.DrawTexture(bubble, _ring);
        }

        // A filled circle (inner = 0) or a ring (inner = share of the radius left hollow), antialiased.
        private static Texture2D MakeRing(int size, float inner, Color colour)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            float outerR = size * 0.5f - 1f;
            float innerR = outerR * inner;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(outerR - d + 0.5f);
                if (inner > 0f) a *= Mathf.Clamp01(d - innerR + 0.5f);
                tex.SetPixel(x, y, new Color(colour.r, colour.g, colour.b, colour.a * a));
            }
            tex.Apply();
            return tex;
        }

        // A triangle pointing up, antialiased.
        private static Texture2D MakeArrow(int size, Color colour)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fy = (y + 0.5f) / size;              // 0 at the base, 1 at the tip
                float halfWidth = 0.5f * (1f - fy) * size;  // in pixels
                float fromMiddle = Mathf.Abs(x + 0.5f - size * 0.5f);
                float a = Mathf.Clamp01(halfWidth - fromMiddle + 0.5f);
                tex.SetPixel(x, y, new Color(colour.r, colour.g, colour.b, colour.a * a));
            }
            tex.Apply();
            return tex;
        }
    }
}
