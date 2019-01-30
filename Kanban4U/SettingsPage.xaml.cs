using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Kanban4U
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (Settings.PersonalAccessToken == null)
            {
                Settings.PersonalAccessToken = "";
            }
            PersonalAccessToken.Text = Settings.PersonalAccessToken;

            if (Logic.PersonalAccessTokenAuthenticationFailed)
            {
                PATErrorMessageTextBlock.Visibility = Visibility.Visible;
                PATErrorMessageTextBlock.Text = "Personal Access Token authentication failed, fix string above or clear and save an empty string to re-try web authentication.";
            }

            var user = await Logic.GetCurrentUser();
            CurrentUser.Text = user.DisplayName;
            CurrentUserEmail.Text = user.Email;

            var list = await Logic.GetTeams();
            Teams.ItemsSource = list;
            var chosenTeamId = GlobalSettings.Instance.Team?.TeamId;
            Teams.SelectedItem = list.FirstOrDefault(x => x.TeamId == chosenTeamId);
            Teams.IsEnabled = true;
        }

        private void PersonalAccessToken_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PersonalAccessToken.Text != Settings.PersonalAccessToken)
            {
                UpdatePAT.Visibility = Visibility.Visible;
                UpdatePAT.IsEnabled = true;
            }
            else
            {
                UpdatePAT.IsEnabled = false;
            }
        }

        private void UpdatePAT_Click(object sender, RoutedEventArgs e)
        {
            Settings.PersonalAccessToken = PersonalAccessToken.Text;
            UpdatePAT.IsEnabled = false;
            PATErrorMessageTextBlock.Visibility = Visibility.Collapsed;
        }

        public GlobalSettings Settings = GlobalSettings.Instance;

        public string IterationAdminUri
        {
            get
            {
                return Configuration.CurrentConfig.TeamVSTSUri + $"/OS/_settings/work-team?_a=iterations";
            }
        }

        public int CachedWorkItemsCount
        {
            get
            {
                return Settings.GetCachedWorkItemsCount();
            }
        }

        private void ClearWorkItemCache(object sender, RoutedEventArgs e)
        {
            Settings.ClearWorkItemCache();
        }
    }
}
