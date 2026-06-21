using UnityEngine;

public class RemoveUiDebug : MonoBehaviour
{
    [SerializeField] private GameObject[] uiObjects;
    
    private bool _isHidden;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            _isHidden = !_isHidden;

            foreach (var ui in uiObjects)
            {
                if (ui != null)
                    ui.SetActive(!_isHidden);
            }
        }
    }
}
