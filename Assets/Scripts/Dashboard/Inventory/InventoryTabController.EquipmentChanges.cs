using System;
using MyGame.Run;

public sealed partial class InventoryTabController
{
    private void SubscribeToEquipmentChanges()
    {
        var eq = RunSession.Equipment;
        if (eq == null)
            return;

        if (ReferenceEquals(eq, _subscribedEquipment))
            return;

        UnsubscribeFromEquipmentChanges();

        _subscribedEquipment = eq;
        if (_onEquipmentChanged != null)
            _subscribedEquipment.Changed += _onEquipmentChanged;
    }

    private void UnsubscribeFromEquipmentChanges()
    {
        if (_subscribedEquipment == null)
            return;

        if (_onEquipmentChanged != null)
            _subscribedEquipment.Changed -= _onEquipmentChanged;

        _subscribedEquipment = null;
    }
}
