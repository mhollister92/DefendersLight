﻿/*
 * Author(s): Isaiah Mann
 * Description: Controls the set up and behaviour of the game world
 */
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class WorldController : MannBehaviour, IWorldController, IObjectPool<GameObject> {
	public static WorldController Instance;
	bool isPaused;
	public GameObject TowerPrefab;
	public GameObject AssaulTowerPrefab;
	public GameObject BarricadeTowerPrefab;
	public GameObject IlluminationTowerPrefab;
	public GameObject EnemyPrefab;
	public GameObject ITowerPrefab {
		get {
			return TowerPrefab;
		}
	}
	public GameObject ICoreOrbInstance {
		get {
			return towerController.CoreOrbInstance;
		}
	}
	public bool HasTowerToPlace {
		get {
			return currentlySelectedPurchaseTower != null;
		}
	}
	Tower currentlySelectedPurchaseTower = null;
	Dictionary<string, Stack<ActiveObjectBehaviour>> spawnPools = new Dictionary<string, Stack<ActiveObjectBehaviour>>();
	SeasonList seasons;
	Season _currentSeason;
	Season currentSeason {
		get {
			return _currentSeason;
		}
		set {
			_currentSeason = value;
		}
	}

	public bool IsPaused {
		get {
			return Time.timeScale == 0;
		}
	}

	public LinearEquation EnemySpawnCountEquation;
	public bool OverrideTowerLevelRequirement {get; private set;}

	Dictionary<string, Unit[]> _unitsByClass;
	public Dictionary<string, Unit[]> UnitsByClass {
		get {
			if (_unitsByClass == null) {
				_unitsByClass = determineUnitsByClass();
			}
			return _unitsByClass;
		}
	}

	Dictionary<string, Unit[]> determineUnitsByClass () {
		Dictionary<string, Unit[]> unitsByClass = new Dictionary<string, Unit[]>();
		foreach (TowerType towerClass in towerController.ITowerTemplatesByType.Keys) {
			unitsByClass.Add(towerClass.ToString(), towerController.ITowerTemplatesByType[towerClass].ToArray());
		}
		unitsByClass.Add(EnemyController.ENEMY_TAG, enemyController.ITemplateUnits);
		return unitsByClass;
	}


	public void UnlockAllTowers () {
		OverrideTowerLevelRequirement = true;
	}

	public void AddToSpawnPool (ActiveObjectBehaviour activeObject) {
		Stack<ActiveObjectBehaviour> pool;
		if (spawnPools.TryGetValue(activeObject.IType, out pool)) {
			pool.Push(activeObject);
		} else {
			pool = new Stack<ActiveObjectBehaviour>();
			pool.Push(activeObject);
			spawnPools.Add(activeObject.IType, pool);
		}
	}

	public bool TryPullFromSpawnPool (string objectType, out ActiveObjectBehaviour activeObject) {
		Stack<ActiveObjectBehaviour> pool;
		if (spawnPools.TryGetValue(objectType, out pool)) {
			if (pool.Count > 0) {
				activeObject = pool.Pop();
				return true;
			} else {
				activeObject = null;
				return false;
			}
		} else {
			activeObject = null;
			return false;
		}
	}

	const string TOWER_UNIT_TEMPLATE_FILE_NAME = "TowerTemplates";
	const string ENEMY_UNIT_TEMPLATE_FILE_NAME = "EnemyTemplates";
	const string SEASONS_DATA_FILE_NAME = "Seasons";

	Dictionary<System.Type, Stack<GameObject>> unitSpawnpool = new Dictionary<System.Type, Stack<GameObject>>();
	TowerController towerController;
	EnemyController enemyController;
	MapController mapController;
	UnitController[] unitControllers;
	DataController dataController;
	StatsPanelController statsPanel;
	TowerPurchasePanelController purchasePanel;
	InputController input;

	int spawnPoints = 1;

	public void Create() {
		createRules();
		SetupUnitControllers();
		PlaceCoreOrb();
		setupUnitControllerCallbacks();
	}

	void createRules () {
		seasons = JsonUtility.FromJson<SeasonList>(dataController.RetrieveJSONFromResources(SEASONS_DATA_FILE_NAME));
		currentSeason = seasons[0];
	}

	void checkForSeasonAdvance (int waveIndex) {
		if (waveIndex > currentSeason.EndingWave) {
			changeSeason(currentSeason.Index + 1);
			increaseSpawnPoints();
		} else if (waveIndex > currentSeason.MiddleWave) {
			// TODO: Implement behaviour if the wave has passed the midway point (increased spawn points)
			increaseSpawnPoints();
		}
	}

	void setupUnitControllerCallbacks () {
		if (enemyController) {
			enemyController.SubscribeToWaveAdvance(checkForSeasonAdvance);
		}
	}

	void teardownUnitControllerCallbacks () {
		if (enemyController) {
			enemyController.UnusubscribeFromWaveAdvance(checkForSeasonAdvance);
		}
	}

	void setupDataControllerCallbacks () {
		if (dataController) {
			dataController.SubscribeToOnLevelUp(onLevelUp);
			dataController.SubscribeToOnXPEarned(onEarnXP);
		}
	}

	void teardownDataControllerCallbacks () {
		if (dataController) {
			dataController.UnsubscribeFromOnLevelUp(onLevelUp);
			dataController.UnsubscribeFromOnXPEarned(onEarnXP);
		}
	}

	void onLevelUp (int newLevel) {
		statsPanel.SetLevel(newLevel);
		onEarnXP(0);
	}

	void onEarnXP (int xpEarned) {
		statsPanel.SetXP(dataController.XP, dataController.XPForLevel);
	}

	void changeSeason (int newSeasonIndex) {
		currentSeason = seasons[newSeasonIndex];
	}

	void increaseSpawnPoints () {
		spawnPoints++;
	}

	int getEnemySpawnPointCount () {
		int spawnPoints = 1;
		int currentWave = enemyController.ICurrentWaveIndex;
		for (int i = 0; i < seasons.Length; i++) {
			if (currentWave < seasons[i].StartingWave) {
				break;
			}
			if (currentWave > seasons[i].MiddleWave) {
				spawnPoints++;
			}
			if (currentWave > seasons[i].EndingWave) {
				spawnPoints++;
			}
		}
		return spawnPoints;
	}
		
	public void StartWave () {
		dataController.NextWave();
		enemyController.SetSpawnPointCount(getEnemySpawnPointCount());
		enemyController.SpawnWave();
	}
		
	public void CollectMana (int count) {
		dataController.CollectMana(count);
		statsPanel.SetMana(dataController.Mana);
	}

	public void EarnXP (int xpEarned) {
		dataController.EarnXP(xpEarned);
	}

	public bool TrySpendMana (int count) {
		if (dataController.TrySpendMana(count)) {
			statsPanel.SetMana(dataController.Mana); 
			return true;
		} else {
			return false;
		}
	}

	public void TogglePause () {
		if (isPaused) {
			Resume();
		} else {
			Pause();
		}
	}

	public void Pause () {
		Time.timeScale = 0;
		isPaused = true;
	}

	public void Resume () {
		Time.timeScale = 1;
		isPaused = false;
	}

	void PlaceCoreOrb () {
		towerController.PlaceCoreOrb(mapController.GetCenterTile());
	}

	void SetupUnitControllers () {
		towerController.Setup(this, dataController, mapController, TOWER_UNIT_TEMPLATE_FILE_NAME);
		enemyController.Setup(this, dataController, mapController, ENEMY_UNIT_TEMPLATE_FILE_NAME, EnemySpawnCountEquation);
		unitControllers = new UnitController[]{towerController, enemyController};
	}

	public void RefreshIlluminations () {
		towerController.RefreshIlluminations();
	}

	public void SendIlluminationToMap (IlluminationTowerBehaviour illuminationTower, bool shouldPlaySound = true) {
		mapController.Illuminate(illuminationTower.GetLocation(), illuminationTower.IlluminationRadius, shouldPlaySound);
	}

	// Cleans up/destroys the world
	public void Teardown() {
		throw new System.NotImplementedException();
	}
		
	public void AddActiveTower (TowerBehaviour tower) {
		towerController.AddActiveTower(tower);
	}

	public void RemoveActiveTower (TowerBehaviour tower) {
		towerController.RemoveActiveTower(tower);
	}

    public void RemoveActiveEnemy (EnemyBehaviour enemy) {
        enemyController.RemoveActiveEnemy(enemy);
    }

    public void HealAllTowers (){
		towerController.HealAllTowers ();
	}

    public void KillAllEnemies() {
        enemyController.KillAllEnemies();
    }

    public void DestroyAllTowers() {
        towerController.DestroyAllTowers();
    }

    public void ToggleGodMode() {
        towerController.ToggleGodMode();
    }

    public void setWave(int waveIndex) {
        enemyController.setWave(waveIndex);
    }
		
	public void AddObject(IWorldObject element) {
		throw new System.NotImplementedException();
	}

	public void RemoveObject(IWorldObject element) {
		throw new System.NotImplementedException();
	}

	public IWorldObject GetObject(string id) {
		throw new System.NotImplementedException();
	}
		
	public void HandleBeginDragPurchase (PointerEventData dragEvent, TowerPurchasePanel towerPanel) {
		input.ToggleDraggingObject(true);
		if (dragEvent != null) {
			towerController.HandleBeginDragPurchase(dragEvent, towerPanel);
			mapController.HighlightValidBuildTiles();
		}
	}

	public void HandleDragPurchase (PointerEventData dragEvent, TowerPurchasePanel towerPanel) {
		if (dragEvent != null) {
			towerController.HandleDragPurchase(dragEvent, towerPanel);
		}
	}

	public void HandleEndDragPurchase (PointerEventData dragEvent, TowerPurchasePanel towerPanel) {
		input.ToggleDraggingObject(false);
		if (dragEvent != null) {
			towerController.HandleEndDragPurchase(dragEvent, towerPanel);
			mapController.UnhighlightValidBuildsTiles();
		}
	}

	public void HandleTowerPurchaseSelected (Tower tower) {
		this.currentlySelectedPurchaseTower = tower;
		mapController.HighlightValidBuildTiles();
	}

	public TowerBehaviour GetPurchaseTowerToPlace (Vector3 startingPosition, bool purchaseLock = false) {
		if (TrySpendMana(currentlySelectedPurchaseTower.ICost)) {
			Tower purchaseTower = currentlySelectedPurchaseTower;
			if (!purchaseLock) {
				currentlySelectedPurchaseTower = null;
				purchasePanel.TryDeselectSelectedPanel(false);
			}
			return towerController.GetTowerBehaviourFromTower(purchaseTower, startingPosition);
		} else {
			return null;
		}
	}

	protected override void SetReferences () {
		SingletonUtil.TryInit(ref Instance, this, gameObject);
	}

	protected override void FetchReferences () {
		dataController = DataController.Instance;
		towerController = TowerController.Instance;
		enemyController = EnemyController.Instance;
		mapController = MapController.Instance;
		statsPanel = StatsPanelController.Instance;
		purchasePanel = TowerPurchasePanelController.Instance;
		input = InputController.Instance;
		setupDataControllerCallbacks();
		setupUI();
		Create();
		StartWave();
		EventController.Event(EventType.LoadGame);
	}

	void setupUI () {
		onLevelUp(dataController.PlayerLevel);
	}

	protected override void CleanupReferences () {
		Instance = null;
		teardownDataControllerCallbacks();
		teardownUnitControllerCallbacks();
	}

	protected override void HandleNamedEvent (string eventName) {
		// Nothing
	}
		
	void handleUnitEvent (string eventName, Unit unit) {
		if (eventName == EventType.EnemyDestroyed) {
			if (unit.Type == "Undead") {
				CollectMana(25);
				EarnXP(10);
			} else if (unit.Type == "Brute") {
				CollectMana(100);
				EarnXP(50);
			} else if (unit.Type == "Shade") {
				CollectMana(200);
				EarnXP(100);
			}
		}
	}

	public string GenerateID (IUnit unit) {
		return unit.IType + System.Guid.NewGuid();
	}
		
	#region JSON Serialization

	public string SerializeAsJSON() {
		string worldAsJSON = string.Empty;
		foreach (UnitController controller in unitControllers) {
			foreach(Unit unit in controller.GetUnits()) {
				worldAsJSON += unit.SerializeAsJSON();
			}
		}
		print(worldAsJSON);
		return worldAsJSON;
	}

	public void SaveAsJSONToPath(string path) {
		throw new System.NotImplementedException();
	}

	public void DeserializeFromJSON(string jsonText) {
		throw new System.NotImplementedException();
	}

	public void DeserializeFromJSONAtPath(string jsonPath) {
		throw new System.NotImplementedException();
	}

	#endregion

	#region Input Handling

	public void HandleZoom (float zoomFactor) {
		throw new System.NotImplementedException();
	}

	public void HandlePan (Vector2 panDirection) {
		throw new System.NotImplementedException();
	}

	#endregion

	#region IObjectPool

	public GameObject CheckoutObject (params object[] arguments) {
		System.Type objectType = (System.Type) arguments[0];
		Stack<GameObject> objectsOfType;
		if (unitSpawnpool.TryGetValue(objectType, out objectsOfType) && objectsOfType.Count > 0) {
			return objectsOfType.Pop();
		} else {
			return NewObject(arguments);
		}
	}

	public GameObject NewObject (params object[] arguments) {
		System.Type objectType = (System.Type) arguments[0];
		return (GameObject) Instantiate(PrefabByType(objectType));
	}

	GameObject PrefabByType (System.Type type) {
		if (type.IsSubclassOf(typeof(Tower))) {
			return TowerPrefab;
		} else if (type.IsSubclassOf(typeof(Enemy))) {
			return EnemyPrefab;
		} else {
			return null;
		}
	}

	public GameObject[] CheckoutObjects (int count, object[] arguments) {
		GameObject[] objects = new GameObject[count];
		for (int i = 0; i < count; i++) {
			objects[i] = CheckoutObject(arguments);
		}
		return objects;
	}

	public void CheckInObject (GameObject instance, params object[] arguments) {
		System.Type objectType = (System.Type) arguments[0];
		if (!unitSpawnpool.ContainsKey(objectType)) {
			unitSpawnpool.Add(objectType, new Stack<GameObject>());
		}
		unitSpawnpool[objectType].Push(instance);
	}

	public void CheckInObjects (GameObject[] instances, params object[] arguments) {
		foreach (GameObject instance in instances) {
			CheckInObject(instance, arguments);
		}
	}

	#endregion

	public GameObject GetTowerPrefab (TowerType towerType) {
		switch (towerType) {
		case TowerType.Assault:
			return AssaulTowerPrefab;
		case TowerType.Barricade:
			return BarricadeTowerPrefab;
		case TowerType.Illumination:
			return IlluminationTowerPrefab;
		default:
			return null;
		}
	}

	public static void AttachBehaviourScript (System.Type unitType, GameObject instance) {
		if (unitType == typeof(AssaultTower)) {
			instance.AddComponent<AssaultTowerBehaviour>();
		} else if (unitType == typeof(BarricadeTower)) {
			instance.AddComponent<BarricadeTowerBehaviour>();
		} else if (unitType == typeof(IlluminationTower)) {
			instance.AddComponent<IlluminationTowerBehaviour>();
		} else if (unitType == typeof(Enemy)) {
			instance.AddComponent<EnemyBehaviour>();
		}
	}

	public static void AttackBehaviourScript (TowerType towerType, GameObject instance) {
		switch (towerType) {
		case TowerType.Assault:
			instance.AddComponent<AssaultTowerBehaviour>();
			break;
		case TowerType.Barricade:
			instance.AddComponent<BarricadeTowerBehaviour>();
			break;
		case TowerType.Illumination:
			instance.AddComponent<IlluminationTowerBehaviour>();
			break;
		}
	}

	protected override void SubscribeEvents () {
		base.SubscribeEvents ();
		EventController.OnUnitEvent += handleUnitEvent;
	}

	protected override void UnusbscribeEvents () {
		base.UnusbscribeEvents ();
		EventController.OnUnitEvent -= handleUnitEvent;
	}

	#region TowerController

	public Tower[] GetTowersOfType (TowerType type) {
		return towerController.GetTowersOfType(type);
	}

	public Sprite GetTowerSprite (string towerKey) {
		return towerController.GetTowerSprite(towerKey);
	}

	#endregion

	#region Enemy Controller

	public Sprite GetEnemySprite (string enemyKey) {
		return enemyController.GetEnemySprite(enemyKey);
	}

	#endregion
}
