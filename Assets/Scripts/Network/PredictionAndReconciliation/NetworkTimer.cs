namespace Network
{
    public class NetworkTimer
    {
        public float TimeBetweenTick;
        public int CurrentTick { get; private set; }
        
        private float _timer;

        
        public NetworkTimer(float serverTickRate)
        {
            TimeBetweenTick = 1f /  serverTickRate;
            
        }

        public void Update(float deltaTime)
        {
            _timer += deltaTime;
            
        }

        public bool ShouldTick()
        {
            if (_timer >= TimeBetweenTick)
            {
                _timer -= TimeBetweenTick;
                CurrentTick++;
                return true;
            }
            
            return false;
            
        }
        
    }
}

