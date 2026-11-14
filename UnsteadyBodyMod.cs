using System;
using System.Collections.Generic;
using Modding;
using UnityEngine;

namespace UnsteadyBody
{
    /// <summary>
    /// Gives Steady Body (charm 14) a second side. Turned over at a bench, it becomes
    /// Unsteady Body: nail strikes throw the Knight backwards much harder, downward
    /// strikes launch them skywards, and upward strikes in the air slam them back down.
    /// </summary>
    public class UnsteadyBodyMod : Mod, IGlobalSettings<UnsteadyBodySettings>, ILocalSettings<UnsteadyBodySaveData>, IMenuMod, ITogglableMod
    {
        internal static UnsteadyBodyMod Instance;

        /// <summary>Logs only when Detailed logging is on: step-by-step details for bug reports.</summary>
        internal static void Verbose(string message)
        {
            if (Settings.DetailedLogging) Instance?.Log(message);
        }
        internal static UnsteadyBodySettings Settings = new UnsteadyBodySettings();
        internal static UnsteadyBodySaveData Local = new UnsteadyBodySaveData();

        private const int CharmId = 14;
        // The charm's text (name, description, hints) lives in Strings.cs, in every language.

        public UnsteadyBodyMod() : base("Unsteady Body") { }

        public override string GetVersion() => "0.1.0.0";

        public override void Initialize()
        {
            Instance = this;

            ModHooks.LanguageGetHook += OnLanguageGet;
            ModHooks.NewGameHook += OnNewGame;

            On.HeroController.RecoilLeft += OnRecoilLeft;
            On.HeroController.RecoilRight += OnRecoilRight;
            On.HeroController.RecoilLeftLong += OnRecoilLeftLong;
            On.HeroController.RecoilRightLong += OnRecoilRightLong;
            On.HeroController.RecoilDown += OnRecoilDown;
            On.HeroController.Bounce += OnBounce;
            On.HeroController.BounceHigh += OnBounceHigh;

            CharmIcon.Load();
            CharmIcon.Refresh();
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            On.ShopItemStats.Awake += OnShopItemAwake;
            On.CharmIconList.GetSprite += OnGetCharmSprite;
            On.CharmDisplay.Start += OnCharmDisplayStart;
            Momentum.Hook();
            Glide.Hook();
            JellyBlast.Hook();

            _tracker = new GameObject("Unsteady Body Tracker");
            UnityEngine.Object.DontDestroyOnLoad(_tracker);
            _tracker.AddComponent<TrackerBubble>();
            _tracker.AddComponent<LaunchGuard>();
            _tracker.AddComponent<CharmFlipper>();
            _tracker.AddComponent<GeigerCounter>();
        }

        private GameObject _tracker;

        public void Unload()
        {
            ModHooks.LanguageGetHook -= OnLanguageGet;
            ModHooks.NewGameHook -= OnNewGame;

            On.HeroController.RecoilLeft -= OnRecoilLeft;
            On.HeroController.RecoilRight -= OnRecoilRight;
            On.HeroController.RecoilLeftLong -= OnRecoilLeftLong;
            On.HeroController.RecoilRightLong -= OnRecoilRightLong;
            On.HeroController.RecoilDown -= OnRecoilDown;
            On.HeroController.Bounce -= OnBounce;
            On.HeroController.BounceHigh -= OnBounceHigh;

            Launch.RestoreVanilla();

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            On.ShopItemStats.Awake -= OnShopItemAwake;
            On.CharmIconList.GetSprite -= OnGetCharmSprite;
            On.CharmDisplay.Start -= OnCharmDisplayStart;
            Momentum.Unhook();
            Glide.Unhook();
            JellyBlast.Unhook();
            CharmIcon.Restore();

            if (_tracker != null) UnityEngine.Object.Destroy(_tracker);
            _tracker = null;
        }

        // ---------- Charm name and description ----------

