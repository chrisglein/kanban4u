using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Kanban4U
{
    public sealed partial class PersonalAccessTokenDialog : ContentDialog
    {
        public PersonalAccessTokenDialog()
        {
            this.InitializeComponent();
        }
                
        public string PersonalAccessToken
        {
            get { return (string)GetValue(PersonalAccessTokenProperty); }
            set { SetValue(PersonalAccessTokenProperty, value); }
        }
        public static readonly DependencyProperty PersonalAccessTokenProperty =
            DependencyProperty.Register("PersonalAccessToken", typeof(string), typeof(PersonalAccessTokenDialog), new PropertyMetadata(""));

        public string CreateTokenUri
        {
            get { return Configuration.CurrentConfig.TeamVSTSUri +"/_details/security/tokens"; }
        }

    }
}
