using System;
using System.Collections.Generic;
using MyGame.Combat;
using MyGame.Common;
using MyName.Equipment;

namespace MyGame.Inventory
{
    public sealed class PlayerEquipment
    {
        public event Action Changed;

        public sealed class EquipmentInstance
        {
            public string instanceId;
            public string equipmentId;
            public List<BaseStatModifier> rolledBaseStatMods = new List<BaseStatModifier>();
            public List<DerivedStatModifier> rolledDerivedStatMods =
                new List<DerivedStatModifier>();
            public List<CombatStatModifier> rolledCombatStatMods =
                new List<CombatStatModifier>();
            public List<SpellVariableOverride> rolledSpellOverrides =
                new List<SpellVariableOverride>();
        }

        private readonly Dictionary<string, EquipmentInstance> _instancesById =
            new Dictionary<string, EquipmentInstance>();

        private readonly Dictionary<EquipmentSlot, string> _equippedInstanceBySlot =
            new Dictionary<EquipmentSlot, string>();

        public IReadOnlyDictionary<string, EquipmentInstance> Instances => _instancesById;
        public IReadOnlyDictionary<EquipmentSlot, string> Equipped => _equippedInstanceBySlot;

        public EquipmentInstance GetInstance(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return null;
            return _instancesById.TryGetValue(instanceId, out var inst) ? inst : null;
        }

        public void AddOrReplace(EquipmentInstance instance)
        {
            if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId))
                return;

            _instancesById[instance.instanceId] = instance;
            Changed?.Invoke();
        }

        public bool Equip(EquipmentSlot slot, string instanceId)
        {
            if (slot == EquipmentSlot.None)
                return false;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                bool changed = _equippedInstanceBySlot.Remove(slot);
                if (changed)
                    Changed?.Invoke();
                return true;
            }

            if (!_instancesById.ContainsKey(instanceId))
                return false;

            if (
                _equippedInstanceBySlot.TryGetValue(slot, out var existing)
                && existing == instanceId
            )
                return true;

            _equippedInstanceBySlot[slot] = instanceId;
            Changed?.Invoke();
            return true;
        }

        public string GetEquippedInstanceId(EquipmentSlot slot)
        {
            return _equippedInstanceBySlot.TryGetValue(slot, out var id) ? id : null;
        }

        public bool TryGetEquippedInstance(EquipmentSlot slot, out EquipmentInstance instance)
        {
            instance = null;
            if (slot == EquipmentSlot.None)
                return false;

            if (
                !_equippedInstanceBySlot.TryGetValue(slot, out var id)
                || string.IsNullOrWhiteSpace(id)
            )
                return false;

            instance = GetInstance(id);
            return instance != null;
        }

        public IEnumerable<EquipmentInstance> GetEquippedInstances()
        {
            foreach (var kvp in _equippedInstanceBySlot)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                    continue;

                var inst = GetInstance(kvp.Value);
                if (inst != null)
                    yield return inst;
            }
        }

        public void Clear()
        {
            _instancesById.Clear();
            _equippedInstanceBySlot.Clear();
            Changed?.Invoke();
        }

        public void EnforceEquippedValidity()
        {
            // Remove equipped links pointing to missing instances.
            var toRemove = new List<EquipmentSlot>();
            foreach (var kvp in _equippedInstanceBySlot)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value) || !_instancesById.ContainsKey(kvp.Value))
                    toRemove.Add(kvp.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _equippedInstanceBySlot.Remove(toRemove[i]);
        }
    }
}
