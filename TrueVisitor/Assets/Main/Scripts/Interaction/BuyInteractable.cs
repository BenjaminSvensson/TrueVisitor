using UnityEngine;
using UnityEngine.Events;

public class BuyInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private string itemId = "item";
    [SerializeField] private string displayName = "Item";
    [SerializeField] private int price = 1;
    [SerializeField] private int quantity = 1;
    [SerializeField] private bool addItemToInventory = true;
    [SerializeField] private bool buyOnlyOnce = true;
    [SerializeField] private bool disableAfterPurchase = false;
    [SerializeField] private UnityEvent onPurchased;
    [SerializeField] private UnityEvent onCannotAfford;
    [SerializeField] private UnityEvent onAlreadyPurchased;
    [SerializeField] private AudioSource purchaseSound;

    private bool purchased;

    public void Interact()
    {
        PlayerInventory inventory = GetInventory();
        if (inventory == null)
        {
            Debug.LogWarning($"{nameof(BuyInteractable)} could not find a {nameof(PlayerInventory)} to buy '{displayName}'.", this);
            return;
        }

        if (buyOnlyOnce && purchased)
        {
            onAlreadyPurchased?.Invoke();
            return;
        }

        if (!inventory.TrySpendCoins(price))
        {
            onCannotAfford?.Invoke();
            return;
        }

        if (addItemToInventory)
        {
            inventory.AddItem(itemId, displayName, quantity);
            purchaseSound?.Play();
        }

        purchased = true;
        onPurchased?.Invoke();

        if (disableAfterPurchase)
        {
            gameObject.SetActive(false);
        }
    }

    private PlayerInventory GetInventory()
    {
        if (playerInventory != null)
        {
            return playerInventory;
        }

        playerInventory = FindAnyObjectByType<PlayerInventory>();
        return playerInventory;
    }

    private void OnValidate()
    {
        price = Mathf.Max(0, price);
        quantity = Mathf.Max(1, quantity);

        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = "item";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = itemId;
        }
    }
}
