using Kanban4U.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Kanban4U
{
    public sealed partial class WorkItemTableCell : UserControl, INotifyPropertyChanged
    {
        public WorkItem Model
        {
            get
            {
                return _model;
            }
            set
            {
                this.SetProperty(PropertyChanged, ref _model, value);
            }
        }
        private WorkItem _model;

        public double RemainingDaysToWidth(double d)
        {
            // Arbitrary 1 day = 50 pixels. Round to nearest .25 and then scale by 50 to get desired width.
            return Math.Max(Math.Round(d * 4) / 4, 0) * 50;
        }

        public WorkItemTableCell()
        {
            this.InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            MoreInfoPopup.IsOpen = true;
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            MoreInfoPopup.IsOpen = false;
        }
    }
}
