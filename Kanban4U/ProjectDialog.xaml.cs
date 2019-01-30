using System;
using System.Collections.Generic;
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

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Kanban4U
{
    public sealed partial class ProjectDialog : ContentDialog
    {
        public ProjectDialog()
        {
            this.InitializeComponent();
        }

        public string ProjectUrl
        {
            get { return (string)GetValue(ProjectUrlProperty); }
            set { SetValue(ProjectUrlProperty, value); }
        }
        public static readonly DependencyProperty ProjectUrlProperty =
            DependencyProperty.Register("ProjectUrl", typeof(string), typeof(ProjectDialog), new PropertyMetadata("https://microsoft.visualstudio.com"));
    }
}
