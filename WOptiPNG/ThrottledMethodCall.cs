using System;
using System.Timers;

namespace WOptiPNG
{
    //allows only one method call in X milliseconds
    public class ThrottledMethodCall
    {
        private readonly Timer _timer;

        public ThrottledMethodCall(Action action, double milliseconds)
        {
            _timer = new Timer(milliseconds) {AutoReset = false};
            _timer.Elapsed += (sender, e) => action();
        }

        public void Call()
        {
            if (_timer.Enabled)
            {
                return;
            }
            _timer.Start();
        }
    }
}