using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrencyDisplay : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private TextMeshProUGUI inventoryText;
    [SerializeField] private bool createRuntimeUi = true;
    [SerializeField] private string prefix = "Coins: ";
    [SerializeField] private string emptyInventoryText = "Inventory\nEmpty";
    [SerializeField] private Vector2 anchoredPosition = new Vector2(24f, -24f);
    [SerializeField] private Vector2 panelSize = new Vector2(170f, 44f);
    [SerializeField] private Vector2 inventoryPanelOffset = new Vector2(0f, -52f);
    [SerializeField] private Vector2 inventoryPanelSize = new Vector2(220f, 150f);

    private PlayerInventory subscribedInventory;
    private Canvas runtimeCanvas;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        EnsureCurrencyText();
    }

    private void OnEnable()
    {
        if (runtimeCanvas != null)
        {
            runtimeCanvas.enabled = true;
        }

        SubscribeToInventory();
        Refresh();
    }

    private void Start()
    {
        EnsureCurrencyText();
        SubscribeToInventory();
        Refresh();
    }

    private void Update()
    {
        if (subscribedInventory == null)
        {
            SubscribeToInventory();
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (runtimeCanvas != null)
        {
            runtimeCanvas.enabled = false;
        }

        UnsubscribeFromInventory();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInventory();
    }

    private void SubscribeToInventory()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (inventory == null)
        {
            inventory = FindAnyObjectByType<PlayerInventory>();
        }

        if (subscribedInventory == inventory)
        {
            return;
        }

        UnsubscribeFromInventory();
        subscribedInventory = inventory;

        if (subscribedInventory != null)
        {
            subscribedInventory.OnCoinsChanged.AddListener(SetCoins);
            subscribedInventory.OnItemAdded.AddListener(OnInventoryChanged);
            subscribedInventory.OnItemRemoved.AddListener(OnInventoryChanged);
            subscribedInventory.OnSelectedItemChanged.AddListener(OnInventoryChanged);
        }
    }

    private void UnsubscribeFromInventory()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnCoinsChanged.RemoveListener(SetCoins);
            subscribedInventory.OnItemAdded.RemoveListener(OnInventoryChanged);
            subscribedInventory.OnItemRemoved.RemoveListener(OnInventoryChanged);
            subscribedInventory.OnSelectedItemChanged.RemoveListener(OnInventoryChanged);
            subscribedInventory = null;
        }
    }

    private void Refresh()
    {
        SetCoins(subscribedInventory != null ? subscribedInventory.Coins : 0);
        RefreshInventory();
    }

    private void SetCoins(int coins)
    {
        if (currencyText != null)
        {
            currencyText.text = $"{prefix}{coins}";
        }
    }

    private void OnInventoryChanged(string itemId)
    {
        RefreshInventory();
    }

    private void RefreshInventory()
    {
        if (inventoryText == null)
        {
            return;
        }

        if (subscribedInventory == null || subscribedInventory.Items.Count == 0)
        {
            inventoryText.text = emptyInventoryText;
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder("Inventory");
        if (subscribedInventory.SelectedItem != null)
        {
            builder.Append('\n');
            builder.Append("Selected: ");
            builder.Append(GetItemName(subscribedInventory.SelectedItem));
        }

        for (int i = 0; i < subscribedInventory.Items.Count; i++)
        {
            InventoryItemStack item = subscribedInventory.Items[i];
            if (item == null || item.Quantity <= 0)
            {
                continue;
            }

            builder.Append('\n');
            builder.Append(subscribedInventory.IsSelected(item.ItemId) ? "> " : "  ");
            builder.Append(GetItemName(item));
            builder.Append(" x");
            builder.Append(item.Quantity);
        }

        inventoryText.text = builder.ToString();
    }

    private string GetItemName(InventoryItemStack item)
    {
        return string.IsNullOrWhiteSpace(item.DisplayName) ? item.ItemId : item.DisplayName;
    }

    private void EnsureCurrencyText()
    {
        if ((currencyText != null && inventoryText != null) || !createRuntimeUi)
        {
            return;
        }

        CreateRuntimeUi();
    }

    private void CreateRuntimeUi()
    {
        GameObject canvasObject = new GameObject("Currency HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        runtimeCanvas = canvasObject.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.sortingOrder = 50;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Currency Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelTransform = panelObject.GetComponent<RectTransform>();
        panelTransform.anchorMin = new Vector2(0f, 1f);
        panelTransform.anchorMax = new Vector2(0f, 1f);
        panelTransform.pivot = new Vector2(0f, 1f);
        panelTransform.anchoredPosition = anchoredPosition;
        panelTransform.sizeDelta = panelSize;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);
        panelImage.raycastTarget = false;

        GameObject textObject = new GameObject("Currency Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textTransform = textObject.GetComponent<RectTransform>();
        textTransform.anchorMin = Vector2.zero;
        textTransform.anchorMax = Vector2.one;
        textTransform.offsetMin = new Vector2(14f, 4f);
        textTransform.offsetMax = new Vector2(-14f, -4f);

        currencyText = textObject.GetComponent<TextMeshProUGUI>();
        currencyText.raycastTarget = false;
        currencyText.color = Color.white;
        currencyText.fontSize = 24f;
        currencyText.fontStyle = FontStyles.Bold;
        currencyText.alignment = TextAlignmentOptions.MidlineLeft;
        currencyText.text = $"{prefix}0";

        GameObject inventoryPanelObject = new GameObject("Inventory Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        inventoryPanelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform inventoryPanelTransform = inventoryPanelObject.GetComponent<RectTransform>();
        inventoryPanelTransform.anchorMin = new Vector2(0f, 1f);
        inventoryPanelTransform.anchorMax = new Vector2(0f, 1f);
        inventoryPanelTransform.pivot = new Vector2(0f, 1f);
        inventoryPanelTransform.anchoredPosition = anchoredPosition + inventoryPanelOffset;
        inventoryPanelTransform.sizeDelta = inventoryPanelSize;

        Image inventoryPanelImage = inventoryPanelObject.GetComponent<Image>();
        inventoryPanelImage.color = new Color(0f, 0f, 0f, 0.38f);
        inventoryPanelImage.raycastTarget = false;

        GameObject inventoryTextObject = new GameObject("Inventory Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        inventoryTextObject.transform.SetParent(inventoryPanelObject.transform, false);

        RectTransform inventoryTextTransform = inventoryTextObject.GetComponent<RectTransform>();
        inventoryTextTransform.anchorMin = Vector2.zero;
        inventoryTextTransform.anchorMax = Vector2.one;
        inventoryTextTransform.offsetMin = new Vector2(14f, 10f);
        inventoryTextTransform.offsetMax = new Vector2(-14f, -10f);

        inventoryText = inventoryTextObject.GetComponent<TextMeshProUGUI>();
        inventoryText.raycastTarget = false;
        inventoryText.color = Color.white;
        inventoryText.fontSize = 20f;
        inventoryText.fontStyle = FontStyles.Bold;
        inventoryText.alignment = TextAlignmentOptions.TopLeft;
        inventoryText.text = emptyInventoryText;
    }
}
