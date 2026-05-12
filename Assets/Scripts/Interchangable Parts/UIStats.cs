using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIStats : MonoBehaviour
{
    public TextMeshProUGUI hitPoints;
    public TextMeshProUGUI ammunitionCount;

    private int maxAmmunition;

    // compatibility test
    public Image colorCheck;

    public void UIHealthPoints(int currentHitPoints)
    {
        hitPoints.text = "HP: " + currentHitPoints;
    }

    public void UIMaxAmmo(int max)
    {
        maxAmmunition = max;
    }

    public void UIAmmo(int current)
    {
        ammunitionCount.text = current + " / " + maxAmmunition;
    }

    public void ColorChecker(bool comp)
    {
        if (comp) {
            colorCheck.color = Color.green;
        } else {
            colorCheck.color = Color.red;
        }
    }

    void Update()
    {
        
    }
}
