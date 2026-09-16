# Stonewards Admin Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A BepInEx plugin that opens a polished in-game admin menu on F1 with player resurrect/kill, an item spawner, and god mode / infinite stamina / super pickaxe toggles.

**Architecture:** One `BaseUnityPlugin` (`Plugin`) binds config and polls the hotkey in `Update`, exactly like `stonewards-upgrade-queue`. The UI is Unity UI Toolkit (`UnityEngine.UIElements`) attached to the game's HUD `UIDocument` and styled with a local `Theme.cs`, matching `stonewards-mod-settings`. Admin actions go through the game's own Mirror RPCs — every one we need is `[Command(requiresAuthority = false)]`, so they work as host or as a plain client. Cheats that have no RPC (stamina, dig strength) are client-side: a Harmony patch for stamina, `CharacterStat` modifiers for the pickaxe. All game-independent logic (item search/filtering, action gating) lives in plain classes compiled into an xunit project, following `stonewards-clear-battlefield`.

**Tech Stack:** C# / netstandard2.1 class library, BepInEx 5.4.2305, HarmonyX (`0Harmony`), Mirror, Unity UI Toolkit, xunit (net8.0) for the pure logic.

**Spec:** `CLAUDE.md` (project brief: goals, out of scope, suggested stack)

## Game API Reference (verified by decompiling `Assembly-CSharp.dll` with ilspycmd)

These are the exact members the tasks below call. They were read out of the shipped assembly at
`/var/mnt/data/SteamLibrary/steamapps/common/Stonewards/Stonewards_Data/Managed/Assembly-CSharp.dll`.

| Need | Member |
| --- | --- |
| All known players | `static readonly List<FirstPersonController> FirstPersonController.LocalPlayers` (filled in `OnStartClient` for every player, cleared in `OnStopClient`) |
| Player name / id | `FirstPersonController.playerName`, `.playerID` (SyncVars) |
| Player stats component | `FirstPersonController.PlayerStats` → `PlayerStats` |
| Is dead | `PlayerStats.isDead` (SyncVar) |
| Health | `PlayerStats.GetCurrentHealth()`, `.GetHealthPercentage()`, `.MaxHealth.Value` |
| Kill / raise | `[Command(requiresAuthority = false)] PlayerStats.CmdDebugForceIsDead(FirstPersonController player, bool _isDead)` |
| Revive with health | `[Command(requiresAuthority = false)] FirstPersonController.CmdUseReviveTool(FirstPersonController _Player, Vector3 _Position, float _HealthPercentage)` |
| Heal / set health | `[Command(requiresAuthority = false)] PlayerStats.CmdUpdateHealth(float _Amount)` |
| Item list | `ItemDatabaseSO.items` (`List<ItemDataSO>`); `ItemDataSO.itemID`, `.GetLocalizedName()`, `.itemIcon`, `.isStackable`, `.maxStackSize`, `.Rarity` |
| Item lookup | `ItemManager.Instance.GetItemDataSoByID(string)` |
| Spawn item on ground | `[Command(requiresAuthority = false)] ItemManager.CmdInstantiatePickableNewItem(string _ItemID, int _Count, Vector3 _Position)` |
| Spawn item to inventory | `[Command(requiresAuthority = false)] FirstPersonController.CmdForcePickupItem(FirstPersonController _Owner, int _Count, string _ItemID)` |
| Stamina | `PlayerStats.ConsumeStamina(float, bool)` and private `PlayerStats.HandleStamina()`; private field `currentStamina`; `PlayerStats.MaxStamina.Value` |
| Dig power | `PlayerStats.DigStrength`, `.DiggingSpeed`, `.HeavyDigMultiplier` — all `CharacterStat` |
| Stat modifiers | `CharacterStat.AddOrReplaceModifier(StatModifier)`, `CharacterStat.RemoveAllModifiersFromSource(string)`, `new StatModifier(float value, StatModType type, string sourceID)`, `enum StatModType { FLAT, PERCENT_ADD, PERCENT_MULT }` |
| Damage gate (host) | `PlayerStats.CanTakeDamage(DamageType _Type)` → bool |
| Input state | `InputManager.Instance.CurrentInputState`, `.SetState(InputManager.InputState, bool enableCallback = true)`, `enum InputState { None, MainMenu, Gameplay, Inventory, Dead, ..., GeneralMenu, ... }` |
| HUD document | `Hud.Instance.hudDocument` (`UIDocument`) → `.rootVisualElement` |
| Scene / managers | `GameManager.Instance.SceneType` (`GameSceneType.Hub` / `.Level`) |
| Server check | `Mirror.NetworkServer.active`, `Mirror.NetworkClient.active` |

## Global Constraints

- Target framework `netstandard2.1`; `LangVersion` `latest`; `Nullable` `disable`; `DebugType` `embedded`.
- Assembly name and root namespace: `AdminMenu`. Plugin GUID `com.darkharasho.stonewards.adminmenu`, name `AdminMenu`, version `0.1.0` (keep `PluginVersion`, `<Version>` and `thunderstore/manifest.json` `version_number` identical).
- Thunderstore package folder / zip name: `darkharasho-AdminMenu`. Team `darkharasho`, community `stonewards`.
- Dependency string in the manifest: `BepInEx-BepInExPack-5.4.2305`.
- Never reference the game assemblies from the test project. Tests only compile game-independent source files, as in `stonewards-clear-battlefield/tests`.
- Every `Config.Bind` call passes a `ConfigDescription` with a `ConfigurationManagerAttributes` tag (`DispName`, `Order`) so darkharasho-ModSettings renders it nicely. Do not take an assembly reference on ModSettings.
- All game-object access is null-guarded: the menu can be opened in the hub, in a level, or while dead, and `Hud.Instance`, `ItemManager.Instance`, `InputManager.Instance` may all be null during scene loads.
- Match the existing mods' comment style: short `///` summaries that explain *why*, not *what*.

---

### Task 1: Project scaffolding that builds and loads

