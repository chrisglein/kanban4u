using System;
using System.ComponentModel;

namespace Kanban4U.Models
{
    public class TeamSettings : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void FirePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public int DaysInIteration
        {
            get
            {
                return _daysInIteration;
            }
            set
            {
                if (_daysInIteration != value)
                {
                    _daysInIteration = value;
                    FirePropertyChanged(nameof(DaysInIteration));
                }
            }
        }
        private int _daysInIteration;

        public int DaysRemaining
        {
            get
            {
                return _daysRemaining;
            }
            set
            {
                if (_daysRemaining != value)
                {
                    _daysRemaining = value;
                    FirePropertyChanged(nameof(DaysRemaining));
                }
            }
        }
        private int _daysRemaining;

        public string Iteration
        {
            get
            {
                return _iteration;
            }
            set
            {
                if (_iteration != value)
                {
                    _iteration = value;
                    FirePropertyChanged(nameof(Iteration));
                }
            }
        }
        private string _iteration;

        public string IterationPath
        {
            get
            {
                return _iterationPath;
            }
            set
            {
                if (_iterationPath != value)
                {
                    _iterationPath = value;
                    FirePropertyChanged(nameof(IterationPath));
                }
            }
        }
        private string _iterationPath;

        public string ProjectUrl { get; set; }
    }
}