        private string OnLanguageGet(string key, string sheetTitle, string orig)
        {
            if (sheetTitle == "UI")
            {
                // The charm menu asks for a charm's name when the cursor lands on it.
                if (CharmFlipper.ReadingText)
                {
                    // The flipper reading the text itself: not the menu.
                }
                else if (key.StartsWith("CHARM_NAME_") && int.TryParse(key.Substring("CHARM_NAME_".Length), out int id))
                {
                    CharmFlipper.SelectedCharm = id;
                }
                else if (key.StartsWith("INV_NAME"))
                {
                    CharmFlipper.SelectedCharm = -1; // moved to the inventory pane
                }

                if (key == "CHARM_NAME_" + CharmId) return Local.Unsteady ? Strings.CharmName : orig;
                if (key == "CHARM_DESC_" + CharmId) return Local.Unsteady ? Strings.CharmDescription + Strings.UnsteadyHint : orig + Strings.SteadyHint;
            }
            else if (sheetTitle == "Journal")
            {
                CharmFlipper.SelectedCharm = -1; // moved to the journal pane
            }

            // Salubra's shop listing, if it uses its own description text.
            if (!Local.Unsteady && CharmIcon.ShopDescKey != null && key == CharmIcon.ShopDescKey)
            {
                return orig + Strings.SteadyHint;
            }

            return orig;
        }

        // ---------- Charm icon ----------

        private static void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
        {
            CharmIcon.Refresh();
        }

        private static Sprite OnGetCharmSprite(On.CharmIconList.orig_GetSprite orig, CharmIconList self, int id)
        {
            Sprite sprite = orig(self, id);
            return id == CharmId ? CharmIcon.OverrideLookup(sprite) : sprite;
        }

        private static void OnCharmDisplayStart(On.CharmDisplay.orig_Start orig, CharmDisplay self)
        {
            orig(self);
            CharmFlipper.AfterCharmDisplayStart(self);
        }

        private static void OnShopItemAwake(On.ShopItemStats.orig_Awake orig, ShopItemStats self)
        {
            orig(self);
            CharmIcon.ApplyToShopItem(self);
        }

        // ---------- Sideways recoil ----------
        // The game skips sideways recoil when charm 14 is worn (that's all Steady
        // Body does). With the charm worn, these hooks hide it for the length of
        // the call so the recoil happens, using the boosted values from Launch.

        private static void OnRecoilLeft(On.HeroController.orig_RecoilLeft orig, HeroController self)
        {
            if (AngledLaunch.IgnoringOwnPush) return;
            Launch.Apply(self);
            bool was = IsRecoiling(self);
            WithoutSteadyBody(() => orig(self));
            if (!was && IsRecoiling(self) && Launch.Active) LaunchSounds.Launch(self);
        }

        private static void OnRecoilRight(On.HeroController.orig_RecoilRight orig, HeroController self)
        {
            if (AngledLaunch.IgnoringOwnPush) return;
            Launch.Apply(self);
            bool was = IsRecoiling(self);
            WithoutSteadyBody(() => orig(self));
            if (!was && IsRecoiling(self) && Launch.Active) LaunchSounds.Launch(self);
        }

        private static bool IsRecoiling(HeroController hero) => hero.cState.recoilingLeft || hero.cState.recoilingRight;

        // Long recoils (nail clashes and the like) ignore Steady Body in the base
        // game. They share the recoil duration, so keep the values in step.
        private static void OnRecoilLeftLong(On.HeroController.orig_RecoilLeftLong orig, HeroController self)
        {
            Launch.Apply(self);
            orig(self);
        }

        private static void OnRecoilRightLong(On.HeroController.orig_RecoilRightLong orig, HeroController self)
        {
            Launch.Apply(self);
            orig(self);
        }

        private static void WithoutSteadyBody(Action recoil)
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null || !Launch.Active) // Steady side: the game's own Steady Body applies
            {
                recoil();
                return;
            }

