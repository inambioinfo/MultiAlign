﻿using System;
using System.Windows;
using System.Windows.Data;

namespace MultiAlign.Converters
{
    
    public class BoolVisibilityConverter : DependencyObject, IValueConverter
    {



        public Visibility TrueVisibility
        {
            get { return (Visibility)GetValue(TrueVisibilityProperty); }
            set { SetValue(TrueVisibilityProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TrueVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TrueVisibilityProperty =
            DependencyProperty.Register("TrueVisibility", typeof(Visibility), typeof(BoolVisibilityConverter), new UIPropertyMetadata(Visibility.Visible));



        public Visibility FalseVisibility
        {
            get { return (Visibility)GetValue(FalseVisibilityProperty); }
            set { SetValue(FalseVisibilityProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FalseVisibility.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FalseVisibilityProperty =
            DependencyProperty.Register("FalseVisibility", typeof(Visibility), typeof(BoolVisibilityConverter), new UIPropertyMetadata(Visibility.Collapsed));



        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var bValue = (bool)value;
            if (bValue)
                return TrueVisibility;
            return FalseVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var visibility = (Visibility)value;

            if (visibility == Visibility.Visible)
                return true;
            return false;
        }
        #endregion
    }
}
