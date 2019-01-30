using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;

namespace Kanban4U
{
    static class ExceptionDialog
    {
        public static async Task ShowAsync(Exception e)
        {
            string exceptionMessage = e.ToString();
            var dialog = new Windows.UI.Xaml.Controls.ContentDialog()
            {
                Title = "Something went wrong",
                Content = exceptionMessage,
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = "Copy",
                CloseButtonText = "Ok"
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var package = new DataPackage();
                package.SetText(exceptionMessage);
                Clipboard.SetContent(package);
            }
        }
    }
}
