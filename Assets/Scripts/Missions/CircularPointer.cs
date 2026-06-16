using UnityEngine;

namespace UI
{
    public class CircularPointer : MonoBehaviour
    {
        [SerializeField] private RectTransform pointer;
        [SerializeField] private float radius = 90f;
        [SerializeField] private float rotationSpeed = 180f;

        public RectTransform PointerRect => pointer;

        private float currentAngle;

        private void Update()
        {
            currentAngle += rotationSpeed * Time.deltaTime;
            currentAngle %= 360f;

            pointer.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }
        
    } 
}
    