**Files:**
- Create: `src/AdminMenu/AdminMenu.csproj`
- Create: `src/AdminMenu/Plugin.cs`
- Create: `src/AdminMenu/ConfigurationManagerAttributes.cs`
- Create: `src/AdminMenu/Hotkey.cs`
- Create: `scripts/update-refs.sh`
- Create: `.gitignore` additions (`Local.props`, `bin/`, `obj/`, `dist/`)

**Interfaces:**
- Consumes: nothing.
- Produces: `AdminMenu.Plugin` with `public const string PluginGuid/PluginName/PluginVersion`, `internal static ManualLogSource Log`, `internal static Plugin Instance`; `internal static class Hotkey` with `public static bool WasPressed(KeyboardShortcut shortcut)`.

- [ ] **Step 1: Copy the three files that are identical across the author's mods**

```bash
cp ../stonewards-upgrade-queue/src/UpgradeQueue/Hotkey.cs src/AdminMenu/Hotkey.cs
cp ../stonewards-upgrade-queue/src/UpgradeQueue/ConfigurationManagerAttributes.cs src/AdminMenu/ConfigurationManagerAttributes.cs
cp ../stonewards-mod-settings/scripts/update-refs.sh scripts/update-refs.sh
sed -i 's/namespace UpgradeQueue/namespace AdminMenu/' src/AdminMenu/Hotkey.cs src/AdminMenu/ConfigurationManagerAttributes.cs
```

Then edit `scripts/update-refs.sh`: change the comment `src/ModSettings/ModSettings.csproj` to `src/AdminMenu/AdminMenu.csproj`, and add `UnityEngine.PhysicsModule` and `UnityEngine.UI` to `game_dlls` — the menu raycasts for a spawn position and the item list shows sprites. Also add `Mirror` to `game_dlls`.

- [ ] **Step 2: Write the csproj**

Copy `../stonewards-mod-settings/src/ModSettings/ModSettings.csproj` verbatim, then change `AssemblyName`/`RootNamespace` to `AdminMenu`, `DeployPath` to `$(ProfilePath)/BepInEx/plugins/darkharasho-AdminMenu`, and add these references alongside the existing ones:

```xml
    <Reference Include="Mirror" HintPath="$(ManagedPath)/Mirror.dll" Private="false" />
    <Reference Include="UnityEngine.PhysicsModule" HintPath="$(ManagedPath)/UnityEngine.PhysicsModule.dll" Private="false" />
    <Reference Include="Unity.InputSystem" HintPath="$(ManagedPath)/Unity.InputSystem.dll" Private="false" />
```

- [ ] **Step 3: Write Plugin.cs**

```csharp
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.stonewards.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
```

- [ ] **Step 4: Refresh the reference assemblies and build**

Run: `scripts/update-refs.sh && dotnet build src/AdminMenu/AdminMenu.csproj -c Release`
Expected: build succeeds, and `Deployed AdminMenu.dll to .../plugins/darkharasho-AdminMenu`.

- [ ] **Step 5: Verify it loads in game**

Launch Stonewards through r2modman, reach the main menu, then:
Run: `grep AdminMenu ~/.config/r2modmanPlus-local/Stonewards/profiles/Default/BepInEx/LogOutput.log`
Expected: `[Info   :  AdminMenu] AdminMenu 0.1.0 loaded`

- [ ] **Step 6: Commit**

```bash
git add src scripts lib .gitignore
git commit -m "feat: scaffold AdminMenu plugin"
```

---

### Task 2: Menu state, config, and the F1 toggle

**Files:**
- Create: `src/AdminMenu/MenuGate.cs`
- Create: `tests/AdminMenu.Tests/AdminMenu.Tests.csproj`
- Create: `tests/AdminMenu.Tests/MenuGateTests.cs`
- Modify: `src/AdminMenu/Plugin.cs`

**Interfaces:**
- Consumes: `Plugin.Log`, `Hotkey.WasPressed`.
- Produces:
  - `Plugin.Enabled` (`ConfigEntry<bool>`), `Plugin.ToggleKey` (`ConfigEntry<KeyboardShortcut>`), `Plugin.CloseOnEscape` (`ConfigEntry<bool>`), `Plugin.RequireHost` (`ConfigEntry<bool>`).
  - `internal enum MenuInputState { None, Gameplay, Inventory, Dead, Menu, Other }`
  - `internal static class MenuGate` with `public static bool CanToggle(bool pluginEnabled, bool inGame, MenuInputState state, bool menuOpen)`.

- [ ] **Step 1: Write the failing test**

`tests/AdminMenu.Tests/MenuGateTests.cs`:

```csharp
using AdminMenu;
using Xunit;

public class MenuGateTests
{
    [Fact]
    public void OpensDuringGameplay()
    {
        Assert.True(MenuGate.CanToggle(pluginEnabled: true, inGame: true, MenuInputState.Gameplay, menuOpen: false));
    }

    [Fact]
    public void OpensWhileDeadSoYouCanReviveYourself()
    {
        Assert.True(MenuGate.CanToggle(pluginEnabled: true, inGame: true, MenuInputState.Dead, menuOpen: false));
    }

    [Fact]
    public void DoesNotOpenInTheMainMenuOrOtherGameMenus()
    {
        Assert.False(MenuGate.CanToggle(pluginEnabled: true, inGame: false, MenuInputState.None, menuOpen: false));
        Assert.False(MenuGate.CanToggle(pluginEnabled: true, inGame: true, MenuInputState.Other, menuOpen: false));
    }

    [Fact]
    public void DoesNotOpenWhenDisabled()
    {
        Assert.False(MenuGate.CanToggle(pluginEnabled: false, inGame: true, MenuInputState.Gameplay, menuOpen: false));
    }

    [Fact]
    public void AlwaysClosesWhenOpenEvenIfTheStateTurnedUnsupported()
    {
        Assert.True(MenuGate.CanToggle(pluginEnabled: false, inGame: false, MenuInputState.Other, menuOpen: true));
    }
}
```

