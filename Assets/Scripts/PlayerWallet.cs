using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public int money = 1000;

    public bool CanPay(int amount)
    {
        return amount >= 0 &&
            money >= amount;
    }

    public bool Pay(int amount)
    {
        if (!CanPay(amount))
        {
            return false;
        }

        money -= amount;
        return true;
    }

    public void AddMoney(int amount)
    {
        money += amount;

        if (money < 0)
        {
            money = 0;
        }
    }
}
