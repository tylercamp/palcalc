using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DotNetKit.Windows
{
    sealed class DependencyVariable<T>
        : DependencyObject
    {
        static readonly DependencyProperty valueProperty =
            DependencyProperty.Register(
                "Value", 
                typeof(T), 
                typeof(DependencyVariable<T>)
            );

        public static DependencyProperty ValueProperty
        {
            get { return valueProperty; }
        }

        public T Value
        {
            get { return (T)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public void SetBinding(Binding binding)
        {
            BindingOperations.SetBinding(this, ValueProperty, binding);
        }

        public void SetBinding(object dataContext, string propertyPath)
        {
            SetBinding(new Binding(propertyPath) { Source = dataContext });
        }
    }
}