- [ ] **Step 2: Create the test project and run it to see it fail**

`tests/AdminMenu.Tests/AdminMenu.Tests.csproj` — copy `../stonewards-clear-battlefield/tests/ClearBattlefield.Tests/ClearBattlefield.Tests.csproj` and replace the `<Compile Include>` block with:

```xml
  <ItemGroup>
    <Compile Include="../../src/AdminMenu/MenuGate.cs" Link="MenuGate.cs" />
  </ItemGroup>
```

Run: `dotnet test tests/AdminMenu.Tests --maxWorkers=1`
Expected: FAIL — `MenuGate` does not exist.

- [ ] **Step 3: Write MenuGate.cs**

```csharp
namespace AdminMenu
{
    /// <summary>The game's input states, reduced to the ones the menu cares about.</summary>
    internal enum MenuInputState
    {
        None,
        Gameplay,
        Inventory,
        Dead,
        Menu,
        Other,
    }

    /// <summary>
    /// When the toggle key may open or close the menu. Closing is always allowed: a scene change or a
    /// death can move the game into a state the menu would not have opened in, and it must not get stuck.
    /// </summary>
    internal static class MenuGate
    {
        public static bool CanToggle(bool pluginEnabled, bool inGame, MenuInputState state, bool menuOpen)
        {
            if (menuOpen)
                return true;
            if (!pluginEnabled || !inGame)
                return false;
            return state == MenuInputState.Gameplay || state == MenuInputState.Inventory || state == MenuInputState.Dead;
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AdminMenu.Tests --maxWorkers=1`
Expected: PASS, 5 tests.

- [ ] **Step 5: Bind the config in Plugin.Awake**

Insert before `_harmony = new Harmony(PluginGuid);`:

```csharp
            // Display names and order are for the in-game ModSettings menu; they don't change the .cfg.
            Enabled = Config.Bind("General", "Enabled", true, new ConfigDescription(
                "Allow the admin menu to be opened.", null,
                new ConfigurationManagerAttributes { DispName = "Enable admin menu", Order = 30 }));
            ToggleKey = Config.Bind("Controls", "ToggleKey", new KeyboardShortcut(KeyCode.F1), new ConfigDescription(
                "Key that opens and closes the admin menu.", null,
                new ConfigurationManagerAttributes { DispName = "Open / close admin menu", Order = 20 }));
            CloseOnEscape = Config.Bind("Controls", "CloseOnEscape", true, new ConfigDescription(
                "Also close the admin menu with Escape, instead of opening the pause menu.", null,
                new ConfigurationManagerAttributes { DispName = "Close with Escape", Order = 10 }));
            RequireHost = Config.Bind("General", "RequireHost", false, new ConfigDescription(
                "Hide the player and item actions unless you are the host. The game accepts them from any client; turn this on if you only want to use them in your own games.", null,
                new ConfigurationManagerAttributes { DispName = "Host-only actions", Order = 20 }));
```

with the matching `internal static ConfigEntry<...>` fields, and `using BepInEx.Configuration; using UnityEngine;`.

- [ ] **Step 6: Poll the hotkey in Plugin.Update**

```csharp
        private void Update()
        {
            var state = ReadInputState();
            var inGame = GameManager.Instance != null;
            if (Hotkey.WasPressed(ToggleKey.Value) && MenuGate.CanToggle(Enabled.Value, inGame, state, AdminMenuController.IsOpen))
                AdminMenuController.Toggle();
        }

        /// <summary>Maps the game's input state onto the handful of cases the menu gate distinguishes.</summary>
        private static MenuInputState ReadInputState()
        {
            if (InputManager.Instance == null)
                return MenuInputState.None;
            switch (InputManager.Instance.CurrentInputState)
            {
                case InputManager.InputState.Gameplay: return MenuInputState.Gameplay;
                case InputManager.InputState.Inventory: return MenuInputState.Inventory;
                case InputManager.InputState.Dead: return MenuInputState.Dead;
                case InputManager.InputState.GeneralMenu: return MenuInputState.Menu;
                case InputManager.InputState.None: return MenuInputState.None;
                default: return MenuInputState.Other;
            }
        }
```

`AdminMenuController` arrives in Task 3; until then, add a temporary stub in `Plugin.cs` with `internal static bool IsOpen` and a `Toggle()` that logs — it is replaced, not extended, in Task 3.

- [ ] **Step 7: Build, verify the hotkey fires**

Run: `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`, launch a game, press F1.
Expected: the stub's log line appears in `LogOutput.log` in gameplay, and does not appear while in the pause menu.

- [ ] **Step 8: Commit**

```bash
git add src tests
git commit -m "feat: toggle the admin menu with a configurable hotkey"
```

---

### Task 3: Theme and the menu shell

**Files:**
- Create: `src/AdminMenu/Theme.cs`
- Create: `src/AdminMenu/AdminMenuPanel.cs`
- Create: `src/AdminMenu/AdminMenuController.cs`
- Modify: `src/AdminMenu/Plugin.cs` (drop the stub)

**Interfaces:**
- Consumes: `Plugin.Log`, `Plugin.CloseOnEscape`.
- Produces:
  - `Theme` — copied from ModSettings, so `Theme.Window/Surface/Control/ControlHover/Field/Line/Gold/Text/Muted`, `StyleButton(Button, bool outlined = true)`, `StyleControls(VisualElement)`, `SetBorder(VisualElement, float, Color, float)`, `SetPadding(VisualElement, float, float)`, `SetHover(VisualElement, Color, Color)`.
  - `internal sealed class AdminMenuPanel` with `public VisualElement Root { get; }`, `public bool IsOpen { get; }`, `public event Action CloseRequested`, `public void Open()`, `public void Close()`, `public void AddTab(string title, VisualElement content, Action onShown)`.
  - `internal static class AdminMenuController` with `public static bool IsOpen`, `public static void Toggle()`, `public static void Close()`.

- [ ] **Step 1: Copy the theme**

