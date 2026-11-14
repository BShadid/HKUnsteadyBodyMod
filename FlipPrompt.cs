using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GlobalEnums;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// A "[Dream Nail] Destabilize" / "[Dream Nail] Stabilize" button prompt in the
    /// charm menu, at the bottom left of the details panel, mirroring the game's own
    /// "Equip"/"Unequip" prompt. Shown whenever the charm can be turned over: at a
    /// bench, with the cursor on the charm.
    ///
    /// Built from the game's own pieces, so it matches the menu's look:
    /// - the word is a copy of the "Equip"/"Unequip" text, with its own wording;
    /// - the button icon is a copy of the Dream Nail icon from the inventory
    ///   screen's controls panel, so it follows whatever Dream Nail is bound to.
    /// Anything in the copies that would overwrite them (the menu's own scripts)
    /// is removed before they're switched on.
    /// </summary>
    internal static class FlipPrompt
    {
        // What pressing the button would do: on the Steady side it destabilizes the
        // charm, on the Unsteady side it stabilizes it again.
        private static string Label => UnsteadyBodyMod.Local.Unsteady ? Strings.Stabilize : Strings.Destabilize;
        private static string _shownLabel;
        private const float Gap = 0.12f;

        // How far to shift the prompt left of an exact mirror, in icon widths.
        private const float NudgeLeft = 0.6f;

        private static GameObject _prompt;
        private static GameObject _word;
        private static GameObject _icon;
        private static Transform _details;
        private static Component _description;
        private static Transform _originalText;
        private static Transform _originalIcon;
        private static bool _gaveUp;
        private static float _nextTry;

        internal static void Update(bool show)
        {
            if (!show)
            {
                if (_prompt != null) _prompt.SetActive(false);
                return;
            }

            if (_prompt == null && !TryCreate()) return;

            bool appearing = !_prompt.activeSelf;
            _prompt.SetActive(true);
            if (appearing && _word != null) Texts.RefreshAll(_word);
            if (_shownLabel != Label && _word != null)
            {
                // Re-measure straight away, or the first frame would be laid out
                // using the old word's size.
                Texts.SetAll(_word, Label);
                Texts.RefreshAll(_word);
                _shownLabel = Label;
            }
            Layout();

            // The menu fades its pieces in and out by changing their transparency.
            // Match the game's own prompt, so this one appears and fades with it.
            float alpha = Texts.Alpha(_originalText.GetComponent(Texts.Type));
            foreach (Component c in Texts.All(_prompt.transform)) Texts.SetAlpha(c, alpha);
            foreach (SpriteRenderer r in _prompt.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color col = r.color;
                r.color = new Color(col.r, col.g, col.b, alpha);
            }

            if (!_reported)
            {
                _reported = true;
                SpriteRenderer iconPicture = _prompt.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault();
                UnsteadyBodyMod.Verbose(
                    $"Flip hint shown at {_prompt.transform.position} (game's prompt at {_originalText.position}), " +
                    $"transparency {alpha:0.00}, icon picture '{(iconPicture != null && iconPicture.sprite != null ? iconPicture.sprite.name : "none")}', " +
                    $"icon size {(iconPicture != null ? iconPicture.transform.lossyScale.ToString() : "none")}");
            }
        }

        private static bool _reported;

        // "(icon) Destabilize" or "(icon) Stabilize" on the same line as the game's "Unequip (icon)", lined up
        // with the left edge of the description above it. Positions come from what's
        // actually drawn, so it holds whatever the screen size.
        private static void Layout()
        {
            Renderer iconPicture = _icon != null ? _icon.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.sprite != null) : null;
            Component wordText = _word != null ? Texts.All(_word.transform).FirstOrDefault(c => c.GetComponent<Renderer>() is Renderer r && r.enabled) : null;
            Component gameWord = _originalText != null ? _originalText.GetComponent(Texts.Type) : null;
            Renderer gameIcon = _originalIcon != null ? _originalIcon.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.sprite != null) : null;
            Component description = DescriptionText();
            if (iconPicture == null || wordText == null || gameWord == null || description == null)
            {
                // Can't measure: fall back to one line above the game's prompt.
                Transform container = _prompt.transform.parent;
                _prompt.transform.position = (container != null ? container.position : Vector3.zero) + new Vector3(0f, _lift, 0f);
                return;
            }

            _prompt.transform.localPosition = Vector3.zero;

            // Mirror the game's prompt around the middle of the details panel, marked by
            // the charm's picture (centred above the description, and reliably measured):
            // the Dream Nail icon starts as far left of the middle as the (A) ends right of it.
            Renderer picture = _details != null ? _details.Find("Detail Sprite")?.GetComponent<SpriteRenderer>() : null;
            float left;
            if (picture != null && gameIcon != null)
            {
                float middle = picture.bounds.center.x;
                left = 2f * middle - gameIcon.bounds.max.x;

                // The description is aligned left, not justified, so an exact mirror can
                // look pushed in. Nudge left a little, still inside the description's edge.
                left -= iconPicture.bounds.size.x * NudgeLeft;
            }
            else
            {
                left = Texts.LetterBounds(description).min.x;
            }
            float textY = Texts.LetterBounds(gameWord).center.y;
            float iconY = gameIcon != null ? gameIcon.bounds.center.y : textY;

            Bounds ib = iconPicture.bounds;
            _icon.transform.position += new Vector3(left - ib.min.x, iconY - ib.center.y, 0f);
            ib = iconPicture.bounds;

            float gap = ib.size.x * 0.45f;
            // Measured by its letters, not its text box (which is wide and right-aligned).
            Bounds wb = Texts.LetterBounds(wordText);
            _word.transform.position += new Vector3(ib.max.x + gap - wb.min.x, textY - wb.center.y, 0f);
        }

        // The description in the details panel: its longest text.
        private static Component DescriptionText()
        {
            if (_description == null || !_description.gameObject.activeInHierarchy)
            {
                _description = _details == null ? null : Texts.All(_details)
                    .Where(c => c.gameObject.activeInHierarchy)
                    .OrderByDescending(c => (Texts.Get(c) ?? "").Length)
                    .FirstOrDefault();
            }
            return _description;
        }

        // How far above the game's prompt to sit: a little more than the button icon's height.
        private static float _lift = 0.9f;

        private static float LineHeight(GameObject icon)
        {
            SpriteRenderer r = icon.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(x => x.sprite != null);
            if (r == null) return 0.9f;
            float h = r.sprite.bounds.size.y * Mathf.Abs(r.transform.lossyScale.y);
            return h > 0.05f && h < 3f ? h * 1.35f : 0.9f;
        }

        internal static void Destroy()
        {
            if (_prompt != null) UnityEngine.Object.Destroy(_prompt);
            _prompt = null;
            _originalText = null;
            _originalIcon = null;
            _gaveUp = false;
            _reported = false;
            _lift = 0.9f;
            _word = null;
            _icon = null;
            _shownLabel = null;
            _details = null;
            _description = null;
        }

        private static bool TryCreate()
        {
            if (_gaveUp || Time.unscaledTime < _nextTry) return false;
            _nextTry = Time.unscaledTime + 0.5f;

            GameObject inventory = GameObject.Find("_GameCameras/HudCamera/Inventory");
            Transform charms = inventory != null ? inventory.transform.Find("Charms") : null;
            if (charms == null) return false;

            // 1. The game's own prompt: its "Equip"/"Unequip" word, showing right now.
            // Found by its place in the menu, so it works in every language; the
            // English words are only a fallback in case another mod moves it.
            Component equipText = null;
            Transform promptText = charms.Find("Confirm Action/Text");
            if (promptText != null && promptText.gameObject.activeInHierarchy)
                equipText = Texts.Find(promptText, t => !string.IsNullOrEmpty(t) && t.Trim().Length > 0, activeOnly: true);
            if (equipText == null)
                equipText = Texts.Find(charms, t => IsEquipWord(t), activeOnly: true);
            if (equipText == null) return false; // not on screen yet: try again shortly

            _originalText = equipText.transform;
            _originalIcon = NearestPicture(_originalText);

            // 2. A Dream Nail button icon to copy.
            Transform dreamNailIcon = FindDeep(inventory.transform, "IconDreamnail");

            if (dreamNailIcon == null && _originalIcon == null)
            {
                _gaveUp = true;
                UnsteadyBodyMod.Instance?.Log("No Flip hint: couldn't find a button icon to copy.");
                Discover(inventory.transform, charms);
                return false;
            }

            Transform container = _originalText.parent;
            var root = new GameObject("Unsteady Body Flip Prompt");
            root.layer = _originalText.gameObject.layer;
            root.transform.SetParent(container, false);
            root.SetActive(false);

            // The word.
            GameObject word = CopyInto(_originalText, root.transform);
            _word = word;
            _details = charms.Find("Details");
            word.transform.localPosition = _originalText.localPosition;
            word.transform.localScale = _originalText.localScale;
            Texts.SetAll(word, Label);
            Texts.RefreshAll(word);
            _shownLabel = Label;

            // The button icon: the Dream Nail one if found, else the menu's own icon re-pointed.
            Transform iconSource = dreamNailIcon != null ? dreamNailIcon : _originalIcon;
            GameObject icon = CopyInto(iconSource, root.transform);
            _icon = icon;
            if (_originalIcon != null)
            {
                icon.transform.localPosition = _originalIcon.localPosition;
                icon.transform.localScale = _originalIcon.localScale;
            }
            else
            {
                // No button icon found beside the word: put the Dream Nail icon just after it.
                icon.transform.position = _originalText.position + new Vector3(1.1f, 0f, 0f);
            }
            if (dreamNailIcon == null) PointAtDreamNail(icon);
            HideExtraWords(icon);

            _prompt = root;
            _lift = LineHeight(icon);
            DescribeContainer(container);

            UnsteadyBodyMod.Verbose(
                $"Flip hint built from {PathOf(_originalText)} and {(dreamNailIcon != null ? PathOf(dreamNailIcon) : "the menu's own icon")}");
            return true;
        }

        private static bool IsEquipWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.Trim();
            return t.Equals("Equip", StringComparison.OrdinalIgnoreCase) || t.Equals("Unequip", StringComparison.OrdinalIgnoreCase);
        }

        // The button picture next to the prompt's word: the closest picture among
        // the word's neighbours.
        private static Transform NearestPicture(Transform word)
        {
            Transform parent = word.parent;
            if (parent == null) return null;
            Transform best = null;
            float bestDistance = float.MaxValue;
            foreach (SpriteRenderer r in parent.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.transform == word || r.transform.IsChildOf(word)) continue;
                if (r.sprite == null) continue;
                Vector3 size = r.sprite.bounds.size;
                Vector3 scale = r.transform.lossyScale;
                if (Mathf.Abs(size.x * scale.x) > 2f || Mathf.Abs(size.y * scale.y) > 2f) continue; // a backdrop, not a button
                float d = Vector3.Distance(r.transform.position, word.position);
                if (d < bestDistance) { bestDistance = d; best = r.transform; }
            }
            // Only a direct neighbour counts, so the copy can sit in the same place.
            while (best != null && best.parent != parent) best = best.parent;
            return best;
        }

        // Copies a piece of the menu with none of its scripts running until the
        // ones that would overwrite it are removed.
        private static GameObject CopyInto(Transform source, Transform parent)
        {
            var holder = new GameObject("Unsteady Body Copy Holder");
            holder.SetActive(false);
            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
            copy.name = source.name + " (Unsteady Body)";

            foreach (MonoBehaviour m in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m == null) continue;
                string type = m.GetType().Name;
                if (type == "PlayMakerFSM" || type.StartsWith("SetTextMeshProGameText"))
                {
                    UnityEngine.Object.DestroyImmediate(m);
                }
            }

            copy.transform.SetParent(parent, false);
            copy.SetActive(true);
            UnityEngine.Object.Destroy(holder);
            return copy;
        }

        // The inventory's Dream Nail icon comes with its own word ("Hold"). Keep only
        // the text the icon itself uses as its key label (the letter shown on keyboard
        // keys), and hide the rest.
        private static void HideExtraWords(GameObject icon)
        {
            var keep = new HashSet<Component>();
            foreach (MonoBehaviour m in icon.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m == null || !m.GetType().Name.Contains("ButtonIcon")) continue;
                for (Type t = m.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                {
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (f.GetValue(m) is Component c && Texts.Type.IsInstanceOfType(c)) keep.Add(c);
                    }
                }
            }

            foreach (Component c in Texts.All(icon.transform))
            {
                if (keep.Contains(c)) continue;
                Renderer r = c.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
                Texts.Set(c, "");
            }
        }

        private static void PointAtDreamNail(GameObject icon)
        {
            foreach (MonoBehaviour m in icon.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (m == null || !m.GetType().Name.Contains("ButtonIcon")) continue;
                Type t = m.GetType();
                MethodInfo setAction = t.GetMethod("SetAction", new[] { typeof(HeroActionButton) });
                if (setAction != null) { setAction.Invoke(m, new object[] { HeroActionButton.DREAM_NAIL }); continue; }
                FieldInfo field = t.GetField("action", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(HeroActionButton)) field.SetValue(m, HeroActionButton.DREAM_NAIL);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        // One-off: what's inside the game's prompt, for adjusting placement.
        private static void DescribeContainer(Transform container)
        {
            var log = UnsteadyBodyMod.Instance;
            if (log == null || container == null || !UnsteadyBodyMod.Settings.DetailedLogging) return;
            log.Log($"Game's prompt '{container.name}' at {container.position}, lifting Flip by {_lift:0.00}");
            foreach (Transform child in container)
            {
                if (child.name.Contains("Unsteady Body")) continue;
                string types = string.Join(", ", child.GetComponents<Component>().Select(c => c.GetType().Name));
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                string sprite = sr != null && sr.sprite != null ? $", sprite '{sr.sprite.name}' {sr.sprite.bounds.size}" : "";
                log.Log($"  {child.name} at {child.position} [{types}]{sprite}");
            }
        }

        private static Bounds BoundsOf(Transform a, Transform b)
        {
            var renderers = new List<Renderer>();
            if (a != null) renderers.AddRange(a.GetComponentsInChildren<Renderer>());
            if (b != null) renderers.AddRange(b.GetComponentsInChildren<Renderer>());
            if (renderers.Count == 0) return new Bounds(a != null ? a.position : Vector3.zero, Vector3.one * 0.5f);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        // If the prompt can't be built: list what the charm menu does contain, so
        // the next attempt doesn't have to guess.
        private static void Discover(Transform inventory, Transform charms)
        {
            var log = UnsteadyBodyMod.Instance;
            if (log == null) return;

            foreach (Component c in Texts.All(charms).Take(40))
            {
                log.Log($"  text  {PathOf(c.transform)} = '{Texts.Get(c)}'");
            }
            foreach (MonoBehaviour m in inventory.GetComponentsInChildren<MonoBehaviour>(true)
                         .Where(m => m != null && m.GetType().Name.Contains("Icon")).Take(40))
            {
                log.Log($"  icon  {PathOf(m.transform)} ({m.GetType().Name})");
            }
        }

        /// <summary>The game's menu text (TextMeshPro), reached through reflection.</summary>
        private static class Texts
        {
            private static readonly Type TextType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_Text", false))
                .FirstOrDefault(t => t != null);

            private static readonly PropertyInfo TextProperty = TextType?.GetProperty("text");
            private static readonly PropertyInfo ColorProperty = TextType?.GetProperty("color");

            internal static Type Type => TextType ?? typeof(Component);

            internal static float Alpha(Component c)
            {
                if (ColorProperty == null || c == null) return 1f;
                return ColorProperty.GetValue(c, null) is Color col ? col.a : 1f;
            }

            internal static void SetAlpha(Component c, float alpha)
            {
                if (ColorProperty == null || c == null) return;
                if (ColorProperty.GetValue(c, null) is Color col) ColorProperty.SetValue(c, new Color(col.r, col.g, col.b, alpha), null);
            }

            internal static IEnumerable<Component> All(Transform root) =>
                TextType == null ? Enumerable.Empty<Component>() : root.GetComponentsInChildren(TextType, true);

            internal static Component Find(Transform root, Func<string, bool> matches, bool activeOnly) =>
                All(root).FirstOrDefault(c => (!activeOnly || c.gameObject.activeInHierarchy) && matches(Get(c)));

            internal static string Get(Component c) =>
                TextProperty != null && c != null ? TextProperty.GetValue(c, null) as string : null;

            internal static void Set(Component c, string value)
            {
                if (TextProperty != null && c != null) TextProperty.SetValue(c, value, null);
            }

            private static readonly PropertyInfo LetterBoundsProperty = TextType?.GetProperty("textBounds");

            /// <summary>Where the letters themselves are drawn, in world space.</summary>
            internal static Bounds LetterBounds(Component c)
            {
                if (c == null) return new Bounds();
                if (LetterBoundsProperty?.GetValue(c, null) is Bounds local && local.size.x > 0f)
                {
                    Vector3 a = c.transform.TransformPoint(local.min);
                    Vector3 b = c.transform.TransformPoint(local.max);
                    var world = new Bounds((a + b) * 0.5f, Vector3.zero);
                    world.Encapsulate(a);
                    world.Encapsulate(b);
                    return world;
                }
                Renderer r = c.GetComponent<Renderer>();
                return r != null ? r.bounds : new Bounds(c.transform.position, Vector3.zero);
            }

            private static readonly MethodInfo ForceMeshUpdate = TextType?.GetMethods()
                .Where(m => m.Name == "ForceMeshUpdate" && m.GetParameters().All(p => p.ParameterType == typeof(bool)))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();

            /// <summary>Makes the text lay itself out now, so its size can be measured this frame.</summary>
            internal static void RefreshAll(GameObject root)
            {
                if (ForceMeshUpdate == null) return;
                object[] args = ForceMeshUpdate.GetParameters().Select(_ => (object)false).ToArray();
                foreach (Component c in All(root.transform))
                {
                    if (!c.gameObject.activeInHierarchy) continue;
                    try { ForceMeshUpdate.Invoke(c, args); } catch { }
                }
            }

            internal static void SetAll(GameObject root, string value)
            {
                if (TextProperty == null) return;
                foreach (Component c in All(root.transform)) TextProperty.SetValue(c, value, null);
            }
        }
    }
}
