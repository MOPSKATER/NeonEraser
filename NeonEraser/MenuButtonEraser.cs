using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonEraser
{
    internal class MenuButtonEraser : UnityEngine.UI.Button
    {
        private string levelID;

        internal void Setup(string levelID)
        {
            this.levelID = levelID;
            onClick.AddListener(CallbackEraseButton);
        }

        private void CallbackEraseButton() // TODO Add a notifyer
        {
            if (Keyboard.current.leftCtrlKey.isPressed)
            {
                Debug.Log("Erasing " + levelID);
                Eraser.eraser.Erase(levelID);
                return;
            }
        }
    }
}
