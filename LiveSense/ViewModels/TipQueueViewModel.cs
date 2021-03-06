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

        public void PublishTip(string amount)
        {
            if (!int.TryParse(amount, out var result))
                return;

            if (result < 0)
                result = MathUtils.Random(1, 500);

            TipQueue.Enqueue(new ServiceTip("Manual", "Anonymous", result));
        }

        public void ClearQueue()
            => TipQueue.Clear();
    }
}
