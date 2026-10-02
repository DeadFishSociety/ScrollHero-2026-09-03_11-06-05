using Services;
using TMPro;
using UnityEngine;

// Shows the current username in the input field and renames it on the server when editing ends.
// Failures are shown in the error text under the input.
public class UsernameSetting : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text errorText;

    [Header("Messages ({0} is the username)")]
    [SerializeField] private string takenMessage = "The username \"{0}\" is already taken.";
    [SerializeField] private string failedMessage = "Your username was not changed. Check your internet connection and try again.";

    private void OnEnable()
    {
        inputField.SetTextWithoutNotify(SettingsService.Current.username);
        errorText.text = "";
        inputField.onEndEdit.AddListener(OnEndEdit);
    }

    private void OnDisable() => inputField.onEndEdit.RemoveListener(OnEndEdit);

    private async void OnEndEdit(string value)
    {
        string newUsername = value.Trim();
        errorText.text = "";

        if (newUsername.Length == 0 || newUsername == SettingsService.Current.username)
        {
            inputField.SetTextWithoutNotify(SettingsService.Current.username);
            return;
        }

        inputField.interactable = false;
        ClaimResult result = await SettingsService.ChangeUsername(newUsername);

        // The scene may have been left while the request was running.
        if (this == null)
        {
            return;
        }

        inputField.interactable = true;
        inputField.SetTextWithoutNotify(SettingsService.Current.username);

        if (result == ClaimResult.Taken)
        {
            errorText.text = string.Format(takenMessage, newUsername);
        }
        else if (result == ClaimResult.Failed)
        {
            errorText.text = failedMessage;
        }
    }
}
