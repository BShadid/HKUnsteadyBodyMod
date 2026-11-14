using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Turns the charm over between Steady Body and Unsteady Body.
    ///
    /// To flip it: sit at a bench, open the charm menu, select the charm (in the
    /// main charm menu or the equipped row) and press the Dream Nail button. Like any
    /// other charm change, it only works at a bench. Every picture of the charm on
    /// screen turns over, and the name and description on the right change. Which
    /// way up it is gets saved with each save file.
    /// </summary>
    internal class CharmFlipper : MonoBehaviour
    {
        private const int CharmId = 14;
        private const string NameKey = "CHARM_NAME_14";
        private const string DescKey = "CHARM_DESC_14";
        private const float HalfFlipSeconds = 0.08f;

        // Inventory states that mean it's closed (the same list other mods use).
        private static readonly HashSet<string> InventoryClosedStates = new HashSet<string>
        {
            "Init", "Init Enemy List", "Closed", "Can Open Inventory?", "No Inv",
        };

        /// <summary>
        /// The charm the menu last asked for the name of. The charm menu asks each
        /// time the cursor lands on a charm, so the language hook sets this. It's only
        /// a first hint: an empty notch asks for nothing, so it can be out of date.
        /// </summary>
        internal static int SelectedCharm = -1;

        /// <summary>True while this class reads the charm's text itself, so those reads don't count as the menu asking.</summary>
        internal static bool ReadingText;

        private bool _flipping;

        private void Update()
        {
            if (!InventoryOpen())
            {
                SelectedCharm = -1;
                FlipPrompt.Update(false);
                return;
            }

            PlayerData pd = PlayerData.instance;
            bool selectable = pd != null && pd.atBench && pd.gotCharm_14 && SelectedCharm == CharmId;

            // The details panel must be showing this charm right now. Next to the
            // charm, an empty notch shows no name, so this rules it out.
            bool showing = selectable && DetailsShowCharm();
            FlipPrompt.Update(showing);

            if (_flipping || !selectable) return;

            InputHandler input = InputHandler.Instance;
            if (input == null || !input.inputActions.dreamNail.WasPressed) return;

            if (!showing)
            {
                UnsteadyBodyMod.Verbose("Dream Nail pressed, but the details panel isn't showing the charm. Not flipping.");
                LogMenuState();
                return;
            }

            StartCoroutine(Flip(_nameText));
        }

        private void OnDestroy() => FlipPrompt.Destroy();

        // The details panel's name text, once found, and the charm's current name.
        private Component _nameText;
        private float _nextNameSearch;
        private string _currentName;
        private bool _currentNameUnsteady;
        private string _currentNameLanguage;

        private bool DetailsShowCharm()
        {
            if (!Texts.Available) return true; // can't check: trust the menu's hint

            string language = Strings.CurrentLanguage();
            if (_currentName == null || _currentNameUnsteady != UnsteadyBodyMod.Local.Unsteady || _currentNameLanguage != language)
            {
                _currentName = Read(NameKey);
                _currentNameUnsteady = UnsteadyBodyMod.Local.Unsteady;
                _currentNameLanguage = language;
            }

            if (_nameText != null && _nameText.gameObject.activeInHierarchy && SameText(Texts.Get(_nameText), _currentName)) return true;

            // Look for it again, but not every frame.
            if (Time.unscaledTime < _nextNameSearch) return false;
            _nextNameSearch = Time.unscaledTime + 0.25f;
            string name = _currentName;
            _nameText = Texts.FindShowing(t => SameText(t, name));
            return _nameText != null;
        }

        private static bool InventoryOpen()
        {
            string state = GameManager.instance?.inventoryFSM?.Fsm?.ActiveStateName;
            return state != null && !InventoryClosedStates.Contains(state);
        }

        private IEnumerator Flip(Component nameText)
        {
            _flipping = true;

            // Everything showing the charm now: its pictures and its description.
            string oldDesc = Read(DescKey);
            Component descText = Texts.Available
                ? Texts.FindShowing(t => SameText(t, oldDesc)) ?? Texts.FindShowing(t => SameStart(t, oldDesc))
                : null;
            var report = new List<string>();
            List<SpriteRenderer> icons = FindCharmPictures(report);

            // Each picture's normal size, measured once so the animation always returns to it.
            var baseScales = new Dictionary<Transform, Vector3>();
            foreach (SpriteRenderer icon in icons)
            {
                if (!baseScales.ContainsKey(icon.transform)) baseScales[icon.transform] = icon.transform.localScale;
            }

            // First half: squash the pictures flat.
            yield return Squash(baseScales, 1f, 0f);

            UnsteadyBodyMod.Local.Unsteady = !UnsteadyBodyMod.Local.Unsteady;
            CharmIcon.Refresh();
            Launch.Apply(HeroController.instance);

            Sprite now = CharmIcon.CurrentSprite();
            if (now != null)
            {
                foreach (SpriteRenderer icon in icons)
                {
                    if (icon != null) icon.sprite = now;
                }
            }

            if (nameText != null) Texts.Set(nameText, Read(NameKey));
            if (descText != null) Texts.Set(descText, Read(DescKey));

            LaunchSounds.Flip(HeroController.instance);

            // Second half: open them back up, now the other way up.
            yield return Squash(baseScales, 0f, 1f);

            UnsteadyBodyMod.Verbose(
                (UnsteadyBodyMod.Local.Unsteady ? "Turned over: Unsteady Body" : "Turned over: Steady Body")
                + $" ({icons.Count} pictures, name {(nameText != null ? "updated" : "not found")}, description {(descText != null ? "updated" : "not found")})");
            foreach (string line in report) UnsteadyBodyMod.Verbose(line);
            UnsteadyBodyMod.Verbose($"  now showing sprite '{(now != null ? now.name : "none")}'");
            _flipping = false;
        }

        private static IEnumerator Squash(Dictionary<Transform, Vector3> baseScales, float from, float to)
        {
            for (float t = 0f; t < HalfFlipSeconds; t += Time.unscaledDeltaTime)
            {
                SetHeight(baseScales, Mathf.Lerp(from, to, t / HalfFlipSeconds));
                yield return null;
            }
            SetHeight(baseScales, to);
        }

        private static void SetHeight(Dictionary<Transform, Vector3> baseScales, float factor)
        {
            foreach (KeyValuePair<Transform, Vector3> pair in baseScales)
            {
                if (pair.Key == null) continue;
                Vector3 b = pair.Value;
                pair.Key.localScale = new Vector3(b.x, b.y * factor, b.z);
            }
        }

        // Every picture of the charm on screen: in the main charm menu (including its
        // darkened copy when equipped), in the equipped row, and in the details panel.
        // Found two ways, because other mods (such as Custom Knight skins) can give the
        // charm's display its own copy of the picture that wouldn't otherwise match.
        private static List<SpriteRenderer> FindCharmPictures(List<string> report)
        {
            var found = new List<SpriteRenderer>();

            // The charm's own places in the charm menu, found by name. This works
            // whatever picture they show, including another mod's copy of the icon.
            GameObject pane = GameObject.Find("_GameCameras/HudCamera/Inventory/Charms");
            if (pane != null)
            {
                foreach (string path in new[] { "Collected Charms/14/Sprite", "Details/Detail Sprite" })
                {
                    Transform t = pane.transform.Find(path);
                    SpriteRenderer r = t != null ? t.GetComponent<SpriteRenderer>() : null;
                    if (r != null && t.gameObject.activeInHierarchy && !found.Contains(r))
                    {
                        found.Add(r);
                        report.Add($"  place    {Describe(r)}");
                    }
                }
            }

            foreach (CharmDisplay display in Resources.FindObjectsOfTypeAll<CharmDisplay>())
            {
                if (display == null || display.id != CharmId || !display.gameObject.scene.IsValid() || !display.gameObject.activeInHierarchy) continue;
                foreach (SpriteRenderer r in new[] { display.spriteRenderer, display.flashSpriteRenderer })
                {
                    if (r != null && !found.Contains(r))
                    {
                        found.Add(r);
                        report.Add($"  display  {Describe(r)}");
                    }
                }
            }

            foreach (SpriteRenderer r in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (r != null && !found.Contains(r) && r.gameObject.scene.IsValid() && r.gameObject.activeInHierarchy && CharmIcon.IsCharmSprite(r.sprite))
                {
                    found.Add(r);
                    report.Add($"  picture  {Describe(r)}");
                }
            }
            return found;
        }

        private static string Describe(SpriteRenderer r)
        {
            string sprite = r.sprite != null ? r.sprite.name : "none";
            string texture = r.sprite != null && r.sprite.texture != null ? r.sprite.texture.name : "none";
            return $"{PathOf(r.transform)} (sprite '{sprite}', texture '{texture}', active {r.enabled})";
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>
        /// Called when the game builds a charm's picture. Other mods (Custom Knight)
        /// can set their own picture at that moment; this runs after them, so a charm
        /// placed in the equipped row while turned over shows the right side.
        /// </summary>
        internal static void AfterCharmDisplayStart(CharmDisplay display)
        {
            if (display == null || display.id != CharmId || !UnsteadyBodyMod.Local.Unsteady) return;
            Sprite now = CharmIcon.CurrentSprite();
            if (now == null) return;
            if (display.spriteRenderer != null) display.spriteRenderer.sprite = now;
            if (display.flashSpriteRenderer != null) display.flashSpriteRenderer.sprite = now;
        }

        // ---------- Text ----------

        private static string Read(string key)
        {
            ReadingText = true;
            try { return Language.Language.Get(key, "UI"); }
            finally { ReadingText = false; }
        }

        private static bool SameText(string shown, string expected) =>
            shown != null && expected != null
            && string.Equals(Plain(shown), Plain(expected), StringComparison.OrdinalIgnoreCase);

        // Text as shown on screen: line breaks instead of <br>, no surrounding space.
        private static string Plain(string s) => s.Replace("<br>", "\n").Replace("\r", "").Trim();

        private static bool SameStart(string shown, string expected)
        {
            if (shown == null || expected == null || expected.Length < 24) return false;
            return Plain(shown).StartsWith(Plain(expected).Substring(0, 24), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The game's menu text uses TextMeshPro. Reached through reflection, so the
        /// mod doesn't need to be built against it.
        /// </summary>
        private static class Texts
        {
            private static readonly Type TextType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_Text", false))
                .FirstOrDefault(t => t != null);

            private static readonly PropertyInfo TextProperty = TextType?.GetProperty("text");

            internal static bool Available => TextType != null && TextProperty != null;

            internal static Component FindShowing(Func<string, bool> matches)
            {
                if (!Available) return null;
                foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(TextType))
                {
                    if (o is Component c && c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy
                        && matches(TextProperty.GetValue(c, null) as string))
                    {
                        return c;
                    }
                }
                return null;
            }

            internal static string Get(Component c) =>
                Available && c != null ? TextProperty.GetValue(c, null) as string : null;

            internal static void Set(Component c, string value)
            {
                if (Available && c != null) TextProperty.SetValue(c, value?.Replace("<br>", "\n"), null);
            }
        }

        // ---------- Diagnostics ----------

        // Writes the charm menu's own cursor-tracking values to the log, so a refusal
        // that shouldn't have happened (or the reverse) can be pinned down.
        private static readonly FieldInfo CharmsMenuFsmField =
            typeof(CharmDisplay).GetField("charmsMenuFsm", BindingFlags.NonPublic | BindingFlags.Static);

        private static void LogMenuState()
        {
            try
            {
                var fsm = CharmsMenuFsmField?.GetValue(null) as PlayMakerFSM;
                if (fsm == null) return;
                string ints = string.Join(", ", fsm.FsmVariables.IntVariables.Select(v => $"{v.Name}={v.Value}"));
                UnsteadyBodyMod.Verbose($"Charm menu: state '{fsm.ActiveStateName}', {ints}");
            }
            catch (Exception e)
            {
                UnsteadyBodyMod.Verbose("Couldn't read the charm menu's state: " + e.Message);
            }
        }
    }
}
