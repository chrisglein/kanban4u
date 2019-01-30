using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kanban4U.Models
{
    public class DaySummaryModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void FirePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public DateTime Date
        {
            get { return _date; }
            set
            {
                if (_date != value)
                {
                    _date = value;
                    FirePropertyChanged(nameof(Date));
                }
            }
        }
        private DateTime _date;

        public double Amount
        {
            get { return _amount; }
            set
            {
                if (_amount != value)
                {
                    _amount = value;
                    FirePropertyChanged(nameof(Amount));
                }
            }
        }
        private double _amount;

        public string Details
        {
            get { return _details; }
            set
            {
                if (_details != value)
                {
                    _details = value;
                    FirePropertyChanged(nameof(Details));
                }
            }
        }
        private string _details;
    }
}
