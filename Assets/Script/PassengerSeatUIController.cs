using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PassengerSeatUIController : MonoBehaviour
{
    [SerializeField] private Image passengerIcon;

    private void Start()
    {    

        // Initialize passenger icon as hidden
        if (passengerIcon != null)
        {
            passengerIcon.enabled = false;
        }
        else
        {
            Debug.LogError("No passenger icon found for PassengerSeatUIController: " + gameObject.name);
        }
    }

    public void SetOccupied(bool occupied)
    {
        if (passengerIcon != null)
        {
            passengerIcon.enabled = occupied;
        }
    }

    public bool IsOccupied()
    {
        return passengerIcon != null && passengerIcon.enabled;
    }
}