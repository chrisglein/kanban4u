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

using MUXC = Microsoft.UI.Xaml.Controls;

namespace Kanban4U
{
    public sealed partial class NavigationFrame : Page
    {
        public NavigationFrame()
        {
            this.InitializeComponent();

            NavigationView.SelectedItem = 
                NavigationView.MenuItems.FirstOrDefault(x => ((MUXC.NavigationViewItem)x).Tag as string == GlobalSettings.Instance.SelectedNavigationViewItemTag) 
                ?? NavigationView.MenuItems[0];
        }

        private void NavigationView_SelectionChanged(MUXC.NavigationView sender, MUXC.NavigationViewSelectionChangedEventArgs args)
        {
            var selected = (MUXC.NavigationViewItem)args.SelectedItem;
            GlobalSettings.Instance.SelectedNavigationViewItemTag = selected.Tag as string;
            if (args.IsSettingsSelected)
            {
                MainFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                switch (selected.Tag)
                {
                    case "IndividualHorizontal":
                    case "IndividualVertical":
                        bool isHorizontal = Equals(selected.Tag, "IndividualHorizontal");
                        if (MainFrame.Content is MainPage mainPage)
                        {
                            mainPage.IsHorizontal = isHorizontal;
                        }
                        else
                        {
                            MainFrame.Navigate(typeof(MainPage), isHorizontal);
                        }
                        break;

                    case "TeamBurndown":
                        MainFrame.Navigate(typeof(TeamBurndown));
                        break;
                }
            }
        }
    }
}