```bash
cp ../stonewards-mod-settings/src/ModSettings/Theme.cs src/AdminMenu/Theme.cs
sed -i 's/namespace ModSettings/namespace AdminMenu/' src/AdminMenu/Theme.cs
```

- [ ] **Step 2: Write AdminMenuPanel.cs**

A full-screen overlay, built the same way as `ModSettingsPanel`: absolute position filling the panel, `backgroundColor = new Color(0f, 0f, 0f, 0.7f)`, a centred window at `width = Length.Percent(72f)`, `height = Length.Percent(78f)`, `maxWidth = 1200f`, `Theme.Window` background and a 3px `Theme.WindowBorder`. Header row: a bold 34px `Label("ADMIN MENU")` on the left, a `Button("Close")` on the right styled with `Theme.StyleButton`. Below the header, a left rail of tab buttons (`width = 180f`) and a content area that fills the rest.

`AddTab` appends a `Button` to the rail, styled with `Theme.StyleButton(button, outlined: false)`, and hides every tab's content except the selected one; the selected tab's button gets `Theme.Gold` border via `Theme.SetBorder(button, 2f, Theme.Gold, 4f)` and the others `Theme.Line`. Selecting a tab calls its `onShown` so tabs can refresh their data when they come into view.

The close button and, when `Plugin.CloseOnEscape.Value`, a `KeyDownEvent` handler for `KeyCode.Escape` on the overlay both raise `CloseRequested`. Register the key handler with `TrickleDown.TrickleDown` and call `evt.StopPropagation()` so the game does not also open the pause menu.

Call `Theme.StyleControls(_overlay)` once at the end of the constructor and again whenever a tab adds controls.

- [ ] **Step 3: Write AdminMenuController.cs**

```csharp
using System;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Owns the single panel instance and attaches it to the game's HUD document. The HUD document is
    /// recreated on scene changes, so the attachment is rechecked every time the menu opens.
    /// </summary>
    internal static class AdminMenuController
    {
        private static AdminMenuPanel _panel;
        private static Hud _attachedHud;
        private static InputManager.InputState _stateBeforeOpen;

        public static bool IsOpen => _panel != null && _panel.IsOpen;

        public static void Toggle()
        {
            if (IsOpen)
                Close();
            else
                Open();
        }

        private static void Open()
        {
            if (!Attach())
                return;

            if (InputManager.Instance != null)
            {
                _stateBeforeOpen = InputManager.Instance.CurrentInputState;
                // GeneralMenu is the state the game's own full-screen menus use: player input off, cursor free.
                InputManager.Instance.SetState(InputManager.InputState.GeneralMenu);
            }
            _panel.Open();
            _panel.Root.Focus();
        }

        public static void Close()
        {
            if (_panel == null || !_panel.IsOpen)
                return;
            _panel.Close();
            if (InputManager.Instance != null)
                InputManager.Instance.SetState(_stateBeforeOpen);
        }
    }
}
```

`Attach()` returns false when `Hud.Instance?.hudDocument?.rootVisualElement` is null. Otherwise it creates the panel on first use (wiring `CloseRequested += Close` and calling the tab builders from Tasks 4–6), and re-adds `_panel.Root` to the HUD root whenever `_panel.Root.panel == null` or the `Hud` instance changed — the same staleness check `HudCounter.Update` uses. Set `_panel.Root.style.position = Position.Absolute` and `BringToFront()` after adding so it draws over the HUD.

- [ ] **Step 4: Replace the stub in Plugin.cs and build**

Delete the temporary `AdminMenuController` stub. Run: `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`
Expected: build succeeds.

- [ ] **Step 5: Verify in game**

Launch a game, press F1.
Expected: the overlay appears over the HUD with a visible cursor, the camera stops following the mouse, Escape and the Close button both close it, and the game returns to normal control afterwards.

- [ ] **Step 6: Commit**

```bash
git add src
git commit -m "feat: add the admin menu overlay shell"
```

---

### Task 4: Players tab — resurrect and kill

**Files:**
- Create: `src/AdminMenu/PlayerActions.cs`
- Create: `src/AdminMenu/PlayersTab.cs`
- Modify: `src/AdminMenu/AdminMenuController.cs` (register the tab)

**Interfaces:**
- Consumes: `AdminMenuPanel.AddTab`, `Theme`, `Plugin.RequireHost`.
- Produces: `internal static class PlayerActions` with `public static List<FirstPersonController> Players()`, `public static void Kill(FirstPersonController player)`, `public static void Revive(FirstPersonController player)`, `public static void Heal(FirstPersonController player)`, `public static bool ActionsAllowed { get; }`.

- [ ] **Step 1: Write PlayerActions.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Player admin actions. Every call goes through a game Command marked
    /// <c>requiresAuthority = false</c>, so they work from the host and from a plain client alike.
    /// </summary>
    internal static class PlayerActions
    {
        /// <summary>Host-only mode is a user choice, not a technical limit; the game accepts these from any client.</summary>
        public static bool ActionsAllowed => !Plugin.RequireHost.Value || NetworkServer.active;

        public static List<FirstPersonController> Players() =>
            FirstPersonController.LocalPlayers
                .Where(p => p != null && p.PlayerStats != null)
                .OrderBy(p => p.playerName)
                .ToList();

        public static void Kill(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null || player.PlayerStats.isDead)
                return;
            player.PlayerStats.CmdDebugForceIsDead(player, true);
            Plugin.Log.LogInfo($"Killed {player.playerName}");
        }

        public static void Revive(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null || !player.PlayerStats.isDead)
                return;
            // The revive tool restores health and plays the game's own revive flow; ForceIsDead alone would
            // clear the flag but leave the player at zero health.
            player.CmdUseReviveTool(player, player.transform.position, 1f);
            Plugin.Log.LogInfo($"Revived {player.playerName}");
        }

        public static void Heal(FirstPersonController player)
        {
            if (!ActionsAllowed || player == null || player.PlayerStats == null)
                return;
            var stats = player.PlayerStats;
            stats.CmdUpdateHealth(stats.MaxHealth.Value - stats.GetCurrentHealth());
        }
    }
}
```

- [ ] **Step 2: Write PlayersTab.cs**

`public static VisualElement Build(out Action refresh)` returns a `ScrollView`. `refresh` clears it and adds one row per `PlayerActions.Players()`: a name `Label` (bold, `Theme.Text`), a health `Label` showing `$"{Mathf.CeilToInt(stats.GetCurrentHealth())} / {Mathf.CeilToInt(stats.MaxHealth.Value)}"` in `Theme.Muted` (or `"DEAD"` in a red `new Color(0.85f, 0.35f, 0.35f)` when `stats.isDead`), and `Revive`, `Heal`, `Kill` buttons styled with `Theme.StyleButton`. Disable `Revive` unless `isDead`, disable `Kill` when `isDead`, and disable every button when `!PlayerActions.ActionsAllowed`, showing a `Theme.Muted` note reading `"Host-only actions is on and you are not the host."` above the list.

After each button click, call `refresh()` so the row state updates without reopening the menu. Pass `refresh` as the tab's `onShown` callback so the list is current every time the tab is selected.

- [ ] **Step 3: Register the tab and build**

In `AdminMenuController.Attach()`, after creating the panel:

```csharp
            var players = PlayersTab.Build(out var refreshPlayers);
            _panel.AddTab("Players", players, refreshPlayers);
