using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Shows Unsteady Body's icon (upside down, sky blue) while the charm is turned
    /// over, and Steady Body's own icon otherwise, in the inventory, Salubra's shop
    /// and randomizer pop-ups. The new sprite is sized to match whatever sprite it
    /// replaces, so it appears at the same size on screen.
    /// </summary>
    internal static class CharmIcon
    {
        private const int CharmId = 14;
        private const string ResourceName = "UnsteadyBody.png";
        private const string ShopBoolName = "gotCharm_14";

        private static Texture2D _texture;
        private static Sprite _inventorySprite;
        private static Sprite _originalInventorySprite;
        private static Sprite _shopSprite;

        private static bool ShowUnsteady => UnsteadyBodyMod.Local.Unsteady;

        /// <summary>The text key Salubra's shop uses for Steady Body's description, once seen.</summary>
        internal static string ShopDescKey;

        // The game's own Steady Body icon (or a skin's), the first time we see it.
        private static Sprite _vanillaSprite;

        private static void NoteVanilla(Sprite s)
        {
            if (_vanillaSprite == null && s != null && (_texture == null || s.texture != _texture)) _vanillaSprite = s;
        }

        /// <summary>Whether a sprite is a picture of this charm, either side up.</summary>
        internal static bool IsCharmSprite(Sprite s)
        {
            if (s == null) return false;
            if (_texture != null && s.texture == _texture) return true; // any of ours
            if (s == _vanillaSprite || s == _originalInventorySprite) return true;
            Sprite v = _vanillaSprite;
            return v != null && s.texture == v.texture && s.rect == v.rect;
        }

        /// <summary>The icon the charm should show right now.</summary>
        internal static Sprite CurrentSprite() =>
            CharmIconList.Instance != null ? CharmIconList.Instance.GetSprite(CharmId) : null;

        /// <summary>Brings every icon in line with which way up the charm is.</summary>
        internal static void Refresh()
        {
            ApplyToInventory();
            ApplyToItemChanger();
        }

        internal static void Load()
        {
            if (_texture != null) return;

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                UnsteadyBodyMod.Instance?.LogError($"Charm icon resource '{ResourceName}' not found.");
                return;
            }

            var data = new byte[stream.Length];
            int read = 0;
            while (read < data.Length)
            {
                int n = stream.Read(data, read, data.Length - read);
                if (n <= 0) break;
                read += n;
            }

            _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!_texture.LoadImage(data))
            {
                UnsteadyBodyMod.Instance?.LogError("Couldn't read the charm icon image.");
                _texture = null;
                return;
            }
            _texture.wrapMode = TextureWrapMode.Clamp;
            UnsteadyBodyMod.Instance?.Log($"Loaded charm icon ({_texture.width}x{_texture.height}).");
        }

        // The charm grid asks CharmIconList.GetSprite for each icon as it draws it.
        // Answering that call for charm 14 means nothing that rewrites the icon
        // list (such as Custom Knight resetting skins) can bring the old icon back.
        private static Sprite _lookupSprite;

        internal static Sprite OverrideLookup(Sprite fromGame)
        {
            NoteVanilla(fromGame);
            if (!ShowUnsteady)
            {
                return fromGame != null && fromGame == _inventorySprite && _originalInventorySprite != null
                    ? _originalInventorySprite
                    : fromGame;
            }
            if (_texture == null) return fromGame;
            if (fromGame != null && (fromGame == _inventorySprite || fromGame == _lookupSprite)) return fromGame;
            if (_lookupSprite == null) _lookupSprite = SpriteLike(fromGame);
            return _lookupSprite;
        }

        /// <summary>Puts the right icon into the game's charm icon list. Safe to call repeatedly.</summary>
        internal static void ApplyToInventory()
        {
            if (!ShowUnsteady)
            {
                RestoreInventory();
                return;
            }

            CharmIconList list = CharmIconList.Instance;
            if (_texture == null || list == null || list.spriteList == null || list.spriteList.Length <= CharmId) return;

            Sprite current = list.spriteList[CharmId];
            if (current != null && current == _inventorySprite) return;
            NoteVanilla(current);

            _originalInventorySprite = current;
            _inventorySprite = SpriteLike(current);
            list.spriteList[CharmId] = _inventorySprite;
        }

        /// <summary>Called after a shop item wakes up. Notes Steady Body's description key, and swaps its picture if the charm is turned over.</summary>
        internal static void ApplyToShopItem(ShopItemStats item)
        {
            if (item == null || item.playerDataBoolName != ShopBoolName) return;
            ShopDescKey = item.descConvo;
            if (_texture == null || !ShowUnsteady) return;

            Transform child = item.transform.Find("Item Sprite");
            SpriteRenderer renderer = child != null ? child.GetComponent<SpriteRenderer>() : null;
            if (renderer == null) return;

            if (_shopSprite == null) _shopSprite = SpriteLike(renderer.sprite);
            renderer.sprite = _shopSprite;
        }

        // ---------- ItemChanger (randomizer) support ----------
        // ItemChanger shows items using its own copy of each icon, looked up by name
        // ("Charms.14") and kept in a private cache that it checks first. Putting our
        // sprite in that cache makes every randomizer pop-up and shop show it.
        // Done through reflection, so ItemChanger is optional: if it isn't installed,
        // or its internals change, this does nothing.

        private const string ItemChangerKey = "Charms.14";
        private static IDictionary<string, Sprite> _itemChangerCache;
        private static Sprite _itemChangerSprite;
        private static Sprite _originalItemChangerSprite;
        private static bool _itemChangerSearched;

        internal static void ApplyToItemChanger()
        {
            if (_texture == null) return;
            if (!_itemChangerSearched) FindItemChanger();
            if (_itemChangerCache == null) return;

            if (!ShowUnsteady)
            {
                RestoreItemChanger();
                return;
            }

            if (_itemChangerCache.TryGetValue(ItemChangerKey, out Sprite current) && current != null && current == _itemChangerSprite) return;

            if (current == null) current = LoadItemChangerOriginal();
            _originalItemChangerSprite = current;
            if (_itemChangerSprite == null) _itemChangerSprite = SpriteLike(current);
            _itemChangerCache[ItemChangerKey] = _itemChangerSprite;
        }

        private static object _itemChangerManager;
        private static MethodInfo _itemChangerGetSprite;

        private static void FindItemChanger()
        {
            _itemChangerSearched = true;
            try
            {
                Type managerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("ItemChanger.Internal.SpriteManager", false))
                    .FirstOrDefault(t => t != null);
                if (managerType == null) return; // ItemChanger not installed

                _itemChangerManager = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                _itemChangerGetSprite = managerType.GetMethod("GetSprite", new[] { typeof(string) });
                FieldInfo cacheField = managerType.GetField("_cachedSprites", BindingFlags.NonPublic | BindingFlags.Instance);

                if (_itemChangerManager == null || cacheField == null)
                {
                    UnsteadyBodyMod.Instance?.LogWarn("ItemChanger found, but its sprite cache wasn't. Randomizer pop-ups will show the old icon.");
                    return;
                }

                _itemChangerCache = cacheField.GetValue(_itemChangerManager) as IDictionary<string, Sprite>;
                UnsteadyBodyMod.Instance?.Log("ItemChanger found: using the Unsteady Body icon in randomizer pop-ups and shops.");
            }
            catch (Exception e)
            {
                UnsteadyBodyMod.Instance?.LogWarn("Couldn't set up ItemChanger icon support: " + e.Message);
                _itemChangerCache = null;
            }
        }

        // Loads ItemChanger's own Steady Body icon, so ours can match its size.
        private static Sprite LoadItemChangerOriginal()
        {
            try { return _itemChangerGetSprite?.Invoke(_itemChangerManager, new object[] { ItemChangerKey }) as Sprite; }
            catch { return null; }
        }

        internal static void Restore()
        {
            RestoreItemChanger();
            RestoreInventory();
        }

        private static void RestoreItemChanger()
        {
            if (_itemChangerCache != null && _itemChangerSprite != null
                && _itemChangerCache.TryGetValue(ItemChangerKey, out Sprite cached) && cached == _itemChangerSprite)
            {
                if (_originalItemChangerSprite != null) _itemChangerCache[ItemChangerKey] = _originalItemChangerSprite;
                else _itemChangerCache.Remove(ItemChangerKey);
            }
        }

        private static void RestoreInventory()
        {
            CharmIconList list = CharmIconList.Instance;
            if (list != null && list.spriteList != null && list.spriteList.Length > CharmId
                && list.spriteList[CharmId] == _inventorySprite && _originalInventorySprite != null)
            {
                list.spriteList[CharmId] = _originalInventorySprite;
            }
            _inventorySprite = null;
            _originalInventorySprite = null;
        }

        // Same on-screen size and pivot as the sprite being replaced.
        private static Sprite SpriteLike(Sprite original)
        {
            float pixelsPerUnit = 100f;
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            if (original != null && original.rect.width > 0f && original.rect.height > 0f)
            {
                pixelsPerUnit = _texture.width / (original.rect.width / original.pixelsPerUnit);
                pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
            }

            return Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), pivot, pixelsPerUnit);
        }
    }
}
