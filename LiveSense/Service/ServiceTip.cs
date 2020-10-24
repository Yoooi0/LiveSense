using Stylet;

namespace LiveSense.Service
{
    public class ServiceTip : PropertyChangedBase
    {
        public string Username { get; }
        public int Amount { get; }

        public float? Progress { get; set; }

        public ServiceTip(string username, int amount)
        {
            Username = username;
            Amount = amount;
        }
    }
}
