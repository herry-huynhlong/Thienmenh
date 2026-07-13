using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ItemInventoryRuntimeTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();
        InvokeStaticMethod("ItemInventory", "ClearRuntimeCache");
        InvokeStaticMethod("SimpleItemShop", "ClearRuntimeStockCache");
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteAll();
        InvokeStaticMethod("ItemInventory", "ClearRuntimeCache");
        InvokeStaticMethod("SimpleItemShop", "ClearRuntimeStockCache");
    }

    [UnityTest]
    public IEnumerator SharedInventoryLoad_DoesNotSelfMergeDurableItems()
    {
        ScriptableObject durableItem =
            CreateItem(
                "durable-item",
                "PhapBao",
                maxDurability: 50);

        SaveInventory(
            "TestSharedInventory_Durable",
            CreateItemStack(
                durableItem,
                amount: 1,
                durability: 50,
                maxDurability: 50));

        GameObject inventoryObject = new GameObject("SharedInventoryDurable");
        inventoryObject.SetActive(false);

        Component inventory =
            AddInventoryComponent(inventoryObject);
        SetFieldValue(inventory, "shareRuntimeItems", true);
        SetFieldValue(inventory, "keepInspectorItemsWhenLoadingSave", true);
        SetFieldValue(inventory, "runtimeKey", "TestSharedInventory_Durable");

        inventoryObject.SetActive(true);
        yield return null;

        IList inventoryItems = GetInventoryItems(inventory);
        Assert.AreEqual(1, inventoryItems.Count);
        Assert.AreSame(durableItem, GetStackItem(inventoryItems[0]));
        Assert.AreEqual(1, GetIntFieldValue(inventoryItems[0], "amount"));
    }

    [UnityTest]
    public IEnumerator LoadInventory_KeepsInspectorDefaultsWhenEnabled()
    {
        ScriptableObject savedItem =
            CreateItem(
                "saved-item",
                "DanDuoc");
        ScriptableObject inspectorItem =
            CreateItem(
                "inspector-item",
                "DanDuoc");

        SaveInventory(
            "TestSharedInventory_Defaults",
            CreateItemStack(
                savedItem,
                amount: 2));

        GameObject inventoryObject = new GameObject("SharedInventoryDefaults");
        inventoryObject.SetActive(false);

        Component inventory =
            AddInventoryComponent(inventoryObject);
        SetFieldValue(inventory, "shareRuntimeItems", true);
        SetFieldValue(inventory, "keepInspectorItemsWhenLoadingSave", true);
        SetFieldValue(inventory, "runtimeKey", "TestSharedInventory_Defaults");
        GetInventoryItems(inventory).Add(
            CreateItemStack(
                inspectorItem,
                amount: 3));

        inventoryObject.SetActive(true);
        yield return null;

        Assert.AreEqual(2, GetInventoryItems(inventory).Count);
        Assert.AreEqual(2, GetInventoryAmount(inventory, savedItem));
        Assert.AreEqual(3, GetInventoryAmount(inventory, inspectorItem));
    }

    static ScriptableObject CreateItem(
        string itemId,
        string itemTypeName,
        int maxDurability = 0)
    {
        ScriptableObject item =
            ScriptableObject.CreateInstance("StatItemData");
        Assert.NotNull(item, "Failed to create StatItemData test instance.");

        Type statItemType = item.GetType();
        Type itemTypeEnum = statItemType.Assembly.GetType("ItemType");
        Assert.NotNull(itemTypeEnum, "Failed to resolve ItemType enum.");

        object itemTypeValue = Enum.Parse(itemTypeEnum, itemTypeName);

        SetFieldValue(item, "itemId", itemId);
        SetFieldValue(item, "itemName", itemId);
        SetFieldValue(item, "itemType", itemTypeValue);
        SetFieldValue(item, "maxDurability", maxDurability);
        SetFieldValue(
            item,
            "consumeOnUse",
            !string.Equals(itemTypeName, "PhapBao", StringComparison.Ordinal));
        return item;
    }

    static object CreateItemStack(
        ScriptableObject item,
        int amount,
        int durability = 0,
        int maxDurability = 0)
    {
        Type itemStackType = ResolveGameType("ItemStack");
        Assert.NotNull(itemStackType, "Failed to resolve ItemStack type.");

        object stack = Activator.CreateInstance(itemStackType);
        SetFieldValue(stack, "item", item);
        SetFieldValue(stack, "amount", amount);
        SetFieldValue(stack, "durability", durability);
        SetFieldValue(stack, "maxDurability", maxDurability);
        return stack;
    }

    static void SaveInventory(
        string runtimeKey,
        params object[] stacks)
    {
        Type itemStackType = ResolveGameType("ItemStack");
        Assert.NotNull(itemStackType, "Failed to resolve ItemStack type.");

        Type listType = typeof(List<>).MakeGenericType(itemStackType);
        IList list = Activator.CreateInstance(listType) as IList;
        Assert.NotNull(list, "Failed to create ItemStack list.");

        for (int i = 0; i < stacks.Length; i++)
        {
            list.Add(stacks[i]);
        }

        Type saveSystemType = ResolveGameType("GameSaveSystem");
        Assert.NotNull(saveSystemType, "Failed to resolve GameSaveSystem type.");

        MethodInfo saveInventory = saveSystemType.GetMethod(
            "SaveInventory",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(saveInventory, "GameSaveSystem.SaveInventory was not found.");
        saveInventory.Invoke(null, new object[] { runtimeKey, list });
    }

    static Component AddInventoryComponent(GameObject target)
    {
        Type inventoryType = ResolveGameType("ItemInventory");
        Assert.NotNull(inventoryType, "Failed to resolve ItemInventory type.");
        return target.AddComponent(inventoryType);
    }

    static IList GetInventoryItems(Component inventory)
    {
        FieldInfo itemsField = inventory.GetType().GetField(
            "items",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(itemsField, "ItemInventory.items field was not found.");
        IList items = itemsField.GetValue(inventory) as IList;
        Assert.NotNull(items, "ItemInventory.items is not an IList.");
        return items;
    }

    static UnityEngine.Object GetStackItem(object stack)
    {
        FieldInfo itemField = stack.GetType().GetField(
            "item",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(itemField, "ItemStack.item field was not found.");
        return itemField.GetValue(stack) as UnityEngine.Object;
    }

    static int GetInventoryAmount(
        Component inventory,
        ScriptableObject item)
    {
        MethodInfo getAmount = inventory.GetType().GetMethod(
            "GetAmount",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(getAmount, "ItemInventory.GetAmount was not found.");
        object value = getAmount.Invoke(inventory, new object[] { item });
        return value is int amount ? amount : 0;
    }

    static int GetIntFieldValue(
        object target,
        string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName + " was not found.");
        return (int)field.GetValue(target);
    }

    static void SetFieldValue(
        object target,
        string fieldName,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field, target.GetType().Name + "." + fieldName + " was not found.");
        field.SetValue(target, value);
    }

    static void InvokeStaticMethod(
        string typeName,
        string methodName)
    {
        Type type = ResolveGameType(typeName);
        Assert.NotNull(type, "Failed to resolve " + typeName + " type.");

        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method, typeName + "." + methodName + " was not found.");
        method.Invoke(null, null);
    }

    static Type ResolveGameType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
