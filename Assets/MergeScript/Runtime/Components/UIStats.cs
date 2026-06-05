// UI display component; updates HP text, ammo counter, and compatibility indicator.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStats : MonoBehaviour
{
    [Header("Text Fields")]
    public TextMeshProUGUI hitPoints;
    public TextMeshProUGUI ammunitionCount;

    [Header("Compatibility Indicator")]
    // Image tinted green when the current gun configuration is valid,
    // red when it contains incompatible attachments.
    public Image colorCheck;

    // Stores the maximum ammo value set by UIMaxAmmo so UIAmmo can
    // display a "current / max" format without needing the value again.
    private int _maxAmmunition;

    // Updates the HP text field. Called by Player.BuffAttributes whenever
    // the player's currentHitPoints value changes.
    public void UIHealthPoints(int currentHP)
    {
        if (hitPoints != null) hitPoints.text = "HP: " + currentHP;
    }

    // Stores the magazine capacity. Call this whenever a new magazine is equipped
    // so subsequent UIAmmo calls can display the correct maximum.
    public void UIMaxAmmo(int max) => _maxAmmunition = max;

    // Updates the ammo counter text to "current / max" format.
    // Call this each time the player fires or reloads.
    public void UIAmmo(int current)
    {
        if (ammunitionCount != null)
            ammunitionCount.text = current + " / " + _maxAmmunition;
    }

    // Tints the colorCheck image green for a valid configuration, red for an invalid one.
    // Call ValidateAndNotify on GunAssemblyController and pipe the result here.
    public void ColorChecker(bool isCompatible)
    {
        if (colorCheck != null)
            colorCheck.color = isCompatible ? Color.green : Color.red;
    }
}
