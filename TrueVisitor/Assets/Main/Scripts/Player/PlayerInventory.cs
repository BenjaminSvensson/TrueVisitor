using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class InventoryItemStack
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private int quantity;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public int Quantity => quantity;

    public InventoryItemStack(string itemId, string displayName, int quantity)
    {
        this.itemId = itemId;
        this.displayName = displayName;
        this.quantity = Mathf.Max(0, quantity);
    }

    public void Add(int amount)
    {
        quantity = Mathf.Max(0, quantity + amount);
    }
}

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private int coins = 10;
    [SerializeField] private List<InventoryItemStack> items = new List<InventoryItemStack>();
    [SerializeField] private UnityEvent<int> onCoinsChanged;
    [SerializeField] private UnityEvent<string> onItemAdded;

    public int Coins => coins;
    public IReadOnlyList<InventoryItemStack> Items => items;

    public bool CanAfford(int price)
    {
        return coins >= Mathf.Max(0, price);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;
        onCoinsChanged?.Invoke(coins);
    }

    public bool TrySpendCoins(int price)
    {
        price = Mathf.Max(0, price);
        if (coins < price)
        {
            return false;
        }

        coins -= price;
        onCoinsChanged?.Invoke(coins);
        return true;
    }

    public void AddItem(string itemId, string displayName, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }

        InventoryItemStack existingItem = FindItem(itemId);
        if (existingItem != null)
        {
            existingItem.Add(quantity);
        }
        else
        {
            items.Add(new InventoryItemStack(itemId, string.IsNullOrWhiteSpace(displayName) ? itemId : displayName, quantity));
        }

        onItemAdded?.Invoke(itemId);
    }

    public bool HasItem(string itemId)
    {
        return FindItem(itemId) != null;
    }

    public int GetQuantity(string itemId)
    {
        InventoryItemStack item = FindItem(itemId);
        return item != null ? item.Quantity : 0;
    }

    private InventoryItemStack FindItem(string itemId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItemStack item = items[i];
            if (item != null && item.ItemId == itemId)
            {
                return item;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        coins = Mathf.Max(0, coins);

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] == null || string.IsNullOrWhiteSpace(items[i].ItemId))
            {
                items.RemoveAt(i);
            }
        }
    }
}
