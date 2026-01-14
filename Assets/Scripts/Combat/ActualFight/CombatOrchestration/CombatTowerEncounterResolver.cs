using UnityEngine;

namespace MyGame.Combat.Tower
{
    /// <summary>
    /// Resolves CombatTowerContext into concrete encounter data:
    /// tower definition -> floor entry -> monster definition.
    /// Keeps database lookup logic out of the screen/controller.
    /// </summary>
    public sealed class CombatTowerEncounterResolver
    {
        private readonly TowerDatabase _towerDatabase;

        public CombatTowerEncounterResolver(TowerDatabase towerDatabase)
        {
            _towerDatabase = towerDatabase;
        }

        public bool TryResolve(
            CombatTowerContext context,
            out TowerDefinition towerDef,
            out TowerFloorEntry floorEntry,
            out MonsterDefinition monsterDef
        )
        {
            towerDef = null;
            floorEntry = null;
            monsterDef = null;

            if (context == null)
            {
                Debug.LogError("[CombatTowerEncounterResolver] Missing CombatTowerContext.");
                return false;
            }

            if (_towerDatabase == null)
            {
                Debug.LogError("[CombatTowerEncounterResolver] TowerDatabase not assigned.");
                return false;
            }

            towerDef = _towerDatabase.GetById(context.towerId);
            if (towerDef == null)
            {
                Debug.LogError(
                    $"[CombatTowerEncounterResolver] Tower '{context.towerId}' not found."
                );
                return false;
            }

            floorEntry = towerDef.GetFloor(context.floor);
            if (floorEntry == null || floorEntry.monster == null)
            {
                Debug.LogError(
                    $"[CombatTowerEncounterResolver] No monster defined for tower '{context.towerId}', floor {context.floor}."
                );
                return false;
            }

            monsterDef = floorEntry.monster;
            return true;
        }
    }
}
