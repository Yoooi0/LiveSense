using Stylet;

namespace LiveSense.Service
{
    public class ServiceTip : PropertyChangedBase
    {
        public string Service { get; }
        public string Username { get; }
        public int Amount { get; }

        public float? Progress { get; set; }

        public ServiceTip(string service, string username, int amount)
        {
            Service = service;
            Username = username;
            Amount = amount;
        }
    }
}
