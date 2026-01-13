using System;
using UnityEngine;

namespace MyGame.Economy
{
    [Serializable]
    public sealed class Currency
    {
        [SerializeField]
        private int amount;

        public int Amount => amount;

        public Currency(int startAmount = 0)
        {
            amount = Mathf.Max(0, startAmount);
        }

        public void Add(int value)
        {
            if (value <= 0)
                return;
            amount += value;
        }

        public bool CanAfford(int cost)
        {
            return cost >= 0 && amount >= cost;
        }

        public bool TrySpend(int cost)
        {
            if (!CanAfford(cost))
                return false;

            amount -= cost;
            return true;
        }

        public void Set(int value)
        {
            amount = Mathf.Max(0, value);
        }
    }
}
