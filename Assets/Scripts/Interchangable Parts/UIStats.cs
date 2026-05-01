using UnityEngine;
using TMPro;

public class UIStats : MonoBehaviour
{
    public TextMeshProUGUI hitPoints;
    public TextMeshProUGUI ammunitionCount;

    public void UIHealthPoints(int currentHitPoints)
    {
        hitPoints.text = "HP: " + currentHitPoints;
    }

    public void UIAmmo(int max, int current)
    {
        ammunitionCount.text = current + " / " + max;
    }

    void Update()
    {
        
    }
}
