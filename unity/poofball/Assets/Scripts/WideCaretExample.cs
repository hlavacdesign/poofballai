using TMPro;
using UnityEngine;

public class WideCaretExample : MonoBehaviour
{
    [SerializeField] private TMP_InputField myInputField;

    void Start()
    {
        if (myInputField != null)
        {
            // Set caret width to 10 (or whatever larger value you like)
            myInputField.caretWidth = 30;
        }
    }
}
