using UnityEngine;
using UnityEngine.InputSystem;

namespace KnightOnline.Client.Input
{
    public sealed class KeyboardMovementInput : IMovementInputProvider
    {
        public Vector2 GetMovementDirection()
        {
            float x = 0f;
            float y = 0f;

            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;

            var direction = new Vector2(x, y);
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}