            pd.equippedCharm_14 = false;
            try { recoil(); }
            finally { pd.equippedCharm_14 = true; }
        }

        // ---------- Pogo (downward strike) ----------

        private static void OnBounce(On.HeroController.orig_Bounce orig, HeroController self)
        {
            if (AngledLaunch.IgnoringOwnPush) return;
            Launch.Apply(self);
            bool was = self.cState.bouncing;
            orig(self);
            PogoSound(self, was);
        }

        private static void OnBounceHigh(On.HeroController.orig_BounceHigh orig, HeroController self)
        {
            if (AngledLaunch.IgnoringOwnPush) return;
            Launch.Apply(self);
            bool was = self.cState.bouncing;
            orig(self);
            PogoSound(self, was);
        }

        private static void PogoSound(HeroController hero, bool wasBouncing)
        {
            if (!wasBouncing && hero.cState.bouncing && Launch.Active && Settings.PogoLaunch) LaunchSounds.Launch(hero);
        }

        // ---------- Upward strike in the air ----------

        private static void OnRecoilDown(On.HeroController.orig_RecoilDown orig, HeroController self)
        {
            if (AngledLaunch.IgnoringOwnPush) return;
            orig(self);
            if (Settings.UpwardSlam && Launch.Active && !self.controlReqlinquished)
            {
                Launch.Slam(self);
                LaunchSounds.Launch(self);
            }
        }

        // ---------- Settings ----------

        public void OnLoadGlobal(UnsteadyBodySettings s) => Settings = s ?? new UnsteadyBodySettings();

        public void OnLoadLocal(UnsteadyBodySaveData s)
        {
            Local = s ?? new UnsteadyBodySaveData();
            CharmIcon.Refresh();
        }

        public UnsteadyBodySaveData OnSaveLocal() => Local;

        private static void OnNewGame()
        {
            Local = new UnsteadyBodySaveData();
            CharmIcon.Refresh();
        }

        public UnsteadyBodySettings OnSaveGlobal() => Settings;

        public bool ToggleButtonInsideMenu => true;

        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            var entries = new List<IMenuMod.MenuEntry>();
            if (toggleButtonEntry.HasValue) entries.Add(toggleButtonEntry.Value);

            entries.Add(new IMenuMod.MenuEntry
            {
                Name = Strings.Menu("StrengthName"),
                Description = Strings.Menu("StrengthDesc"),
                Values = Strings.StrengthNames,
                Saver = i => Settings.Strength = i,
                Loader = () => Mathf.Clamp(Settings.Strength, 0, Launch.StrengthNames.Length - 1),
            });

            entries.Add(new IMenuMod.MenuEntry
            {
                Name = Strings.Menu("SpeedName"),
                Description = Strings.Menu("SpeedDesc"),
                Values = Strings.SpeedLimitNames,
                Saver = i => Settings.SpeedLimit = i,
                Loader = () => Mathf.Clamp(Settings.SpeedLimit, 0, Launch.SpeedLimitNames.Length - 1),
            });

            entries.Add(new IMenuMod.MenuEntry
            {
                Name = Strings.Menu("PogoName"),
                Description = Strings.Menu("PogoDesc"),
                Values = Strings.OffOn,
                Saver = i => Settings.PogoLaunch = i == 1,
                Loader = () => Settings.PogoLaunch ? 1 : 0,
            });

            entries.Add(new IMenuMod.MenuEntry
            {
                Name = Strings.Menu("SlamName"),
                Description = Strings.Menu("SlamDesc"),
                Values = Strings.OffOn,
                Saver = i => Settings.UpwardSlam = i == 1,
                Loader = () => Settings.UpwardSlam ? 1 : 0,
            });

            entries.Add(new IMenuMod.MenuEntry
            {
                Name = Strings.Menu("CarryName"),
                Description = Strings.Menu("CarryDesc"),
                Values = Strings.OffOn,
                Saver = i => Settings.CarryMomentum = i == 1,
                Loader = () => Settings.CarryMomentum ? 1 : 0,
            });

            entries.Add(new IMenuMod.MenuEntry
            {
                Name = "Detailed logging",
                Description = "Writes step-by-step details to ModLog.txt. Turn on when reporting a bug.",
                Values = Strings.OffOn,
                Saver = i => Settings.DetailedLogging = i == 1,
                Loader = () => Settings.DetailedLogging ? 1 : 0,
            });

            return entries;
        }
    }
}
