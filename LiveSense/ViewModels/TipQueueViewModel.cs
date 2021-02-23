using LiveSense.Common;
using LiveSense.Service;
using Stylet;

namespace LiveSense.ViewModels
{
    public class TipQueueViewModel : Screen
    {
        public ITipQueue TipQueue { get; }

        public TipQueueViewModel(ITipQueue queue)
        {
            TipQueue = queue;
        }

        public void PublishTip(string amount) //TODO: random
            => TipQueue.Enqueue(new ServiceTip("Manual", "Anonymous", int.Parse(amount)));

        public void ClearQueue()
            => TipQueue.Clear();
    }
}
