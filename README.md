1) Big-picture architecture (layers)
A. Unity scene bootstrap layer
This is “what exists in the scene” and holds references to assets:

Assets/Scripts/Helpers/ScreenSwapper.cs

A MonoBehaviour that owns the UI root and the navigation system:
Screens: one active at a time (e.g., Main Menu, Tower, Character Creation).
Overlays: can stack (e.g., Load Game overlay).
Global modal: confirmation dialogs, etc.
Tooltip system: UI helper.
Depends on:
UnityEngine.UIElements (UIDocument, VisualElement, VisualTreeAsset)
Your controller interfaces:
Assets/Scripts/Helpers/IScreenController.cs
(and similarly overlays/modals via IOverlayController / IModalController—I see IModalController.cs and an overlay controller implemented, so the overlay interface exists even if not shown in the search snippets)
Assets/Scripts/Run/GameConfigProvider.cs (MyGame.Run)

A singleton MonoBehaviour that exposes central ScriptableObject databases/configs to runtime:
SpellDatabase, EffectDatabase, SpellProgressionConfig, StartingSpellConfig
PlayerIconDatabase, PlayerClassDatabase
This is your “service locator for configs”.
B. UI layout assets (UXML/USS) + their controllers
You use UI Toolkit patterns:

UXML describes a screen/overlay layout
Controllers bind to named elements in the instantiated UXML and attach logic
Examples:

App root

Assets/UI Toolkit/AppRoot/AppRoot.uxml
Assets/UI Toolkit/AppRoot/appRoot.uss
Defines:
screen-host (where screens clone into)
overlay-host (where overlays clone into)
modal-host
tooltip-host
autosave element
Main Menu screen

Assets/UI Toolkit/MainMenu/MainMenu.uxml
Controller: Assets/Scripts/Controllers/Menu/MainMenuController.cs
Binds to buttons by name: NewGame, LoadGame, Settings, ExitGame
Calls ScreenSwapper navigation:
ShowScreen("char_creation")
ShowOverlay("load_game")
ShowGlobalModal(...)
Load Game overlay

Assets/UI Toolkit/MainMenu/LoadGame.uxml
Controller: Assets/Scripts/Save/LoadGameController.cs (implements overlay controller)
Loads a save slot, then:
persists it via SaveService
sets it into SaveSession
initializes runtime systems via RunSession.InitializeFromSave(...)
then navigates into the dashboard/main gameplay screen
Tower screens

Example UXML: Assets/UI Toolkit/Tower/InsideTower.uxml
Styles: Assets/UI Toolkit/Tower/tower.uss, Assets/UI Toolkit/Tower/FloorInfoCard.uss
Controller: Assets/Scripts/Controllers/Tower/TowerController.cs
Uses tower ids and runtime tower progress to enable/disable UI (“locked towers”)
Global modal

Controller: Assets/UI Toolkit/Components/Modal/GlobalModalController.cs
Used by ScreenSwapper.ShowGlobalModal(...)
C. Runtime session state (in-memory game state)
This is your “currently running playthrough”, separate from disk save:

Assets/Scripts/Run/RunSession.cs (MyGame.Run)
Holds runtime objects like:
Spellbook (player spells runtime)
Towers (tower progress runtime)
Bridges save-data ↔ runtime:
InitializeFromSave(save, db, progression) uses:
SpellSaveMapper.LoadFromSave(...)
TowerSaveMapper.LoadFromSave(...)
ApplyRuntimeToSave(save) uses:
SpellSaveMapper.WriteToSave(...)
TowerSaveMapper.WriteToSave(...)
D. Save system (disk persistence)
This is the JSON-on-disk system plus an in-memory “current save”:

Assets/Scripts/Save/SaveService.cs (MyGame.Save)

Reads/writes SaveData JSON in Application.persistentDataPath
Handles save migrations via data.version upgrades (v2 → v10 etc.)
Also ensures defaults (e.g., towers list exists and has all towers)
Assets/Scripts/Save/SaveSession.cs (MyGame.Save)

Global static “current loaded save + slot”
SaveNow() delegates to SaveService.SaveToSlot(...)
E. Domain/gameplay logic (combat, spells, towers)
This is where gameplay rules live, mostly independent of UI:

Combat

Assets/Scripts/Combat/ActualFight/CombatEngine.cs (MyGame.Combat)
Emits events like HpChangedEvent, ManaChangedEvent, CombatLogEvent
Assets/Scripts/Combat/ActualFight/CombatOrchestration/CombatSessionCoordinator.cs
Coordinates engine + UI sinks + log sinks
Assets/Scripts/Combat/ActualFight/RngPipeline/UnityRng.cs
RNG abstraction implementation using UnityEngine.Random
Spells

Assets/Scripts/Spells/SpellSaveMapper.cs (MyGame.Spells)
Converts between SaveData.spells and runtime PlayerSpellbook
Databases/config are provided by GameConfigProvider
Towers

Assets/Scripts/Tower/TowerSaveMapper.cs (MyGame.Towers)
Converts between SaveData.towers and runtime TowerRunProgress
2) “Which file works with what file” (key interconnections)
Navigation + UI composition
ScreenSwapper.cs
clones AppRoot.uxml (root contains screen-host, overlay-host, modal-host, tooltip-host)
for each screen/overlay entry, clones a VisualTreeAsset (UXML) into the correct host
then calls controller Bind(...):
screen controller: IScreenController.Bind(screenHost, swapper, context)
overlay controller: IOverlayController.Bind(overlayHost, swapper, context) (interface not shown but implied by LoadGameController : ... IOverlayController)
modal controller: IModalController.Bind(modalRoot, swapper)
So: UXML → (ScreenSwapper clones) → Controller.Bind() attaches behavior.

Main menu flow → Load game → runtime init
MainMenuController.cs calls:
ScreenSwapper.ShowOverlay("load_game")
LoadGameController.cs (overlay) calls:
SaveService.LoadSlotOrNull(...) (to read disk)
SaveService.SaveToSlot(...) (when saving new or updating)
SaveSession.SetCurrent(slot, data) (sets active save)
RunSession.InitializeFromSave(data, GameConfigProvider.Instance.SpellDatabase, GameConfigProvider.Instance.SpellProgression) (runtime state)
So: UI menu triggers overlay → overlay reads SaveService → sets SaveSession → initializes RunSession.

Save mappers connect domain runtime ↔ disk save
RunSession.InitializeFromSave(...) uses:
SpellSaveMapper.LoadFromSave(save, db, progression)
TowerSaveMapper.LoadFromSave(save)
RunSession.ApplyRuntimeToSave(save) uses:
SpellSaveMapper.WriteToSave(book, save)
TowerSaveMapper.WriteToSave(run, save)
So: SaveData (disk model) ⇄ Mappers ⇄ runtime models (Spellbook, TowerRunProgress).