using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Kanban4U
{
    public static class INotifyPropertyChangedExtensions
    {
        public static bool SetProperty<T>(this INotifyPropertyChanged instance, PropertyChangedEventHandler handler, ref T variable, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(variable, value))
            {
                variable = value;
                handler?.Invoke(instance, new PropertyChangedEventArgs(propertyName));
                return true;
            }
            return false;
        }
    }
}
