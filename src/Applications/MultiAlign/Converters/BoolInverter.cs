﻿using System;
using System.Windows.Data;

namespace MultiAlign.Converters
{
    public class BoolInverter: IValueConverter      
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return false;

            var enabled = (bool)value;
            if (enabled)
                return false;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