```

Run: `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`
Expected: build succeeds.

- [ ] **Step 4: Verify in game**

Single player: open F1, confirm your own name and health show. Click Kill — the death screen appears. Reopen F1 (allowed in the Dead state) and click Revive — you get back up at full health. Repeat as host with a second client connected and confirm both players appear and can be killed and revived.

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat: add the players tab with revive, heal and kill"
```

---

### Task 5: Items tab — searchable spawner

**Files:**
- Create: `src/AdminMenu/ItemSearch.cs`
- Create: `src/AdminMenu/ItemCatalog.cs`
- Create: `src/AdminMenu/ItemsTab.cs`
- Create: `tests/AdminMenu.Tests/ItemSearchTests.cs`
- Modify: `tests/AdminMenu.Tests/AdminMenu.Tests.csproj`, `src/AdminMenu/AdminMenuController.cs`

**Interfaces:**
- Consumes: `PlayerActions.ActionsAllowed`, `Theme`.
- Produces:
  - `internal sealed class ItemEntry { public string Id; public string Name; public string Rarity; public bool Stackable; public int MaxStack; }`
  - `internal static class ItemSearch` with `public static List<ItemEntry> Filter(IEnumerable<ItemEntry> items, string search)`.
  - `internal static class ItemCatalog` with `public static List<ItemEntry> All()`, `public static void SpawnToGround(ItemEntry item, int count)`, `public static void SpawnToInventory(ItemEntry item, int count)`.

- [ ] **Step 1: Write the failing test**

`tests/AdminMenu.Tests/ItemSearchTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using AdminMenu;
using Xunit;

public class ItemSearchTests
{
    private static readonly List<ItemEntry> Items = new List<ItemEntry>
    {
        new ItemEntry { Id = "item_iron_ore", Name = "Iron Ore", Rarity = "Common" },
        new ItemEntry { Id = "item_gold_ore", Name = "Gold Ore", Rarity = "Rare" },
        new ItemEntry { Id = "weapon_iron_sword", Name = "Iron Sword", Rarity = "Common" },
    };

    [Fact]
    public void EmptySearchReturnsEverything()
    {
        Assert.Equal(3, ItemSearch.Filter(Items, "").Count);
        Assert.Equal(3, ItemSearch.Filter(Items, null).Count);
    }

    [Fact]
    public void MatchesNameCaseInsensitively()
    {
        Assert.Equal(new[] { "item_gold_ore" }, ItemSearch.Filter(Items, "gold").Select(i => i.Id));
    }

    [Fact]
    public void MatchesTheRawItemIdSoIdsFromLogsCanBePastedIn()
    {
        Assert.Equal(new[] { "weapon_iron_sword" }, ItemSearch.Filter(Items, "weapon_iron").Select(i => i.Id));
    }

    [Fact]
    public void ExactNameMatchesSortAbovePartialOnes()
    {
        var results = ItemSearch.Filter(Items, "iron");
        Assert.Equal(new[] { "item_iron_ore", "weapon_iron_sword" }, results.Select(i => i.Id));
    }

    [Fact]
    public void TrimsTheSearchSoTrailingSpacesStillMatch()
    {
        Assert.Single(ItemSearch.Filter(Items, "  gold  "));
    }
}
```

- [ ] **Step 2: Add the files to the test project and run it**

Add to `tests/AdminMenu.Tests/AdminMenu.Tests.csproj`:

```xml
    <Compile Include="../../src/AdminMenu/ItemSearch.cs" Link="ItemSearch.cs" />
```

Run: `dotnet test tests/AdminMenu.Tests --maxWorkers=1`
Expected: FAIL — `ItemSearch` does not exist.

