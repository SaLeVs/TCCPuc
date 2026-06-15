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

            float rad = currentAngle * Mathf.Deg2Rad;

            float x = Mathf.Cos(rad) * radius;
            float y = Mathf.Sin(rad) * radius;

            pointer.anchoredPosition = new Vector2(x, y);
        }
        
    } 
}
    

