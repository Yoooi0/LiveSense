using LiveSense.Common;
using LiveSense.Service;
using Stylet;

namespace LiveSense.ViewModels
{
    public class TipQueueViewModel : PropertyChangedBase
    {
        public ITipQueue TipQueue { get; }

        public TipQueueViewModel(ITipQueue queue)
        {
            TipQueue = queue;
        }

        public void PublishTip(string amount)
            => TipQueue.Enqueue(new ServiceTip("Anonymous", int.Parse(amount)));

        public void ClearQueue()
            => TipQueue.Clear();
    }
}