- [ ] **Step 3: Write ItemSearch.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminMenu
{
    /// <summary>One spawnable item, kept free of game types so the search can be tested without the game.</summary>
    internal sealed class ItemEntry
    {
        public string Id;
        public string Name;
        public string Rarity;
        public bool Stackable;
        public int MaxStack = 1;
    }

    /// <summary>
    /// Filters the item list by display name or raw ID. IDs match too, so an ID copied out of a log or
    /// another mod can be pasted straight into the box.
    /// </summary>
    internal static class ItemSearch
    {
        public static List<ItemEntry> Filter(IEnumerable<ItemEntry> items, string search)
        {
            var all = items ?? Enumerable.Empty<ItemEntry>();
            var term = (search ?? string.Empty).Trim();
            if (term.Length == 0)
                return all.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();

            return all
                .Select(item => new { item, rank = Rank(item, term) })
                .Where(x => x.rank < int.MaxValue)
                .OrderBy(x => x.rank)
                .ThenBy(x => x.item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.item)
                .ToList();
        }

        /// <summary>Lower is better: exact name, then name prefix, then anything containing the term.</summary>
        private static int Rank(ItemEntry item, string term)
        {
            if (Equals(item.Name, term))
                return 0;
            if (StartsWith(item.Name, term))
                return 1;
            if (Contains(item.Name, term))
                return 2;
            if (Contains(item.Id, term))
                return 3;
            return int.MaxValue;
        }

        private static bool Equals(string text, string term) =>
            text != null && string.Equals(text, term, StringComparison.OrdinalIgnoreCase);

        private static bool StartsWith(string text, string term) =>
            text != null && text.StartsWith(term, StringComparison.OrdinalIgnoreCase);

        private static bool Contains(string text, string term) =>
            text != null && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AdminMenu.Tests --maxWorkers=1`
Expected: PASS, 10 tests.

- [ ] **Step 5: Write ItemCatalog.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// Reads the game's item database and spawns from it. The database lives on the networked
    /// <see cref="ItemManager"/>, so nothing here works before a level or the hub has loaded.
    /// </summary>
    internal static class ItemCatalog
    {
        private static readonly Dictionary<string, ItemDataSO> ById = new Dictionary<string, ItemDataSO>();

        public static List<ItemEntry> All()
        {
            ById.Clear();
            var manager = ItemManager.Instance;
            var database = manager == null ? null : Traverse.Create(manager).Field<ItemDatabaseSO>("itemDataBase").Value;
            if (database == null)
            {
                Plugin.Log.LogWarning("Item database not available yet; load into the hub or a level first");
                return new List<ItemEntry>();
            }

            var entries = new List<ItemEntry>();
            foreach (var item in database.items.Where(i => i != null && !string.IsNullOrEmpty(i.itemID)))
            {
                ById[item.itemID] = item;
                entries.Add(new ItemEntry
                {
                    Id = item.itemID,
                    Name = string.IsNullOrEmpty(item.GetLocalizedName()) ? item.itemID : item.GetLocalizedName(),
                    Rarity = item.Rarity.ToString(),
                    Stackable = item.isStackable,
                    MaxStack = Mathf.Max(1, item.maxStackSize),
                });
            }
            return entries;
        }

        public static void SpawnToInventory(ItemEntry item, int count)
        {
            var player = LocalPlayer();
            if (item == null || player == null || !PlayerActions.ActionsAllowed)
                return;
            player.CmdForcePickupItem(player, count, item.Id);
            Plugin.Log.LogInfo($"Spawned {count}x {item.Id} into the inventory");
        }

        public static void SpawnToGround(ItemEntry item, int count)
        {
            var player = LocalPlayer();
            if (item == null || player == null || ItemManager.Instance == null || !PlayerActions.ActionsAllowed)
                return;
            // A step in front of the player, at their feet, so the drop never lands inside a wall.
            var position = player.transform.position + player.transform.forward * 1.5f;
            ItemManager.Instance.CmdInstantiatePickableNewItem(item.Id, count, position);
            Plugin.Log.LogInfo($"Spawned {count}x {item.Id} on the ground");
        }

        private static FirstPersonController LocalPlayer() =>
            FirstPersonController.LocalPlayers.FirstOrDefault(p => p != null && p.isLocalPlayer);
    }
}
```

`itemDataBase` is a private `ItemDatabaseSO` field on `ItemManager`; read it with `HarmonyLib.Traverse` (add `using HarmonyLib;`). If a public accessor turns up during implementation, use it instead and delete the Traverse call.

- [ ] **Step 6: Write ItemsTab.cs**

`public static VisualElement Build(out Action refresh)`. Layout: a `TextField` search box at the top (placeholder via a `Theme.Muted` label when empty), a count `IntegerField` next to it defaulting to 1 and clamped to `[1, 999]`, a `Toggle("Spawn into inventory")` defaulting to true, and a `ScrollView` of results below.

The list rebuilds on every `TextField` `ChangeEvent<string>` by calling `ItemSearch.Filter(_all, value)` and showing at most 200 rows (with a `Theme.Muted` label `"… more results; refine the search"` when truncated — rebuilding thousands of `VisualElement`s per keystroke is what makes UI Toolkit lists stutter). Each row: a 32×32 `VisualElement` whose `style.backgroundImage` is `new StyleBackground(item.itemIcon)` when the sprite exists, the name, the rarity in `Theme.Muted`, and a `Button("Spawn")` calling `ItemCatalog.SpawnToInventory` or `SpawnToGround` depending on the toggle, clamping the count to `item.MaxStack` when `!item.Stackable`.

`refresh` re-reads `ItemCatalog.All()` into `_all` and reapplies the current filter; pass it as the tab's `onShown`.

- [ ] **Step 7: Register the tab, build, verify in game**

```csharp
            var items = ItemsTab.Build(out var refreshItems);
            _panel.AddTab("Items", items, refreshItems);
```

Run: `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`, then in game open the Items tab, type part of an item name, spawn 5 of it into the inventory, then spawn the same item to the ground and confirm it is pickable.

- [ ] **Step 8: Commit**

```bash
git add src tests
git commit -m "feat: add the item spawner tab"
```

---

### Task 6: Cheats tab — god mode, infinite stamina, super pickaxe

**Files:**
- Create: `src/AdminMenu/Cheats.cs`
- Create: `src/AdminMenu/Patches/CheatPatches.cs`
- Create: `src/AdminMenu/CheatsTab.cs`
- Modify: `src/AdminMenu/Plugin.cs` (config entries, per-frame upkeep), `src/AdminMenu/AdminMenuController.cs`

**Interfaces:**
- Consumes: `Plugin.Log`, `PlayerActions.ActionsAllowed`, `Theme`.
- Produces:
  - `Plugin.GodMode`, `Plugin.InfiniteStamina`, `Plugin.SuperPickaxe` (`ConfigEntry<bool>`), `Plugin.SuperPickaxeMultiplier` (`ConfigEntry<float>`).
  - `internal static class CheatsTab` with `public static VisualElement Build(out Action refresh)`.
  - `internal static class Cheats` with `public static void Update()`, `public static void ApplyPickaxe(FirstPersonController player)`, `public static bool GodModeActive { get; }`, `public static bool InfiniteStaminaActive { get; }`, `public const string ModifierSource = "AdminMenu.SuperPickaxe"`.

- [ ] **Step 1: Bind the cheat config entries in Plugin.Awake**

```csharp
            GodMode = Config.Bind("Cheats", "GodMode", false, new ConfigDescription(
                "Keep your health topped up and block incoming damage. As a client, damage is applied by the host, so this heals you back instead of preventing the hit.", null,
                new ConfigurationManagerAttributes { DispName = "God mode", Order = 30 }));
            InfiniteStamina = Config.Bind("Cheats", "InfiniteStamina", false, new ConfigDescription(
                "Never run out of stamina when sprinting, climbing or blocking.", null,
                new ConfigurationManagerAttributes { DispName = "Infinite stamina", Order = 20 }));
            SuperPickaxe = Config.Bind("Cheats", "SuperPickaxe", false, new ConfigDescription(
                "Multiply dig strength, digging speed and the heavy dig bonus.", null,
                new ConfigurationManagerAttributes { DispName = "Super pickaxe", Order = 10 }));
            SuperPickaxeMultiplier = Config.Bind("Cheats", "SuperPickaxeMultiplier", 5f, new ConfigDescription(
                "How much stronger the super pickaxe is. 1 is the unmodified game.",
                new AcceptableValueRange<float>(1f, 25f),
                new ConfigurationManagerAttributes { DispName = "Super pickaxe strength", Order = 5 }));
```

These are ordinary config entries, so the toggles persist across sessions and also show up in darkharasho-ModSettings.

- [ ] **Step 2: Write Cheats.cs**

```csharp
using System.Linq;
using UnityEngine;

namespace AdminMenu
{
    /// <summary>
    /// The cheat toggles. God mode tops health back up through a game Command every frame it drops; the
    /// stamina and pickaxe cheats are local, because the game tracks both on the owning client.
    /// </summary>
    internal static class Cheats
    {
        public const string ModifierSource = "AdminMenu.SuperPickaxe";

        private static bool _pickaxeApplied;
        private static float _appliedMultiplier;

        public static bool GodModeActive => Plugin.GodMode.Value && PlayerActions.ActionsAllowed;
        public static bool InfiniteStaminaActive => Plugin.InfiniteStamina.Value;

        /// <summary>Called every frame from <see cref="Plugin.Update"/>.</summary>
        public static void Update()
        {
            var player = FirstPersonController.LocalPlayers.FirstOrDefault(p => p != null && p.isLocalPlayer);
            if (player == null || player.PlayerStats == null)
            {
                _pickaxeApplied = false;
                return;
            }

            var stats = player.PlayerStats;
            if (GodModeActive && !stats.isDead && stats.GetCurrentHealth() < stats.MaxHealth.Value)
                stats.CmdUpdateHealth(stats.MaxHealth.Value - stats.GetCurrentHealth());

            ApplyPickaxe(player);
        }

        /// <summary>
        /// Adds or removes the dig modifiers. The stats are rebuilt when the archetype changes, so the
        /// multiplier is reapplied whenever it is missing rather than only on toggle.
        /// </summary>
        public static void ApplyPickaxe(FirstPersonController player)
        {
            var stats = player.PlayerStats;
            var wanted = Plugin.SuperPickaxe.Value;
            var multiplier = Mathf.Max(1f, Plugin.SuperPickaxeMultiplier.Value);

            if (wanted && (!_pickaxeApplied || !Mathf.Approximately(multiplier, _appliedMultiplier)))
            {
                var mod = new StatModifier(multiplier, StatModType.PERCENT_MULT, ModifierSource);
                stats.DigStrength.AddOrReplaceModifier(mod);
                stats.DiggingSpeed.AddOrReplaceModifier(mod);
                stats.HeavyDigMultiplier.AddOrReplaceModifier(mod);
                _pickaxeApplied = true;
                _appliedMultiplier = multiplier;
                Plugin.Log.LogInfo($"Super pickaxe on at x{multiplier}");
            }
            else if (!wanted && _pickaxeApplied)
            {
                stats.DigStrength.RemoveAllModifiersFromSource(ModifierSource);
                stats.DiggingSpeed.RemoveAllModifiersFromSource(ModifierSource);
                stats.HeavyDigMultiplier.RemoveAllModifiersFromSource(ModifierSource);
                _pickaxeApplied = false;
                Plugin.Log.LogInfo("Super pickaxe off");
            }
        }
    }
}
```

`PERCENT_MULT` semantics must be confirmed against `CharacterStat.CalculateFinalValue` during implementation: if it treats the value as a factor, pass `multiplier`; if it treats it as a percentage to add, pass `multiplier - 1f`. Verify with a log line printing `stats.DigStrength.Value` before and after.

- [ ] **Step 3: Write Patches/CheatPatches.cs**

```csharp
using HarmonyLib;
using UnityEngine;

namespace AdminMenu.Patches
{
    /// <summary>Stamina is tracked on the owning client, so a local patch is enough to keep the bar full.</summary>
    [HarmonyPatch(typeof(PlayerStats))]
    internal static class StaminaPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("ConsumeStamina")]
        private static bool SkipConsumption(PlayerStats __instance)
        {
            return !(Cheats.InfiniteStaminaActive && IsLocal(__instance));
        }

        [HarmonyPostfix]
        [HarmonyPatch("HandleStamina")]
        private static void RefillAfterRegen(PlayerStats __instance)
        {
            if (!Cheats.InfiniteStaminaActive || !IsLocal(__instance))
                return;
            // Sprinting and climbing drain the field directly instead of calling ConsumeStamina.
            Traverse.Create(__instance).Field<float>("currentStamina").Value = __instance.MaxStamina.Value;
        }

        private static bool IsLocal(PlayerStats stats) =>
            stats != null && stats.Player != null && stats.Player.isLocalPlayer;
    }

    /// <summary>
    /// Damage is decided on the server, so this only bites when you are the host. As a client, god mode
    /// falls back to the heal-up in <see cref="Cheats.Update"/>.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.CanTakeDamage))]
    internal static class CanTakeDamagePatch
    {
        private static void Postfix(PlayerStats __instance, ref bool __result)
        {
            if (Cheats.GodModeActive && __instance != null && __instance.Player != null && __instance.Player.isLocalPlayer)
                __result = false;
        }
    }
}
```

- [ ] **Step 4: Call Cheats.Update from Plugin.Update**

Add `Cheats.Update();` at the end of `Plugin.Update`, guarded by `if (Enabled.Value)`.

- [ ] **Step 5: Write CheatsTab.cs**

A column of rows, one per cheat: a bold `Label` with the cheat's name, a `Theme.Muted` description `Label` (reuse the config `ConfigDescription` text, wrapped with `style.whiteSpace = WhiteSpace.Normal`), and a `Toggle` bound to the config entry — `toggle.value = entry.Value` on build and on `onShown`, `toggle.RegisterValueChangedCallback(evt => entry.Value = evt.newValue)`. Add a `Slider` for `SuperPickaxeMultiplier` with `lowValue = 1f`, `highValue = 25f`, shown only while `SuperPickaxe` is on, and a live value `Label`. Call `Theme.StyleControls` on the tab root after building.

- [ ] **Step 6: Register the tab, build, verify in game**

```csharp
            var cheats = CheatsTab.Build(out var refreshCheats);
            _panel.AddTab("Cheats", cheats, refreshCheats);
```

Run: `dotnet build src/AdminMenu/AdminMenu.csproj -c Release`. In game: sprint with infinite stamina on and confirm the bar never drains; take enemy damage with god mode on and confirm health returns to full; mine a block with super pickaxe on and off and compare how long it takes. Then confirm turning super pickaxe off restores the original dig speed (the modifier was removed, not stacked).

- [ ] **Step 7: Commit**

```bash
git add src
git commit -m "feat: add god mode, infinite stamina and super pickaxe"
```

---

### Task 7: Packaging and release

**Files:**
- Create: `thunderstore/manifest.json`, `thunderstore/icon.png`, `CHANGELOG.md`, `scripts/package.sh`, `scripts/thunderstore-publish.sh`, `.github/workflows/release.yml`
- Modify: `README.md`

**Interfaces:**
- Consumes: the built `AdminMenu.dll`.
- Produces: `dist/darkharasho-AdminMenu-<version>.zip` containing `manifest.json`, `icon.png`, `README.md` and `AdminMenu.dll`.

- [ ] **Step 1: Copy the release scripts and workflow**

```bash
cp ../stonewards-mod-settings/scripts/package.sh scripts/package.sh
cp ~/.claude/templates/thunderstore/thunderstore-publish.sh scripts/thunderstore-publish.sh
cp ../stonewards-mod-settings/.github/workflows/release.yml .github/workflows/release.yml
chmod +x scripts/*.sh
sed -i 's/ModSettings/AdminMenu/g' scripts/package.sh .github/workflows/release.yml
```

Read both files afterwards and fix anything the blunt `sed` got wrong — in particular the release-notes footer text and the `--community stonewards` argument, which must stay as it is.

- [ ] **Step 2: Write thunderstore/manifest.json**

```json
{
    "name": "AdminMenu",
    "version_number": "0.1.0",
    "website_url": "https://github.com/darkharasho/stonewards-admin-menu",
    "description": "In-game admin menu on F1: revive or kill players, spawn any item, god mode, infinite stamina and a super pickaxe.",
    "dependencies": [
        "BepInEx-BepInExPack-5.4.2305"
    ]
}
```

- [ ] **Step 3: Add the icon and changelog**

`thunderstore/icon.png` must be exactly 256×256. Verify with:
Run: `python3 -c "import struct;d=open('thunderstore/icon.png','rb').read();print(struct.unpack('>II', d[16:24]))"`
Expected: `(256, 256)`

`CHANGELOG.md`:

```markdown
# Changelog

## 0.1.0

- First release: admin menu on F1 with player revive/kill, an item spawner, god mode, infinite stamina and a super pickaxe.
```

- [ ] **Step 4: Package locally and inspect the zip**

Run: `scripts/package.sh && unzip -l dist/darkharasho-AdminMenu-0.1.0.zip`
Expected: `manifest.json`, `icon.png`, `README.md` and `AdminMenu.dll` at the zip root.

- [ ] **Step 5: Expand README.md**

Add sections matching the other mods' READMEs: what it does, the feature list, the default F1 binding and how to rebind it (config file path `BepInEx/config/com.darkharasho.stonewards.adminmenu.cfg`, or darkharasho-ModSettings in game), a multiplayer note, and a build-from-source section pointing at `Local.props` and `scripts/update-refs.sh`.

- [ ] **Step 6: Set the Thunderstore secret**

```bash
( eval "$(grep '^export THUNDERSTORE_ACCESS_TOKEN=' ~/.bashrc)"; printf %s "$THUNDERSTORE_ACCESS_TOKEN" | gh secret set THUNDERSTORE_TOKEN -R darkharasho/stonewards-admin-menu )
```

Run: `gh secret list -R darkharasho/stonewards-admin-menu`
Expected: `THUNDERSTORE_TOKEN` is listed.

- [ ] **Step 7: Commit**

```bash
git add thunderstore scripts .github README.md CHANGELOG.md
git commit -m "chore: add Thunderstore packaging and the release workflow"
```

Do not push a `v0.1.0` tag until the author confirms — Thunderstore never allows replacing a published version. The first upload also needs `--categories` passed to `scripts/thunderstore-publish.sh`; pick the categories on the Thunderstore side or add the flag for the first run only.
