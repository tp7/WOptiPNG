using System;
using System.Threading;

namespace WOptiPng
{
    public class TimedMessage : BindableModel
    {
        private string _message;

        public string Message
        {
            get { return _message; }
            private set
            {
                if (_message == value)
                {
                    return;
                }
                _message = value;
                OnPropertyChanged();
            }
        }

        private Timer _timer;
        private string _lastMessage;

        public void SetMessage(string message, TimeSpan? timeout)
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
            else
            {
                //last message is permanent, save it
                _lastMessage = Message;
            }
            
            Message = message;

            if (timeout != null)
            {
                _timer = new Timer(
                    obj => { Message = _lastMessage; },
                    null,
                    (long)timeout.GetValueOrDefault().TotalMilliseconds,
                    Timeout.Infinite);
            }
        }
    }
}