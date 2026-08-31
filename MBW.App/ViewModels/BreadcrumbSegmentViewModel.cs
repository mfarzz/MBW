using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;

namespace MBW.App.ViewModels
{
    public sealed class BreadcrumbSegmentViewModel
    {
        public BreadcrumbSegmentViewModel(string label, Action? navigate, bool isLast)
        {
            Label = label;
            IsLast = isLast;
            if (!isLast && navigate is not null)
            {
                NavigateCommand = new RelayCommand(navigate);
            }
        }

        public string Label { get; }

        public bool IsLast { get; }

        public IRelayCommand? NavigateCommand { get; }

        public bool IsClickable => NavigateCommand is not null;

        public Visibility LinkVisibility => IsClickable ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LabelVisibility => IsLast ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ChevronVisibility => IsLast ? Visibility.Collapsed : Visibility.Visible;
    }
}
