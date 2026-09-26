using UnityEngine;

namespace Missions.Puzzles
{
    public class CircularPointer : MonoBehaviour
    {
        [SerializeField] private RectTransform pointer;
        [SerializeField] private float rotationSpeed = 180f;

        public RectTransform PointerRect => pointer;

        private float _currentAngle;

        private void Update()
        {
            _currentAngle += rotationSpeed * Time.deltaTime;
            _currentAngle %= 360f;

            pointer.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }

    }
}
